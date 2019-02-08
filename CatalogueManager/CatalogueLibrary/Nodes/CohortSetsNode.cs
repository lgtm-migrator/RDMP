// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CatalogueLibrary.Data;
using CatalogueLibrary.Data.Aggregation;
using CatalogueLibrary.Data.Cohort;

namespace CatalogueLibrary.Nodes
{
    /// <summary>
    /// Collection of all <see cref="AggregateConfiguration"/> in a <see cref="Catalogue"/> that are involved in cohort creation (See <see cref="CohortIdentificationConfiguration"/>).
    /// </summary>
    public class CohortSetsNode
    {
        public Catalogue Catalogue { get; set; }

        public CohortSetsNode(Catalogue catalogue, AggregateConfiguration[] cohortAggregates)
        {
            Catalogue = catalogue;
        }

        public override string ToString()
        {
            return "Cohort Sets";
        }

        protected bool Equals(CohortSetsNode other)
        {
            return Catalogue.Equals(other.Catalogue);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((CohortSetsNode) obj);
        }

        public override int GetHashCode()
        {
            return Catalogue.GetHashCode() * this.GetType().GetHashCode();
        }
    }
}
