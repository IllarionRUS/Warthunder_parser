using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Xml.Serialization;
using Microsoft.Office.Interop.Excel;
using WPF_TabletMap;
using ZedGraph;
using System.Windows.Controls;
using System.Windows.Input;
using Action = System.Action;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using Window = System.Windows.Window;
using WarThunderParser.Controls;
using WarThunderParser.Core;

namespace WarThunderParser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool Started;
        private GraphSettings _graphSettings;
        private CollectSettings _collectSettings;
        private FlightDataRecorder[] _recorders;     
        private Dictionary<string, Saver> _graphfileextensions;
        HookDemoHelper _keyHooker = new HookDemoHelper();

        public ObservableCollection<CheckedListItem<string>> TableColumns { get; set; }
        public ObservableCollection<CheckedListItem<string>> Ordinats { get; set; }
        private FdrManager m_Manager;
        private DataProcessingHelper m_DataProcessingHelper;

        private SaveManager m_SaveManager;
        private OpenManager m_OpenManager;

        public static void ExceptionHandler(object sender, UnhandledExceptionEventArgs args)
        {
            var e = (Exception)args.ExceptionObject;
            MessageBox.Show("Exception caught: " + e.Message + Environment.NewLine + "Runtime terminating: " +
                            args.IsTerminating);
        }

        public MainWindow()
        {
            InitializeComponent();

            AppDomain.CurrentDomain.UnhandledException += ExceptionHandler;
            _graphSettings = new GraphSettings();
            _collectSettings = new CollectSettings();
            DataContext = this;
            _keyHooker.SetHook();
            _keyHooker.OnHook += OnHook;
            m_SaveManager = new SaveManager();
            m_OpenManager = new OpenManager();
            _graphfileextensions = new Dictionary<string, Saver> { { "graphics files (*.grph)|*.grph", new BinSaver() } };
                                  

            var serializer = new XmlSerializer(typeof(List<string>));
            if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "Filters.xml"))
            {
                using (var filestream = new FileStream("Filters.xml", FileMode.Open))
                {
                    _collectSettings.FilterList = (List<string>) serializer.Deserialize(filestream);
                }
            }
            else
            {
                _collectSettings.FilterList = new List<string>();
            }

            var zedGraph = (ZedGraphControl)WinHost.Child;
            zedGraph.GraphPane.CurveList.Clear();
            GraphPane pane = zedGraph.GraphPane;           
            pane.Title.Text = "";
            pane.XAxis.Title.Text = "";
            pane.YAxis.Title.Text = "";
                        
            dg_Data.EnableColumnVirtualization = true;
            dg_Data.EnableRowVirtualization = true;
            Ordinats = new ObservableCollection<CheckedListItem<string>>();
            TableColumns = new ObservableCollection<CheckedListItem<string>>();
            lb_Measures.ItemsSource = Ordinats;
            lb_Columns.ItemsSource = TableColumns;

            _recorders = new[]
            {
                new FlightDataRecorder("http://127.0.0.1:8111/state",true, _collectSettings.FailureDelay),
                new FlightDataRecorder("http://127.0.0.1:8111/indicators",false, _collectSettings.FailureDelay)
            };
            m_Manager = new FdrManager(_recorders, _collectSettings.RequestInterval);
            
            m_DataProcessingHelper = new DataProcessingHelper(m_Manager);
            m_DataProcessingHelper.GraphControl = (ZedGraphControl)WinHost.Child;
            m_DataProcessingHelper.DataGrid = dg_Data;
            m_DataProcessingHelper.CollectSettings = _collectSettings;
            m_DataProcessingHelper.GraphSettings = _graphSettings;

            m_Manager.OnStartDataCollecting += OnStartNewDataCollecting;
            m_Manager.OnDataCollected += OnDataCollected;
            m_Manager.OnTotalFailure += OnFailure;
            m_Manager.OnRecorderFailure += OnRecorderFailure;
        }

        private SavedState[] Load()
        {
            try
            { 
                var openResult = m_OpenManager.OpenMultiple(_graphfileextensions);
                if (openResult == null)
                    return null;
                return openResult.Cast<SavedState>().ToArray();
            } 
            catch (Exception e)
            {
                MessageBox.Show(Properties.Resources.common_error + " (" + e.Message + ")");
                return null;
            }
        }

        private void Save()
        {
            SavedState state = new SavedState(this);
            m_SaveManager.Save(_graphfileextensions, state, state.getName());
        }

        private void OnHook(HookDemoHelper.HookEventArgs e)
        {
           if ((e.Code == 120)&&(!Started))
            {
                StartButton_Click(new object(),new RoutedEventArgs());
            }
           if ((e.Code == 121) && (Started))
            {
                StopButton_Click(new object(), new RoutedEventArgs());
            }
        }

        private void OnFailure(FdrManagerEventArgs e)
        {
            TableColumns.Clear();
            Ordinats.Clear();
            cb_Abscissa.Items.Clear();

            Action onFailureAction = delegate()
            {
                StatusLabelMain.Content = Properties.Resources.state_failed;
                Started = false;
            };
            if (!Started)
                return;
            Dispatcher.Invoke(onFailureAction);
            MessageBox.Show(e.Message);            
        }

        private void OnRecorderFailure(FdrRecorderFailureEventArgs e)
        {
            if (!Started)
                return;
            StatusLabelSecond.Content = string.Format(Properties.Resources.state_recorder_failed_format, e.Recorder.Uri, e.Reason);
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (Started)
                return;
            Started = true;
            
            m_Manager.StartDataCollect();
        }       

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Started)
                return;
            m_Manager.StopDataCollect();  
        }
        
        private void btn_Excel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (m_DataProcessingHelper.GetCollectedMeasuresNames().Length == 0)
                    return;
                if (!_collectSettings.ExcelSelectedOnly)
                {
                    dg_Data.SelectAll();
                }
                dg_Data.ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader;
                ApplicationCommands.Copy.Execute(null, dg_Data);
                Microsoft.Office.Interop.Excel.Application app;
                Workbook workbook;
                Worksheet worksheet;
                object misvalue = System.Reflection.Missing.Value;
                app = new Microsoft.Office.Interop.Excel.Application();
                app.Visible = true;
                workbook = app.Workbooks.Add(misvalue);
                worksheet = (Worksheet)workbook.Worksheets.Item[1];
                Range CR = (Range)worksheet.Cells[1, 1];
                CR.Select();
                worksheet.PasteSpecial(CR, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Properties.Resources.common_error + " (" + ex.Message + ")");
            }
        }
                
        private void OnDataCollected(FdrManagerEventArgs e)
        {
            Ordinats.Clear();
            cb_Abscissa.Items.Clear();
            TableColumns.Clear();

            foreach (var title in m_DataProcessingHelper.GetCollectedMeasuresNames())
            {
                CheckedListItem<string> item = new CheckedListItem<string>() { Item = title };
                item.PropertyChanged += onOrdinateChecked;
                Ordinats.Add(item);                
                cb_Abscissa.Items.Add(title);

                item = new CheckedListItem<string>() { Item = title };
                item.PropertyChanged += onTabColumnChecked;
                TableColumns.Add(item);
            }

            StatusLabelMain.Content = Properties.Resources.state_success;
            Started = false;
        }

        void OnStartNewDataCollecting(FdrManagerEventArgs e)
        {
            Ordinats.Clear();
            cb_Abscissa.Items.Clear();
            TableColumns.Clear();
            StatusLabelSecond.Content = "";
            StatusLabelMain.Content = Properties.Resources.state_collecting;
        }

        private void btn_Graph_Save_Click(object sender, RoutedEventArgs e)
        {
            if (m_DataProcessingHelper == null || m_DataProcessingHelper.GetCollectedMeasuresNames() == null || m_DataProcessingHelper.GetCollectedMeasuresNames().Count() == 0)
            {
                MessageBox.Show(Properties.Resources.save_error);
            }
            else
            {
                Save();
            }            
        }       

        private void btn_Graph_Open_Click(object sender, RoutedEventArgs e)
        {
            var opened = Load();
            if (opened == null)
                return;
            cb_Abscissa.Items.Clear();
            Ordinats.Clear();

            m_DataProcessingHelper.loadState(opened[0].Data);
            foreach (var title in m_DataProcessingHelper.GetCollectedMeasuresNames())
            {
                CheckedListItem<string> item = new CheckedListItem<string>() { Item = title };                
                Ordinats.Add(item);
                cb_Abscissa.Items.Add(title);

                item = new CheckedListItem<string>() { Item = title };
                item.PropertyChanged += onTabColumnChecked;
                TableColumns.Add(item);
            }

            cb_Abscissa.SelectionChanged -= cb_Abscissa_SelectionChanged;

            cb_Abscissa.SelectedItem = m_DataProcessingHelper.Graphs.First().XAxis;
            m_DataProcessingHelper.Graphs.Select(g => g.YAxis).ToList().ForEach(y => Ordinats.Where(o => o.Item.Equals(y)).First().IsChecked = true);
            opened[0].CheckedTables.ForEach(c => TableColumns.Where(t => t.Item.Equals(c)).First().IsChecked = true);

            cb_Abscissa.SelectionChanged += cb_Abscissa_SelectionChanged;
            Ordinats.ToList().ForEach(o => o.PropertyChanged += onOrdinateChecked);

        }
        
        private void btn_Help_Click(object sender, RoutedEventArgs e)
        {
            (new HelpWindow()).ShowDialog();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void btn_Graph_Prefs_Click(object sender, RoutedEventArgs e)
        {
            GraphSettings result = (new GraphSetupWindow()).ShowSettings(_graphSettings);            
            if (result != null)
            {
                _graphSettings = result;
                m_DataProcessingHelper.GraphSettings = _graphSettings;
            }
        }

        private void btn_Preferences_Click(object sender, RoutedEventArgs e)
        {
            CollectSettings result = (new CollectSetupWindow()).ShowSettings(_collectSettings);
            if (result != null)
            {
                _collectSettings = result;
                m_DataProcessingHelper.CollectSettings = _collectSettings;
            } 
        }

        private void WinHost_ChildChanged(object sender, System.Windows.Forms.Integration.ChildChangedEventArgs e)
        {

        }

        private void cb_Abscissa_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            m_DataProcessingHelper.SetAbscissa(cb_Abscissa.SelectedItem == null ? null : cb_Abscissa.SelectedItem.ToString());
        }

        private void onOrdinateChecked(object sender, PropertyChangedEventArgs args)
        {
            CheckedListItem<string> item = sender as CheckedListItem<string>;
            if (item.IsChecked)
                m_DataProcessingHelper.AddOrdinate(item.Item);
            else
                m_DataProcessingHelper.RemoveOrdinate(item.Item);
        }

        private void onTabColumnChecked(object sender, PropertyChangedEventArgs args)
        {
            CheckedListItem<string> item = sender as CheckedListItem<string>;
            if (item.IsChecked)
                m_DataProcessingHelper.ShowColumn(item.Item);
            else
                m_DataProcessingHelper.HideColumn(item.Item);
        }

        private void btn_ToMetrical_Click(object sender, RoutedEventArgs e)
        {
            m_DataProcessingHelper.ConvertToMetrical();
        }

        private void btn_ToImperial_Click(object sender, RoutedEventArgs e)
        {
            m_DataProcessingHelper.ConvertToImperial();
        }

        [Serializable]
        private class SavedState
        {
            internal DataProcessingHelper.SavedState Data { get; set; }
            internal List<string> CheckedTables { get; set; }

            public SavedState(MainWindow parent)
            {
                Data = parent.m_DataProcessingHelper.getSavedState();
                CheckedTables = parent.TableColumns.Where(c => c.IsChecked).Select(c => c.Item).ToList();
            }

            public string getName()
            {
                return Data.getName();
            }
        }

    }
        
}
