// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CatalogueLibrary;
using CatalogueLibrary.DataFlowPipeline;
using DataLoadEngine.Job;
using LoadModules.Generic.DataProvider.FlatFileManipulation;
using DataLoadEngineTests.Resources;
using NUnit.Framework;
using ReusableLibraryCode;
using ReusableLibraryCode.Progress;

namespace DataLoadEngineTests.Integration
{
    [Category("Integration")]
    public class ExcelConversionTest
    {
        private readonly Stack<DirectoryInfo> _dirsToCleanUp = new Stack<DirectoryInfo>();
        private DirectoryInfo _parentDir;
        bool officeInstalled = false;

        [OneTimeSetUp]
        public void SetUp()
        {
            officeInstalled = OfficeVersionFinder.GetVersion(OfficeVersionFinder.OfficeComponent.Excel) != null;

            var testDir = new DirectoryInfo(TestContext.CurrentContext.WorkDirectory);
            _parentDir = testDir.CreateSubdirectory("ExcelConversionTest");
            _dirsToCleanUp.Push(_parentDir);
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            while (_dirsToCleanUp.Any())
                _dirsToCleanUp.Pop().Delete(true);
        }

        private HICProjectDirectory CreateHICProjectDirectoryForTest(string directoryName)
        {
            var hicProjectDirectory = HICProjectDirectory.CreateDirectoryStructure(_parentDir, directoryName);
            _dirsToCleanUp.Push(hicProjectDirectory.RootPath);
            return hicProjectDirectory;
        }

        [Test]
        public void TestExcelFunctionality_OnSimpleXlsx()
        {
            if (!officeInstalled)
                Assert.Inconclusive();

            var hicProjectDirectory = CreateHICProjectDirectoryForTest("TestExcelFunctionality_OnSimpleXlsx");

            //clean up anything in the test project folders forloading directory
            foreach (FileInfo fileInfo in hicProjectDirectory.ForLoading.GetFiles())
                fileInfo.Delete();

            string targetFile = Path.Combine(hicProjectDirectory.ForLoading.FullName, "Test.xlsx");
            File.WriteAllBytes(targetFile, Resource1.TestExcelFile1);

            TestConversionFor(targetFile, "*.xlsx", 5, hicProjectDirectory);
        }

        [Test]
        public void TestExcelFunctionality_DodgyFileExtension()
        {
            if (!officeInstalled)
                Assert.Inconclusive();

            var hicProjectDirectory = CreateHICProjectDirectoryForTest("TestExcelFunctionality_DodgyFileExtension");

            //clean up anything in the test project folders forloading directory
            foreach (FileInfo fileInfo in hicProjectDirectory.ForLoading.GetFiles())
                fileInfo.Delete();

            string targetFile = Path.Combine(hicProjectDirectory.ForLoading.FullName, "Test.xml");
            File.WriteAllText(targetFile, Resource1.TestExcelFile2);

            var ex = Assert.Throws<Exception>(()=>TestConversionFor(targetFile, "*.fish", 1, hicProjectDirectory));

            Assert.IsTrue(ex.Message.StartsWith("Did not find any files matching Pattern '*.fish' in directory"));
        }


        [Test]
        public void TestExcelFunctionality_OnExcelXml()
        {
            if (!officeInstalled)
                Assert.Inconclusive();

            var hicProjectDirectory = CreateHICProjectDirectoryForTest("TestExcelFunctionality_OnExcelXml");

            //clean up anything in the test project folders forloading directory
            foreach (FileInfo fileInfo in hicProjectDirectory.ForLoading.GetFiles())
                fileInfo.Delete();


            string targetFile = Path.Combine(hicProjectDirectory.ForLoading.FullName, "Test.xml");
            File.WriteAllText(targetFile, Resource1.TestExcelFile2);

            TestConversionFor(targetFile, "*.xml", 1, hicProjectDirectory);

        }

        private void TestConversionFor(string targetFile,string fileExtensionToConvert, int expectedNumberOfSheets, HICProjectDirectory hicProjectDirectory)
        {
            FileInfo f = new FileInfo(targetFile);

            try
            {
                Assert.IsTrue(f.Exists);
                Assert.IsTrue(f.Length > 100);

                ExcelToCSVFilesConverter converter = new ExcelToCSVFilesConverter();

                var job = new ThrowImmediatelyDataLoadJob(new ThrowImmediatelyDataLoadEventListener(){ThrowOnWarning =  true, WriteToConsole =  true});
                job.HICProjectDirectory = hicProjectDirectory;

                converter.ExcelFilePattern = fileExtensionToConvert;
                converter.Fetch(job, new GracefulCancellationToken());

                FileInfo[] filesCreated = hicProjectDirectory.ForLoading.GetFiles("*.csv");

                Assert.AreEqual(expectedNumberOfSheets,filesCreated.Length);

                foreach (FileInfo fileCreated in filesCreated)
                {
                    Assert.IsTrue(Regex.IsMatch(fileCreated.Name, "Sheet[0-9].csv"));
                    Assert.GreaterOrEqual(fileCreated.Length, 100);
                    fileCreated.Delete();
                }
            }
            finally
            {
                f.Delete();
            }
        }
    }
}
