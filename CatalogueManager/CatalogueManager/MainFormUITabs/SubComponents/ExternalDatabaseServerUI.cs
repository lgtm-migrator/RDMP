// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CatalogueLibrary.Data;
using CatalogueManager.Collections;
using CatalogueManager.ItemActivation;
using CatalogueManager.Rules;
using CatalogueManager.SimpleControls;
using CatalogueManager.TestsAndSetup.ServicePropogation;
using FAnsi;
using ReusableLibraryCode;
using ReusableLibraryCode.DataAccess;
using ReusableUIComponents;

namespace CatalogueManager.MainFormUITabs.SubComponents
{
    /// <summary>
    /// Allows you to change the connection strings of a known ExternalDatabaseServer.
    /// 
    /// <para>ExternalDatabaseServers are references to existing servers.  They have a logistical name (what you want to call it) and servername.  Optionally you can
    /// specify a database (required in the case of references to specific databases e.g. Logging Database), if you omit it then the 'master' database will be used.
    /// If you do not specify a username/password then Integrated Security will be used when connecting (the preferred method).  Usernames and passwords are stored
    /// in encrypted form (See PasswordEncryptionKeyLocationUI).</para>
    /// </summary>
    public partial class ExternalDatabaseServerUI : ExternalDatabaseServerUI_Design, ISaveableUI
    {
        private ExternalDatabaseServer _server;
        private bool bloading;

        public ExternalDatabaseServerUI()
        {
            InitializeComponent();
            AssociatedCollection = RDMPCollection.Tables;

            ddDatabaseType.DataSource = Enum.GetValues(typeof(DatabaseType));
        }

        public override void SetDatabaseObject(IActivateItems activator, ExternalDatabaseServer databaseObject)
        {
            base.SetDatabaseObject(activator, databaseObject);
            _server = databaseObject;

            bloading = true;
            
            try
            {
                SetupDropdownItems();

                tbPassword.Text = _server.GetDecryptedPassword();
                ddDatabaseType.SelectedItem = _server.DatabaseType;
                pbDatabaseProvider.Image = Activator.CoreIconProvider.GetImage(_server.DatabaseType);

                pbServer.Image = Activator.CoreIconProvider.GetImage(_server);

                AddChecks(databaseObject);
            }
            finally
            {
                bloading = false;
            }
        }

        protected override void SetBindings(BinderWithErrorProviderFactory rules, ExternalDatabaseServer databaseObject)
        {
            base.SetBindings(rules, databaseObject);

            Bind(tbID,"Text","ID",s=>s.ID);
            Bind(tbName,"Text","Name",s=>s.Name);
            Bind(tbServerName, "Text", "Server", s => s.Server);
            Bind(tbMappedDataPath, "Text", "MappedDataPath", s => s.MappedDataPath);
            Bind(tbDatabaseName, "Text", "Database", s => s.Database);
            Bind(tbUsername, "Text", "Username", s => s.Username);
            Bind(ddSetKnownType, "Text", "CreatedByAssembly", s => s.CreatedByAssembly);
        }

        private void SetupDropdownItems()
        {
            ddSetKnownType.Items.Clear();
            ddSetKnownType.Items.AddRange(
                AppDomain.CurrentDomain.GetAssemblies() //get all current assemblies that are loaded
                .Select(n => n.GetName().Name)//get the name of the assembly
                .Where(s => s.EndsWith(".Database") && //if it is a .Database assembly advertise it to the user as a known type of database
                    !(s.EndsWith("CatalogueLibrary.Database") || s.EndsWith("DataExportManager.Database"))).ToArray()); //unless it's one of the core ones (catalogue/data export)
        }
        
        private void tbPassword_TextChanged(object sender, EventArgs e)
        {
            if(!bloading)
                _server.Password = tbPassword.Text;
        }
        
        private void btnClearKnownType_Click(object sender, EventArgs e)
        {
            _server.CreatedByAssembly = null;
            ddSetKnownType.SelectedItem = null;
            ddSetKnownType.Text = null;
        }

        private void ddDatabaseType_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_server == null)
                return;

            var type = (DatabaseType)ddDatabaseType.SelectedValue;
            _server.DatabaseType = type;
            pbDatabaseProvider.Image = Activator.CoreIconProvider.GetImage(type);
        }
    }

    [TypeDescriptionProvider(typeof(AbstractControlDescriptionProvider<ExternalDatabaseServerUI_Design, UserControl>))]
    public abstract class ExternalDatabaseServerUI_Design:RDMPSingleDatabaseObjectControl<ExternalDatabaseServer>
    {
    }
}
