﻿using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using MapsDirectlyToDatabaseTable;
using Rdmp.Core.Curation.Data;
using ReusableLibraryCode.Checks;

namespace Rdmp.Core.CommandLine.Interactive
{
    public class CommandLineObjectPickerArgumentValue
    {

        public string RawValue { get; }
        public int Index { get; }

        public ReadOnlyCollection<IMapsDirectlyToDatabaseTable> DatabaseEntities { get; }

        public CommandLineObjectPickerArgumentValue(string rawValue,int idx)
        {
            RawValue = rawValue;
            Index = idx;
        }

        public CommandLineObjectPickerArgumentValue(string rawValue,int idx,IMapsDirectlyToDatabaseTable[] entities):this(rawValue, idx)
        {
            DatabaseEntities = new ReadOnlyCollection<IMapsDirectlyToDatabaseTable>(entities);
        }

        /// <summary>
        /// Returns the contents of this class expressed as the given <paramref name="paramType"/> or null if the current state
        /// does not describe an object of that Type
        /// </summary>
        /// <param name="paramType"></param>
        /// <returns></returns>
        public object GetValueForParameterOfType(Type paramType)
        {
            if (typeof(DirectoryInfo).IsAssignableFrom(paramType))
                return new DirectoryInfo(RawValue);

            if (typeof(string) == paramType)
                return RawValue;

            //it's an array of DatabaseEntities
            if(paramType.IsArray && typeof(DatabaseEntity).IsAssignableFrom(paramType.GetElementType()))
                return DatabaseEntities;
            
            if (typeof(DatabaseEntity).IsAssignableFrom(paramType))
                return GetOneDatabaseEntity<DatabaseEntity>();

            if (typeof(IMightBeDeprecated).IsAssignableFrom(paramType))
                return GetOneDatabaseEntity<IMightBeDeprecated>();

            if (typeof (IDisableable) == paramType)
                return GetOneDatabaseEntity<IDisableable>();

            if (typeof (INamed) == paramType)
                return GetOneDatabaseEntity<INamed>();
            
            if (typeof (IDeleteable) == paramType)
                return GetOneDatabaseEntity<IDeleteable>();
            
            if (typeof(ICheckable) == paramType)
                return GetOneDatabaseEntity<ICheckable>();
            
            if (paramType.IsValueType && !typeof(Enum).IsAssignableFrom(paramType))
                return Convert.ChangeType(RawValue, paramType);
            
            return null;
        }

        private object GetOneDatabaseEntity<T>()
        {
            //if there are not exactly 1 database entity
            if (DatabaseEntities == null || DatabaseEntities.Count == 1)
                return null;

            //return the single object as the type you want e.g. ICheckable
            if(DatabaseEntities[0] is T)
                return DatabaseEntities.Single();
            
            //it's not ICheckable, user made an invalid object selection
            throw new CommandLineObjectPickerParseException($"Specified object was not an '{typeof(T)}''",Index,RawValue);
        }

        public bool HasValueOfType(Type paramType)
        {
            try
            {
                return GetValueForParameterOfType(paramType) != null;
            }
            catch (Exception e)
            {
                //could be parse error or something
                Console.WriteLine($"Bad parameter.  Expected Type '{paramType}' for index {Index}.  Error was:{e.Message}");
                return false;
            }
        }
    }
}