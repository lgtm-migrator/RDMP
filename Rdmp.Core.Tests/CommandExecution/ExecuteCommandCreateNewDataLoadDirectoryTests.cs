﻿// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using NUnit.Framework;
using Rdmp.Core.Curation.Data.DataLoad;
using System.IO;

namespace Rdmp.Core.Tests.CommandExecution
{
    class ExecuteCommandCreateNewDataLoadDirectoryTests : CommandCliTests
    {
        [Test]
        public void TestCreateNewDataLoadDirectory_CreateDeepFolder_NoLmd()
        {
            var root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "abc");
            if(Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
            var toCreate = Path.Combine(root, "def", "ghi");

            Run("CreateNewDataLoadDirectory","null", toCreate);

            Assert.IsTrue(Directory.Exists(root));
        }

        [Test]
        public void TestCreateNewDataLoadDirectory_WithLoadMetadata()
        {
            var root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "def");
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
            var lmd = WhenIHaveA<LoadMetadata>();

            Assert.IsNull(lmd.LocationOfFlatFiles);

            Run("CreateNewDataLoadDirectory", $"LoadMetadata:{lmd.ID}", root);

            Assert.IsTrue(Directory.Exists(root));
            Assert.AreEqual(root,lmd.LocationOfFlatFiles);
        }
    }
}
