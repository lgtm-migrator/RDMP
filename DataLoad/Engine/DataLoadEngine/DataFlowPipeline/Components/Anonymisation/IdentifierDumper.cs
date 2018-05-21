using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using CatalogueLibrary;
using CatalogueLibrary.Data;
using CatalogueLibrary.Data.DataLoad;
using DataLoadEngine.Checks.Checkers;
using DataLoadEngine.DatabaseManagement;
using MapsDirectlyToDatabaseTable;
using ReusableLibraryCode;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.DataAccess;
using ReusableLibraryCode.DatabaseHelpers.Discovery;
using ReusableLibraryCode.Progress;

namespace DataLoadEngine.DataFlowPipeline.Components.Anonymisation
{
    /// <summary>
    /// Anonymises data during a Data Load by dropping columns prior to it reaching the LIVE table.  This is done for all PreLoadDiscardedColumns configured
    /// on the TableInfo.  Depending on the PreLoadDiscardedColumn.Destination the dropped values may be stored in an 'identifier dump' database.  This is
    /// usually done to seperate identifiable data (patient name, dob etc) from data (prescription of drug X on date Y) or drop sensitive data entirely.
    /// </summary>
    public class IdentifierDumper :IHasRuntimeName, IDisposeAfterDataLoad,ICheckable
    {
        public TableInfo TableInfo
        {
            get { return _tableInfo; }
            set
            {
                _tableInfo = value;
            }
        }

        public List<PreLoadDiscardedColumn> ColumnsToRouteToSomewhereElse { get; set; }
        private TableInfo _tableInfo;

        private ExternalDatabaseServer _externalDatabaseServer;
        private DiscoveredDatabase _dumpDatabase;

        private const int Timeout = 5000;
        public bool HasAtLeastOneColumnToStoreInDump { get; private set; }

        private const string IdentifierDumpCreatorStoredprocedure = "sp_createIdentifierDump";


        public IdentifierDumper(TableInfo tableInfo)
        {
            TableInfo = tableInfo;
            var columnsToDump = tableInfo.PreLoadDiscardedColumns;
            
            //something is destined for the identifier dump
            HasAtLeastOneColumnToStoreInDump = columnsToDump.Any(c => c.GoesIntoIdentifierDump());
            
            if (HasAtLeastOneColumnToStoreInDump)
                if (tableInfo.IdentifierDumpServer_ID == null)//id dump server is missing
                    throw new ArgumentException("TableInfo " + tableInfo.Name + " does not have a listed IdentifierDump ExternalDatabaseServer but has some columns configured as  DiscardedColumnDestination.StoreInIdentifiersDump or DiscardedColumnDestination.Dilute, go into the PreLoadDiscarded columns configuration window and select a Server to dump identifiers into");
                else
                {
                    //the place to store identifiers (at least those that are StoreInIdentifiersDump)
                    _externalDatabaseServer = tableInfo.IdentifierDumpServer;
                    _dumpDatabase = DataAccessPortal.GetInstance().ExpectDatabase(_externalDatabaseServer, DataAccessContext.DataLoad);
                }

            ColumnsToRouteToSomewhereElse = new List<PreLoadDiscardedColumn>(columnsToDump);
        }


        public void DumpAllIdentifiersInTable(DataTable inDataTable)
        {
            if (HasAtLeastOneColumnToStoreInDump)
            {
                //bulk insert into STAGING
                using (var con = (SqlConnection)_dumpDatabase.Server.GetConnection())
                {
                    if(con.State != ConnectionState.Open)
                        con.Open();

                    SqlBulkCopy bulkCopy = new SqlBulkCopy(con);
                    bulkCopy.DestinationTableName = GetStagingRuntimeName();
            
                    List<string> uniqueNamesAdded = new List<string>();

                    //wire up the identifiers
                    foreach (PreLoadDiscardedColumn column in ColumnsToRouteToSomewhereElse.Where(c => c.GoesIntoIdentifierDump()))
                    {
                        bulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(column.RuntimeColumnName,column.RuntimeColumnName));
                        uniqueNamesAdded.Add(column.RuntimeColumnName);
                    }

                    var pks = TableInfo.ColumnInfos.Where(c => c.IsPrimaryKey).ToArray();
            
                    //wire up the primary keys
                    foreach (ColumnInfo pk in pks)
                    {
                        var pkName = pk.GetRuntimeName(LoadStage.AdjustRaw);

                        //if we have not already added it (can be the case if there is a PreLoadDiscardedColumn which is also a primary key e.g. in the case of dilution)
                        if (uniqueNamesAdded.Contains(pkName))
                            continue;

                        uniqueNamesAdded.Add(pkName);
                        bulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(pkName,pkName));
                        
                        
                    }

                    try
                    {
                        bulkCopy.WriteToServer(inDataTable);
                    }
                    catch (Exception e)
                    {
                        throw new Exception("IdentifierDumper STAGING insert (" + bulkCopy.DestinationTableName + ") failed, make sure you have called CreateSTAGINGTable() before trying to Dump identifiers (also you should call DropStagging() when you are done)",e);
                    }
                    MergeStagingWithLive(pks.Select(col => col.GetRuntimeName(LoadStage.AdjustRaw)).ToArray());
                    
                }
            }
            
