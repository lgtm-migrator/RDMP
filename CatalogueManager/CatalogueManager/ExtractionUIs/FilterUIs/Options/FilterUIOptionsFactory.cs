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
using DataExportLibrary.Data.DataTables;

namespace CatalogueManager.ExtractionUIs.FilterUIs.Options
{
    public class FilterUIOptionsFactory
    {
        public FilterUIOptions Create(IFilter filter)
        {
            var aggregateFilter = filter as AggregateFilter;
            var deployedExtractionFilter = filter as DeployedExtractionFilter;
            var masterCatalogueFilter = filter as ExtractionFilter;

            if (aggregateFilter != null)
                return new AggregateFilterUIOptions(aggregateFilter);

            if (deployedExtractionFilter != null)
                return new DeployedExtractionFilterUIOptions(deployedExtractionFilter);

            if (masterCatalogueFilter != null)
                return new ExtractionFilterUIOptions(masterCatalogueFilter);

            throw new Exception("Expected IFilter '" + filter +
                                    "' to be either an AggregateFilter, DeployedExtractionFilter or a master ExtractionFilter but it was " +
                                    filter.GetType().Name);
        }
    }
}
