﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CatalogueLibrary.Data.Cohort;
using CatalogueLibrary.Data.Pipelines;
using CatalogueLibrary.DataFlowPipeline;
using CatalogueLibrary.DataFlowPipeline.Requirements;
using CatalogueManager.Icons.IconProvision;
using CatalogueManager.ItemActivation;
using DataExportLibrary.CohortCreationPipeline;
using DataExportLibrary.CohortCreationPipeline.Sources;
using DataExportLibrary.Data.DataTables;
using DataExportLibrary.ExtractionTime.ExtractionPipeline.Sources;
using DataExportLibrary.Interfaces.Pipeline;
using DataExportLibrary.Repositories;
using DataExportManager.CohortUI.CohortSourceManagement;
using LoadModules.Generic.Attachers;
using LoadModules.Generic.DataFlowSources;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.Progress;
using ReusableUIComponents.SingleControlForms;

namespace DataExportManager.Wizard
{
    /// <summary>
    /// Provides a single screen allowing you to execute a CohortIdentificationConfiguration or load an IdentifierList into the snapshot store, allocate release identifiers and build an 
    /// extraction project with specific datasets.  Each time you use this user interface you will get a new Project so do not use the wizard if you already have an existing Project e.g.
    /// if you want to do a project refresh or adjust a cohort etc (In such a case you should use CohortIdentificationCollectionUI to add a new ExtractionConfiguration/Cohort to your existing
    /// Project).
    /// </summary>
    public partial class CreateNewDataExtractionProjectUI : Form
    {
        private readonly IActivateItems _activator;
        private Project[] _existingProjects;
        private int _projectNumber;
        private FileInfo _cohortFile;
        private Project _project;
        private ExtractionConfiguration _configuration;
        private ExtractableCohort _cohortCreated;

        public ExtractionConfiguration ExtractionConfigurationCreatedIfAny { get; private set; }
        
        public CreateNewDataExtractionProjectUI(IActivateItems activator)
        {
            _activator = activator;
            InitializeComponent();
            
            if(activator == null || activator.RepositoryLocator == null)
                return;

            _existingProjects = activator.RepositoryLocator.DataExportRepository.GetAllObjects<Project>();
            var highestNumber = _existingProjects.Max(p => p.ProjectNumber);

            tbProjectNumber.Text = highestNumber == null ? "1" : (highestNumber.Value + 1).ToString();

            pbCohort.Image = activator.CoreIconProvider.GetImage(RDMPConcept.CohortIdentificationConfiguration);
            pbCohortFile.Image = activator.CoreIconProvider.GetImage(RDMPConcept.File);
            pbCohortSources.Image = activator.CoreIconProvider.GetImage(RDMPConcept.ExternalCohortTable);

            IdentifyCompatiblePipelines();

            IdentifyCompatibleCohortSources();

            olvDatasets.AddObjects(activator.RepositoryLocator.DataExportRepository.GetAllObjects<ExtractableDataSet>());

            cbxCohort.DataSource = activator.RepositoryLocator.CatalogueRepository.GetAllObjects<CohortIdentificationConfiguration>();
            cbxCohort.PropertySelector = collection => collection.Cast<CohortIdentificationConfiguration>().Select(c => c.ToString());
            ClearCic();
        }

        private void IdentifyCompatibleCohortSources()
        {
            var sources = _activator.RepositoryLocator.DataExportRepository.GetAllObjects<ExternalCohortTable>();

            ddCohortSources.Items.AddRange(sources);

            if (sources.Length == 1)
            {
                ddCohortSources.SelectedItem = sources[0];
                ddCohortSources.Enabled = false;
            }
            
            btnCreateNewCohortSource.Enabled = sources.Length == 0; 

        }

