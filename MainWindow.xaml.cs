using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Xml.Serialization;
using Microsoft.Office.Interop.Excel;
using WPF_TabletMap;
using ZedGraph;
using System.Windows.Controls;
using System.Windows.Input;
using Action = System.Action;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using ListBox = System.Windows.Controls.ListBox;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;
using Window = System.Windows.Window;
using WarThunderParser.controls;
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
        private Dictionary<string, string> _traslateDictionary;        
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
            this.DataContext = this;
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
                        
            serializer = new XmlSerializer(typeof(string[][]));
            string[][] openResult=null;
            _traslateDictionary = new Dictionary<string, string>();
            if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "Translate.xml"))
            {
                using (var filestream = new FileStream("Translate.xml", FileMode.Open))
                {
                    openResult = (string[][])serializer.Deserialize(filestream);
                }
            }
            if (openResult != null)
            {
                foreach (var pair in openResult)
                {
                    _traslateDictionary.Add(pair[0],pair[1]);
                }
            }

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

        private Graph[] OpenGraph()
        {
            var openResult = m_OpenManager.OpenMultiple(_graphfileextensions);
            if (openResult == null) return null;
            return openResult.Cast<Graph>().ToArray();
        }

        private void SaveGraph(Graph toSave)
        {
            m_SaveManager.Save(_graphfileextensions, toSave, toSave.ToString());
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
                StatusLabelMain.Content = "Сбор данных завершился с ошибками. Нажмите Start или F9 для начала нового сбора.";
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
            StatusLabelSecond.Content = "Сборщик " + e.Recorder.Uri + " прекратил сбор с сообщением: " + e.Reason;
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
                MessageBox.Show("Произошла ошибка! (" + ex.Message + ")");
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

            StatusLabelMain.Content = "Данные успешно собраны. Нажмите Start или F9 для начала нового сбора.";
            Started = false;
        }

        void OnStartNewDataCollecting(FdrManagerEventArgs e)
        {
            TableColumns.Clear();
            StatusLabelSecond.Content = "";
            StatusLabelMain.Content = "Идет сбор данных, нажмите Stop или F10 для завершения.";
        }

        private void btn_Graph_Save_Click(object sender, RoutedEventArgs e)
        {
            if (m_DataProcessingHelper == null || m_DataProcessingHelper.Graphs == null || m_DataProcessingHelper.Graphs.Count == 0)
            {
                MessageBox.Show("Графики не выбраны");
            }
            else
            {
                foreach (Graph graph in m_DataProcessingHelper.Graphs)
                {
                    SaveGraph(graph);
                }
            }            
        }       

        private void btn_Graph_Open_Click(object sender, RoutedEventArgs e)
        {
            var opened = OpenGraph();
            if (opened == null)
                return;
            m_DataProcessingHelper.Clear();
            cb_Abscissa.Items.Clear();
            Ordinats.Clear();

            Array.ForEach(opened, g => m_DataProcessingHelper.Graphs.Add(g));
            m_DataProcessingHelper.Redraw();
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
    }
        
}
