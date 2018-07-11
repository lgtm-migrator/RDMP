﻿using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CatalogueLibrary.CommandExecution.AtomicCommands;
using CatalogueLibrary.Data;
using CatalogueLibrary.Data.Cohort;
using CatalogueManager.CommandExecution.AtomicCommands;
using CatalogueManager.Icons.IconProvision;
using CatalogueManager.ItemActivation;
using DataExportLibrary.Data.DataTables;
using MapsDirectlyToDatabaseTableUI;
using ReusableLibraryCode.Icons.IconProvision;

namespace DataExportManager.CommandExecution.AtomicCommands.CohortCreationCommands
{
    public class ExecuteCommandCreateNewCohortByExecutingACohortIdentificationConfiguration:CohortCreationCommandExecution
    {
        private CohortIdentificationConfiguration _cic;
        private CohortIdentificationConfiguration[] _allConfigurations;

        public ExecuteCommandCreateNewCohortByExecutingACohortIdentificationConfiguration(IActivateItems activator,ExternalCohortTable externalCohortTable = null) : base(activator)
        {
            _allConfigurations = activator.CoreChildProvider.AllCohortIdentificationConfigurations;
            ExternalCohortTable = externalCohortTable;

            if (!_allConfigurations.Any())
                SetImpossible("You do not have any CohortIdentificationConfigurations yet, you can create them through the 'Cohorts Identification Toolbox' accessible through Window=>Cohort Identification");
        }

        public override string GetCommandHelp()
        {
            return "Run the cohort identification configuration (query) and save the resulting final cohort identifier list into a saved cohort database";
        }

        public override void Execute()
        {
            base.Execute();

            if(_cic == null)
                if(!SelectOne(Activator.RepositoryLocator.CatalogueRepository,out _cic))
                    return;

            var request = GetCohortCreationRequest("Patients in CohortIdentificationConfiguration '" + _cic  +"' (ID=" +_cic.ID +")" );

            //user choose to cancel the cohort creation request dialogue
            if (request == null)
                return;

            var configureAndExecute = GetConfigureAndExecuteControl(request, "Execute CIC " + _cic + " and commmit results");

            configureAndExecute.AddInitializationObject(_cic);
            configureAndExecute.TaskDescription = "You have selected a Cohort Identification Configuration that you created in the CohortManager.  This configuration will be compiled into SQL and executed, the resulting identifier list will be commmented to the named project/cohort ready for data export.  If your query takes a million years to run, try caching some of the subqueries (in CohortManager.exe).  This dialog requires you to select/create an appropriate pipeline. " + TaskDescriptionGenerallyHelpfulText;

            configureAndExecute.PipelineExecutionFinishedsuccessfully += OnImportCompletedSuccessfully;

            Activator.ShowWindow(configureAndExecute);
        }

        void OnImportCompletedSuccessfully(object sender, CatalogueLibrary.DataFlowPipeline.Events.PipelineEngineEventArgs args)
        {
            //see if we can associate the cic with the project
            var cmd = new ExecuteCommandAssociateCohortIdentificationConfigurationWithProject(Activator).SetTarget(Project).SetTarget(_cic);

            //we can!
            if (!cmd.IsImpossible)
                cmd.Execute();
        }

        public override Image GetImage(IIconProvider iconProvider)
        {
            return iconProvider.GetImage(RDMPConcept.CohortIdentificationConfiguration, OverlayKind.Import);
        }

        public override IAtomicCommandWithTarget SetTarget(DatabaseEntity target)
        {
            base.SetTarget(target);
            
            if (target is CohortIdentificationConfiguration)
                _cic = (CohortIdentificationConfiguration) target;

            return this;
        }
    }
}
