using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Xml.Serialization;
using Microsoft.Office.Interop.Excel;
using WPF_TabletMap;
using ZedGraph;
using System.Windows.Controls;
using System.Windows.Input;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;
using Window = System.Windows.Window;

namespace Test123
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool Started;
        ObservableCollection<Graph> graphCollectionFull { get; set;}
        ObservableCollection<Graph> graphCollectionSelected { get; set; }
        private List<FlightDataRecorder> _recorders;
        private bool isOnlySelected = false;
        private bool isFiltersAllowed = true;
        private Graph currentGraph;
        private List<string> _filterList;
        private Dictionary<string, string> _traslateDictionary;
        double dpiX = 0, dpiY = 0;
        public MainWindow()
        {
            InitializeComponent();
            
            this.DataContext = this;
            _keyHooker.SetHook();
            _keyHooker.OnHook += OnHook;
            _saveManager = new SaveManager();
            _openManager = new OpenManager();
            _graphfileextensions = new Dictionary<string, Saver> { { "graphics files (*.grph)|*.grph", new BinSaver() } };
            graphCollectionFull = new ObservableCollection<Graph>();
            graphCollectionSelected = new ObservableCollection<Graph>();
            GraphListBox.ItemsSource = graphCollectionFull;
            GraphListBox.SelectionChanged += GraphListBox_SelectionChanged;
            var serializer = new XmlSerializer(typeof(List<string>));
            if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "Filters.xml"))
            {
                using (var filestream = new FileStream("Filters.xml", FileMode.Open))
                {
                    _filterList = (List<string>) serializer.Deserialize(filestream);
                }
            }
            else
            {
                _filterList = new List<string>();
            }
            FilterListBox.ItemsSource = _filterList;
            var zedGraph = (ZedGraphControl)WinHost.Child;
            zedGraph.GraphPane.CurveList.Clear();
            GraphPane myPane = zedGraph.GraphPane;
            myPane.Title.Text = "";
            myPane.XAxis.Title.Text = "";
            myPane.YAxis.Title.Text = "";
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
        }

        void GraphListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var addList = e.AddedItems;
            var xAxis = new List<string>();
            foreach (Graph item in GraphListBox.SelectedItems)
            {
                if(!xAxis.Contains(item.XAxis)) xAxis.Add(item.XAxis);
            }
            if (xAxis.Count > 1)
            {
                foreach (var item in addList)
                {
                    GraphListBox.SelectedItems.Remove(item);
                }
                MessageBox.Show("Графики имеют разные оси абсцисс!");
                return;
            }
            graphCollectionSelected.Clear();
            foreach (Graph item in GraphListBox.SelectedItems)
            {
                graphCollectionSelected.Add(item);
            }
            Redraw();
        }
     
        private FdrTableAdapter _fdrTableAdapter;
        private DateTime _syncTime;
        private SaveManager _saveManager;
        private OpenManager _openManager;
        private Dictionary<string, Saver> _graphfileextensions;
        HookDemoHelper _keyHooker = new HookDemoHelper();

        void OnHook(HookDemoHelper.HookEventArgs e)
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

        private bool _isSmooth = false;
        void Redraw()
        {
            if ((graphCollectionSelected == null)||(graphCollectionSelected.Count ==0)) return;
            var zedGraph = (ZedGraphControl)WinHost.Child;
            zedGraph.GraphPane.CurveList.Clear();
            GraphPane myPane = zedGraph.GraphPane;
            myPane.YAxisList.Clear();
            var yAxises = new Dictionary<string, int>();
            foreach (var graph in graphCollectionSelected)
            {
                var line = graph.GetLineItem();
                if (!yAxises.ContainsKey(graph.YAxis)) yAxises.Add(graph.YAxis, myPane.AddYAxis(graph.YAxis));
                line.YAxisIndex = yAxises[graph.YAxis];
                myPane.CurveList.Add(line);
            }
            myPane.XAxis.Title.Text = graphCollectionSelected[0].XAxis;
            myPane.Title.Text = "";
            zedGraph.AxisChange();
            zedGraph.Invalidate();
            zedGraph.Refresh();
        }
        Graph[] OpenGraph()
        {
           
            var openResult = _openManager.OpenMultiple(_graphfileextensions);
            if (openResult == null) return null;
            return openResult.Cast<Graph>().ToArray();
        }

        void SaveGraph(Graph toSave)
        {
            _saveManager.Save(_graphfileextensions, toSave, toSave.ToString());
        }

        void OnFail()
        {
            if (!Started) return;
            StatusLabel.Content = "Сбор данных завершился с ошибками. Нажмите Start или F9 для начала нового сбора.";
            Started = false;
        }
        void OnGoodEnd()
        {
            if (!Started) return;
            this.Dispatcher.Invoke(CollectData);
           // CollectData();
            Started = false;
        }

        void CollectData()
        {
            if (!Started) return;
            List<string> inputList = null;
            if (isFiltersAllowed)
            {
                inputList = _filterList;
            }
            _syncTime = new DateTime(0);
            foreach (var flightDataRecorder in _recorders)
            {
                if (flightDataRecorder.GetInitTime > _syncTime)
                {
                    _syncTime = flightDataRecorder.GetInitTime;
                }
            }
            _syncTime = _syncTime.AddMilliseconds(_recorders[0].InterpInterval);
            _adapter = new FdrTableAdapter(_recorders.ToArray(), DataGrid1, CheckBoxPanel,
            GraphPanel, _syncTime, inputList, _traslateDictionary);
            OnDataCollected();
            
        }
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (Started) return;
            Started = true;
            _recorders = new List<FlightDataRecorder>()
            {
                new FlightDataRecorder("http://127.0.0.1:8111/state",
                    (int) RequestIntervalBox.Value, (int) InterpIntervalBox.Value),
                new FlightDataRecorder("http://127.0.0.1:8111/indicators",
                    (int) RequestIntervalBox.Value, (int) InterpIntervalBox.Value)
            };
            DataGrid1.Columns.Clear();
            foreach (var flightDataRecorder in _recorders)
            {
                flightDataRecorder.OnFail += OnFail;
                flightDataRecorder.OnGoodEnd += OnGoodEnd;
                flightDataRecorder.GetParams(true);
                flightDataRecorder.Start();
            }
            if (Started)
                OnStartNewDataCollection();
        }
        private FdrTableAdapter _adapter;

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var flightDataRecorder in _recorders)
            {
                flightDataRecorder.Stop();
            }     
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            MessageBox.Show((0.4%0.5).ToString());
        }

        private void DataGrid1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if ((_adapter == null) || (_adapter.Values == null)) return;
            GraphListBox.SelectedIndex = -1;
            var start = 0;
            var end = _adapter.Values.Count -1 ;
            if (DataGrid1.SelectedItems.Count > 1)
            {
                 start = DataGrid1.SelectedIndex;
                 end = DataGrid1.SelectedIndex + DataGrid1.SelectedItems.Count;
            }
            double[] abs = new double[end - start];
            double[] ord = new double[end - start];
            for (int i = start; i < end; i++)
            {
                if (_adapter.CurGraphOrd > 0)
                    ord[i - start] = _adapter.Values[i].Value[_adapter.CurGraphOrd - 1];
                else
                    ord[i - start] = _adapter.Values[i].Key.TotalMilliseconds;
                if (_adapter.CurGraphAbs > 0)
                    abs[i - start] = _adapter.Values[i].Value[_adapter.CurGraphAbs - 1];
                else
                    abs[i - start] = _adapter.Values[i].Key.TotalMilliseconds;
            }
            var zedGraph = (ZedGraphControl)WinHost.Child;
            zedGraph.GraphPane.CurveList.Clear();
            GraphPane myPane = zedGraph.GraphPane;
            var p1 = new PointPairList(abs,ord);
            /*var curve = myPane.AddCurve(_adapter.FullNames(_adapter.CurGraphOrd) + " of " + _adapter.FullNames(_adapter.CurGraphAbs), p1, System.Drawing.Color.Blue, SymbolType.None);
            curve.Line.Width = 2.0F;*/
            currentGraph = new Graph(p1, _adapter.FullNames(_adapter.CurGraphOrd) + " of " + _adapter.FullNames(_adapter.CurGraphAbs), _adapter.FullNames(_adapter.CurGraphAbs), _adapter.FullNames(_adapter.CurGraphOrd));
            myPane.CurveList.Add(currentGraph.GetLineItem());
            myPane.Title.Text = "";
            myPane.XAxis.Title.Text = currentGraph.XAxis;
            myPane.YAxis.Title.Text = currentGraph.YAxis;
            zedGraph.AxisChange();
            zedGraph.Invalidate();
            
            zedGraph.Refresh();
          
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if ((_adapter == null) || (_adapter.Values == null)) return;
            if (!isOnlySelected)
            {
                DataGrid1.SelectAll();
            }
            DataGrid1.ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader;
            ApplicationCommands.Copy.Execute(null,DataGrid1);
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

        private void OnDataCollected()
        {
            GraphTabItem.Visibility = Visibility.Visible;
            TableStackPanel.Visibility = Visibility.Visible;
            StatusLabel.Content = "Данные успешно собраны. Нажмите Start или F9 для начала нового сбора.";

        }

        void OnStartNewDataCollection()
        {
            TableStackPanel.Visibility = Visibility.Collapsed;
            StatusLabel.Content = "Идет сбор данных, нажмите Stop или F10 для завершения.";
            
        }
        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            if ((GraphListBox.SelectedIndex == -1) && (currentGraph != null))
            {
                if (!string.IsNullOrWhiteSpace(NameTextBox.Text)) currentGraph.GraphName = NameTextBox.Text;
                SaveGraph(currentGraph);
                return;
            }
            foreach (Graph graph in GraphListBox.SelectedItems)
            {
                SaveGraph(graph);
            }
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
           // MessageBox.Show(NameTextBox.Text);
            if(currentGraph==null)return;
            if (!string.IsNullOrWhiteSpace(NameTextBox.Text)) currentGraph.GraphName = NameTextBox.Text;
            if(graphCollectionFull.Contains(currentGraph))return;
            graphCollectionFull.Add(currentGraph);
         //   GraphListBox.SelectedItems.Add((object) currentGraph);
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            var opened = OpenGraph();
            if (opened == null) return;
            foreach (var graph in opened)
            {
                graphCollectionFull.Add(graph);
            }

        }
        
        private void SmoothCheckBox_Click(object sender, RoutedEventArgs e)
        {
            _isSmooth = SmoothCheckBox.IsChecked.Value;
            Redraw();
        }

        private void ExcelExportCheckBox_Click(object sender, RoutedEventArgs e)
        {
            isOnlySelected = ExcelExportCheckBox.IsChecked.Value;
        }

        private void Button_Click_6(object sender, RoutedEventArgs e)
        {
            
        }

        private void Button_Click_8(object sender, RoutedEventArgs e)
        {
            if ((_adapter == null) || (_adapter.Values == null)) return;
            _adapter.FootToMeter(bool.Parse((string)(sender as Button).Tag));
        }

        private void AcceptFilterButton_Click(object sender, RoutedEventArgs e)
        {
            List<string> inputList = new List<string>();
            if (isFiltersAllowed)
            {
                inputList = _filterList;
            }
            if ((_adapter == null) || (_adapter.Values == null)) return;
            _adapter = new FdrTableAdapter(_recorders.ToArray(), DataGrid1, CheckBoxPanel,
                GraphPanel, _syncTime, inputList, _traslateDictionary);
            
        }

        private void AddFilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(FilterTextBox.Text))
            {
                _filterList.Add(FilterTextBox.Text);
            }
            FilterListBox.Items.Refresh();
        }

        private void DeleteFilterButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (string item in FilterListBox.SelectedItems)
            {
                _filterList.Remove(item);
            }
            FilterListBox.Items.Refresh();
        }

        private void AcceptDataSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if ((_adapter == null) || (_adapter.Values == null)) return;
            _adapter.NewInterpInterval((int)InterpIntervalBox.Value);
        }

        private void InterpIntervalBox_ValueChanged(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click_7(object sender, RoutedEventArgs e)
        {
            (new HelpWindow()).ShowDialog();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void FilterCheckBox_Click(object sender, RoutedEventArgs e)
        {
            isFiltersAllowed = FilterCheckBox.IsChecked.Value;
        }








    }
}
