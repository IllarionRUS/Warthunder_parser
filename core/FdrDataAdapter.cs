using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Threading;
using WarThunderParser.Core;
using Binding = System.Windows.Data.Binding;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using DataGrid = System.Windows.Controls.DataGrid;
using Label = System.Windows.Controls.Label;

namespace WarThunderParser
{
    public class FdrDataAdapter
    {
        const string TimeTranslate = "Время, мс";
        const string ColumnNameTurnRadius = "Радиус гор. виража, м";
        const string ColumnNameTurnTime = "Время гор. виража, сек";
        const string ColumnNameTASAcceleration = "Ускорение TAS, м/с^2";
        const string ColumnNameIASAcceleration = "Ускорение IAS, м/с^2";
        private ObservableCollection<KeyValuePair<TimeSpan, double[]>> _values;
        public ObservableCollection<KeyValuePair<TimeSpan, double[]>> Values
        {
            get { return _values; }
            set
            {
                _values = value;
                if ((_values != null)&&(OnCreateCollection!=null))
                {
                    OnCreateCollection();
                }
            }
        }

        public int CurGraphAbs { get; private set; }
        public int CurGraphOrd { get; private set; }
        private int _interpInterval;
        public int InterpInterval
        {
            get { return _interpInterval; }
            set
            {
                if (_interpInterval != value)
                {
                    _interpInterval = value;
                    Initialize(_recorders);
                }
            }
        }

        public delegate void CollectionCreatedEventHandler();
        public event CollectionCreatedEventHandler OnCreateCollection;
        public string FullNames(int index)
        {
            if (index > 0)
            {
                return ColumnsNames[index - 1];
            }
            else
            {
                return TimeTranslate;
            }
        }

        Dictionary<int,int> _arrayPositionDictionary = new Dictionary<int, int>();
        public List<string> ColumnsNames;
        public int TotalCount { get { return ColumnsNames.Count ; } }
        private DataGrid _dataGrid;
        private WrapPanel _graphwrapPanel;
        private WrapPanel _columnsWrapPanel;
        private FlightDataRecorder[] _recorders;
        private List<string> _filterList;
        private Dictionary<string, string> _translateDictionary;
        private List<List<int>> _filterIndexes;
        

        string GetBaseorTranslate(string baseValue)
        {
            if (_translateDictionary.ContainsKey(baseValue)) return _translateDictionary[baseValue];
            return baseValue;
        }

        void GetValues()
        {
            int turnNum = ColumnsNames.IndexOf(GetBaseorTranslate("turn"));
            int TASNum = ColumnsNames.IndexOf(GetBaseorTranslate("TAS"));
            int IASNum = ColumnsNames.IndexOf(GetBaseorTranslate("IAS"));
            var lists = new List<List<double>[]>();
            int minCount = int.MaxValue;
            var syncTime = new DateTime(0);
            foreach (var flightDataRecorder in _recorders)
            {
                if (flightDataRecorder.InitTime > syncTime)
                {
                    syncTime = flightDataRecorder.InitTime;
                }
                
            }
            foreach (var flightDataRecorder in _recorders)
            {
                lists.Add(flightDataRecorder.GetApproxList(syncTime,InterpInterval));
                
                if (lists.Last()[0].Count < minCount)
                {
                    minCount = lists.Last()[0].Count;
                }
            }

            for (int i = 0; i < minCount; i++)
            {
                var toAdd = new double[TotalCount];
                var curMark = TimeSpan.FromMilliseconds(lists[0][0][i]);
                for (int j = 0; j < lists.Count; j++)
                {
                    int paramsCount = lists[j].Count();
                    var curData = new List<double>();
                    for (int k = 1; k < paramsCount; k++)
                    {
                        if (_filterIndexes[j].Contains(k)) continue;
                        curData.Add(lists[j][k][i]);
                    }
                    curData.CopyTo(toAdd, _arrayPositionDictionary[j]);
                }
                Values.Add(new KeyValuePair<TimeSpan, double[]>(curMark, toAdd));
            }
            for (int i = 1; i < minCount; i++)
            {
                if (turnNum >= 0)
                {
                    Values[i].Value[TotalCount - 4] = Math.PI*2/Math.Abs(Values[i].Value[turnNum]);
                    Values[i].Value[TotalCount - 3] = Values[i].Value[TASNum]/Math.Abs(Values[i].Value[turnNum]);
                }
                double dt = Values[i].Key.TotalSeconds - Values[i - 1].Key.TotalSeconds;
                if (Values[i - 1].Value[TASNum] == 0)
                {
                    Values[i].Value[TotalCount - 2] = 0;
                }
                else
                {
                    Values[i].Value[TotalCount - 2] = (Values[i].Value[TASNum] - Values[i - 1].Value[TASNum]) / dt;
                }
                if (Values[i - 1].Value[IASNum] == 0)
                {
                    Values[i].Value[TotalCount - 1] = 0;
                }
                else
                {
                    Values[i].Value[TotalCount - 1] = (Values[i].Value[IASNum] - Values[i - 1].Value[IASNum]) / dt;
                }
            }
            Values[0].Value[TotalCount - 4] = Values[1].Value[TotalCount - 4];
            Values[0].Value[TotalCount - 3] = Values[1].Value[TotalCount - 3];
            Values[0].Value[TotalCount - 2] = Values[1].Value[TotalCount - 2];
            Values[0].Value[TotalCount - 1] = Values[1].Value[TotalCount - 1];
        }

