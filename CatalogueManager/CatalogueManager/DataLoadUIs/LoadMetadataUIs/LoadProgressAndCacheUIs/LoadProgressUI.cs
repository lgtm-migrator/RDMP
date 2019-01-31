using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using CatalogueLibrary.Data;
using CatalogueLibrary.Data.Cache;
using CatalogueLibrary.Data.DataLoad;
using CatalogueManager.Collections;
using CatalogueManager.CommandExecution.AtomicCommands;
using CatalogueManager.CommandExecution.AtomicCommands.WindowArranging;
using CatalogueManager.Icons.IconProvision;
using CatalogueManager.ItemActivation;
using CatalogueManager.Rules;
using CatalogueManager.SimpleControls;
using CatalogueManager.TestsAndSetup.ServicePropogation;
using MapsDirectlyToDatabaseTable.Revertable;
using ReusableUIComponents;

namespace CatalogueManager.DataLoadUIs.LoadMetadataUIs.LoadProgressAndCacheUIs
{
    /// <summary>
    /// Let's you configure the settings of a LoadProgress (see LoadProgress) including how many days to ideally load in each data load, what date has currently been loaded up to etc.
    /// 
    /// <para>Each LoadProgress can be tied to a Cache progress.  If you are using a LoadProgress without a cache then it is up to your load implementation to respect the time period being loaded 
    /// (e.g. when using RemoteTableAttacher you should make use of the @startDate and @endDate parameters are in your fetch query).  See CacheProgressUI for a description of caching and 
    /// permission windows.</para>
    /// </summary>
    public partial class LoadProgressUI : LoadProgressUI_Design, ISaveableUI
    {
        private LoadProgress _loadProgress;
        
        public LoadProgressUI()
        {
            InitializeComponent();
            loadProgressDiagram1.LoadProgressChanged += ReloadUIFromDatabase;
            AssociatedCollection = RDMPCollection.DataLoad;
        }
        
        private void ReloadUIFromDatabase()
        {
            loadProgressDiagram1.SetLoadProgress(_loadProgress, _activator);
            loadProgressDiagram1.Visible = true;

            tbDataLoadProgress.ReadOnly = true;
                
            if(_loadProgress.OriginDate != null)
                tbOriginDate.Text = _loadProgress.OriginDate.ToString();
                
            tbDataLoadProgress.Text = _loadProgress.DataLoadProgress != null ? _loadProgress.DataLoadProgress.ToString() : "";
        }

        private void btnEditLoadProgress_Click(object sender, EventArgs e)
        {
            tbDataLoadProgress.ReadOnly = false;
        }

        private void tbDataLoadProgress_TextChanged(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(tbDataLoadProgress.Text))
                    _loadProgress.DataLoadProgress = null;
                else
                    _loadProgress.DataLoadProgress = DateTime.Parse(tbDataLoadProgress.Text);

                tbDataLoadProgress.ForeColor = Color.Black;
            }
            catch (Exception)
            {
                tbDataLoadProgress.ForeColor = Color.Red;
            }
        }

        private void tbOriginDate_TextChanged(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(tbOriginDate.Text))
                    _loadProgress.OriginDate = null;
                else
                    _loadProgress.OriginDate = DateTime.Parse(tbOriginDate.Text);

                tbOriginDate.ForeColor = Color.Black;
            }
            catch (Exception)
            {
                tbOriginDate.ForeColor = Color.Red;
            }
        }

        public override void SetDatabaseObject(IActivateItems activator, LoadProgress databaseObject)
        {
            base.SetDatabaseObject(activator, databaseObject);
            _loadProgress = databaseObject;
            
            ReloadUIFromDatabase();

            AddHelp(nDefaultNumberOfDaysToLoadEachTime, "ILoadProgress.DefaultNumberOfDaysToLoadEachTime");

            AddToMenu(new ExecuteCommandActivate(activator,databaseObject.LoadMetadata),"Execute Load");
        }


        protected override void SetBindings(BinderWithErrorProviderFactory rules, LoadProgress databaseObject)
        {
            base.SetBindings(rules, databaseObject);

            Bind(tbID,"Text","ID",l=>l.ID);
            Bind(tbName, "Text", "Name", l => l.Name);
            Bind(nDefaultNumberOfDaysToLoadEachTime, "Value", "DefaultNumberOfDaysToLoadEachTime", l => l.DefaultNumberOfDaysToLoadEachTime);
        }
    }

    [TypeDescriptionProvider(typeof(AbstractControlDescriptionProvider<LoadProgressUI_Design, UserControl>))]
    public abstract class LoadProgressUI_Design:RDMPSingleDatabaseObjectControl<LoadProgress>
    {
    }
}
