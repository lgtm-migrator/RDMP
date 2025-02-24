// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;

namespace ReusableLibraryCode
{
    /// <summary>
    /// Checks whether two DirectoryInfo objects are the same based on FullName
    /// </summary>
    public class DirectoryInfoComparer : IEqualityComparer<DirectoryInfo>
    {
        public bool Equals(DirectoryInfo x, DirectoryInfo y)
        {
            if (object.ReferenceEquals(x, y))
                return true;
            if (x == null || y == null)
                return false;
            return x.FullName == y.FullName;
        }

        public int GetHashCode(DirectoryInfo obj)
        {
            if (obj == null)
                return 0;
            return obj.FullName.GetHashCode();
        }
    }

    public static class DirectoryInfoExtensions
    {
        public static void CopyAll(this DirectoryInfo source, DirectoryInfo target)
        {
            if (String.Equals(source.FullName, target.FullName, StringComparison.InvariantCultureIgnoreCase))
            {
                return;
            }

            // Check if the target directory exists, if not, create it.
            if (Directory.Exists(target.FullName) == false)
            {
                Directory.CreateDirectory(target.FullName);
            }

            // Copy each file into its new directory.
            foreach (FileInfo fi in source.GetFiles())
            {
                fi.CopyTo(Path.Combine(target.ToString(), fi.Name), true);
            }

            // Copy each subdirectory using recursion.
            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir = target.CreateSubdirectory(diSourceSubDir.Name);
                diSourceSubDir.CopyAll(nextTargetSubDir);
            }
        }
    }
}