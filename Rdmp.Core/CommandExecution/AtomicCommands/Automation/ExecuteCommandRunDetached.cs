﻿// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Diagnostics;
using SixLabors.ImageSharp;
using System.IO;
using Rdmp.Core.CommandLine.Options;
using Rdmp.Core.Icons.IconProvision;
using ReusableLibraryCode;
using ReusableLibraryCode.Icons.IconProvision;
using SixLabors.ImageSharp.PixelFormats;

namespace Rdmp.Core.CommandExecution.AtomicCommands.Automation
{
    public class ExecuteCommandRunDetached : AutomationCommandExecution, IAtomicCommand
    {
        private string _rdmpBinaryPath;

        public ExecuteCommandRunDetached(IBasicActivateItems activator, Func<RDMPCommandLineOptions> commandGetter)
            : base(activator, commandGetter)
        {
            _rdmpBinaryPath = Path.Combine(UsefulStuff.GetExecutableDirectory().FullName, "cli", AutomationServiceExecutable);

            if (!File.Exists(_rdmpBinaryPath))
                SetImpossible($"{_rdmpBinaryPath} did not exist");

            if (!BasicActivator.IsAbleToLaunchSubprocesses)
            {
                SetImpossible($"Client does not support launching subprocesses");
            }
        }

        public override string GetCommandHelp()
        {
            return "Generates the execute command line invocation (including arguments)";
        }

        public override Image<Rgba32> GetImage(IIconProvider iconProvider)
        {
            return Image.Load<Rgba32>(CatalogueIcons.Exe);
        }

        public override void Execute()
        {
            base.Execute();
            BasicActivator.LaunchSubprocess(new ProcessStartInfo(_rdmpBinaryPath, GetCommandText(true)));
        }
    }
}