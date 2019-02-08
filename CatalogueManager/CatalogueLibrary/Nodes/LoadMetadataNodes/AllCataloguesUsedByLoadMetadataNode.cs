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
using CatalogueLibrary.Data.Cohort;
using CatalogueLibrary.Data.DataLoad;

namespace CatalogueLibrary.Nodes.LoadMetadataNodes
{
    /// <summary>
    /// Collection of all the <see cref="Catalogue"/>s which are currently associated with a given <see cref="Data.DataLoad.LoadMetadata"/>.  This governs
    /// which tables are created in RAW=>STAGING=>LIVE.
    /// </summary>
    public class AllCataloguesUsedByLoadMetadataNode : IOrderable
    {
        public LoadMetadata LoadMetadata { get; private set; }

        public AllCataloguesUsedByLoadMetadataNode(LoadMetadata lmd)
        {
            LoadMetadata = lmd;
        }

        public override string ToString()
        {
            return "Catalogues";
        }

        protected bool Equals(AllCataloguesUsedByLoadMetadataNode other)
        {
            return Equals(LoadMetadata, other.LoadMetadata);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((AllCataloguesUsedByLoadMetadataNode) obj);
        }

        public override int GetHashCode()
        {
            return (LoadMetadata != null ? LoadMetadata.GetHashCode() : 0);
        }

        public int Order { get { return 1; } set{} }
    }
}
