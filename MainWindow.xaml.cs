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
using WarThunderParser.core;

namespace WarThunderParser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool Started;
        private bool _isLegendVisible, _isAxisLabelVisible;
        private GraphSettings _graphSettings;
        private CollectSettings _collectSettings;
        ObservableCollection<Graph> graphCollectionFull { get; set;}
        ObservableCollection<Graph> graphCollectionSelected { get; set; }
        private FlightDataRecorder[] _recorders;
        private Graph currentGraph;
        private Dictionary<string, string> _traslateDictionary;
        private SaveManager _saveManager;
        private OpenManager _openManager;
        private Dictionary<string, Saver> _graphfileextensions;
        HookDemoHelper _keyHooker = new HookDemoHelper();

        public ObservableCollection<CheckedListItem<string>> Ordinats { get; set; }
        private DataProcessingHelper m_DataProcessingHelper;

        public MainWindow()
        {
            InitializeComponent();
            AppDomain.CurrentDomain.UnhandledException += ExceptionHandler;
            _graphSettings = new GraphSettings();
            _collectSettings = new CollectSettings();
            _isAxisLabelVisible = !AxisShowCheckBox.IsChecked.Value;
            _isLegendVisible = !LegendShowCheckBox.IsChecked.Value;
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

            Ordinats = new ObservableCollection<CheckedListItem<string>>();
            lb_Measures.ItemsSource = Ordinats;
        }
        static void ExceptionHandler(object sender, UnhandledExceptionEventArgs args)
        {
            var e = (Exception)args.ExceptionObject;
            MessageBox.Show("Exception caught: " + e.Message + Environment.NewLine + "Runtime terminating: " +
                            args.IsTerminating);
        }

        void GraphListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var addList = e.AddedItems;
            var xAxis = new List<string>();
            foreach (Graph item in GraphListBox.SelectedItems)
            {
                string axisName;
                var match = Regex.Match(item.XAxis, ".+,");
                axisName = match.Length != 0 ? match.Value.Remove(match.Value.Length - 1) : item.XAxis;
                axisName = _traslateDictionary.ContainsKey(axisName) ? _traslateDictionary[axisName] : item.XAxis;
                item.XAxis = axisName;
                if (!xAxis.Contains(axisName)) xAxis.Add(axisName);
                match = Regex.Match(item.YAxis, ".+,");
                axisName = match.Length != 0 ? match.Value.Remove(match.Value.Length - 1) : item.YAxis;
                axisName = _traslateDictionary.ContainsKey(axisName) ? _traslateDictionary[axisName] : item.YAxis;
                item.YAxis = axisName;
                
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


        public void GraphListItemsDoubleClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            var sourceTextBox = e.Source as TextBox;
            GraphListBox.UnselectAll();
            sourceTextBox.IsReadOnly = false;
            sourceTextBox.BorderThickness = new Thickness(1);
        }

        public void GraphListGotFocus(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            var filterListBox = e.Source as TextBox;
            DependencyObject item = VisualTreeHelper.GetParent(filterListBox);
            do
            {
                item = VisualTreeHelper.GetParent(item);
            } while (item.GetType()!=typeof(ListBoxItem));
            var listBoxItem = (item as ListBoxItem);
            listBoxItem.IsSelected = !listBoxItem.IsSelected;

        }

        public void GraphListItemsLostFocus(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            var sourceTextBox = e.Source as TextBox;
            sourceTextBox.IsReadOnly = true;
            sourceTextBox.BorderThickness = new Thickness(0);
        }

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


        void AxisTitleRedraw(bool showAxis)
        {
            if (!AllowDrawing()) return;
            var zedGraph = (ZedGraphControl)WinHost.Child;
            var myPane = zedGraph.GraphPane;
            myPane.XAxis.Title.IsVisible = showAxis;
            foreach (var axis in myPane.YAxisList)
            {
                axis.Title.IsVisible = showAxis;
            }
            zedGraph.Invalidate();
        }

        void LegendRedraw(bool showLegend)
        {
            if (!AllowDrawing()) return;
            var zedGraph = (ZedGraphControl)WinHost.Child;
            var myPane = zedGraph.GraphPane;
            foreach (var curve in myPane.CurveList)
            {
                curve.Label.IsVisible = showLegend;
            }
            zedGraph.Invalidate();
        }

        bool AllowDrawing()
        {
            return
                true;//((graphCollectionSelected != null) && (graphCollectionSelected.Count != 0)) || (currentGraph != null);
        }

        void Redraw()
        {
            if (true)
                return;
            List<Graph> graphList;
            if ((graphCollectionSelected == null) || (graphCollectionSelected.Count == 0))
            {
                if (currentGraph == null) return;
                graphList = new List<Graph>{currentGraph};
            }
            else
            {
                graphList = new List<Graph>(graphCollectionSelected);
            }
            var zedGraph = (ZedGraphControl)WinHost.Child;
            zedGraph.GraphPane.CurveList.Clear();
            GraphPane pane = zedGraph.GraphPane;
            pane.YAxisList.Clear();
            var yAxises = new Dictionary<string, int>();
            for (int k = 0; k < graphList.Count; k++)
            {
                Graph graph = graphList[k];
                LineItem line = k < _graphSettings.CurveLines.Count ? graph.GetLineItem(_graphSettings.CurveLines[k].LineColor,SymbolType.None, 2.0f) : graph.GetLineItem();
                if (_graphSettings.Smooth)
                {
                    var curList = (IPointList)line.Points.Clone();
                    var average = new MovingAverage(_graphSettings.SmoothPeriod);

                    switch (_graphSettings.SmoothType)
                    {
                        case SmoothModel.Average:
                            for (int i = 0; i < curList.Count; i++)
                            {
                                average.Push(curList[i].Y);
                                curList[i].Y = average.Average;
                            }
                            break;
                        case SmoothModel.Median:
                            for (int i = 0; i < curList.Count; i++)
                            {
                                average.Push(curList[i].Y);
                                curList[i].Y = average.Median;
                            }
                            break;
                        default:
                            throw new InvalidEnumArgumentException();
                    }
                    
                    line.Points = curList;
                }
                if (!yAxises.ContainsKey(graph.YAxis)) yAxises.Add(graph.YAxis, pane.AddYAxis(graph.YAxis));
                line.YAxisIndex = yAxises[graph.YAxis];
                pane.CurveList.Add(line);
            }
            pane.XAxis.Title.Text = graphList.Last().XAxis;
            pane.Title.Text = "";
            
            zedGraph.AxisChange();
            LegendRedraw(_isLegendVisible);
            AxisTitleRedraw(_isAxisLabelVisible);
            
            #region AxisGrids
            pane.XAxis.MajorGrid.IsVisible = _graphSettings.MajorGrid;
            pane.YAxis.MajorGrid.IsVisible = _graphSettings.MajorGrid;
            pane.XAxis.MinorGrid.IsVisible = _graphSettings.MinorGrid;
            pane.YAxis.MinorGrid.IsVisible = _graphSettings.MinorGrid;
            pane.YAxis.MajorGrid.DashOn = 10;
            pane.YAxis.MajorGrid.DashOff = 5;
            pane.XAxis.MajorGrid.DashOn = 10;
            pane.XAxis.MajorGrid.DashOff = 5;
            pane.YAxis.MajorGrid.DashOn = 10;
            pane.YAxis.MajorGrid.DashOff = 5;
            pane.XAxis.MinorGrid.DashOn = 1;
            pane.XAxis.MinorGrid.DashOff = 2;
            pane.YAxis.MinorGrid.DashOn = 1;
            pane.YAxis.MinorGrid.DashOff = 2;
            #endregion DashOnOff
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
        
        void OnFailure(FdrManagerEventArgs e)
        {
            Action onFailureAction = delegate()
            {
                StatusLabelMain.Content = "Сбор данных завершился с ошибками. Нажмите Start или F9 для начала нового сбора.";
                Started = false;
            };
            if (!Started) return;
            Dispatcher.Invoke(onFailureAction);
            MessageBox.Show(e.Message);
            


        }
        void OnRecorderFailure(FdrRecorderFailureEventArgs e)
        {
            if (!Started) return;
            StatusLabelSecond.Content = "Сборщик " + e.Recorder.Uri + " прекратил сбор с сообщением: " + e.Reason;
        }
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (Started) return;
            Started = true;
            _recorders = new []
            {
                new FlightDataRecorder("http://127.0.0.1:8111/state",true, _collectSettings.FailureDelay),
                new FlightDataRecorder("http://127.0.0.1:8111/indicators",false, _collectSettings.FailureDelay)
            };
            DataGrid1.Columns.Clear();
            _manager = new FdrManager(_recorders, _collectSettings.RequestInterval);
            m_DataProcessingHelper = new DataProcessingHelper(_manager);
            m_DataProcessingHelper.CollectSettings = _collectSettings;
            m_DataProcessingHelper.GraphControl = (ZedGraphControl)WinHost.Child;
            m_DataProcessingHelper.GraphSettings = _graphSettings;

            _manager.OnStartDataCollecting += OnStartNewDataCollecting;
            _manager.OnDataCollected += OnDataCollected;
            _manager.OnTotalFailure += OnFailure;
            _manager.OnRecorderFailure += OnRecorderFailure;
            _manager.StartDataCollect();
        }
        private FdrDataAdapter _adapter;
        private FdrManager _manager;

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Started) return;
            _manager.StopDataCollect();  
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            MessageBox.Show((0.4%0.5).ToString());
        }



        void DrawNewGraph()
        {
            if (true)
                return;

            if ((_adapter == null) || (_adapter.Values == null)) return;
            GraphListBox.SelectedIndex = -1;
            var start = 0;
            var end = _adapter.Values.Count - 1;
            if (DataGrid1.SelectedIndex!=-1)
            {
                start = DataGrid1.SelectedIndex;
                if (DataGrid1.SelectedItems.Count > 1)
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
            var p1 = new PointPairList(abs, ord);
            currentGraph = new Graph(p1, _adapter.FullNames(_adapter.CurGraphOrd) + " of " + _adapter.FullNames(_adapter.CurGraphAbs), _adapter.FullNames(_adapter.CurGraphAbs), _adapter.FullNames(_adapter.CurGraphOrd));
            Redraw();
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if ((_adapter == null) || (_adapter.Values == null)) return;
            if (!_collectSettings.ExcelSelectedOnly)
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

        private void AxisChooseCombobox_DropDownClosed(object sender, EventArgs e)
        {
            DrawNewGraph();
            
        }
                
        private void OnDataCollected(FdrManagerEventArgs e)
        {

            _adapter = new FdrDataAdapter(DataGrid1, CheckBoxPanel, GraphPanel, _collectSettings.FilterList, _traslateDictionary, _collectSettings.InterpInterval);
            _manager.InitializeAdapter(_adapter);
            TableStackPanel.Visibility = Visibility.Visible;
            StatusLabelMain.Content = "Данные успешно собраны. Нажмите Start или F9 для начала нового сбора.";
            foreach (var child in GraphPanel.Children)
            {
                var axisChooseBox = child as ComboBox;
                if (axisChooseBox  != null)
                {
                    axisChooseBox.DropDownClosed += AxisChooseCombobox_DropDownClosed;
                }
            } 
            if (GraphListBox.SelectedIndex == -1)
            {
                DrawNewGraph();
            }
            Started = false;
        }

        void OnStartNewDataCollecting(FdrManagerEventArgs e)
        {
            StatusLabelSecond.Content = "";
            TableStackPanel.Visibility = Visibility.Collapsed;
            StatusLabelMain.Content = "Идет сбор данных, нажмите Stop или F10 для завершения.";
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            if ((GraphListBox.SelectedIndex == -1) && (currentGraph != null))
            {
                currentGraph.GraphName = (new AskGraphNameWindow()).GetName(currentGraph.GraphName);
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
            if(currentGraph==null)
                return;
            if (graphCollectionFull.Contains(currentGraph))
                return;
            graphCollectionFull.Add((Graph)currentGraph.Clone());
            GraphListExpander.IsExpanded = true;
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            var opened = OpenGraph();
            if (opened == null) return;
            foreach (var graph in opened)
            {
                bool noDouble = true;
                foreach (var graph1 in graphCollectionFull)
                {
                    noDouble &= !graph.Equals(graph1);
                }
                if (noDouble) graphCollectionFull.Add(graph);

            }
            if (GraphListBox.SelectedIndex == -1)
            {
                GraphListBox.SelectedItems.Add(GraphListBox.Items[0]);
            }
            GraphListExpander.IsExpanded = true;
        }
        

        private void Button_Click_6(object sender, RoutedEventArgs e)
        {
            
        }

        private void Button_Click_8(object sender, RoutedEventArgs e)
        {
            if ((_adapter == null) || (_adapter.Values == null)) return;
            _adapter.FootToMeter(bool.Parse((string)(sender as Button).Tag));
        }



        private void AcceptDataSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if ((_adapter == null) || (_adapter.Values == null)) return;
            _adapter.InterpInterval = _collectSettings.InterpInterval;
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


        private const int MinRequestInterval = 0;
        private const int DefaultRequestInterval = 0;

        private const int MinInterpInterval = 50;
        private const int DefaultInterpInterval = 200;

        private void AxisShowCheckBox_Click(object sender, RoutedEventArgs e)
        {
            _isAxisLabelVisible = !AxisShowCheckBox.IsChecked.Value;
            AxisTitleRedraw(_isAxisLabelVisible);
        }

        
        private void LegendShowCheckBox_Click(object sender, RoutedEventArgs e)
        {
            _isLegendVisible = !LegendShowCheckBox.IsChecked.Value;
            LegendRedraw(_isLegendVisible);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Button_Click_9(object sender, RoutedEventArgs e)
        {
            GraphSettings result = (new GraphSetupWindow()).ShowSettings(_graphSettings);
            if (result != null)
            {
                _graphSettings = result;
            }
            Redraw();
        }

        private void Button_Click_10(object sender, RoutedEventArgs e)
        {
            CollectSettings result = (new CollectSetupWindow()).ShowSettings(_collectSettings);
            if (result != null)
            {
                _collectSettings = result;
            }
            if(_adapter==null)return;
            _adapter.ReCalc(_collectSettings.FilterList,_traslateDictionary,_collectSettings.InterpInterval);
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if(!AllowDrawing())return;
            var selectedItems = new List<Graph>(GraphListBox.SelectedItems.Cast<Graph>());
            foreach (var item in selectedItems)
            {
                graphCollectionFull.Remove(item);
            }
            GraphListBox.Items.Refresh();
        }

        private void DataGrid1_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DataGrid1.UnselectAll();
        }

        private void GraphTabItem_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((GraphListBox.SelectedIndex == -1) && (currentGraph != null))
            {
                MessageBox.Show("!");
                Redraw();
            }
        }

        private void WinHost_ChildChanged(object sender, System.Windows.Forms.Integration.ChildChangedEventArgs e)
        {

        }

        private void cb_Abscissa_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            m_DataProcessingHelper.setAbscissa(cb_Abscissa.SelectedItem.ToString());
        }

        private void onOrdinateChecked(object sender, PropertyChangedEventArgs args)
        {
            CheckedListItem<string> item = sender as CheckedListItem<string>;
            if (item.IsChecked)
                m_DataProcessingHelper.addOrdinate(item.Item);
            else
                m_DataProcessingHelper.removeOrdinate(item.Item);
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count==0) return;
            if((e.AddedItems[0] == GraphTabItem)&&(GraphListBox.SelectedIndex == -1) && (currentGraph != null))
            {
                DrawNewGraph();
            }
        }
    }
        
}
