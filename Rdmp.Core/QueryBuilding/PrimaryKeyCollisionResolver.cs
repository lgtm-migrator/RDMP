// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FAnsi.Discovery.QuerySyntax;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.DataLoad;
using Rdmp.Core.DataLoad.Triggers;

namespace Rdmp.Core.QueryBuilding
{
    /// <summary>
    /// The RDMP data load engine is designed to prevent duplicate data entering your live database.  This is achieved by requiring a primary key defined by the source
    /// data (i.e. not an autonum).  However it is expected that semantically correct primary keys will not be perfectly supplied in all cases by data providers, for example
    /// if 'TestLabCode' is the primary key on biochemistry but duplicates appear with unique values in 'DataAge' it would be reasonable to assume that newer 'DataAge' records
    /// replace older ones.  Therefore we might decide to keep the primary key as 'TestLabCode' and then discard duplicate records based on preserving the latest 'DataAge'.
    /// 
    /// <para>This class handles creating the query that deletes duplicates based on the column preference order supplied (See ConfigurePrimaryKeyCollisionResolution). </para>
    /// </summary>
    public class PrimaryKeyCollisionResolver
    {
        private readonly ITableInfo _tableInfo;
        private readonly IQuerySyntaxHelper _querySyntaxHelper;
        private const string WithCTE = "WITH CTE (DuplicateCount)";
        private const string SelectRownum = "\t SELECT ROW_NUMBER()";
        private const string DeleteBit =
@"DELETE 
FROM CTE 
WHERE DuplicateCount > 1";

        /// <summary>
        /// Creates a new collision resolver using the primary keys and resolution order of the supplied <see cref="TableInfo"/>
        /// </summary>
        /// <param name="tableInfo"></param>
        public PrimaryKeyCollisionResolver(ITableInfo tableInfo)
        {
            _tableInfo = tableInfo;
            _querySyntaxHelper = _tableInfo.GetQuerySyntaxHelper();
        }

        /// <summary>
        /// Get the SQL to run to delete records colliding on primary key
        /// </summary>
        /// <returns></returns>
        public string GenerateSQL()
        {
            ColumnInfo[] pks;
            List<IResolveDuplication> resolvers;

            return GenerateSQL(out pks, out resolvers);
        }

        private string GenerateSQL(out ColumnInfo[] pks, out List<IResolveDuplication> resolvers)
        {
            string sql = "";
            string tableNameInRAW = GetTableName();

            var cols = _tableInfo.ColumnInfos.ToArray();
            pks = cols.Where(col => col.IsPrimaryKey).ToArray();
            
            if(!pks.Any())
                throw new Exception("TableInfo " + _tableInfo.GetRuntimeName() + " does not have any primary keys defined so cannot resolve primary key collisions");

            string primaryKeys = pks.Aggregate("", (s, n) => s + _querySyntaxHelper.EnsureWrapped(n.GetRuntimeName(LoadStage.AdjustRaw)) + ",");
            primaryKeys = primaryKeys.TrimEnd(new[] {','});


            sql += "/*Notice how entities are not fully indexed with Database, this is because this code will run on RAW servers, prior to reaching STAGING/LIVE - the place where there are primary keys*/" + Environment.NewLine;

            sql += WithCTE + Environment.NewLine;
            sql += "AS" + Environment.NewLine;
            sql += "(" + Environment.NewLine;
            sql += SelectRownum + " OVER(" + Environment.NewLine;
            sql += "\t PARTITION BY" + Environment.NewLine;
            sql += "\t\t " + primaryKeys + Environment.NewLine;
            sql += "\t ORDER BY"  + Environment.NewLine;

            sql += "\t /*Priority in which order they should be used to resolve duplication of the primary key values, order by:*/"  + Environment.NewLine;
            
            resolvers = new List<IResolveDuplication>();

            resolvers.AddRange(cols.Where(c => c.DuplicateRecordResolutionOrder != null));
            resolvers.AddRange(_tableInfo.PreLoadDiscardedColumns.Where(c => c.DuplicateRecordResolutionOrder != null));

            if (!resolvers.Any())
                throw new Exception("The ColumnInfos of TableInfo " + _tableInfo + " do not have primary key resolution orders configured (do not know which order to use non primary key column values in to resolve collisions).  Fix this by right clicking a TableInfo in CatalogueManager and selecting 'Configure Primary Key Collision Resolution'.");

            //order by the priority of columns 
            foreach (IResolveDuplication column in resolvers.OrderBy(col => col.DuplicateRecordResolutionOrder))
            {
                if(column is ColumnInfo && ((ColumnInfo)column).IsPrimaryKey )
                    throw new Exception("Column " + column.GetRuntimeName() + " is flagged as primary key when it also has a DuplicateRecordResolutionOrder, primary keys cannot be used to resolve duplication since they are the hash!  Resolve this in the CatalogueManager by right clicking the offending TableInfo " + _tableInfo.GetRuntimeName() + " and editing the resolution order");
                
                sql = AppendRelevantOrderBySql(sql, column);
            }

            //trim the last remaining open bracket
            sql = sql.TrimEnd(new[] {',','\r','\n'}) + Environment.NewLine;

            sql += ") AS DuplicateCount" + Environment.NewLine;
            sql += "FROM " + tableNameInRAW + Environment.NewLine;
            sql += ")" + Environment.NewLine;

            sql += DeleteBit;

            return sql;
        }