        private void IdentifyCompatiblePipelines()
        {
            var p = _activator.RepositoryLocator.CatalogueRepository.GetAllObjects<Pipeline>();
            
            foreach (Pipeline pipeline in p)
            {
                var source = pipeline.Source;
                var destination = pipeline.Destination;


                //pipeline doesn't have a source / destination
                if(source == null || destination == null)
                    continue;

                //source defines use case
                var sourceType = source.GetClassAsSystemType();
                var destinationType = destination.GetClassAsSystemType();

                if (typeof (ExecuteDatasetExtractionSource).IsAssignableFrom(sourceType))
                    ddExtractionPipeline.Items.Add(pipeline);

                //destination is not a cohort destination
                if(!typeof(ICohortPipelineDestination).IsAssignableFrom(destinationType))
                    continue;

                //cic
                if (typeof(CohortIdentificationConfigurationSource).IsAssignableFrom(sourceType))
                    ddCicPipeline.Items.Add(pipeline);
                
                //flat file
                if (typeof(DelimitedFlatFileDataFlowSource).IsAssignableFrom(sourceType))
                    ddFilePipeline.Items.Add(pipeline);
            }

            //for each dropdown if theres only one option
            foreach (var dd in new ComboBox[]{ddCicPipeline,ddExtractionPipeline,ddFilePipeline})
            {
                if (dd.Items.Count == 1)
                {
                    dd.SelectedItem = dd.Items[0];
                    dd.Enabled = false;
                }
            }
            
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            var browser = new FolderBrowserDialog();
            if (browser.ShowDialog() == DialogResult.OK)
                tbExtractionDirectory.Text = browser.SelectedPath;
        }

        private void CreateNewDataExtractionProjectUI_Load(object sender, EventArgs e)
        {

        }

        private void tbProjectNumber_TextChanged(object sender, EventArgs e)
        {
            ragProjectNumber.Reset();

            try
            {
                _projectNumber = int.Parse(tbProjectNumber.Text);

                var collisionProject = _existingProjects.FirstOrDefault(p => p.ProjectNumber == _projectNumber);
                if(collisionProject != null)
                    ragProjectNumber.Fatal(new Exception("There is already an existing Project ('" + collisionProject + "') with ProjectNumber " + _projectNumber));
            }
            catch (Exception ex)
            {
                ragProjectNumber.Fatal(ex);
            }
        }

        private void btnSelectClearCohortFile_Click(object sender, EventArgs e)
        {
            if (_cohortFile != null)
            {
                ClearFile();
                return;
            }
            
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Comma Separated Values|*.csv";
            DialogResult result = ofd.ShowDialog();

            if (result == DialogResult.OK)
                SelectFile(new FileInfo(ofd.FileName));
        }

        private void ClearFile()
        {
            _cohortFile = null;
            gbCic.Enabled = true;

            lblCohortFile.Text = "Cohort File...";
            btnSelectClearCohortFile.Text = "Browse...";
            btnSelectClearCohortFile.Left = Math.Min(gbFile.Width - btnSelectClearCohortFile.Width, lblCohortFile.Right + 5);
        }

        private void SelectFile(FileInfo fileInfo)
        {
            _cohortFile = fileInfo;
            gbCic.Enabled = false;

            tbCohortName.Text = _cohortFile.Name;

            lblCohortFile.Text = _cohortFile.Name;
            btnSelectClearCohortFile.Text = "Clear";
            btnSelectClearCohortFile.Left = Math.Min(gbFile.Width - btnSelectClearCohortFile.Width, lblCohortFile.Right + 5);
            
        }

        private void cbxCohort_SelectedIndexChanged(object sender, EventArgs e)
        {
            var cic = cbxCohort.SelectedItem as CohortIdentificationConfiguration;

            if(cic != null)
            {
                Cursor.Current = Cursors.WaitCursor;
                try
                {
                    ragCic.Reset();

                    tbCohortName.Text = cic.ToString();

                    var source = new CohortIdentificationConfigurationSource();
                    source.PreInitialize(cic,new ThrowImmediatelyDataLoadEventListener());
                    source.Check(ragCic);

                    ClearFile();
                    
                }
                finally
                {
                    Cursor.Current = Cursors.Default;
                }
            }
            
            gbFile.Enabled = cic == null;
            
        }

        private void btnClearCohort_Click(object sender, EventArgs e)
        {
            ClearCic();
        }

        private void ClearCic()
        {
            cbxCohort.SelectedItem = null;
            tbCohortName.Text = null;
        }

        private void btnExecute_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;

            string problem = AllRequiredDataPresent();

