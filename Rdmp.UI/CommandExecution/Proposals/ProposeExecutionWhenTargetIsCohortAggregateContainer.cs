// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using Rdmp.Core.CommandExecution;
using Rdmp.Core.CommandExecution.AtomicCommands;
using Rdmp.Core.CommandExecution.Combining;
using Rdmp.Core.Curation.Data.Cohort;
using Rdmp.UI.CommandExecution.AtomicCommands;
using Rdmp.UI.ItemActivation;


namespace Rdmp.UI.CommandExecution.Proposals
{
    class ProposeExecutionWhenTargetIsCohortAggregateContainer : RDMPCommandExecutionProposal<CohortAggregateContainer>
    {
        public ProposeExecutionWhenTargetIsCohortAggregateContainer(IActivateItems activator):base(activator)
        {
            
        }

        public override bool CanActivate(CohortAggregateContainer target)
        {
            return true;
        }

        public override void Activate(CohortAggregateContainer target)
        {
            var cmd = new ExecuteCommandAddCatalogueToCohortIdentificationSetContainer(ItemActivator, target,null);
            if(!cmd.IsImpossible)
                cmd.Execute();
        }

        public override ICommandExecution ProposeExecution(ICombineToMakeCommand cmd, CohortAggregateContainer targetCohortAggregateContainer, InsertOption insertOption = InsertOption.Default)
        {
           
            //Target is a cohort container (UNION / INTERSECT / EXCEPT)

            //source is catalogue
            if (cmd is CatalogueCombineable sourceCatalogueCombineable)
                return new ExecuteCommandAddCatalogueToCohortIdentificationSetContainer(ItemActivator,sourceCatalogueCombineable, targetCohortAggregateContainer);

            //source is aggregate
            var sourceAggregateCommand = cmd as AggregateConfigurationCombineable;

            if (sourceAggregateCommand != null)
            {
                //if it is not already involved in cohort identification 
                if(!sourceAggregateCommand.Aggregate.IsCohortIdentificationAggregate)
                    return new ExecuteCommandAddAggregateConfigurationToCohortIdentificationSetContainer(ItemActivator, sourceAggregateCommand, targetCohortAggregateContainer);

                
                var cic = sourceAggregateCommand.CohortIdentificationConfigurationIfAny;

                if(cic != null && !cic.Equals(targetCohortAggregateContainer.GetCohortIdentificationConfiguration()))
                {
                    //its a cic aggregate but it is one outside of our tree so instead offer adding (not moving/reordering)
                    return new ExecuteCommandAddAggregateConfigurationToCohortIdentificationSetContainer(ItemActivator,sourceAggregateCommand, targetCohortAggregateContainer);
                }
                else
                {
                    //we are dragging around inside our own tree

                    //it is involved in cohort identification already, presumably it's a reorder?
                    if(sourceAggregateCommand.ContainerIfAny != null)
                        if(insertOption == InsertOption.Default)
                            return new ExecuteCommandMoveAggregateIntoContainer(ItemActivator, sourceAggregateCommand, targetCohortAggregateContainer);
                        else
                            return new ExecuteCommandReOrderAggregate(ItemActivator, sourceAggregateCommand, targetCohortAggregateContainer, insertOption);
                
                    //it's a patient index table
                    if (sourceAggregateCommand.IsPatientIndexTable)
                        return new ExecuteCommandMakePatientIndexTableIntoRegularCohortIdentificationSetAgain(ItemActivator, sourceAggregateCommand, targetCohortAggregateContainer);
                
                
                    //ok it IS a cic aggregate but it doesn't have any container so it must be an orphan
                    return new ExecuteCommandMoveAggregateIntoContainer(ItemActivator, sourceAggregateCommand, targetCohortAggregateContainer);
                }
            }

            //source is another container (UNION / INTERSECT / EXCEPT)
            var sourceCohortAggregateContainerCommand = cmd as CohortAggregateContainerCombineable;

            if (sourceCohortAggregateContainerCommand != null)
            {
                //can never drag the root container elsewhere
                if (sourceCohortAggregateContainerCommand.ParentContainerIfAny == null)
                    return null;

                //they are trying to drag it onto its current parent
                if (sourceCohortAggregateContainerCommand.ParentContainerIfAny.Equals(targetCohortAggregateContainer))
                    return null;

                //its being dragged into a container (move into new container)
                if(insertOption == InsertOption.Default)
                    return new ExecuteCommandMoveCohortAggregateContainerIntoSubContainer(ItemActivator, sourceCohortAggregateContainerCommand, targetCohortAggregateContainer);
                
                //its being dragged above/below a container (reorder)
                return new ExecuteCommandReOrderAggregateContainer(ItemActivator, sourceCohortAggregateContainerCommand, targetCohortAggregateContainer, insertOption);
                
            }
            return null;
        }
    }
}