        private string GetTableName()
        {
            return _tableInfo.GetRuntimeName(LoadStage.AdjustRaw);
        }

        private string AppendRelevantOrderBySql(string sql, IResolveDuplication col)
        {
            string colname = _querySyntaxHelper.EnsureWrapped(col.GetRuntimeName(LoadStage.AdjustRaw));

            string direction = col.DuplicateRecordResolutionIsAscending ? " ASC" : " DESC";

            //dont bother adding these because they are hic generated
            if (SpecialFieldNames.IsHicPrefixed(colname))
                return sql;

            ValueType valueType = GetDataType(col.Data_type);

            if (valueType == ValueType.CharacterString)
            {
                //character strings are compared first by LENGTH (to prefer longer data)
                //then by alphabetical comparison to prefer things towards the start of the alphabet (because this makes sense?!)
                return 
                    sql +
                    "LEN(ISNULL(" + colname + "," + GetNullSubstituteForComparisonsWithDataType(col.Data_type, true) + "))" + direction + "," + Environment.NewLine +
                    "ISNULL(" + colname + "," + GetNullSubstituteForComparisonsWithDataType(col.Data_type, true) + ")" + direction + "," + Environment.NewLine;
            }

            return sql + "ISNULL(" + colname + "," + GetNullSubstituteForComparisonsWithDataType(col.Data_type, true) + ")" + direction + "," + Environment.NewLine;
        }

        /// <summary>
        /// Generates the SQL that will be run to determine whether there are any record collisions on primary key (in RAW)
        /// </summary>
        /// <returns></returns>
        public string GenerateCollisionDetectionSQL()
        {
            string tableNameInRAW = GetTableName();
            var pks = _tableInfo.ColumnInfos.Where(col => col.IsPrimaryKey).ToArray();

            string sql = "";
            sql += "select case when exists(" + Environment.NewLine;
            sql += "select 1 FROM" + Environment.NewLine;
            sql += tableNameInRAW + Environment.NewLine;
            sql += "group by " + pks.Aggregate("", (s, n) => s + _querySyntaxHelper.EnsureWrapped(n.GetRuntimeName(LoadStage.AdjustRaw)) + ",") + Environment.NewLine;
            sql = sql.TrimEnd(new[] {',','\r','\n'}) + Environment.NewLine;
            sql += "having count(*) > 1" + Environment.NewLine;
            sql += ") then 1 else 0 end" + Environment.NewLine;

            return sql;
        }

