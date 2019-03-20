// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CatalogueLibrary.Data;
using CatalogueLibrary.Data.Cohort;
using CatalogueLibrary.Data.DataLoad;
using CatalogueLibrary.Nodes;
using CatalogueLibrary.Nodes.LoadMetadataNodes;
using CatalogueLibrary.Providers;
using CatalogueManager.AutoComplete;
using CatalogueManager.Collections;
using CatalogueManager.Collections.Providers;
using CatalogueManager.Collections.Providers.Filtering;
using CatalogueManager.Icons.IconProvision;
using CatalogueManager.ItemActivation;
using CatalogueManager.ItemActivation.Emphasis;
using CatalogueManager.TestsAndSetup.ServicePropogation;
using CatalogueManager.Theme;
using DataExportLibrary.Data.DataTables;
using DataExportLibrary.Providers.Nodes;
using MapsDirectlyToDatabaseTable;
using MapsDirectlyToDatabaseTable.Attributes;
using ReusableLibraryCode.Icons.IconProvision;
using ReusableLibraryCode.Settings;
using ReusableUIComponents.ScintillaHelper;
using ScintillaNET;
using IContainer = CatalogueLibrary.Data.IContainer;

namespace CatalogueManager.SimpleDialogs.NavigateTo
{
    /// <summary>
    /// Allows you to search all objects in your database and rapidly select 1 which will be shown via the Emphasis system.
    /// </summary>
    public partial class NavigateToObjectUI : RDMPForm
    {
        private readonly Dictionary<IMapsDirectlyToDatabaseTable, DescendancyList> _searchables;
        private ICoreIconProvider _coreIconProvider;
        private FavouritesProvider _favouriteProvider;

        private Scintilla _scintilla;

        private const int MaxMatches = 30;
        private List<IMapsDirectlyToDatabaseTable> _matches;
        private object oMatches = new object();

        //drawing
        private int keyboardSelectedIndex = 0;
        private int mouseSelectedIndex = 0;

        private Color keyboardSelectionColor = Color.FromArgb(210, 230, 255);
        private Color mouseSelectionColor = Color.FromArgb(230, 245, 251);

        private const float DrawMatchesStartingAtY = 50;
        private const float RowHeight = 20;

        private const int DiagramTabDistance = 20;

        private Bitmap _magnifier;
        private int _diagramBottom;


        private Task _lastFetchTask = null;
        private CancellationTokenSource _lastCancellationToken;
        private AutoCompleteProvider _autoCompleteProvider;
        private Type[] _types;
        private HashSet<string> _typeNames;

        /// <summary>
        /// Object types that appear in the task bar as filterable types
        /// </summary>
        private Dictionary<Type,RDMPCollection> EasyFilterTypesAndAssociatedCollections = new Dictionary<Type, RDMPCollection>()
        {
            {typeof (Catalogue),RDMPCollection.Catalogue},
            {typeof (CatalogueItem),RDMPCollection.Catalogue},
            {typeof (SupportingDocument),RDMPCollection.Catalogue},
            {typeof (Project),RDMPCollection.DataExport},
            {typeof (ExtractionConfiguration),RDMPCollection.DataExport},
            {typeof (ExtractableCohort),RDMPCollection.SavedCohorts},
            {typeof (CohortIdentificationConfiguration),RDMPCollection.Cohort},
            {typeof (TableInfo),RDMPCollection.Tables},
            {typeof (ColumnInfo),RDMPCollection.Tables},
            {typeof (LoadMetadata),RDMPCollection.DataLoad},
        };


