﻿// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using MapsDirectlyToDatabaseTable.Versioning;
using NUnit.Framework;
using Rdmp.Core.CohortCreation.Execution;
using Rdmp.Core.Curation;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.Aggregation;
using Rdmp.Core.Curation.Data.Cohort;
using Rdmp.Core.Curation.Data.Cohort.Joinables;
using Rdmp.Core.Databases;
using ReusableLibraryCode.Checks;
using System;
using System.Data;
using System.Linq;
using Tests.Common.Scenarios;
using Rdmp.Core.QueryCaching.Aggregation;
using static Rdmp.Core.CohortCreation.Execution.CohortCompilerRunner;

namespace Rdmp.Core.Tests.CohortCreation.QueryTests
{
    /// <summary>
    /// Tests caching the results of an <see cref="AggregateConfiguration"/> which hits up multiple underlying tables.
    /// </summary>
    class CohortCompilerCacheJoinableTest:FromToDatabaseTests
    {
        [Test]
        public void CohortIdentificationConfiguration_Join_PatientIndexTable()
        {
            DataTable header = new DataTable();
            header.Columns.Add("ID");
            header.Columns.Add("Chi");
            header.Columns.Add("Age");
            header.Columns.Add("Date");
            header.Columns.Add("Healthboard");
            header.PrimaryKey = new []{header.Columns["ID"] };

            header.Rows.Add("1","0101010101",50,new DateTime(2001,1,1),"T");
            header.Rows.Add("2", "0202020202", 50, new DateTime(2002, 2, 2), "T");

            var hTbl = From.CreateTable("header",header);
            var cata = Import(hTbl,out var hTi, out _);
            cata.Name = "My Combo Join Catalogue";
            cata.SaveToDatabase();

            var scripter = new MasterDatabaseScriptExecutor(To);
            var patcher = new QueryCachingPatcher();
            scripter.CreateAndPatchDatabase(patcher,new AcceptAllCheckNotifier());
            var edsCache = new ExternalDatabaseServer(CatalogueRepository,"Cache", new QueryCachingPatcher());
            edsCache.SetProperties(To);

            DataTable results = new DataTable();
            results.Columns.Add("Header_ID");
            results.Columns.Add("TestCode");
            results.Columns.Add("Result");

            results.Rows.Add("1","HBA1C",50);
            results.Rows.Add("1", "ECOM", "Hi fellas");
            results.Rows.Add("1", "ALB", 100);
            results.Rows.Add("2", "ALB", 50);

            var rTbl = From.CreateTable("results", results);
            
            var importer = new TableInfoImporter(CatalogueRepository,rTbl);
            importer.DoImport(out var rTi,out ColumnInfo[] rColInfos);

            var fe = new ForwardEngineerCatalogue(rTi,rColInfos);
            fe.ExecuteForwardEngineering(cata);
            
            //Should now be 1 Catalogue with all the columns (tables will have to be joined to build the query though)
            Assert.AreEqual(8,cata.GetAllExtractionInformation(ExtractionCategory.Core).Length);

            var ji = new JoinInfo(CatalogueRepository,
                rTi.ColumnInfos.Single(ci=>ci.GetRuntimeName().Equals("Header_ID",StringComparison.CurrentCultureIgnoreCase)),
                hTi.ColumnInfos.Single(ci => ci.GetRuntimeName().Equals("ID", StringComparison.CurrentCultureIgnoreCase)),
                ExtractionJoinType.Right,
                null
                );

            //setup a cic that uses the cache
            var cic = new CohortIdentificationConfiguration(CatalogueRepository,"MyCic");
            cic.CreateRootContainerIfNotExists();
            cic.QueryCachingServer_ID = edsCache.ID;
            cic.SaveToDatabase();

            //create a patient index table that shows all the times that they had a test in any HB (with the HB being part of the result set)
            var acPatIndex = new AggregateConfiguration(CatalogueRepository,cata,"My PatIndes");
            
            var eiChi = cata.GetAllExtractionInformation(ExtractionCategory.Core).Single(ei => ei.GetRuntimeName().Equals("Chi"));
            eiChi.IsExtractionIdentifier = true;
            acPatIndex.CountSQL = null;
            eiChi.SaveToDatabase();

            acPatIndex.AddDimension(eiChi);
            acPatIndex.AddDimension(cata.GetAllExtractionInformation(ExtractionCategory.Core).Single(ei => ei.GetRuntimeName().Equals("Date")));
            acPatIndex.AddDimension(cata.GetAllExtractionInformation(ExtractionCategory.Core).Single(ei => ei.GetRuntimeName().Equals("Healthboard")));
            
            cic.EnsureNamingConvention(acPatIndex);

            var joinable = new JoinableCohortAggregateConfiguration(CatalogueRepository,cic,acPatIndex);
            
            Assert.IsTrue(acPatIndex.IsCohortIdentificationAggregate);
            Assert.IsTrue(acPatIndex.IsJoinablePatientIndexTable());

            var compiler = new CohortCompiler(cic);

            var runner = new CohortCompilerRunner(compiler,50);

            var cancellation = new System.Threading.CancellationToken();
            runner.Run(cancellation);

            //they should not be executing and should be completed
            Assert.IsFalse(compiler.Tasks.Any(t=>t.Value.IsExecuting));
            Assert.AreEqual(Phase.Finished,runner.ExecutionPhase);

            var manager = new CachedAggregateConfigurationResultsManager(edsCache);

            var cacheTableName = manager.GetLatestResultsTableUnsafe(acPatIndex,AggregateOperation.JoinableInceptionQuery);

            Assert.IsNotNull(cacheTableName,"No results were cached!");

            var cacheTable = To.ExpectTable(cacheTableName.GetRuntimeName());
            
            //chi, Date and TestCode
            Assert.AreEqual(3,cacheTable.DiscoverColumns().Length);

            //healthboard should be a string
            Assert.AreEqual(typeof(string),cacheTable.DiscoverColumn("Healthboard").DataType.GetCSharpDataType());

            /*  Query Cache contains this:
             *
Chi	Date	Healthboard
0101010101	2001-01-01 00:00:00.0000000	T
0202020202	2002-02-02 00:00:00.0000000	T
*/

            Assert.AreEqual(2, cacheTable.GetRowCount());

            //Now we could add a new AggregateConfiguration that uses the joinable!
        }
    }
}
