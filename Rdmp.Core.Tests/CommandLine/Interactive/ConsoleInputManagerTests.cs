// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Rdmp.Core.CommandExecution;
using Rdmp.Core.CommandLine.Interactive;
using ReusableLibraryCode.Checks;
using Tests.Common;

namespace Rdmp.Core.Tests.CommandLine.Interactive
{
    class ConsoleInputManagerTests : UnitTests
    {
        [Test]
        public void TestDisallowInput()
        {
            var manager = new ConsoleInputManager(RepositoryLocator, new ThrowImmediatelyCheckNotifier());
            manager.DisallowInput = true;
            
            Assert.Throws<InputDisallowedException>(()=>manager.GetString(new DialogArgs { WindowTitle = "bob" }, null));
        }
    }
}