            //now drop the columns
            foreach (PreLoadDiscardedColumn preLoadDiscardedColumn in ColumnsToRouteToSomewhereElse)
                if (inDataTable.Columns.Contains(preLoadDiscardedColumn.RuntimeColumnName))
                {
                    if (preLoadDiscardedColumn.Destination != DiscardedColumnDestination.Dilute)
                        inDataTable.Columns.Remove(preLoadDiscardedColumn.RuntimeColumnName);
                }
                else
                    throw new Exception("Could not find " + preLoadDiscardedColumn.RuntimeColumnName + " in pipeline column collection");
        }

     

        private void MergeStagingWithLive(string[] pks)
        {

            using (var con = _dumpDatabase.Server.GetConnection())
            {
                con.Open();

                string[] allColumns =
                    pks.Select(col => col) //get the primary keys
                        .Union(
                            ColumnsToRouteToSomewhereElse.Where(
                                c => c.GoesIntoIdentifierDump()) //and the columns due to end up in the dump
                                .Select(dump => dump.RuntimeColumnName))
                        .ToArray();

                //INSERT NEW RECORDS
                //"MERGE [Demography]..[GP_ULTRA] AS dest USING [DLE_STAGING]..[Demography_GP_ULTRA_STAGING] AS source ON (source.[gmc] = dest.[gmc] AND source.[gp_code] = dest.[gp_code] AND source.[practice_code] = dest.[practice_code] AND source.[date_into_practice] = dest.[date_into_practice]) WHEN NOT MATCHED BY TARGET THEN INSERT ([notes], [gmc], [gp_code], [gp_cksum], [practice_code], [practice_cksum], [surname], [forename], [initials], [date_into_practice], [date_out_of_practice], hic_dataLoadRunID) VALUES (source.[notes], source.[gmc], source.[gp_code], source.[gp_cksum], source.[practice_code], source.[practice_cksum], source.[surname], source.[forename], source.[initials], source.[date_into_practice], source.[date_out_of_practice], 4718) OUTPUT $action, inserted.*;"
                string mergeSql = "MERGE " + Environment.NewLine;
                mergeSql += GetRuntimeName() + " AS dest " + Environment.NewLine;
                mergeSql += "USING " + GetStagingRuntimeName() + " AS source " + Environment.NewLine;
                mergeSql += "ON  (" + Environment.NewLine;
                mergeSql = pks.Aggregate(mergeSql,(s,n)=>s + " source.["+n+"]=dest.["+n+"] AND").TrimEnd(new []{'A','N','D',' '}) + Environment.NewLine;
                mergeSql += ") WHEN NOT MATCHED BY TARGET THEN INSERT (" + Environment.NewLine;
                mergeSql = allColumns.Aggregate(mergeSql, (s, n) => s + "[" + n + "],").TrimEnd(new[] { ',', ' '}) + Environment.NewLine;
                mergeSql += ") VALUES (" + Environment.NewLine;
                mergeSql = allColumns.Aggregate(mergeSql, (s, n) => s + " source.[" + n + "],").TrimEnd(new[] { ',', ' ' }) + Environment.NewLine;
                mergeSql += ");" + Environment.NewLine;

                DbCommand cmdInsert = _dumpDatabase.Server.GetCommand(mergeSql, con);
                cmdInsert.CommandTimeout = Timeout;
                cmdInsert.ExecuteNonQuery();
            
                //PERFORM overwrite with UPDATES
                string updateSql = "WITH ToUpdate AS (" + Environment.NewLine;
                updateSql += "SELECT stag.* FROM " + GetStagingRuntimeName() + " AS stag" + Environment.NewLine;
                updateSql += "LEFT OUTER JOIN " + GetRuntimeName() + " AS prod" + Environment.NewLine;
                updateSql += "ON ( " + Environment.NewLine;
                updateSql += "/*Primary Keys JOIN*/" + Environment.NewLine;
                updateSql = pks.Aggregate(updateSql, (s, n) => s + " stag.[" + n + "]=prod.[" + n + "] AND").TrimEnd(new[] { 'A', 'N', 'D', ' ' }) + Environment.NewLine;
                updateSql += ") WHERE" + Environment.NewLine;
                updateSql += "/*Primary Keys not null*/" + Environment.NewLine;
                updateSql = pks.Aggregate(updateSql, (s, n) => s + " stag.[" + n + "] IS NOT NULL AND").TrimEnd(new[] { 'A', 'N', 'D', ' ' }) + Environment.NewLine;
                updateSql += "AND EXISTS (SELECT " + Environment.NewLine;
                updateSql += "/*All columns in stag*/" + Environment.NewLine;
                updateSql = allColumns.Aggregate(updateSql, (s, n) => s + " stag.[" + n + "],").TrimEnd(new[] { ',', ' ' }) + Environment.NewLine;
                updateSql += "EXCEPT SELECT" + Environment.NewLine;
                updateSql = allColumns.Aggregate(updateSql, (s, n) => s + " prod.[" + n + "],").TrimEnd(new[] { ',', ' ' }) + Environment.NewLine;
                updateSql += "))" + Environment.NewLine;

                updateSql += Environment.NewLine;
                updateSql += "UPDATE prod SET" + Environment.NewLine;
                updateSql = allColumns.Aggregate(updateSql, (s, n) => s + " prod.[" + n + "]=ToUpdate.["+n+"],").TrimEnd(new[] { ',' }) + Environment.NewLine;
                updateSql += "FROM " + GetRuntimeName() + " AS prod " + Environment.NewLine;
                updateSql += "INNER JOIN ToUpdate ON " + Environment.NewLine;
                updateSql += "(" + Environment.NewLine;
                updateSql = pks.Aggregate(updateSql, (s, n) => s + " prod.[" + n + "]=ToUpdate.[" + n + "] AND").TrimEnd(new[] { 'A', 'N', 'D', ' ' }) + Environment.NewLine; 
                updateSql += ")" + Environment.NewLine;

                DbCommand updateCommand = _dumpDatabase.Server.GetCommand(updateSql,con);
                updateCommand.CommandTimeout = Timeout;
                updateCommand.ExecuteNonQuery();


                DbCommand cmdtruncateIdentifiersArchive = _dumpDatabase.Server.GetCommand("TRUNCATE TABLE " + GetStagingRuntimeName(), con);

                if(!cmdtruncateIdentifiersArchive.CommandText.Contains("_STAGING"))
                    throw new Exception("Were about to run a command that TRUNCATED a non staging table!");
                //clear the table now
                cmdtruncateIdentifiersArchive.ExecuteNonQuery();
                
            }
        }

        public void CreateSTAGINGTable()
        {
            //dont bother if there are no fields going to identifier dump - some could still be going to oblivion
            if(!HasAtLeastOneColumnToStoreInDump)
                return;

            using (var con = _dumpDatabase.Server.GetConnection())
            {
                con.Open();
                DbCommand cmdCreateSTAGING = _dumpDatabase.Server.GetCommand("SELECT TOP 0 * INTO " + GetStagingRuntimeName() + " FROM " + GetRuntimeName(), con);
                cmdCreateSTAGING.ExecuteNonQuery();
            }
        }
        
        public void LoadCompletedSoDispose(ExitCodeType exitCode,IDataLoadEventListener postLoadEventListener)
        {
            if(exitCode == ExitCodeType.Error)
                return;

            DropStaging();
        }
        
        public void Check(ICheckNotifier notifier)
        {

            var columnsToDump = TableInfo.PreLoadDiscardedColumns;
            var duplicates = columnsToDump.GroupBy(k => k.GetRuntimeName()).Where(c => c.Count() > 1).ToArray();

            foreach (var duplicate in duplicates)
                notifier.OnCheckPerformed(
                    new CheckEventArgs(
                        "There are " + duplicate.Count() + " PreLoadDiscardedColumns called '" + duplicate.Key + "' for TableInfo '" +
                        TableInfo + "'", CheckResult.Fail));
                
            //columns that exist in live but are supposedly dropped during load
            var liveColumns = TableInfo.ColumnInfos.ToArray();

            
            foreach (var preLoadDiscardedColumn in columnsToDump)
            {
                var match = liveColumns.FirstOrDefault(c => c.GetRuntimeName().Equals(preLoadDiscardedColumn.GetRuntimeName()));

                if(match != null)
                {
                    if(preLoadDiscardedColumn.Destination != DiscardedColumnDestination.Dilute)
                    {
                        notifier.OnCheckPerformed(new CheckEventArgs("TableInfo "+TableInfo+" declares both a PreLoadDiscardedColumn '" + preLoadDiscardedColumn + "' and a ColumnInfo with the same name", CheckResult.Fail));
                        return;
                    }

                    if (match.IsPrimaryKey && preLoadDiscardedColumn.Destination == DiscardedColumnDestination.Dilute)
                    {
                        notifier.OnCheckPerformed(new CheckEventArgs("TableInfo " + TableInfo + " declares a PreLoadDiscardedColumn '" + preLoadDiscardedColumn + "' but there is a matching ColumnInfo of the same name which IsPrimaryKey", CheckResult.Fail));
                        return;
                    }
                }
            }
            
            if(!HasAtLeastOneColumnToStoreInDump)
            {
                notifier.OnCheckPerformed(new CheckEventArgs("No columns require dumping from TableInfo " + _tableInfo + " so checking is not needed", CheckResult.Success, null));
                return;
            }
            
            var tables = _dumpDatabase.DiscoverTables(false);

            bool stagingTableFound = tables.Any(t => t.GetRuntimeName().Equals(GetStagingRuntimeName()));

            ConfirmDependencies(_dumpDatabase, notifier);

            //detect ongoing loads/dirty cleanup
            if (stagingTableFound)
            {
                bool shouldDrop = notifier.OnCheckPerformed(new CheckEventArgs("STAGING table found " + GetStagingRuntimeName() + " in ANO database",
                    CheckResult.Fail, null, "Drop table " + GetStagingRuntimeName()));

                if(shouldDrop)
                    DropStaging();
            }
            else
                notifier.OnCheckPerformed(new CheckEventArgs("Confirmed absence of Table  " + GetStagingRuntimeName() + "(this will be created during load)",CheckResult.Success, null));

            //confirm that there is a ColumnInfo for every Dilute column
            var columnInfos = _tableInfo.ColumnInfos.ToArray();
            foreach (var dilutedColumn in ColumnsToRouteToSomewhereElse.Where(c=>c.Destination == DiscardedColumnDestination.Dilute))
            {
                if(!columnInfos.Any(c=>c.GetRuntimeName().Equals(dilutedColumn.RuntimeColumnName)))
                    notifier.OnCheckPerformed(new CheckEventArgs("PreLoadDiscardedColumn called " + dilutedColumn.GetRuntimeName() +
                                          " is marked for Dilution but does not appear in the TableInfo object's ColumnInfo collection.  Diluted columns must appear both in the LIVE database (in diluted state) and in IdentifierDump (in pristene state) which means that for every PreLoadDiscardedColumn which has the destination Dilution, there must be a ColumnInfo with the same name in LIVE",CheckResult.Fail, null));
            }

            //if there are any columns due to be stored in the Identifier dump
            if(ColumnsToRouteToSomewhereElse.Any(c=>c.GoesIntoIdentifierDump()))
            {
                //see if table exists
                IdentifierDumperSynchronizer synchronizer = new IdentifierDumperSynchronizer(this,_externalDatabaseServer);
                synchronizer.Synchronize(notifier);
                
                //make sure there is a backup trigger enabled on the Identifier dump so that we version updates
                TriggerChecks triggerChecker = new TriggerChecks(_dumpDatabase.ExpectTable( GetRuntimeName()), true); // primary keys - ignoring transforms for ANO
               triggerChecker.Check(notifier);
            }
        }

        public void DropStaging()
        {
            //if we weren't asked to dump anything then we wouldn't have created a staging table in the first place
            if(!HasAtLeastOneColumnToStoreInDump)
                return;

            using (var con = _dumpDatabase.Server.GetConnection())
            {
                con.Open();
                DbCommand cmdDropSTAGING = _dumpDatabase.Server.GetCommand("DROP TABLE " + GetStagingRuntimeName(), con);

                if (!cmdDropSTAGING.CommandText.Contains("STAGING"))
                    throw new Exception("Expected comand " + cmdDropSTAGING.CommandText + " to have the word STAGING in it, do not drop a live ANO table that would be the worst of things!");

                cmdDropSTAGING.ExecuteNonQuery();
            }
        }
        
        private string GetStagingRuntimeName()
        {
            return "ID_" + _tableInfo.GetRuntimeName() + "_STAGING";
        }

        public string GetRuntimeName()
        {
            return "ID_" + _tableInfo.GetRuntimeName();
        }

        public void CreateIdentifierDumpTable(ColumnInfo[] primaryKeyColumnInfos)
        {

            using (var con = (SqlConnection)_dumpDatabase.Server.GetConnection())
           {
               con.Open(); 

              DataTable pks = new DataTable();
              pks.Columns.Add("RuntimeName");
              pks.Columns.Add("DataType");

              foreach (ColumnInfo columnInfo in primaryKeyColumnInfos)
              {
                  string runtimeName = columnInfo.GetRuntimeName(LoadStage.AdjustRaw);
                  string dataType = columnInfo.GetRuntimeDataType(LoadStage.AdjustRaw);
                  
                  pks.Rows.Add(new object[] { runtimeName, dataType });
              }

              DataTable dumpColumns = new DataTable();
              dumpColumns.Columns.Add("RuntimeName");
              dumpColumns.Columns.Add("DataType");
              
              foreach (PreLoadDiscardedColumn discardedColumn in _tableInfo.PreLoadDiscardedColumns.Where(d=>d.GoesIntoIdentifierDump()))
              {
                  if(discardedColumn.RuntimeColumnName.StartsWith("ANO"))
                      throw new Exception("Why are you trying to discard column " + discardedColumn.RuntimeColumnName + ", it looks like an ANO column in which case it should have an ANOTable transform rather than being a dump field.");
                  
                  if(discardedColumn.SqlDataType == null)
                      throw new Exception(discardedColumn.GetType().Name + " called " + discardedColumn.RuntimeColumnName + " does not have an assigned type");
                  
                  dumpColumns.Rows.Add(new object[] { discardedColumn.RuntimeColumnName, discardedColumn.SqlDataType});
              }

              
              if(dumpColumns.Rows.Count == 0)
                  throw new Exception("Cannot create an identifier dump with no dump columns");
              
              SqlCommand cmdCreate = new SqlCommand("EXEC " + IdentifierDumpCreatorStoredprocedure + " @liveTableName,@primaryKeys,@dumpColumns",con);

              cmdCreate.Parameters.AddWithValue("@liveTableName", _tableInfo.GetRuntimeName());

              cmdCreate.Parameters.AddWithValue("@primaryKeys", pks);
              cmdCreate.Parameters["@primaryKeys"].SqlDbType = SqlDbType.Structured;
              cmdCreate.Parameters["@primaryKeys"].TypeName = "dbo.ColumnInfo";

              cmdCreate.Parameters.AddWithValue("@dumpColumns", dumpColumns);
              cmdCreate.Parameters["@dumpColumns"].SqlDbType = SqlDbType.Structured;
              cmdCreate.Parameters["@dumpColumns"].TypeName = "dbo.ColumnInfo";
              
              cmdCreate.ExecuteNonQuery();
               
           }
            
        }


        public void Synchronize(ICheckNotifier notifier)
        {

            //there are no columns going to dump (because constructor didnt give us a server)
            if(_externalDatabaseServer == null)
                return;

            IdentifierDumperSynchronizer synchronizer = new IdentifierDumperSynchronizer(this,_externalDatabaseServer);
            synchronizer.Synchronize(notifier);
        }

        public static void ConfirmDependencies(DiscoveredDatabase dbInfo,ICheckNotifier notifier)
        {
            try
            {
                DiscoveredStoredprocedure[] procedures = dbInfo.DiscoverStoredprocedures();

                if (procedures.Any(p=>p.Name.Equals(IdentifierDumpCreatorStoredprocedure)))
                    notifier.OnCheckPerformed(new CheckEventArgs("Found stored procedure " + IdentifierDumpCreatorStoredprocedure +
                                              " on " + dbInfo, CheckResult.Success, null));
                else
                    notifier.OnCheckPerformed(new CheckEventArgs("Connected successfully to server "+dbInfo+" but did not find the stored procedure " + IdentifierDumpCreatorStoredprocedure +
                                              " in the database (Possibly the ExternalDatabaseServer is not an IdentifierDump database?)",CheckResult.Fail, null));
            }
            catch (Exception e)
            {
                notifier.OnCheckPerformed(new CheckEventArgs(
                    "Exception occurred when trying to find stored procedure " + IdentifierDumpCreatorStoredprocedure +
                    " on " + dbInfo, CheckResult.Fail, e));
            }
        }
    }
}