        /// <summary>
        /// Identifies which Types are checked by default when the NavigateToObjectUI is shown when the given RDMPCollection has focus
        /// </summary>
        public Dictionary<RDMPCollection, Type[]> StartingEasyFilters
            = new Dictionary<RDMPCollection, Type[]>()
            {
                {RDMPCollection.Catalogue, new[] {typeof (Catalogue)}},
                {RDMPCollection.Cohort, new[] {typeof (CohortIdentificationConfiguration)}},
                {RDMPCollection.DataExport, new[] {typeof (Project), typeof (ExtractionConfiguration)}},
                {RDMPCollection.DataLoad, new[] {typeof (LoadMetadata)}},
                {RDMPCollection.SavedCohorts, new[] {typeof (ExtractableCohort)}},
                {RDMPCollection.Tables, new[] {typeof (TableInfo)}},
                {RDMPCollection.None,new []{typeof(SupportingDocument),typeof(CatalogueItem)}} //Add all other Type checkboxes here so that they are recognised as Typenames
};

        private static HashSet<Type> TypesThatAreNotUsefulParents = new HashSet<Type>(
            new []
        {
            typeof(CatalogueItemsNode),
            typeof(DocumentationNode),
            typeof(AggregatesNode),
            typeof(CohortSetsNode),
            typeof(LoadMetadataScheduleNode),
            typeof(AllCataloguesUsedByLoadMetadataNode),
            typeof(AllProcessTasksUsedByLoadMetadataNode),
            typeof(LoadStageNode),
            typeof(PreLoadDiscardedColumnsNode),
            typeof(ProjectCataloguesNode)

        });

        private bool _isClosed;
        private bool _skipEnter;
        private bool _skipEscape;

        private List<Type> showOnlyTypes = new List<Type>();
        private AttributePropertyFinder<UsefulPropertyAttribute> _usefulPropertyFinder;
        
        /// <summary>
        /// The action to perform when the form closes with an object selected (defaults to Emphasise)
        /// </summary>
        public Action<IMapsDirectlyToDatabaseTable> CompletionAction { get; set; }

        public static void RecordThatTypeIsNotAUsefulParentToShow(Type t)
        {
            if(!TypesThatAreNotUsefulParents.Contains(t))
                TypesThatAreNotUsefulParents.Add(t);
        }
        public NavigateToObjectUI(IActivateItems activator, string initialSearchQuery = null,RDMPCollection focusedCollection = RDMPCollection.None):base(activator)
        {
            _coreIconProvider = activator.CoreIconProvider;
            _favouriteProvider = Activator.FavouritesProvider;
            _magnifier = FamFamFamIcons.magnifier;
            InitializeComponent();

            CompletionAction = Emphasise;

            activator.Theme.ApplyTo(toolStrip1);

            _searchables = Activator.CoreChildProvider.GetAllSearchables();
            
            _usefulPropertyFinder = new AttributePropertyFinder<UsefulPropertyAttribute>(_searchables.Keys);

            ScintillaTextEditorFactory factory = new ScintillaTextEditorFactory();
            _scintilla = factory.Create();
            panel1.Controls.Add(_scintilla);
            
            _scintilla.Focus();
            _scintilla.Text = initialSearchQuery;
            
            _scintilla.TextChanged += tbFind_TextChanged;
            _scintilla.PreviewKeyDown += _scintilla_PreviewKeyDown;
            _scintilla.KeyUp += _scintilla_KeyUp;

            _scintilla.Margins[0].Width = 0;//dont show line number

            _scintilla.ClearCmdKey(Keys.Enter);
            _scintilla.ClearCmdKey(Keys.Up);
            _scintilla.ClearCmdKey(Keys.Down);

            FetchMatches(initialSearchQuery,CancellationToken.None);
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            
            _types = _searchables.Keys.Select(k => k.GetType()).Distinct().ToArray();
            _typeNames = new HashSet<string>(_types.Select(t => t.Name));

            foreach (Type t in StartingEasyFilters.SelectMany(v=>v.Value))
            {
                if (!_typeNames.Contains(t.Name))
                    _typeNames.Add(t.Name);
            }

            _autoCompleteProvider = new AutoCompleteProvider(Activator);
            foreach (Type t in _types)
                _autoCompleteProvider.Add(t);

            _autoCompleteProvider.RegisterForEvents(_scintilla);

            Type[] startingFilters = null;

            if (focusedCollection != RDMPCollection.None && StartingEasyFilters.ContainsKey(focusedCollection))
                startingFilters = StartingEasyFilters[focusedCollection];
            
            BackColorProvider backColorProvider = new BackColorProvider();

            foreach (Type t in EasyFilterTypesAndAssociatedCollections.Keys)
            {
                var b = new ToolStripButton();
                b.Image = activator.CoreIconProvider.GetImage(t);
                b.CheckOnClick = true;
                b.Tag = t;
                b.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
                b.Text = t.Name;
                b.CheckedChanged += CollectionCheckedChanged;
                b.Checked = startingFilters != null && startingFilters.Contains(t);

                b.BackgroundImage = backColorProvider.GetBackgroundImage(b.Size, EasyFilterTypesAndAssociatedCollections[t]);

                toolStrip1.Items.Add(b);
            }
        }