        /// <summary>
        /// Generates SQL to show which records would be deleted by primary key collision resolution.  This should be run manually by the data analyst if he is unsure about the 
        /// resolution order / current primary keys
        /// </summary>
        /// <returns></returns>
        public string GeneratePreviewSQL()
        {
            
            ColumnInfo[] pks;
            List<IResolveDuplication> resolvers;
            string basicSQL = GenerateSQL(out pks, out resolvers);

            string commaSeparatedPKs = String.Join(",", pks.Select(c => _querySyntaxHelper.EnsureWrapped(c.GetRuntimeName(LoadStage.AdjustRaw))));
            string commaSeparatedCols = String.Join(",", resolvers.Select(c => _querySyntaxHelper.EnsureWrapped(c.GetRuntimeName(LoadStage.AdjustRaw))));

            //add all the columns to the WITH CTE bit
            basicSQL = basicSQL.Replace(WithCTE,"WITH CTE (" + commaSeparatedPKs + "," + commaSeparatedCols + ",DuplicateCount)");
            basicSQL = basicSQL.Replace(SelectRownum, "\t SELECT " + commaSeparatedPKs + "," + commaSeparatedCols + ",ROW_NUMBER()");
            basicSQL = basicSQL.Replace(DeleteBit, "");

            basicSQL += "select" + Environment.NewLine;
            basicSQL += "\tCase when DuplicateCount = 1 then 'Retained' else 'Deleted' end as PlannedOperation,*" + Environment.NewLine;
            basicSQL += "FROM CTE" + Environment.NewLine;
            basicSQL += "where" + Environment.NewLine;
            basicSQL += "exists" + Environment.NewLine;
            basicSQL += "(" + Environment.NewLine;
            basicSQL += "\tselect 1" + Environment.NewLine;
            basicSQL += "\tfrom" + Environment.NewLine;
            basicSQL += "\t\t"+ GetTableName() + " child" + Environment.NewLine;
            basicSQL += "\twhere" + Environment.NewLine;

            //add the child.pk1 = CTE.pk1 bit to restrict preview only to rows that are going to get compared for nukage
            basicSQL += String.Join("\r\n\t\tand",pks.Select(pk =>  ("\t\tchild." + _querySyntaxHelper.EnsureWrapped(pk.GetRuntimeName(LoadStage.AdjustRaw)) + "= CTE." + _querySyntaxHelper.EnsureWrapped(pk.GetRuntimeName(LoadStage.AdjustRaw)))));

            basicSQL += "\tgroup by" + Environment.NewLine;
            basicSQL += String.Join(",\r\n", pks.Select( pk => "\t\t" + _querySyntaxHelper.EnsureWrapped(pk.GetRuntimeName(LoadStage.AdjustRaw))));

            basicSQL += "\t\t" + Environment.NewLine;
            basicSQL += "\thaving count(*)>1" + Environment.NewLine;
            basicSQL += ")" + Environment.NewLine;

            basicSQL += "order by " + String.Join(",\r\n", pks.Select(pk => _querySyntaxHelper.EnsureWrapped(pk.GetRuntimeName(LoadStage.AdjustRaw))));
            basicSQL += ",DuplicateCount";

            return basicSQL;
        }

        private ValueType GetDataType(string dataType)
        {
            if (
                dataType.StartsWith("decimal") ||
                dataType.StartsWith("float") ||
                dataType.Equals("bigint") ||
                dataType.Equals("bit") ||
                dataType.Contains("decimal") ||
                dataType.Equals("int") ||
                dataType.Equals("money") ||
                dataType.Contains("numeric") ||
                dataType.Equals("smallint") ||
                dataType.Equals("smallmoney") ||
                dataType.Equals("smallint") ||
                dataType.Equals("tinyint") ||
                dataType.Equals("real"))
                return ValueType.Numeric;

            if (dataType.Contains("date"))
                return ValueType.DateTime;

            if (dataType.Contains("time"))
                return ValueType.Time;

            if (dataType.Contains("char") || dataType.Contains("text"))
                return ValueType.CharacterString;

            if (dataType.Contains("binary") || dataType.Contains("image"))
                return ValueType.Binary;

            if (dataType.Equals("cursor") ||
                dataType.Contains("timestamp") ||
                dataType.Contains("hierarchyid") ||
                dataType.Contains("uniqueidentifier") ||
                dataType.Contains("sql_variant") ||
                dataType.Contains("xml") ||
                dataType.Contains("table") ||
                dataType.Contains("spacial"))
                return ValueType.Freaky;

            throw new Exception("Could not figure out the ValueType of SQL Type \"" + dataType + "\"");


        }

