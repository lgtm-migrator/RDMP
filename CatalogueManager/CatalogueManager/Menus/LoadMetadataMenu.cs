﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CatalogueLibrary.Data;
using CatalogueLibrary.Data.DataLoad;
using CatalogueLibrary.Repositories;
using CatalogueManager.Collections;
using CatalogueManager.Collections.Providers;
using CatalogueManager.CommandExecution.AtomicCommands;
using CatalogueManager.CommandExecution.AtomicCommands.UIFactory;
using CatalogueManager.CommandExecution.AtomicCommands.WindowArranging;
using CatalogueManager.DataLoadUIs.LoadMetadataUIs;
using CatalogueManager.Icons.IconOverlays;
using CatalogueManager.Icons.IconProvision;
using CatalogueManager.ItemActivation;
using CatalogueManager.LoadExecutionUIs;
using CatalogueManager.Refreshing;
using MapsDirectlyToDatabaseTableUI;
using RDMPStartup;
using ReusableUIComponents;

namespace CatalogueManager.Menus
{
    [System.ComponentModel.DesignerCategory("")]
    class LoadMetadataMenu:RDMPContextMenuStrip
    {
        private LoadMetadata _loadMetadata;

        public LoadMetadataMenu(RDMPContextMenuStripArgs args, LoadMetadata loadMetadata)
            : base(args, loadMetadata)
        {
            _loadMetadata = loadMetadata;
            Items.Add("Edit description", null,(s, e) => _activator.Activate<LoadMetadataUI, LoadMetadata>(loadMetadata));

            Add(new ExecuteCommandCreateNewLoadMetadata(_activator));
            Items.Add("View Load Diagram", CatalogueIcons.LoadBubble, (s, e) => _activator.ActivateViewLoadMetadataDiagram(this, loadMetadata));

        }

        public void Delete()
        {
            if (MessageBox.Show("Are you sure you want to Delete LoadMetadata '" + _loadMetadata + "'?", "Confirm Delete", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                //delete it from the database
                _loadMetadata.DeleteInDatabase();
              
                Publish(_loadMetadata);
            }
        }
    }
}