        private void CollectionCheckedChanged(object sender, EventArgs e)
        {
            var button = (ToolStripButton) sender;

            var togglingType = (Type) button.Tag;

            if (button.Checked)
                showOnlyTypes.Add(togglingType);
            else
                showOnlyTypes.Remove(togglingType);

            //refresh the objects showing
            tbFind_TextChanged(null, null);
        }


        void _scintilla_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            var autoCompleteShowing = _autoCompleteProvider.IsShowing();
            
            _skipEnter = e.KeyCode == Keys.Enter && autoCompleteShowing;
            _skipEscape = e.KeyCode == Keys.Escape && autoCompleteShowing;
        }
        
        void ApplySyntaxHighlighting()
        {
            if(_isClosed)
                return;
            
            var startPos = 0;
            var endPos = _scintilla.TextLength;

            _scintilla.Styles[1].ForeColor = Color.Blue;

            _scintilla.StartStyling(startPos);
            var text = _scintilla.GetTextRange(startPos, endPos);

            int charPos = 0;
            foreach (string s in text.Split(' '))
            {
                if (_typeNames.Contains(s))
                    _scintilla.SetStyling(s.Length, 1);
                else
                    _scintilla.SetStyling(s.Length, 0);

                charPos += s.Length + 1; //for the space

                //deal with no trailing whitespace
                if(charPos + startPos <= endPos)
                    _scintilla.SetStyling(1, 0); //for the space
            }
        }

        void _scintilla_KeyUp(object sender, KeyEventArgs e)
        {
            if (_autoCompleteProvider.IsShowing())
                return;

            ApplySyntaxHighlighting();
            
            if (e.KeyCode == Keys.Up)
            {
                e.Handled = true;
                MoveSelectionUp();
            }

            if (e.KeyCode == Keys.Down)
            {
                e.Handled = true;
                MoveSelectionDown();
            }

            if (e.KeyCode == Keys.Enter && !_skipEnter)
            {
                e.Handled = true;
                PerformCompletionAction(keyboardSelectedIndex);
            }
            
            if (e.KeyCode == Keys.Escape)
            {
                if(_skipEscape)
                    return;

                e.Handled = true;
                Close();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
           base.OnMouseClick(e);

            if(e.Y<= DrawMatchesStartingAtY || e.Y > (RowHeight * MaxMatches )+ DrawMatchesStartingAtY)
                return;

            PerformCompletionAction(RowIndexFromPoint(e.X, e.Y));
        }
        
        private void PerformCompletionAction(int indexToSelect)
        {
            lock (oMatches)
            {
                if (indexToSelect >= _matches.Count)
                    return;

                Close();
                CompletionAction(_matches[indexToSelect]);
            }
        }

        private void Emphasise(IMapsDirectlyToDatabaseTable o)
        {
            Activator.RequestItemEmphasis(this, new EmphasiseRequest(o, 1) { Pin = UserSettings.FindShouldPin });
        }
        
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            var before = mouseSelectedIndex;
            mouseSelectedIndex = RowIndexFromPoint(e.X, e.Y);
            
            if(before != mouseSelectedIndex)
            {
                AdjustHeight();
                Invalidate();
            }
        }