        /// <summary>
        /// When using ORDER BY to resolve primary key collisions this will specify what substitution to use for null values (such that the ORDER BY works correctly).
        /// </summary>
        /// <param name="datatype">The Sql Server column datatype for the column you are substituting</param>
        /// <param name="min">true to substitute null values for the minimum value of the <paramref name="datatype"/>, false to substitute for the maximum</param>
        /// <returns></returns>
        public string GetNullSubstituteForComparisonsWithDataType(string datatype, bool min)
        {
            //technically these can go lower (real and float) but how realistic is that espcially when SqlServer plays fast and loose with very small numbers in floats... 
            if (datatype.Equals("bigint") || datatype.Equals("real") || datatype.StartsWith("float"))
                if (min)
                    return "-9223372036854775808";
                else
                    return "9223372036854775807";

            if (datatype.Equals("int"))
                if (min)
                    return "-2147483648";
                else
                    return "2147483647";

            if (datatype.Equals("smallint"))
                if (min)
                    return "-32768";
                else
                    return "32767";

            if (datatype.Equals("tinyint"))
                if (min)
                    return "- 1.79E+308";
                else
                    return "255";

            if (datatype.Equals("bit"))
                if (min)
                    return "0";
                else
                    return "1";

            if (datatype.Contains("decimal") || datatype.Contains("numeric"))
            {
                var digits = Regex.Match(datatype, @"(\d+),?(\d+)?");
                string toReturn = "";

                if (min)
                    toReturn = "-";

                //ignore element zero because elment zero is always a duplicate see https://msdn.microsoft.com/en-us/library/system.text.regularexpressions.match.groups%28v=vs.110%29.aspx
                if (digits.Groups.Count == 3 && String.IsNullOrWhiteSpace(digits.Groups[2].Value))
                {
                    for (int i = 0; i < Convert.ToInt32(digits.Groups[1].Value); i++)
                        toReturn += "9";

                    return toReturn;
                }

                if (digits.Groups.Count == 3)
                {
                    int totalDigits = Convert.ToInt32(digits.Groups[1].Value);
                    int digitsAfterDecimal = Convert.ToInt32(digits.Groups[2].Value);

                    for (int i = 0; i < totalDigits + 1; i++)
                        if (i == totalDigits - digitsAfterDecimal)
                            toReturn += ".";
                        else
                            toReturn += "9";

                    return toReturn;
                }
            }

            ValueType valueType = GetDataType(datatype);

            if (valueType == ValueType.CharacterString)
                if (min)
                    return "''";
                else
                    throw new NotSupportedException("Cannot think what the maxmimum character string would be, maybe use min = true instead?");

            if (valueType == ValueType.DateTime)
                if (min)
                    return "'1753-1-1'";
                else
                    throw new NotSupportedException("Cannot think what the maxmimum date would be, maybe use min = true instead?");

            if (valueType == ValueType.Time)
                if (min)
                    return "'00:00:00'";
                else
                    return "'23:59:59'";

            if (valueType == ValueType.Freaky)
                throw new NotSupportedException("Cannot predict null value substitution for freaky datatypes like " + datatype);

            if (valueType == ValueType.Binary)
                throw new NotSupportedException("Cannot predict null value substitution for binary datatypes like " + datatype);


            throw new NotSupportedException("Didn't know what minimum value type to use for " + datatype);

        }

        private enum ValueType
        {
            Numeric,
            DateTime,
            Time,
            CharacterString,
            Binary,
            Freaky
        }
    }
}
