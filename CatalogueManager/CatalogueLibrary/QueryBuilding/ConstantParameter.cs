﻿using System;
using CatalogueLibrary.Checks.SyntaxChecking;
using CatalogueLibrary.Data;
using CatalogueLibrary.DataHelper;
using MapsDirectlyToDatabaseTable;
using MapsDirectlyToDatabaseTable.Attributes;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.DatabaseHelpers.Discovery;
using ReusableLibraryCode.DatabaseHelpers.Discovery.QuerySyntax;

namespace CatalogueLibrary.QueryBuilding
{
    /// <summary>
    ///  Use this class to create standard parameters which you will always manually add in code to a QueryBuilder.  These are not editable
    ///  by users and are not stored in a database.  They should be used for things such as cohortDefinitionID, projectID etc.
    /// </summary>
    public class ConstantParameter : ISqlParameter
    {
        private readonly IQuerySyntaxHelper _syntaxHelper;

        /// <summary>
        /// Creates a new unchangeable always available parameter in a query being built.
        /// </summary>
        /// <param name="parameterSQL">The declaration sql e.g. DECLARE @bob as int</param>
        /// <param name="value">The value to set the paramater e.g. 1</param>
        /// <param name="comment">Some text to appear above the parameter, explaining its purpose</param>
        public ConstantParameter(string parameterSQL,string value,string comment, IQuerySyntaxHelper syntaxHelper)
        {
            _syntaxHelper = syntaxHelper;
            Value = value;
            Comment = comment;
            ParameterSQL = parameterSQL;
        }

        /// <summary>
        /// Not supported for constant parameters
        /// </summary>
        public void SaveToDatabase()
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc cref="ParameterName"/>
        public override string ToString()
        {
            return ParameterName;
        }

        /// <summary>
        /// Checks the syntax of the parameter (See <see cref="ParameterSyntaxChecker"/>)
        /// </summary>
        /// <param name="notifier"></param>
        public void Check(ICheckNotifier notifier)
        {
            new ParameterSyntaxChecker(this).Check(notifier);
        }

        /// <inheritdoc/>
        public IQuerySyntaxHelper GetQuerySyntaxHelper()
        {
            return _syntaxHelper;
        }

        /// <inheritdoc/>
        public string ParameterName { get { return QuerySyntaxHelper.GetParameterNameFromDeclarationSQL(ParameterSQL); } }

        /// <inheritdoc/>
        [Sql]
        public string ParameterSQL { get; set; }

        /// <inheritdoc/>
        [Sql]
        public string Value { get; set; }

        /// <inheritdoc/>
        public string Comment { get; set; }

        /// <summary>
        /// Returns null, <see cref="ConstantParameter"/> are never owned by any objects
        /// </summary>
        /// <returns></returns>
        public IMapsDirectlyToDatabaseTable GetOwnerIfAny()
        {
            return null;
        }
    }
}
