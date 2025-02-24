// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Rdmp.UI.Tests.DesignPatternTests.ClassFileEvaluation
{
    public class ExplicitDatabaseNameChecker
    {
        public void FindProblems(List<string> csFilesFound)
        {
            Dictionary<string,string> problemFiles = new Dictionary<string, string>();
            List<string> prohibitedStrings = new List<string>();
            
            List<string> ignoreList = new List<string>();
            ignoreList.Add("ExplicitDatabaseNameChecker.cs"); //us obviously since we do contain that text!
            ignoreList.Add("DatabaseCreationProgramOptions.cs"); //allowed because it is the usage text for the program.
            ignoreList.Add("AutomationServiceOptions.cs");//allowed because it is the usage text for the program.
            ignoreList.Add("DatabaseTests.cs"); //allowed because it is telling user about how you can setup database tests support
            ignoreList.Add("ChoosePlatformDatabasesUI.Designer.cs"); //allowed because it is a suggestion to user about what prefix to use
            ignoreList.Add("PluginPackagerProgramOptions.cs"); //allwed because it's a suggestion to the user about command line arguments
            ignoreList.Add("DocumentationCrossExaminationTest.cs"); //allowed because its basically a list of comments that are allowed despite not appearing in the codebase
            ignoreList.Add("ResearchDataManagementPlatformOptions.cs"); //allowed because it's an Example


            ignoreList.AddRange(
                new string[]
                {
                    "DleOptions.cs",
                    "CacheOptions.cs",
                    "RDMPCommandLineOptions.cs",
                    "Settings.Designer.cs",
                    "PlatformDatabaseCreationOptions.cs",
                    "PackOptions.cs",
                    "PasswordEncryptionKeyLocation.cs"



                }); //allowed because it's default arguments for CLI

            prohibitedStrings.Add("TEST_");
            prohibitedStrings.Add("RDMP_");

            foreach (string file in csFilesFound)
            {
                if (ignoreList.Any(str=>str.Equals(Path.GetFileName(file))))
                    continue;
                
                var contents = File.ReadAllText(file);
                
                foreach (string prohibited in prohibitedStrings)
                    if (contents.Contains(prohibited))
                    {
                        problemFiles.Add(file, prohibited);
                        break;
                    }
            }

            foreach (var kvp in problemFiles)
                Console.WriteLine("FAIL: File '" + kvp.Key + "' contains a reference to an explicitly prohibited database name string ('" + kvp.Value + "')");

            Assert.AreEqual(0,problemFiles.Count);
        }
    }
}