            try
            {
                if (problem != null)
                {
                    MessageBox.Show(problem);
                    return;
                }

                ragExecute.Reset();

                //create the project
                if (_project == null)
                    _project = new Project(_activator.RepositoryLocator.DataExportRepository, tbProjectName.Text);

                _project.ProjectNumber = int.Parse(tbProjectNumber.Text);
                _project.ExtractionDirectory = tbExtractionDirectory.Text;

                if (!Directory.Exists(_project.ExtractionDirectory))
                    Directory.CreateDirectory(_project.ExtractionDirectory);

                _project.SaveToDatabase();

                if (_configuration == null)
                {

                    _configuration = new ExtractionConfiguration(_activator.RepositoryLocator.DataExportRepository,
                        _project);
                    _configuration.Name = "Cases";
                    _configuration.SaveToDatabase();
                }

                foreach (ExtractableDataSet ds in olvDatasets.CheckedObjects.Cast<ExtractableDataSet>())
                    _configuration.AddDatasetToConfiguration(ds);

                if (_cohortCreated == null)
                {
                    var cohortDefinition = new CohortDefinition(null, tbCohortName.Text, 1, _project.ProjectNumber.Value,
                        (ExternalCohortTable) ddCohortSources.SelectedItem);

                    //execute the cohort creation bit
                    var cohortRequest = new CohortCreationRequest(_project, cohortDefinition,
                        (DataExportRepository) _activator.RepositoryLocator.DataExportRepository, tbCohortName.Text);

                    ComboBox dd;
                    if (_cohortFile != null)
                    {
                        //execute cohort creation from file.
                        cohortRequest.FileToLoad = new FlatFileToLoad(_cohortFile);
                        dd = ddFilePipeline;
                    }
                    else
                    {
                        //execute cohort creation from cic
                        cohortRequest.CohortIdentificationConfiguration =
                            (CohortIdentificationConfiguration) cbxCohort.SelectedItem;
                        dd = ddCicPipeline;
                    }

                    var engine = cohortRequest.GetEngine((Pipeline) dd.SelectedItem,new ThrowImmediatelyDataLoadEventListener());
                    engine.ExecutePipeline(new GracefulCancellationToken());
                    _cohortCreated = cohortRequest.CohortCreatedIfAny;
                }


                //associate the configuration with the cohort
                _configuration.Cohort_ID = _cohortCreated.ID;

                //set the pipeline to use
                var pipeline = (Pipeline)ddExtractionPipeline.SelectedItem;
                if (pipeline != null)
                    _configuration.DefaultPipeline_ID = pipeline.ID;

                _configuration.SaveToDatabase();

                Cursor = Cursors.Default;

                ExtractionConfigurationCreatedIfAny = _configuration;
                
                DialogResult = DialogResult.OK;
                MessageBox.Show("Project Created Succesfully");
                Close();
            }
            catch (Exception exception)
            {
                ragExecute.Fatal(exception);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
            
        }

        private string AllRequiredDataPresent()
        {
            if (string.IsNullOrWhiteSpace(tbProjectName.Text))
                return "You must name your project";

            if (string.IsNullOrWhiteSpace(tbProjectNumber.Text))
                return "You must supply a Project Number";

            if (ragProjectNumber.IsFatal())
                return "There is a problem with the Project Number";

            if (string.IsNullOrWhiteSpace(tbExtractionDirectory.Text))
                return "You must specify a project extraction directory where the flat files will go";

            if (string.IsNullOrWhiteSpace(tbCohortName.Text))
                return "You must provide a name for your cohort";

            if (!olvDatasets.CheckedObjects.Cast<ExtractableDataSet>().Any())
                return "You must check at least one dataset";

            if (ddCicPipeline.SelectedItem == null && _cohortFile == null)
                return "You must select a cohort execution pipeline";

            if (ddFilePipeline.SelectedItem == null && _cohortFile != null)
                return "You must select a cohort file import pipeline";

            if (ddExtractionPipeline.SelectedItem == null)
                return "You must select an extraction pipeline";

            //no problems
            return null;
        }

        private void btnCreateNewCohortSource_Click(object sender, EventArgs e)
        {
            var wizard = new CreateNewCohortDatabaseWizardUI();
            wizard.RepositoryLocator = _activator.RepositoryLocator;
            SingleControlForm.ShowDialog(wizard);
            IdentifyCompatibleCohortSources();

        }
    }
}