        public void ReCalc(List<string> filterList, Dictionary<string, string> translateDictionary, int interpTime)
        {
            _filterList = filterList;
            _interpInterval = interpTime;
            _translateDictionary = translateDictionary;
            if (interpTime > _recorders[0].Values(Consts.Value.Time).Last())
                return;
            Initialize(_recorders);
        }

        public void Initialize(FlightDataRecorder[] recorders)
        {
            _arrayPositionDictionary.Clear();
            _columnsWrapPanel.Children.Clear();
            _graphwrapPanel.Children.Clear();
            _recorders = recorders;
            _dataGrid.ItemsSource = Values;
            _dataGrid.Columns.Clear();
            ColumnsNames = new List<string>();
            Values = new ObservableCollection<KeyValuePair<TimeSpan, double[]>>();
            _filterIndexes = new List<List<int>>();
            int k = 0;
            foreach (var recorder in recorders)
            {
                string[] names = recorder.Names;
                var curfilterindexes = new List<int>();
                _arrayPositionDictionary.Add(k, ColumnsNames.Count);
                k++;
                for (int i = 1; i < names.Length; i++)
                {
                    if ((_filterList != null) && (_filterList.Contains(names[i])))
                    {
                        curfilterindexes.Add(i);
                        continue;
                    }
                    if (_translateDictionary.ContainsKey(names[i]))
                    {
                        ColumnsNames.Add(_translateDictionary[names[i]]);
                    }
                    else
                    {
                        var unit = recorder.Unit(names[i]);
                        if (unit != "") unit = unit.Insert(0, ", ");
                        ColumnsNames.Add(names[i] + unit);
                    }
                }
                _filterIndexes.Add(curfilterindexes);
            }
            int turnNum = ColumnsNames.IndexOf(GetBaseorTranslate("turn"));
            if (turnNum >=0)
            {
                ColumnsNames.Add(ColumnNameTurnTime);
                ColumnsNames.Add(ColumnNameTurnRadius);
            }
            ColumnsNames.Add(ColumnNameTASAcceleration);
            ColumnsNames.Add(ColumnNameIASAcceleration);
            GetValues();
            _dataGrid.ItemsSource = this.Values;
            _dataGrid.AutoGenerateColumns = false;
            var timeColumn = new DataGridTextColumn
            {
                Header = TimeTranslate,
                Binding = new Binding("Key") { Converter = new IntervalConverter() }
            };
            _dataGrid.Columns.Add(timeColumn);
            _graphwrapPanel.Children.Clear();
            _graphwrapPanel.Children.Clear();
            var fullPars = new List<string>(ColumnsNames.ToArray());
            fullPars.Insert(0, "Время, мсек");
            var label = new Label();
            label.Content = "Абсцисса: ";
            label.VerticalAlignment = VerticalAlignment.Top;
            label.Margin = new Thickness(2);
            _graphwrapPanel.Children.Add(label);
            label = new Label();

            var c1 = new ComboBox();
            c1.ItemsSource = fullPars;
            c1.Tag = 0;
            c1.SelectionChanged += graphBox_Click;
            c1.SelectedIndex = 0;
            c1.VerticalAlignment = VerticalAlignment.Top;
            c1.Margin = new Thickness(5);
            CurGraphAbs = c1.SelectedIndex;
            _graphwrapPanel.Children.Add(c1);

            label.Content = "Ордината: ";
            label.VerticalAlignment = VerticalAlignment.Top;
            label.Margin = new Thickness(2);
            _graphwrapPanel.Children.Add(label);

            c1 = new ComboBox();
            c1.ItemsSource = fullPars;
            c1.Tag = 1;
            c1.SelectionChanged += graphBox_Click;
            c1.SelectedIndex = 1;
            c1.VerticalAlignment = VerticalAlignment.Top;
            c1.Margin = new Thickness(5);
            CurGraphOrd = c1.SelectedIndex;
            _graphwrapPanel.Children.Add(c1);

            for (int i = 0; i < ColumnsNames.Count; i++)
            {
                var dataColumn = new DataGridTextColumn
                {
                    Header = ColumnsNames[i],
                    Binding = new Binding("Value[" + i + "]") { StringFormat = "N4" }
                };

                var textBlock = new TextBlock
                {
                    Text = ColumnsNames[i],
                    TextWrapping = TextWrapping.NoWrap
                };
                var box = new CheckBox
                {
                    Content = textBlock,
                    Tag = dataColumn,
                    Width = 160,
                    Margin = new Thickness(5, 3, 0, 3),
                    IsChecked = false
                };
                box.Click += ColumnCheckBox_Click;
                _columnsWrapPanel.Children.Add(box);
                _dataGrid.Columns.Add(dataColumn);
                dataColumn.Visibility = Visibility.Collapsed;
            }
        }