        private void AdjustHeight()
        {
            float newHeight;

            lock (oMatches)
            {
                newHeight = _matches.Count*RowHeight;
            }
            SetClientSizeCore(ClientSize.Width,
                    Math.Max(_diagramBottom,
                        (int)((newHeight) + DrawMatchesStartingAtY)));
            
        }

        private int RowIndexFromPoint(int x, int y)
        {
            y -= (int)DrawMatchesStartingAtY;

            return Math.Max(0,Math.Min(MaxMatches,(int) (y/RowHeight)));
        }

        private void MoveSelectionDown()
        {
            lock (oMatches)
            {
                keyboardSelectedIndex = Math.Min(_matches.Count-1, //don't go above the number matches returned
                    Math.Min(MaxMatches - 1, //don't go above the max number of matches 
                        keyboardSelectedIndex + 1));
            }

            Invalidate();
        }

        private void MoveSelectionUp()
        {
            lock (oMatches)
            {
                keyboardSelectedIndex = Math.Min(_matches.Count - 1,  //if text has been typed then selectedIndex could be higher than the number of matches so set that as a roof
                   Math.Max(0, //also don't go below 0
                       keyboardSelectedIndex - 1)); 
            }
            
            Invalidate();
        }


        protected override void OnDeactivate(EventArgs e)
        {
            if(_autoCompleteProvider.IsShowing())
                return;

            this.Close();
        }


