﻿// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System.Collections.Generic;
using MapsDirectlyToDatabaseTable;
using NUnit.Framework;
using Rdmp.Core.CommandExecution;
using Rdmp.Core.CommandExecution.AtomicCommands;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.Cohort;
using Rdmp.Core.DataExport.Data;
using Rdmp.UI.CommandExecution.AtomicCommands;

namespace Rdmp.UI.Tests.CommandExecution
{
    class ExecuteCommandDeleteTests : UITests
    {

        [Test]
        public void Delete_IsSupportedCommand()
        {
            SetupMEF();
            var invoker = new CommandInvoker(ItemActivator);
            Assert.IsTrue(invoker.IsSupported(typeof(ExecuteCommandDelete)));
        }

        /// <summary>
        /// RDMPDEV-1551 Tests system behaviour when user selects a <see cref="CatalogueItem"/> and the <see cref="ExtractionInformation"/>
        /// that is the immediate child of it and issues a multi delete.  The first delete will also CASCADE delete the other
        /// </summary>
        [Test, UITimeout(50000)]
        public void TestDeleteMultiple_OneObjectInheritsAnother()
        {
            var ei = WhenIHaveA<ExtractionInformation>();
            var ci = ei.CatalogueItem;

            ItemActivator.YesNoResponse = true;

            Assert.IsTrue(ci.Exists());
            Assert.IsTrue(ei.Exists());

            //now because we don't actually have a CASCADE in memory we will have to fake it
            ei.DeleteInDatabase();

            var cmd = new ExecuteCommandDelete(ItemActivator, new IDeleteable[] { ci, ei });
            cmd.Execute();

            Assert.IsFalse(ci.Exists());
            Assert.IsFalse(ei.Exists());
        }

        [Test, UITimeout(50000)]
        public void Test_DeleteRootContainer_IsImpossible()
        {
            var container = WhenIHaveA<CohortAggregateContainer>();
            Assert.IsNotNull(container);
            Assert.IsTrue(container.IsRootContainer(),"expected it to be a root container");

            var cmd = new ExecuteCommandDelete(base.ItemActivator, container);

            Assert.IsTrue(cmd.IsImpossible,"expected command to be impossible");
            StringAssert.Contains("root container",cmd.ReasonCommandImpossible);

        }

        
        [Test, UITimeout(50000)]
        public void Test_Delete2RootContainers_IsImpossible()
        {
            var container1 = WhenIHaveA<CohortAggregateContainer>();
            
            var container2= WhenIHaveA<CohortAggregateContainer>();

            var cmd = new ExecuteCommandDelete(base.ItemActivator, new IDeleteable[]{container1,container2});

            Assert.IsTrue(cmd.IsImpossible,"expected command to be impossible");
            StringAssert.Contains("root container",cmd.ReasonCommandImpossible);

        }

        
        [Test, UITimeout(50000)]
        public void Test_DeleteNonRootContainer_Possible()
        {
            var container = WhenIHaveA<CohortAggregateContainer>();
            var subcontainer = WhenIHaveA<CohortAggregateContainer>();

            var cic = subcontainer.GetCohortIdentificationConfiguration();
            cic.RootCohortAggregateContainer_ID = null;
            cic.SaveToDatabase();

            container.AddChild(subcontainer);
            Assert.IsFalse(subcontainer.IsRootContainer(),"expected it not to be a root container");
            var cmd = new ExecuteCommandDelete(base.ItemActivator, subcontainer);

            Assert.IsFalse(cmd.IsImpossible,"expected command to be possible");

        }

    }
}
