// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Windows.Forms;
using CatalogueManager.ItemActivation;
using CatalogueManager.Menus.MenuItems;
using DataExportManager.SimpleDialogs;

namespace ResearchDataManagementPlatform.Menus.MenuItems
{
    internal class DataExportMenu : RDMPToolStripMenuItem
    {
        public DataExportMenu(IActivateItems activator):base(activator,"Data Export Options")
        {

            Enabled = _activator.RepositoryLocator.DataExportRepository != null;

            DropDownItems.Add(new ToolStripMenuItem("Configure Disclaimer", null, ConfigureDisclaimer));
            DropDownItems.Add(new ToolStripMenuItem("Configure Hashing Algorithm", null, ConfigureHashingAlgorithm));
        }

        private void ConfigureHashingAlgorithm(object sender, EventArgs e)
        {
            var hash = new ConfigureHashingAlgorithm(_activator.RepositoryLocator.DataExportRepository);
            hash.RepositoryLocator = _activator.RepositoryLocator;
            hash.ShowDialog();
        }

        private void ConfigureDisclaimer(object sender, EventArgs e)
        {
            var disclaimer = new ConfigureDisclaimer(_activator.RepositoryLocator.DataExportRepository);
            disclaimer.RepositoryLocator = _activator.RepositoryLocator;
            disclaimer.ShowDialog();
        }
    }
}