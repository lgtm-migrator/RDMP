﻿using System;
using System.IO;
using System.Linq;

namespace CatalogueLibrary
{
    /// <summary>
    /// Basic implementation of IHICProjectDirectory including support for creating new templates on the file system.
    /// </summary>
    public class HICProjectDirectory : IHICProjectDirectory
    {

        internal const string ExampleFixedWidthFormatFileContents = @"From,To,Field,Size,DateFormat
1,7,gmc,7,
8,12,gp_code,5,
13,32,surname,20,
33,52,forename,20,
53,55,initials,3,
56,60,practice_code,5,
61,68,date_into_practice,8,yyyyMMdd
69,76,date_out_of_practice,8,yyyyMMdd
";

        public HICProjectDirectory(string rootPath, bool useTestFolder)
        {
            Test = useTestFolder;
            if (string.IsNullOrWhiteSpace(rootPath))
                throw new Exception("Root path was blank, there is no HICProjectDirectory path specified?");

            RootPath = new DirectoryInfo(rootPath);

            if (RootPath.Name.Equals("Data", StringComparison.CurrentCultureIgnoreCase))
                throw new ArgumentException("HICProjectDirectory should be passed the root folder, not the Data folder");

            DataPath = new DirectoryInfo(Path.Combine(RootPath.FullName, useTestFolder ? "TestData" : "Data"));

            if (!DataPath.Exists)
                throw new DirectoryNotFoundException("Could not find directory '" + DataPath.FullName + "', every HICProjectDirectory must have a Data folder, the root folder was:" + RootPath);

            ForLoading = FindFolderInPathOrThrow(DataPath, "ForLoading");
            ForArchiving = FindFolderInPathOrThrow(DataPath, "ForArchiving");
            ForErrors = FindFolderInPathOrThrow(DataPath, "ForErrors");
            ExecutablesPath = FindFolderInPathOrThrow(RootPath, "Executables");
            Cache = FindFolderInPath(DataPath, "Cache");
        }

        private  DirectoryInfo FindFolderInPath(DirectoryInfo path, string folderName)
        {
            return path.EnumerateDirectories(folderName, SearchOption.TopDirectoryOnly).FirstOrDefault(); ;
        }

        private DirectoryInfo FindFolderInPathOrThrow(DirectoryInfo path, string folderName)
        {
            DirectoryInfo d = path.EnumerateDirectories(folderName, SearchOption.TopDirectoryOnly).FirstOrDefault();

            if (d == null)
                throw new DirectoryNotFoundException("This dataset requires the directory '" + folderName + "' located at " + Path.Combine(path.FullName, folderName));

            return d;
        }

        public DirectoryInfo ForLoading { get; private set; }
        public DirectoryInfo ForArchiving { get; private set; }
        public DirectoryInfo ForErrors { get; private set; }
        public DirectoryInfo Cache { get; private set; }
        public DirectoryInfo RootPath { get; private set; }
        public DirectoryInfo DataPath { get; private set; }
        public DirectoryInfo ExecutablesPath { get; private set; }
        
        public bool Test { get; private set; }
        
        public static HICProjectDirectory CreateDirectoryStructure(DirectoryInfo parentDir, string dirName, bool overrideExistsCheck = false)
        {
            if (!parentDir.Exists)
                throw new Exception("Cannot create directory structure in " + parentDir.FullName + " (it doesn't exist)");

            var projectDir = new DirectoryInfo(Path.Combine(parentDir.FullName, dirName));

            if (!overrideExistsCheck && projectDir.Exists)
                throw new Exception("The directory " + projectDir.FullName + " already exists (and we don't want to accidentally nuke anything)");
            
            projectDir.Create();

            return CreateDirectoryStructure(projectDir, overrideExistsCheck);
            
        }

        public static HICProjectDirectory CreateDirectoryStructure(DirectoryInfo projectDir, bool overrideExistsCheck = false)
        {
          
            var dataDir = projectDir.CreateSubdirectory("Data");
            dataDir.CreateSubdirectory("ForLoading");
            dataDir.CreateSubdirectory("ForArchiving");
            dataDir.CreateSubdirectory("ForErrors");
            dataDir.CreateSubdirectory("Cache");

            StreamWriter swExampleFixedWidth = new StreamWriter(Path.Combine(dataDir.FullName, "ExampleFixedWidthFormatFile.csv"));
            swExampleFixedWidth.Write(ExampleFixedWidthFormatFileContents);
            swExampleFixedWidth.Flush();
            swExampleFixedWidth.Close();

            projectDir.CreateSubdirectory("Executables");

            return new HICProjectDirectory(projectDir.FullName, false);
        }
    }
}
