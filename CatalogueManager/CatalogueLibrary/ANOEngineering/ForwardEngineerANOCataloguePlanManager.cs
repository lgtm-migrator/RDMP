﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using CatalogueLibrary.Data;
using CatalogueLibrary.Data.DataLoad;
using CatalogueLibrary.QueryBuilding;
using CatalogueLibrary.Refactoring;
using CatalogueLibrary.Repositories;
using CatalogueLibrary.Repositories.Construction;
using MapsDirectlyToDatabaseTable;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.DatabaseHelpers.Discovery;
using ReusableLibraryCode.DatabaseHelpers.Discovery.QuerySyntax;
using ReusableLibraryCode.DatabaseHelpers.Discovery.TypeTranslation;

namespace CatalogueLibrary.ANOEngineering
{
    /// <summary>
    /// Configuration class for ForwardEngineerANOCatalogueEngine (See ForwardEngineerANOCatalogueEngine).  This class stores which anonymisation transforms/dilutions
    /// etc to apply to which columns, which TableInfos are to be mirated etc.  Also stores whether the LoadMetadata that is to be created should be a single one off
    /// load or should load in date based batches (e.g. 1 year at a time - use this option if you have too much data in the source table to be migrated in one go - e.g.
    /// tens of millions of records). 
    /// </summary>
    public class ForwardEngineerANOCataloguePlanManager : ICheckable
    {
        public Catalogue Catalogue { get; private set; }
        private ExtractionInformation[] _allExtractionInformations;
        private CatalogueItem[] _allCatalogueItems;
        
        private readonly Dictionary<ColumnInfo,Plan> Plans = new Dictionary<ColumnInfo, Plan>();
        private readonly Dictionary<ColumnInfo, ANOTable> PlannedANOTables = new Dictionary<ColumnInfo, ANOTable>();
        private readonly Dictionary<ColumnInfo, IDilutionOperation> PlannedDilution = new Dictionary<ColumnInfo, IDilutionOperation>();
        private IQuerySyntaxHelper _querySyntaxHelper;

        public List<IDilutionOperation>  DilutionOperations { get; private set; }

        public TableInfo[] TableInfos { get; private set; }

        public DiscoveredDatabase TargetDatabase { get; set; }
        public ColumnInfo DateColumn { get; set; }
        public DateTime? StartDate { get; set; }

        public HashSet<TableInfo> SkippedTables = new HashSet<TableInfo>();

        public ForwardEngineerANOCataloguePlanManager(Catalogue catalogue)
        {
            Catalogue = catalogue;

            RefreshTableInfos();

            DilutionOperations = new List<IDilutionOperation>();
            
            ObjectConstructor constructor = new ObjectConstructor();

            foreach (var operationType in ((CatalogueRepository) catalogue.Repository).MEF.GetTypes<IDilutionOperation>())
                DilutionOperations.Add((IDilutionOperation) constructor.Construct(operationType));
            
            _querySyntaxHelper = TableInfos.Select(t => t.GetQuerySyntaxHelper()).FirstOrDefault();
        }

