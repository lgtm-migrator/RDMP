﻿// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using Rdmp.Core.Curation.Data.Referencing;
using Rdmp.Core.Repositories;
using System;
using System.Collections.Generic;
using System.Data.Common;

namespace Rdmp.Core.Curation.Data
{
    /// <summary>
    /// Describes a point in time state of another <see cref="DatabaseEntity"/>.  Note that the state may be invalid if other
    /// objects have been since deleted.  e.g. if user updates the <see cref="Catalogue.TimeCoverage_ExtractionInformation_ID"/> 
    /// the memento would point to an old <see cref="ExtractionInformation"/> which may be subsequently deleted
    /// </summary>
    public class Memento : ReferenceOtherObjectDatabaseEntity
    {

        #region Database Properties
        private string _username;
        private DateTime _date;
        private Guid _transaction;
        private string _beforeYaml;
        private string _afterYaml;

        public string Username
        {
            get { return _username; }
            set { SetField(ref _username, value); }
        }

        public DateTime Date
        {
            get { return _date; }
            set { SetField(ref _date, value); }
        }
        public Guid Transaction
        {
            get { return _transaction; }
            set { SetField(ref _transaction, value); }
        }
        public string BeforeYaml
        {
            get { return _beforeYaml; }
            set { SetField(ref _beforeYaml, value); }
        }
        public string AfterYaml
        {
            get { return _afterYaml; }
            set { SetField(ref _afterYaml, value); }
        }
        #endregion


        public Memento()
        {

        }
        public Memento(ICatalogueRepository repo,DbDataReader r) : base(repo, r)
        {
            Transaction = new Guid(r["Transaction"].ToString());
            Username = r["Username"].ToString();
            Date = Convert.ToDateTime(r["Date"]);
            BeforeYaml = r["BeforeYaml"].ToString();
            AfterYaml = r["AfterYaml"].ToString();
        }
        public Memento(ICatalogueRepository repository, Guid transaction, DatabaseEntity entity,string beforeYaml, string afterYaml)
        {
            repository.InsertAndHydrate(this, new Dictionary<string, object>
            {
                {"ReferencedObjectID",entity.ID},
                {"ReferencedObjectType",entity.GetType().Name},
                {"ReferencedObjectRepositoryType",entity.Repository.GetType().Name},
                {"Username", Environment.UserName},
                {"Date", DateTime.Now},
                {"Transaction",transaction.ToString("N")},
                {"BeforeYaml",beforeYaml},
                {"AfterYaml",afterYaml},
            });
        }
    }
}
