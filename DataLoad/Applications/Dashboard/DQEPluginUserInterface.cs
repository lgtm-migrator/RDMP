﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CatalogueLibrary.Data;
using CatalogueLibrary.Data.Automation;
using CatalogueLibrary.Data.DataLoad;
using CatalogueManager.CommandExecution;
using CatalogueManager.CommandExecution.AtomicCommands.UIFactory;
using CatalogueManager.Icons.IconProvision;
using CatalogueManager.Icons.IconProvision.Exceptions;
using CatalogueManager.ItemActivation;
using CatalogueManager.LocationsMenu;
using CatalogueManager.PluginChildProvision;
using Dashboard.CommandExecution.AtomicCommands;
using Dashboard.Menus.MenuItems;
using DataQualityEngine.Data;
using MapsDirectlyToDatabaseTableUI;
using ReusableLibraryCode.DataAccess;
using ReusableUIComponents;
using ReusableUIComponents.Icons.IconProvision;

namespace Dashboard
{
    internal class DQEPluginUserInterface : PluginUserInterface
    {

        public DQEPluginUserInterface(IActivateItems itemActivator) : base(itemActivator)
        {

        }

        public override ToolStripMenuItem[] GetAdditionalRightClickMenuItems(object o)
        {
            var c = o as Catalogue;

            if (c != null)
                return new[] {new DQEMenuItem(ItemActivator, c)};

            var lmd = o as LoadMetadata;

            if (lmd != null)
                return GetMenuArray(
                    new ExecuteCommandViewLoadMetadataLogs(ItemActivator).SetTarget(lmd)
                    );

            var slot = o as AutomationServiceSlot;

            if (slot != null)
                return GetMenuArray(
                    new ExecuteCommandStartAutomationServerMonitorUI(ItemActivator).SetTarget(slot)
                    );

            return null;
        }
    }
}
