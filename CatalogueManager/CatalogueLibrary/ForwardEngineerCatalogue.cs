// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System.Collections.Generic;
using CatalogueLibrary.Data;
using CatalogueLibrary.Repositories;

namespace CatalogueLibrary
{
    /// <summary>
    /// Creates a Catalogue from a TableInfo (See TableInfoImporter for how to create a TableInfo from your live database table).  A Catalogue is an extractable dataset
    /// which can be made by joining multiple underlying tables and often contains only a subset of columns (those that are extractable to researchers).
    /// </summary>
    public class ForwardEngineerCatalogue
    {
        private readonly TableInfo _tableInfo;
        private readonly ColumnInfo[] _columnInfos;
        private readonly bool _markAllExtractable;

        /// <summary>
        /// Sets up the class to create a new <see cref="Catalogue"/> from the supplied table reference
        /// </summary>
        /// <param name="tableInfo"></param>
        /// <param name="columnInfos"></param>
        /// <param name="markAllExtractable"></param>
        public ForwardEngineerCatalogue(TableInfo tableInfo, ColumnInfo[] columnInfos, bool markAllExtractable = false)
        {
            _tableInfo = tableInfo;
            _columnInfos = columnInfos;
            _markAllExtractable = markAllExtractable;
        }


        /// <inheritdoc cref="ExecuteForwardEngineering()"/>
        public void ExecuteForwardEngineering(out Catalogue catalogue, out CatalogueItem[] items, out ExtractionInformation[] extractionInformations)
        {
            ExecuteForwardEngineering(null, out catalogue, out items, out extractionInformations);
        }

        /// <summary>
        /// Creates a new <see cref="Catalogue"/> with <see cref="CatalogueItem"/> and <see cref="ExtractionInformation"/> with a one-to-one mapping to
        ///  the <see cref="ColumnInfo"/> this class was constructed with.
        /// </summary>
        public void ExecuteForwardEngineering()
        {
            Catalogue whoCaresCata;
            CatalogueItem[] whoCaresItems;
            ExtractionInformation[] whoCaresInformations;

            ExecuteForwardEngineering(null,out whoCaresCata,out whoCaresItems,out whoCaresInformations);
        }

        /// <summary>
        /// Creates new <see cref="CatalogueItem"/> and <see cref="ExtractionInformation"/> with a one-to-one mapping to the <see cref="ColumnInfo"/> this class was constructed with.
        /// 
        /// <para>These new columns are added to an existing <see cref="Catalogue"/>.  Use this if you want a dataset that draws data from 2 tables using a <see cref="JoinInfo"/></para>
        /// </summary>
        /// <param name="intoExistingCatalogue"></param>
        public void ExecuteForwardEngineering(Catalogue intoExistingCatalogue)
        {
            Catalogue whoCaresCata;
            CatalogueItem[] whoCaresItems;
            ExtractionInformation[] whoCaresInformations;

            ExecuteForwardEngineering(intoExistingCatalogue, out whoCaresCata, out whoCaresItems, out whoCaresInformations);
        }

        /// <inheritdoc cref="ExecuteForwardEngineering()"/>
        public void ExecuteForwardEngineering(Catalogue intoExistingCatalogue,out Catalogue catalogue, out CatalogueItem[] catalogueItems, out ExtractionInformation[] extractionInformations)
        {
            var repo = _tableInfo.CatalogueRepository;

            //if user did not specify an existing catalogue to supplement 
            if (intoExistingCatalogue == null)
                //create a new (empty) catalogue and treat that as the new target
                intoExistingCatalogue = new Catalogue(repo, _tableInfo.GetRuntimeName());

            catalogue = intoExistingCatalogue;
            List<CatalogueItem> catalogueItemsCreated = new List<CatalogueItem>();
            List<ExtractionInformation> extractionInformationsCreated = new List<ExtractionInformation>();

            int order = 0;

            //for each column we will add a new one to the 
            foreach (ColumnInfo col in _columnInfos)
            {
                order++;
                
                //create it with the same name
                CatalogueItem cataItem = new CatalogueItem(repo, intoExistingCatalogue, col.Name.Substring(col.Name.LastIndexOf(".") + 1).Trim('[', ']', '`'));
                catalogueItemsCreated.Add(cataItem);

                if (_markAllExtractable)
                {
                    var newExtractionInfo = new ExtractionInformation(repo, cataItem, col, col.Name);
                    newExtractionInfo.Order = order;
                    newExtractionInfo.SaveToDatabase();
                    extractionInformationsCreated.Add(newExtractionInfo);
                }
                else
                {
                    cataItem.ColumnInfo_ID =  col.ID;
                    cataItem.SaveToDatabase();
                }
            }

            extractionInformations = extractionInformationsCreated.ToArray();
            catalogueItems = catalogueItemsCreated.ToArray();
        }
    }
}