        private void tbFind_TextChanged(object sender, EventArgs e)
        {
            //cancel the last execution if it has not completed yet
            if (_lastFetchTask != null && !_lastFetchTask.IsCompleted)
                _lastCancellationToken.Cancel();

            _lastCancellationToken = new CancellationTokenSource();

            var toFind = _scintilla.Text;

            _lastFetchTask = Task.Run(() => FetchMatches(toFind, _lastCancellationToken.Token))
                .ContinueWith(
                    (s) =>
                    {
                        if (_isClosed)
                            return;
                        
                        try
                        {
                            if(_isClosed)
                                return;

                            AdjustHeight();

                            if (_isClosed)
                                return;

                            Invalidate();
                        }
                        catch (ObjectDisposedException)
                        {
                                
                        }
                    }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void FetchMatches(string text, CancellationToken cancellationToken)
        {
            var scorer = new SearchablesMatchScorer();
            scorer.TypeNames = _typeNames;

            //and the explicit types
            foreach (var showOnlyType in showOnlyTypes)
                text = text + " " + showOnlyType.Name;

            var scores = scorer.ScoreMatches(_searchables, text, cancellationToken);

            if (scores == null)
                return;
            lock (oMatches)
            {
                _matches =
                    scores
                        .Where(score => score.Value > 0)
                        .OrderByDescending(score => score.Value)
                        .ThenByDescending(id=>id.Key.Key.ID) //favour newer objects over ties
                        .Take(MaxMatches)
                        .Select(score => score.Key.Key)
                        .ToList();
            }
        }

        

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            
            float maxWidthUsedDuringRender = 0;
            int renderWidth = panel1.Right;

            //the descendancy diagram
            int diagramStartX = renderWidth + 10;
            int diagramStartY = panel1.Bottom;
            int diagramWidth = Right - diagramStartX;
            
            lock (oMatches)
            {
                //draw Form background
                e.Graphics.FillRectangle(new SolidBrush(SystemColors.Control), 0, 0, renderWidth, (int)((_matches.Count * RowHeight) + DrawMatchesStartingAtY));

                //draw the search icon
                e.Graphics.DrawImage(_magnifier,0,0);
                
                //the match diagram
                if(_matches != null)
                {
                    //draw the icon that represents the object being displayed and it's name on the right
                    for (int i = 0; i < _matches.Count; i++)
                    {
                        bool isFavourite = _favouriteProvider.IsFavourite(_matches[i]);
                        float currentRowStartY = DrawMatchesStartingAtY + (RowHeight*i);

                        var img = _coreIconProvider.GetImage(_matches[i],isFavourite?OverlayKind.FavouredItem:OverlayKind.None);

                        SolidBrush fillColor;

                        if (i == keyboardSelectedIndex)
                            fillColor = new SolidBrush(keyboardSelectionColor);
                        else if( i== mouseSelectedIndex)
                            fillColor = new SolidBrush(mouseSelectionColor);
                        else
                            fillColor = new SolidBrush(Color.White);

                        e.Graphics.FillRectangle(fillColor, 1, currentRowStartY, renderWidth, RowHeight);

                        string text = _matches[i].ToString();

                        float textRight = e.Graphics.MeasureString(text, Font).Width + 20;
                        //record how wide it is so we know how much space is left to draw parents
                        maxWidthUsedDuringRender = Math.Max(maxWidthUsedDuringRender,textRight);

                        e.Graphics.DrawImage(img,1,currentRowStartY);
                        e.Graphics.DrawString(text,Font,Brushes.Black,20,currentRowStartY );

                        string extraText =" " + GetUsefulProperties(_matches[i]);
                        if (!string.IsNullOrWhiteSpace(extraText))
                        {
                            e.Graphics.DrawString(extraText, Font, Brushes.Gray, textRight, currentRowStartY);
                            float extraTextRight = textRight + e.Graphics.MeasureString(extraText, Font).Width;
                            maxWidthUsedDuringRender = Math.Max(maxWidthUsedDuringRender, extraTextRight);
                        }
                    }
                
                    //now draw parent string and icon on the right
                    for (int i = 0; i < _matches.Count; i++)
                    {
                        //get first parent that isn't one of the explicitly useless parent types (I'd rather know the Catalogue of an AggregateGraph than to know it's an under an AggregatesGraphNode)                
                        var descendancy = Activator.CoreChildProvider.GetDescendancyListIfAnyFor(_matches[i]);
                
                        object lastParent = null;

                        if(descendancy != null)
                        {

                            lastParent = descendancy.Parents.LastOrDefault(parent => 
                                !TypesThatAreNotUsefulParents.Contains(parent.GetType())
                                &&
                                !(parent is IContainer)
                                );

                            //if it is the selected node draw the parents diagram too
                            if (i == keyboardSelectedIndex)
                                DrawDescendancyDiagram(e, _matches[i], descendancy, diagramStartX, diagramStartY,diagramWidth);
                        }

                        float currentRowStartY = DrawMatchesStartingAtY + (RowHeight*i);
                    
                        if (lastParent != null)
                        {

                            ColorMatrix cm = new ColorMatrix();
                            cm.Matrix33 = 0.55f;
                            ImageAttributes ia = new ImageAttributes();
                            ia.SetColorMatrix(cm);

                            var rect = new Rectangle(renderWidth - 20, (int)currentRowStartY, 19, 19);
                            var img = _coreIconProvider.GetImage(lastParent);

                            //draw the parents image on the right
                            e.Graphics.DrawImage(img, rect, 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, ia);

                            var horizontalSpaceAvailableToDrawTextInto = renderWidth - (maxWidthUsedDuringRender + 20); 

                            string text = ShrinkTextToFitWidth(lastParent.ToString(),horizontalSpaceAvailableToDrawTextInto,e.Graphics);
                            var spaceRequiredForCurrentText = e.Graphics.MeasureString(text, Font).Width;

                            e.Graphics.DrawString(text, Font, Brushes.DarkGray, renderWidth - (spaceRequiredForCurrentText + 20), currentRowStartY);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns the text drawn for the object e.g. ToString() + (UsefulProperty)
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        private string GetUsefulProperties(IMapsDirectlyToDatabaseTable m)
        {
            StringBuilder sb = new StringBuilder();

            var p = _usefulPropertyFinder.GetProperties(m).ToArray();

            if (p.Length == 0)
                return null;

            sb.Append("(");

            foreach (var propertyInfo in p)
            {
                var attr = _usefulPropertyFinder.GetAttribute(propertyInfo);

                var key = string.IsNullOrWhiteSpace(attr.DisplayName) ? propertyInfo.Name : attr.DisplayName;
                var val = propertyInfo.GetValue(m);
                sb.Append(key + "='" + val + "' ");
            }
            
            sb.Append(")");

            return sb.ToString();
        }

        private void DrawDescendancyDiagram(PaintEventArgs e, IMapsDirectlyToDatabaseTable match, DescendancyList descendancy, int diagramStartX, int diagramStartY, int diagramWidth)
        {
            

            int diagramHeight = (int)(RowHeight * (descendancy.Parents.Length + 1));

            //draw diagram of descendancy 
            e.Graphics.FillRectangle(Brushes.White, diagramStartX, diagramStartY, diagramWidth, diagramHeight);

            //draw the parents
            for (int i = 0; i < descendancy.Parents.Length; i++)
            {
                var lineStartX = diagramStartX + (DiagramTabDistance*i);
                var lineStartY = diagramStartY + (RowHeight*i);

                var img = Activator.CoreIconProvider.GetImage(descendancy.Parents[i]);
                e.Graphics.DrawImage(img, lineStartX, lineStartY);
                e.Graphics.DrawString(descendancy.Parents[i].ToString(), Font, Brushes.Black, lineStartX + 21, lineStartY);

                if (i > 0)
                    DrawTreeNodeIsChildOfBlueLines(e, lineStartX, lineStartY);
            }

            //now draw the last object
            var lastLineStartX = diagramStartX + (DiagramTabDistance * descendancy.Parents.Length);
            var lastLineStartY = diagramStartY + (diagramHeight - RowHeight);
            
            var matchImg = Activator.CoreIconProvider.GetImage(match);
            e.Graphics.DrawImage(matchImg, lastLineStartX, lastLineStartY);
            e.Graphics.DrawString(match.ToString(), Font, Brushes.Black, lastLineStartX + 21, lastLineStartY);

            _diagramBottom = diagramStartY + diagramHeight;

            DrawTreeNodeIsChildOfBlueLines(e, lastLineStartX, lastLineStartY);

        }

        private static void DrawTreeNodeIsChildOfBlueLines(PaintEventArgs e, int lineStartX, float lineStartY)
        {
            //draw the |_ lines
            var midPointX = lineStartX - (DiagramTabDistance/2);
            var midPointY = lineStartY + (RowHeight/2);

            //straight down
            e.Graphics.DrawLine(Pens.Blue, midPointX, lineStartY, midPointX, midPointY);
            //then across
            e.Graphics.DrawLine(Pens.Blue, midPointX, midPointY, lineStartX - 1, midPointY);
        }

        private string ShrinkTextToFitWidth(string originalText, float horizontalSpaceAvailableToDrawTextInto, Graphics g)
        {

            //it fits without truncation
            if (g.MeasureString(originalText, Font).Width < horizontalSpaceAvailableToDrawTextInto)
                return originalText;

            var suffixWidth = g.MeasureString("...", Font).Width;
            
            while (g.MeasureString(originalText, Font).Width + suffixWidth > horizontalSpaceAvailableToDrawTextInto && originalText.Length > 1)
                //knock off a character
                originalText = originalText.Substring(0, originalText.Length - 1);

            return originalText + "...";
        }

        private void NavigateToObjectUI_FormClosed(object sender, FormClosedEventArgs e)
        {
            _isClosed = true;
            _autoCompleteProvider.UnRegister();
        }
    }
}
