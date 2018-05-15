﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ANOStore.ANOEngineering;
using CatalogueLibrary;
using CatalogueLibrary.Data;
using CatalogueLibrary.Data.DataLoad;
using CatalogueLibrary.DataHelper;
using MapsDirectlyToDatabaseTable.Attributes;
using NUnit.Framework;
using ReusableLibraryCode;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.DatabaseHelpers.Discovery;
using Tests.Common;

namespace AnonymisationTests
{
    public class ForwardEngineerANOCatalogueTwoTableTests : TestsRequiringANOStore
    {

        TableInfo t1;
        ColumnInfo[] c1;

        TableInfo t2;
        ColumnInfo[] c2;

        private Catalogue cata1;
        private Catalogue cata2;

        private CatalogueItem[] cataItems1;
        private CatalogueItem[] cataItems2;

        private ExtractionInformation[] eis1;
        private ExtractionInformation[] eis2;
        private ANOTable _anoTable;
        private Catalogue _comboCata;
        private DiscoveredDatabase _destinationDatabase;

        [SetUp]
        public void SetupExampleTables()
        {
            string sql =
            @"CREATE TABLE [dbo].[Tests](
	[chi] [varchar](10) NULL,
	[Date] [datetime] NULL,
	[hb_extract] [varchar](1) NULL,
	[TestId] [int] NOT NULL,
 CONSTRAINT [PK_Tests] PRIMARY KEY CLUSTERED 
(
	[TestId] ASC
)
) 

GO

CREATE TABLE [dbo].[Results](
	[TestId] [int] NOT NULL,
	[Measure] [varchar](10) NOT NULL,
	[Value] [int] NULL,
 CONSTRAINT [PK_Results] PRIMARY KEY CLUSTERED 
(
	[TestId] ASC,
	[Measure] ASC
)
)

GO

ALTER TABLE [dbo].[Results]  WITH CHECK ADD  CONSTRAINT [FK_Results_Tests] FOREIGN KEY([TestId])
REFERENCES [dbo].[Tests] ([TestId])
GO";
            var server = DiscoveredDatabaseICanCreateRandomTablesIn.Server;
            using (var con = server.GetConnection())
            {
                con.Open();
                UsefulStuff.ExecuteBatchNonQuery(sql,con);
            }

            var importer1 = new TableInfoImporter(CatalogueRepository, DiscoveredDatabaseICanCreateRandomTablesIn.ExpectTable("Tests"));
            var importer2 = new TableInfoImporter(CatalogueRepository, DiscoveredDatabaseICanCreateRandomTablesIn.ExpectTable("Results"));

            importer1.DoImport(out t1,out c1);
            
            importer2.DoImport(out t2, out c2);

            var engineer1 = new ForwardEngineerCatalogue(t1, c1, true);
            var engineer2 = new ForwardEngineerCatalogue(t2, c2, true);

            engineer1.ExecuteForwardEngineering(out cata1,out cataItems1,out eis1);
            engineer2.ExecuteForwardEngineering(out cata2, out cataItems2, out eis2);

            CatalogueRepository.JoinInfoFinder.AddJoinInfo(
                c1.Single(e => e.GetRuntimeName().Equals("TestId")),
                c2.Single(e => e.GetRuntimeName().Equals("TestId")),
                ExtractionJoinType.Left,null);

            _anoTable = new ANOTable(CatalogueRepository, ANOStore_ExternalDatabaseServer, "ANOTes", "T");
            _anoTable.NumberOfCharactersToUseInAnonymousRepresentation = 10;
            _anoTable.SaveToDatabase();
            _anoTable.PushToANOServerAsNewTable("int",new ThrowImmediatelyCheckNotifier());
            
            _comboCata = new Catalogue(CatalogueRepository, "Combo Catalogue");
            
            //pk
            var ciTestId = new CatalogueItem(CatalogueRepository, _comboCata, "TestId");
            var colTestId = c1.Single(c => c.GetRuntimeName().Equals("TestId"));
            ciTestId.ColumnInfo_ID = colTestId.ID;
            ciTestId.SaveToDatabase();
            var eiTestId = new ExtractionInformation(CatalogueRepository, ciTestId, colTestId, colTestId.Name);

            //Measure
            var ciMeasure = new CatalogueItem(CatalogueRepository, _comboCata, "Measuree");
            var colMeasure = c2.Single(c => c.GetRuntimeName().Equals("Measure"));
            ciMeasure.ColumnInfo_ID = colMeasure.ID;
            ciMeasure.SaveToDatabase();
            var eiMeasure = new ExtractionInformation(CatalogueRepository, ciMeasure,colMeasure, colMeasure.Name);

            //Date
            var ciDate = new CatalogueItem(CatalogueRepository, _comboCata, "Dat");

            var colDate = c1.Single(c => c.GetRuntimeName().Equals("Date"));
            ciDate.ColumnInfo_ID = colDate.ID;
            ciDate.SaveToDatabase();
            var eiDate = new ExtractionInformation(CatalogueRepository, ciDate, colDate, colDate.Name);

            var destDatabaseName = TestDatabaseNames.GetConsistentName("ANOMigrationTwoTableTests");

            _destinationDatabase = DiscoveredServerICanCreateRandomDatabasesAndTablesOn.ExpectDatabase(destDatabaseName);
            _destinationDatabase.Create(true);
        }


        [Test]
        public void TestAnonymisingJoinKey()
        {
            var plan1 = new ForwardEngineerANOCataloguePlanManager(RepositoryLocator, cata1);
            var testIdHeadPlan = plan1.GetPlanForColumnInfo(c1.Single(c => c.GetRuntimeName().Equals("TestId")));
            plan1.TargetDatabase = _destinationDatabase;

            testIdHeadPlan.Plan = Plan.ANO;
            testIdHeadPlan.ANOTable = _anoTable;

            plan1.Check(new ThrowImmediatelyCheckNotifier());


            var engine1 = new ForwardEngineerANOCatalogueEngine(RepositoryLocator, plan1);
            engine1.Execute();

            var plan1ExtractionInformationsAtDestination = engine1.NewCatalogue.GetAllExtractionInformation(ExtractionCategory.Any);

            var ei1 = plan1ExtractionInformationsAtDestination.Single(e => e.GetRuntimeName().Equals("ANOTestId"));
            Assert.IsTrue(ei1.Exists());

            var plan2 = new ForwardEngineerANOCataloguePlanManager(RepositoryLocator, _comboCata);
            plan2.SkippedTables.Add(t1);
            plan2.TargetDatabase = _destinationDatabase;
            plan2.Check(new ThrowImmediatelyCheckNotifier());

            var engine2 = new ForwardEngineerANOCatalogueEngine(RepositoryLocator,plan2);
            engine2.Execute();

            var plan2ExtractionInformationsAtDestination = engine2.NewCatalogue.GetAllExtractionInformation(ExtractionCategory.Any);

            var ei2 = plan2ExtractionInformationsAtDestination.Single(e => e.GetRuntimeName().Equals("ANOTestId"));
            Assert.IsTrue(ei2.Exists());


        }

        [TearDown]
        public void DropDatabases()
        {
            if(_destinationDatabase.Exists())
                _destinationDatabase.ForceDrop();
        }
    }
}
