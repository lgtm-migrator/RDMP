// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using MapsDirectlyToDatabaseTable;
using Newtonsoft.Json;
using NUnit.Framework;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.ImportExport;
using Rdmp.Core.Curation.Data.Serialization;
using Rdmp.Core.Repositories;
using Tests.Common;

namespace Rdmp.Core.Tests.Curation.JsonSerializationTests
{
    public class JsonSerializationTests:DatabaseTests
    {
        [Test]
        public void TestSerialization_Catalogue()
        {
            if (CatalogueRepository is not TableRepository)
                Assert.Inconclusive("This test does not apply for non db repos");

            Catalogue c = new Catalogue(RepositoryLocator.CatalogueRepository,"Fish");
            
            MySerializeableTestClass mySerializeable = new MySerializeableTestClass(new ShareManager(RepositoryLocator));
            mySerializeable.SelectedCatalogue = c;
            mySerializeable.Title = "War and Pieces";
            
            var dbConverter = new DatabaseEntityJsonConverter(RepositoryLocator);
            var lazyConverter = new PickAnyConstructorJsonConverter(RepositoryLocator);


            var asString = JsonConvert.SerializeObject(mySerializeable, dbConverter,lazyConverter);
            var mySerializeableAfter = (MySerializeableTestClass)JsonConvert.DeserializeObject(asString, typeof(MySerializeableTestClass), new JsonConverter[] { dbConverter, lazyConverter });

            Assert.AreNotEqual(mySerializeable, mySerializeableAfter);
            Assert.AreEqual(mySerializeable.SelectedCatalogue, mySerializeableAfter.SelectedCatalogue);
            Assert.AreEqual(mySerializeable.SelectedCatalogue.Name, mySerializeableAfter.SelectedCatalogue.Name);
            Assert.AreEqual("War and Pieces", mySerializeableAfter.Title);
            mySerializeableAfter.SelectedCatalogue.Name = "Cannon balls";
            mySerializeableAfter.SelectedCatalogue.SaveToDatabase();
            
            Assert.AreNotEqual(mySerializeable.SelectedCatalogue.Name, mySerializeableAfter.SelectedCatalogue.Name);
        }

        //todo null Catalogue test case
    }
    
    
    public class MySerializeableTestClass
    {
        public string Title { get; set; }

        public Catalogue SelectedCatalogue { get; set; }

        private readonly ShareManager _sm;

        public MySerializeableTestClass(IRDMPPlatformRepositoryServiceLocator locator)
        {
            _sm = new ShareManager(locator);
        }
        
        public MySerializeableTestClass(ShareManager sm)
        {
            _sm = sm;
        }
    }

}
