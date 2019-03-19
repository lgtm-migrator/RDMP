// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Linq;
using CatalogueLibrary.Data;
using CatalogueLibrary.Data.Aggregation;
using CatalogueLibrary.Data.Dashboarding;
using CatalogueLibrary.QueryBuilding;
using CatalogueLibrary.Repositories;
using CatalogueLibrary.Spontaneous;
using CatalogueManager.AggregationUIs;
using CatalogueManager.ItemActivation;
using CatalogueManager.Refreshing;
using CatalogueManager.TestsAndSetup.ServicePropogation;
using DataExportLibrary.ExtractionTime.Commands;
using DataExportLibrary.ExtractionTime.UserPicks;
using FAnsi.Discovery.QuerySyntax;

namespace DataExportManager.ProjectUI.Graphs
{
    /// <summary>
    /// As part of the ongoing effort to ensure extracted project data is correct (releasing incorrect data to a researcher is about the worst mistake you can make as a data analyst) a 
    /// feature was added which allows execution of aggregate graphs (See AggregateGraph) on the project extraction SQL.
    /// 
    /// <para>This control lets you execute a dataset aggregate graph but restricted to the dataset extraction configuration you are working on (only including the records which will be extracted
    /// when you execute the project extraction).  This is achieved by adjusting the WHERE statement in the Aggregate SQL built by the AggregateBuilder to include the Cohort joins and any 
    /// data extraction filters.  Because of this transformation, depending on your indexes the graphs may take a long time to run (especially if the basic dataset aggregate graph takes a 
    /// long time to execute).</para>
    /// 
    /// <para>You can speed up these graphs by viewing the SQL generated by the system and running it in SQL Management Studio with the Query Analyser turned on and creating appropriate indexes.</para>
    /// </summary>
    public sealed class ExtractionAggregateGraph:AggregateGraph ,IObjectCollectionControl
    {
        public ExtractDatasetCommand Request { get; private set; }
        private ExtractionAggregateGraphObjectCollection _collection;

        protected override AggregateBuilder GetQueryBuilder(AggregateConfiguration aggregateConfiguration)
        {
            if(Request == null)
                throw new Exception("Request has not been initialized yet, has SetCollection not yet been called?");

            var repo = new MemoryCatalogueRepository();

            //we are hijacking the query builder creation for this graph
            AggregateBuilder toReturn =  base.GetQueryBuilder(aggregateConfiguration);
            
            //instead of only filtering on the filters of the Aggregate, also filter on the configurations data extraction filters AND on the cohort ID
            var spontedContainer = new SpontaneouslyInventedFilterContainer(repo,null, null, FilterContainerOperation.AND);

            //the aggregate has filters (it probably does)
            if (toReturn.RootFilterContainer != null)
                spontedContainer.AddChild(toReturn.RootFilterContainer);//add it

            //the cohort extraction request has filters?
            if(Request.QueryBuilder.RootFilterContainer != null)
                spontedContainer.AddChild(Request.QueryBuilder.RootFilterContainer);//add those too

            //now also add the cohort where statement
            string cohortWhereSql = Request.ExtractableCohort.WhereSQL();

            var spontedFilter = new SpontaneouslyInventedFilter(repo,spontedContainer, cohortWhereSql, "Cohort ID Filter",
                "Cohort ID Filter (" + Request.ExtractableCohort + ")", null);

            //now we need to figure out what impromptu joins are going on in the main query extraction 
            //Normally we have an impromptu join e.g. Inner Join MyCohortTable on CHI = MyCohortTable.CHI 
            //Also we can expect there to be joins to custom tables e.g. Left Join MyCohortCustomTable1 on CHI = MyCustomCohortTable1.CHI (so they can select freaky researcher columns like PatientBarcode etc
            //Finally we expect that there is an impromptu filter which does the cohort ID restriction on the query - we have already dealt with that above with a SpontaneouslyInventedFilter so we can ignore those

            //But maybe some other programmer has sneaked in some other custom lines we should worry about 
            var customLines = Request.QueryBuilder.CustomLines.ToArray();

            //we expected a custom line for this (which we have dealt with above so throw it away)
            customLines = customLines.Where(c => c.Text != Request.ExtractableCohort.WhereSQL()).ToArray();

            //now all that should be left are custom lines which are joins (in theory, otherwise complain)
            if(customLines.Any(c => c.LocationToInsert != QueryComponent.JoinInfoJoin))
                throw new QueryBuildingException("We were busy hijacking the ISqlQueryBuilder returned by Request.GetQueryBuilder and were looking for the custom table / cohort table join custom lines but then we noticed there were other custom lines in the query (like non join ones!)");

            if(!customLines.Any())
                throw new Exception("Expected there to be at least 1 custom join line returned by the ISqlQueryBuilder fetched with Request.GetQueryBuilder but it had 0 so how did it know what cohort table to join against?");

            foreach (CustomLine line in customLines)
                toReturn.AddCustomLine(line.Text,QueryComponent.JoinInfoJoin);
            
            spontedContainer.AddChild(spontedFilter);

            //now set the original aggregate that we are hijacking to have the new sponted container (which includes the original filters + the extraction ones + the cohort ID one)
            toReturn.RootFilterContainer = spontedContainer;

            return toReturn;
        }
        
        protected override object[] GetRibbonObjects()
        {
            if (_collection == null)
                return base.GetRibbonObjects();

            return new object[] { Request.Configuration ,Request.SelectedDataSets,AggregateConfiguration,"Graphing Extraction Query"};
        }

        public void RefreshBus_RefreshObject(object sender, RefreshObjectEventArgs e)
        {
            if (e.Object.Equals(_collection.SelectedDataSets))
                if (e.Exists)
                    _collection.SelectedDataSets.RevertToDatabaseState();
                else
                {
                    Close();
                    return;
                }
            else if (e.Object.Equals(_collection.Graph))
                if (e.Exists)
                    _collection.Graph.RevertToDatabaseState();
                else
                {
                    Close();
                    return;
                }
            else
                return;//change was not to a relevant object

            //now reload the graph because the change was to a relevant object
            LoadGraphAsync();
        }

        private void Close()
        {
            var parent = ParentForm;
            if (parent != null && !parent.IsDisposed)
                parent.Close();//self destruct because object was deleted
        }

        public void SetCollection(IActivateItems activator, IPersistableObjectCollection collection)
        {
            _collection = (ExtractionAggregateGraphObjectCollection) collection;
            _activator = activator;
            RepositoryLocator = _activator.RepositoryLocator;

            var config = _collection.SelectedDataSets.ExtractionConfiguration;
            var ds = _collection.SelectedDataSets.ExtractableDataSet;

            Request = new ExtractDatasetCommand(RepositoryLocator,config, new ExtractableDatasetBundle(ds));
            Request.GenerateQueryBuilder();

            SetAggregate(activator,_collection.Graph);
            LoadGraphAsync();
        }

        public IPersistableObjectCollection GetCollection()
        {
            return _collection;
        }

        public override string GetTabName()
        {
            return _collection.SelectedDataSets.ToString();
        }
    }
}
