﻿// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using MapsDirectlyToDatabaseTable;
using Rdmp.Core.CommandExecution;
using Rdmp.Core.CommandLine.Interactive.Picking;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.Repositories;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.Progress;

namespace Rdmp.Core.CommandLine.Runners
{
    /// <summary>
    /// Abstract base implementation of <see cref="IRunner"/> with convenience methods
    /// </summary>
    public abstract class Runner: IRunner
    {
        public abstract int Run(IRDMPPlatformRepositoryServiceLocator repositoryLocator, IDataLoadEventListener listener, ICheckNotifier checkNotifier, GracefulCancellationToken token);

        /// <summary>
        /// Translates a string <paramref name="arg"/> into an object of type <typeparamref name="T"/>.  String can 
        /// just be the ID e.g. "5" or could be an RDMP command line expression e.g. "LoadMetadata:*Load*Biochemistry*"
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="locator"></param>
        /// <param name="arg"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">Thrown if it is not possible to parse <paramref name="arg"/> into an existing object</exception>
        protected T GetObjectFromCommandLineString<T>(IRDMPPlatformRepositoryServiceLocator locator, string arg) where T : IMapsDirectlyToDatabaseTable
        {
            if (int.TryParse(arg, out var id))
                return locator.CatalogueRepository.GetObjectByID<T>(id);

            var picker = new CommandLineObjectPicker(new[] { arg }, new ThrowImmediatelyActivator(locator));
            if (!picker[0].HasValueOfType(typeof(T)))
                throw new ArgumentException($"Could not translate '{arg}' into a valid object of Type '{typeof(T).Name}'.  The referenced object may not exist or has been renamed.");

            return (T)picker[0].GetValueForParameterOfType(typeof(T));
        }
    }
}
