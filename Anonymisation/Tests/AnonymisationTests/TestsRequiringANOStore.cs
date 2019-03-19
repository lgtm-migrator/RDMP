// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Data.SqlClient;
using System.Linq;
using CatalogueLibrary.Data;
using CatalogueLibrary.Data.DataLoad;
using CatalogueLibrary.Data.Defaults;
using FAnsi.Discovery;
using MapsDirectlyToDatabaseTable.Versioning;
using NUnit.Framework;
using ReusableLibraryCode.Checks;
using Tests.Common;

namespace AnonymisationTests
{
    public class TestsRequiringANOStore:DatabaseTests
    {
        protected ExternalDatabaseServer ANOStore_ExternalDatabaseServer { get; set; }
        protected DiscoveredDatabase ANOStore_Database { get; set; }
        protected string ANOStore_DatabaseName = TestDatabaseNames.GetConsistentName("ANOStore");

        [OneTimeSetUp]
        public void Setup()
        {
            ANOStore_Database = DiscoveredServerICanCreateRandomDatabasesAndTablesOn.ExpectDatabase(ANOStore_DatabaseName);
            
            CreateANODatabase();

            CreateReferenceInCatalogueToANODatabase();

            var t = typeof (ANOStore.Class1);
            Console.WriteLine(t.Name);
        }

        private void DropANODatabase()
        {
            if(ANOStore_Database.Exists())
                ANOStore_Database.Drop();
        }

        [OneTimeTearDown]
        public virtual void FixtureTearDown()
        {
            RemovePreExistingReference();

            // Remove the database from the server
            DropANODatabase();
        }

        private void CreateANODatabase()
        {
            DropANODatabase();

            var ano = typeof(ANOStore.Database.Class1);
            Console.WriteLine("Was ANOStore.Database.dll also loaded into memory:" + ano);

            var scriptCreate = new MasterDatabaseScriptExecutor(ANOStore_Database);
            scriptCreate.CreateAndPatchDatabase(typeof(ANOStore.Class1).Assembly, new ThrowImmediatelyCheckNotifier());
        }

        private void CreateReferenceInCatalogueToANODatabase()
        {
            RemovePreExistingReference();

            //now create a new reference!
            ANOStore_ExternalDatabaseServer = new ExternalDatabaseServer(CatalogueRepository, ANOStore_DatabaseName,typeof(ANOStore.Class1).Assembly);
            ANOStore_ExternalDatabaseServer.SetProperties(ANOStore_Database);

            CatalogueRepository.GetServerDefaults().SetDefault(PermissableDefaults.ANOStore, ANOStore_ExternalDatabaseServer);
        }

        private void RemovePreExistingReference()
        {
            //There will likely be an old reference to the external database server
            var preExisting = CatalogueRepository.GetAllObjects<ExternalDatabaseServer>().SingleOrDefault(e => e.Name.Equals(ANOStore_DatabaseName));

            if (preExisting == null) return;

            //Some child tests will likely create ANOTables that reference this server so we need to cleanup those for them so that we can cleanup the old server reference too
            foreach (var lingeringTablesReferencingServer in CatalogueRepository.GetAllObjects<ANOTable>().Where(a => a.Server_ID == preExisting.ID))
            {
                //unhook the anonymisation transform from any ColumnInfos using it
                foreach (ColumnInfo colWithANOTransform in CatalogueRepository.GetAllObjects<ColumnInfo>().Where(c => c.ANOTable_ID == lingeringTablesReferencingServer.ID))
                {
                    Console.WriteLine("Unhooked ColumnInfo " + colWithANOTransform + " from ANOTable " + lingeringTablesReferencingServer);
                    colWithANOTransform.ANOTable_ID = null;
                    colWithANOTransform.SaveToDatabase();
                }
                
                TruncateANOTable(lingeringTablesReferencingServer);
                lingeringTablesReferencingServer.DeleteInDatabase();
            }

            //now delete the old server reference
            preExisting.DeleteInDatabase();
        }

        protected void TruncateANOTable(ANOTable anoTable)
        {
            Console.WriteLine("Truncating table " + anoTable.TableName + " on server " + ANOStore_ExternalDatabaseServer);
            
            var server = ANOStore_Database.Server;
            using (var con = server.GetConnection())
            {
                con.Open();
                var cmdDelete = server.GetCommand("if exists (select top 1 * from sys.tables where name ='" + anoTable.TableName + "') TRUNCATE TABLE " + anoTable.TableName, con);
                cmdDelete.ExecuteNonQuery();
                con.Close();
            }
        
        }
    }
}