        public string GetEndpointDataType(ColumnInfo col)
        {
            var sourceTypeTranslater = _querySyntaxHelper.TypeTranslater;

            //if we have picked a destination
            ITypeTranslater destinationTypeTranslater;
            if (TargetDatabase != null)
                destinationTypeTranslater = TargetDatabase.Server.GetQuerySyntaxHelper().TypeTranslater;//ensure we handle type translation between the two platforms
            else
                destinationTypeTranslater = sourceTypeTranslater;//otherwise (we haven't picked a destination yet)

            switch (GetPlanForColumnInfo(col))
            {
                case Plan.Drop:
                    return null;
                case Plan.ANO:
                    var anoTable = GetPlannedANOTable(col);

                    if (anoTable == null)
                        return "Unknown";

                    return sourceTypeTranslater.TranslateSQLDBType(anoTable.GetRuntimeDataType(LoadStage.PostLoad),destinationTypeTranslater);
                case Plan.Dilute:
                    var dilution = GetPlannedDilution(col);

                    if (dilution == null)
                        return "Unknown";

                    return destinationTypeTranslater.GetSQLDBTypeForCSharpType(dilution.ExpectedDestinationType);

                case Plan.PassThroughUnchanged:

                    return sourceTypeTranslater.TranslateSQLDBType(col.Data_type, destinationTypeTranslater);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public Plan GetPlanForColumnInfo(ColumnInfo col)
        {
            if(!Plans.ContainsKey(col))
                return Plan.Drop;

            return Plans[col];
        }
        
        public void SetPlan(ColumnInfo col, Plan plan)
        {
            var oldPlan = GetPlanForColumnInfo(col);

            //no change
            if(oldPlan == plan)
                return;

            if (IsMandatoryForMigration(col) && plan == Plan.Drop)
                throw new ArgumentException("Cannot drop column '" + col + "' because it is Mandatory (An ExtractionInformation exists for it in Catalogue(s))", "col");

            //change plan
            Plans[col] = plan;

            //Set diluteness dictionary key depending on plan
            if(plan == Plan.Dilute)
                PlannedDilution.Add(col,null);
            else if (PlannedDilution.ContainsKey(col))
                PlannedDilution.Remove(col);
            
            //Set ANO dictionary key depending on plan
            if(plan == Plan.ANO)
                PlannedANOTables.Add(col,null);
            else
                if(PlannedANOTables.ContainsKey(col)) //plan is not to ANO
                    PlannedANOTables.Remove(col);
        }

        public ANOTable GetPlannedANOTable(ColumnInfo col)
        {
            if (GetPlanForColumnInfo(col) == Plan.ANO)
                return PlannedANOTables[col];

            return null;
        }

        public void SetPlannedANOTable(ColumnInfo col, ANOTable anoTable)
        {
            SetPlan(col,Plan.ANO);
            
            PlannedANOTables[col] = anoTable;
        }

        public IDilutionOperation GetPlannedDilution(ColumnInfo ci)
        {
            if (PlannedDilution.ContainsKey(ci))
                return PlannedDilution[ci];

            return null;
        }

        public void SetPlannedDilution(ColumnInfo col, IDilutionOperation operation)
        {
            SetPlan(col,Plan.Dilute);
            
            PlannedDilution[col] = operation;
        }

        public IExternalDatabaseServer GetIdentifierDumpServer()
        {
            return new ServerDefaults((CatalogueRepository)Catalogue.Repository).GetDefaultFor(ServerDefaults.PermissableDefaults.IdentifierDumpServer_ID);
        }

        
        public bool IsMandatoryForMigration(ColumnInfo col)
        {
            return
                //ColumnInfo is part of the table primary key
                col.IsPrimaryKey;
        }

        public enum Plan
        {
            Drop,
            ANO,
            Dilute,
            PassThroughUnchanged
        }

        public void Check(ICheckNotifier notifier)
        {
            if (TargetDatabase == null)
                notifier.OnCheckPerformed(new CheckEventArgs("No TargetDatabase has been set", CheckResult.Fail));
            else
                if (!TargetDatabase.Exists())
                    notifier.OnCheckPerformed(new CheckEventArgs("TargetDatabase '"+TargetDatabase+"' does not exist", CheckResult.Fail));

            var toMigrateTables = TableInfos.Except(SkippedTables).ToArray();

            if (!toMigrateTables.Any())
                notifier.OnCheckPerformed(new CheckEventArgs("There are no TableInfos selected for anonymisation",CheckResult.Fail));

            try
            {
                
                var joinInfos = GetJoinInfosRequiredCatalogue();
                notifier.OnCheckPerformed(new CheckEventArgs("Generated Catalogue SQL succesfully", CheckResult.Success));

                foreach (JoinInfo joinInfo in joinInfos)
                    notifier.OnCheckPerformed(new CheckEventArgs("Found required JoinInfo '" + joinInfo + "' that will have to be migrated",CheckResult.Success));

                foreach (Lookup lookup in GetLookupsRequiredCatalogue())
                {
                    notifier.OnCheckPerformed(new CheckEventArgs("Found required Lookup '" + lookup + "' that will have to be migrated", CheckResult.Success));

                    //for each key involved in the lookup
                    foreach (ColumnInfo c in new[] { lookup.ForeignKey ,lookup.PrimaryKey,lookup.Description})
                    {
                        //lookup / table has already been migrated 
                        if(SkippedTables.Any(t=>t.ID == c.TableInfo_ID))
                            continue;

                        //make sure that the plan is sensible
                        if (GetPlanForColumnInfo(c) != Plan.PassThroughUnchanged)
                            notifier.OnCheckPerformed(new CheckEventArgs("ColumnInfo '" + c + "' is part of a Lookup so must PassThroughUnchanged", CheckResult.Fail));
                            
                    }
                }
            }
            catch (Exception ex)
            {
                notifier.OnCheckPerformed(new CheckEventArgs("Failed to generate Catalogue SQL", CheckResult.Fail,ex));
            }

            foreach (TableInfo tableInfo in toMigrateTables)
            {
                notifier.OnCheckPerformed(new CheckEventArgs("Evaluating TableInfo '" + tableInfo + "'",CheckResult.Success));

                if (TargetDatabase != null && TargetDatabase.ExpectTable(tableInfo.GetRuntimeName()).Exists())
                    notifier.OnCheckPerformed(new CheckEventArgs("Table '" + tableInfo + "' already exists in Database '" + TargetDatabase + "'",CheckResult.Fail));

                var pks = tableInfo.ColumnInfos.Where(c => c.IsPrimaryKey).ToArray();

                if(!pks.Any())
                    notifier.OnCheckPerformed(new CheckEventArgs("TableInfo '" + tableInfo + "' does not have any Primary Keys, it cannot be anonymised", CheckResult.Fail));

                foreach (ColumnInfo pk in pks)
                {
                    var plan = GetPlanForColumnInfo(pk);

                    if (plan == Plan.Dilute || plan == Plan.Drop)
                        notifier.OnCheckPerformed(new CheckEventArgs("Current plan for column '" + pk + "' ('" + plan +"') is invalid because it is a primary key", CheckResult.Fail));
                }

                if (tableInfo.IsTableValuedFunction)
                    notifier.OnCheckPerformed(new CheckEventArgs("TableInfo '" + tableInfo + "' is an IsTableValuedFunction so cannot be anonymised",CheckResult.Fail));

                EnsureNotAlreadySharedLocally(notifier,tableInfo);
                EnsureNotAlreadySharedLocally(notifier, Catalogue);
            }

            foreach (KeyValuePair<ColumnInfo, ANOTable> kvp in PlannedANOTables.Where(k=>k.Value == null))
                notifier.OnCheckPerformed(new CheckEventArgs("No ANOTable has been picked for ColumnInfo '" + kvp.Key + "'",CheckResult.Fail));

            foreach (KeyValuePair<ColumnInfo, IDilutionOperation> kvp in PlannedDilution.Where(k => k.Value == null))
                notifier.OnCheckPerformed(new CheckEventArgs("No Dilution Operation has been picked for ColumnInfo '" + kvp.Key + "'", CheckResult.Fail));
                
            if(PlannedDilution.Any())
                if (GetIdentifierDumpServer() == null)
                    notifier.OnCheckPerformed(new CheckEventArgs("No default Identifier Dump server has been configured", CheckResult.Fail));

            foreach (KeyValuePair<ColumnInfo, Plan> kvp in Plans)
            {
                if (kvp.Value != Plan.Drop)
                {
                    try
                    {
                        var datatype = GetEndpointDataType(kvp.Key);
                        notifier.OnCheckPerformed(new CheckEventArgs("Determined endpoint data type '"+datatype+"' for ColumnInfo '" + kvp.Key + "'", CheckResult.Success));

                    }
                    catch (Exception e)
                    {
                        notifier.OnCheckPerformed(new CheckEventArgs("Could not determine endpoint data type for ColumnInfo '" + kvp.Key + "'", CheckResult.Fail,e));
                    }
                }
            }

            if (DateColumn != null)
            {
                if(GetPlanForColumnInfo(DateColumn) != Plan.PassThroughUnchanged)
                    if(notifier.OnCheckPerformed(new CheckEventArgs("Plan for " + DateColumn + " must be PassThroughUnchanged",CheckResult.Fail,null,"Set plan to PassThroughUnchanged")))
                        SetPlan(DateColumn,Plan.PassThroughUnchanged);

                var usedTables = TableInfos.Except(SkippedTables).Count();
                
                if (usedTables > 1)
                    notifier.OnCheckPerformed(
                        new CheckEventArgs(
                            "You cannot have a date based migration because you are trying to migrate " + usedTables +
                            " TableInfos at once", CheckResult.Fail));

            }

            var refactorer = new SelectSQLRefactorer();

            foreach (ExtractionInformation e in _allExtractionInformations)
                if (!refactorer.IsRefactorable(e))
                    notifier.OnCheckPerformed(new CheckEventArgs("ExtractionInformation '" + e +"' is a not refactorable due to reason:"+ refactorer.GetReasonNotRefactorable(e), CheckResult.Fail));
        }

        private void EnsureNotAlreadySharedLocally<T>(ICheckNotifier notifier,T m) where T:IMapsDirectlyToDatabaseTable
        {
            var shareManager = ((CatalogueRepository)m.Repository).ShareManager;

            if (shareManager.IsExportedObject(m))
            {
                var existingExport = shareManager.GetExportFor(m);
                var existingImportReference = shareManager.GetExistingImport(existingExport.SharingUID);

                if (existingImportReference != null)
                {
                    T existingImportInstance = m.Repository.GetObjectByID<T>(existingImportReference.LocalObjectID);
                    notifier.OnCheckPerformed(new CheckEventArgs(typeof(T) + " '" + m + "' is already locally shared as '" + existingImportInstance + "'", CheckResult.Fail));
                }
            }
        }

        /// <summary>
        /// Re checks the TableInfos associated with the Catalogue incase some have changed
        /// </summary>
        public void RefreshTableInfos()
        {
            _allExtractionInformations = Catalogue.GetAllExtractionInformation(ExtractionCategory.Any);
            _allCatalogueItems = Catalogue.CatalogueItems.Where(ci => ci.ColumnInfo_ID != null).ToArray();

            TableInfos =
                _allCatalogueItems.Where(ci => IsMandatoryForMigration(ci.ColumnInfo))
                    .Select(ci => ci.ColumnInfo.TableInfo)
                    .Distinct()
                    .ToArray();

            //Remove unplanned columns
            foreach (var col in Plans.Keys.ToArray())
                if (!IsStillNeeded(col))
                    Plans.Remove(col);

            foreach (var col in PlannedANOTables.Keys.ToArray())
                if (!IsStillNeeded(col))
                    PlannedANOTables.Remove(col);

            foreach (var col in PlannedDilution.Keys.ToArray())
                if (!IsStillNeeded(col))
                    PlannedDilution.Remove(col);

            //Add new column infos
            foreach (ColumnInfo col in TableInfos.SelectMany(t => t.ColumnInfos))
                if(!Plans.ContainsKey(col))
                    Plans.Add(col, IsMandatoryForMigration(col) ? Plan.PassThroughUnchanged : Plan.Drop);
        }

        public List<JoinInfo> GetJoinInfosRequiredCatalogue()
        {
            var qb = new QueryBuilder(null, null);
            qb.AddColumnRange(Catalogue.GetAllExtractionInformation(ExtractionCategory.Any));
            qb.RegenerateSQL();
            return qb.JoinsUsedInQuery;
        }

        public List<Lookup> GetLookupsRequiredCatalogue()
        {
            var qb = new QueryBuilder(null, null);
            qb.AddColumnRange(Catalogue.GetAllExtractionInformation(ExtractionCategory.Any));
            qb.RegenerateSQL();

            return qb.GetDistinctRequiredLookups().ToList();
        }

        /// <summary>
        /// Make up ANOTable plans based on existing ANOTable/column usages.  For example if the column chi is being migrated and there is at least one column
        /// called chi or ANOchi already existing (probably from another table) then we should suggest using ANOTable ANOchi.  Calling this method will set the
        /// Plan for the matching columns and then return an array of the ColumnInfos where a match was found and Plan set
        /// </summary>
        /// <returns></returns>
        public ColumnInfo[] MakeSuggestions()
        {
            List<ColumnInfo> toReturn = new List<ColumnInfo>();

            var allColumnInfos = Catalogue.Repository.GetAllObjects<ColumnInfo>();

            //for each column info we know about
            foreach (var col in Plans.Keys.ToArray())
            {
                //if there is a ColumnInfo with the same name (or that has ANO prefix)
                var matchingOnName = allColumnInfos.Where(a =>a.GetRuntimeName() == col.GetRuntimeName() || a.GetRuntimeName() == "ANO" + col.GetRuntimeName()).ToArray();

                //and if the same named ColumnInfo(s) have a shared ANOTable (e.g. ANOCHI)
                var agreedAnoTableID = matchingOnName.Where(c=>c.ANOTable_ID != null).Select(c=>c.ANOTable_ID).Distinct().ToArray();

                //if there is a single recommended anotable id amongst all columns with matching name featuring ano tables 
                if (agreedAnoTableID.Count() == 1)
                {
                    SetPlannedANOTable(col,Catalogue.Repository.GetObjectByID<ANOTable>(agreedAnoTableID.Single().Value));
                    toReturn.Add(col);
                }
                //suggest dropping hic_ fields
                else if(col.GetRuntimeName().StartsWith("hic_"))
                {
                    SetPlan(col,Plan.Drop);
                    toReturn.Add(col);
                }
                //suggest that if there are CatalogueItems that reference the ColumnInfo and those CatalogueItems are extractable (have associated ExtractionInformation) that it goes through
                else
                    if(_allCatalogueItems.Any(
                        ci =>
                            ci.ColumnInfo_ID == col.ID &&
                            _allExtractionInformations.Any(ei => ei.CatalogueItem_ID == ci.ID)))
                {
                    SetPlan(col, Plan.PassThroughUnchanged);
                    toReturn.Add(col);
                }

            }
            
            return toReturn.ToArray();
        }
        
        private bool IsStillNeeded(ColumnInfo columnInfo)
        {
            return TableInfos.Any(t => t.ID == columnInfo.TableInfo_ID);
        }
    }
}