        public FdrDataAdapter(DataGrid dataGrid, WrapPanel columnsWrapPanel,
            WrapPanel graphPanel, List<string> filterList, Dictionary<string, string> translateDictionary, int interpTime)
        {
            if (dataGrid == null) throw new ArgumentNullException(null, "DataGrid is null");
            _interpInterval = interpTime;
            _translateDictionary = translateDictionary;
            _filterList = filterList;
            _dataGrid = dataGrid;
            _dataGrid.Columns.Clear();
            _graphwrapPanel = graphPanel;
            _columnsWrapPanel = columnsWrapPanel;
            _graphwrapPanel.Children.Clear();
            _columnsWrapPanel.Children.Clear();
        }

        public class IntervalConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                var interval = (TimeSpan)value;
                var result = interval.TotalSeconds.ToString();
                return result;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }

        public void FootToMeter(bool isDirect)
        {
            if (Values == null) return;
            int altitudeNum = ColumnsNames.IndexOf(GetBaseorTranslate("altitude_hour"));
            if (isDirect)
            {
                foreach (var t in Values)
                {
                    t.Value[altitudeNum] *= 0.3048;
                }
            }
            else
            {
                foreach (var t in Values)
                {
                    t.Value[altitudeNum] /= 0.3048;
                }
            }
            _dataGrid.Items.Refresh();
        }

        void ColumnCheckBox_Click(object sender, RoutedEventArgs e)
        {
            var box = sender as CheckBox;
            if (box.IsChecked.Value)
            {
                (box.Tag as DataGridColumn).Visibility = Visibility.Visible;
            }
            else
            {
                (box.Tag as DataGridColumn).Visibility = Visibility.Collapsed;
            }
        }

        void graphBox_Click(object sender, RoutedEventArgs e)
        {
            var combobox = sender as ComboBox;
            if ((int)combobox.Tag == 0)
            {
                CurGraphAbs = combobox.SelectedIndex;
            }
            if ((int)combobox.Tag == 1)
            {
                CurGraphOrd = combobox.SelectedIndex;
            }
        }

        public class MsecConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                var msec = (double)value;
                return TimeSpan.FromMilliseconds(msec);
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return null;
            }
        }
  
    }
}