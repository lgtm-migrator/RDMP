﻿using System.Windows.Forms;
using BrightIdeasSoftware;
using CatalogueLibrary.CommandExecution.AtomicCommands;
using CatalogueLibrary.Data.Cohort;
using CatalogueManager.Collections;
using CatalogueManager.Icons.IconProvision;
using CatalogueManager.ItemActivation;
using CatalogueManager.ItemActivation.Emphasis;
using CatalogueManager.Menus;
using CohortManager.Collections.Providers;
using CohortManager.CommandExecution.AtomicCommands;
using DataExportLibrary.Data;
using DataExportManager.CommandExecution.AtomicCommands;
using DataExportManager.CommandExecution.AtomicCommands.CohortCreationCommands;
using ReusableUIComponents.ChecksUI;
using ReusableUIComponents.Icons.IconProvision;

namespace CohortManager.Menus
{

    [System.ComponentModel.DesignerCategory("")]
    public class CohortIdentificationConfigurationMenu :RDMPContextMenuStrip
    {
        private CohortIdentificationConfiguration _cic;
        private IAtomicCommandWithTarget _executeAndImportCommand;

        public CohortIdentificationConfigurationMenu(RDMPContextMenuStripArgs args, CohortIdentificationConfiguration cic): base(args, cic)
        {
            _cic = cic;

            Items.Add("View SQL", _activator.CoreIconProvider.GetImage(RDMPConcept.SQL), (s, e) => _activator.ActivateViewCohortIdentificationConfigurationSql(this, cic));
                
            Items.Add(new ToolStripSeparator());

            _executeAndImportCommand = new ExecuteCommandExecuteCohortIdentificationConfigurationAndCommitResults(_activator).SetTarget(cic);
            
            Add(_executeAndImportCommand);
            
            
            //associate with project
            Add(new ExecuteCommandAssociateCohortIdentificationConfigurationWithProject(_activator).SetTarget(cic));
            
            Items.Add(new ToolStripSeparator());

            Items.Add("Clone Configuration", CohortIdentificationIcons.cloneCohortIdentificationConfiguration,
                (s, e) => CloneCohortIdentificationConfiguration());

            var freeze = new ToolStripMenuItem("Freeze Configuration",
                CatalogueIcons.FrozenCohortIdentificationConfiguration, (s, e) => FreezeConfiguration());
            freeze.Enabled = !cic.Frozen;
            Items.Add(freeze);
            
            AddCommonMenuItems();

            Items.Add(new ToolStripSeparator());

            Add(new ExecuteCommandCreateNewCohortIdentificationConfiguration(_activator));
        }

        public CohortIdentificationConfigurationMenu(RDMPContextMenuStripArgs args, ProjectCohortIdentificationConfigurationAssociation association) : this(args,association.CohortIdentificationConfiguration)
        {
            _executeAndImportCommand.SetTarget(association.Project);
        }

        private void CloneCohortIdentificationConfiguration()
        {
            if (
                MessageBox.Show(
                    "This will create a 100% copy of the entire CohortIdentificationConfiguration including all datasets, filters, parameters and set operations, are you sure this is what you want?",
                    "Confirm Cloning", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {

                var checks = new PopupChecksUI("Cloning " + _cic, false);
                var clone = _cic.CreateClone(checks);

                //Load the clone up
                Publish(clone);
                Emphasise(clone);
            }
        }

        private void FreezeConfiguration()
        {
            _cic.Freeze();
            Publish(_cic);
        }
    }

    
}
