using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using WarThunderParser.Controls;
using WarThunderParser.Utils;
using ZedGraph;

namespace WarThunderParser.Core
{
    public class CompareSource
    {
        internal string Title { get; set; }
        private Dictionary<string, List<double>> m_Values;
        private Dictionary<string, string> m_Units;

        public CompareSource(string title, Dictionary<string, List<double>> values, Dictionary<string, string> units)
        {
            Title = title;
            m_Values = values;
            m_Units = units;
        }

        internal Dictionary<string, List<double>> getValues()
        {
            return m_Values;
        }

        internal Dictionary<string, string> getUnits()
        {
            return m_Units;
        }
    }

    public class CompareHelper
    {
        public static readonly string DEFAULT_NAME = Properties.Resources.default_plot_title;
        private const int MAX_ORDINATES = 4; //because 4 default line styles     

        private List<KeyValuePair<string, List<Graph>>> m_Graphs; //<source.Title, graps for source>
        private GraphSettings m_GraphSettings;
        private ObservableCollection<CompareSource> m_Sources;

        private string m_Abs;
        private Metrica m_Metrica;
        private Dictionary<string, int> m_Dimensions;
        private ObservableCollection<CheckedListItem<string>> m_AvailableOrdinates,  m_AvailableSources;

        private ImperialToMetricalConverter m_Imperical2MetricalConverter;
        private MetricalToImperialConverter m_Metrical2ImperialConverter;
        
        private ComboBox m_CbAbscissa;

        public ZedGraphControl GraphControl { get; set; }
        public GraphSettings GraphSettings
        {
            get { return m_GraphSettings; }
            set
            {
                m_GraphSettings = value;
                Redraw();
            }
        }
        public ListBox LbSources
        {
            set
            {
                value.ItemsSource = m_AvailableSources;
            }
        }
        public ListBox LbOdinates
        {
            set
            {
                value.ItemsSource = m_AvailableOrdinates;
            }
        }
        public ComboBox CbAbscissa {
            get
            {
                return m_CbAbscissa;
            }
            set
            {
                m_CbAbscissa = value;
                m_CbAbscissa.SelectionChanged += OnAbscissaChanged;
            }
        }

        public CompareHelper()
        {
            m_Metrica = Metrica.Metric;
            m_Graphs = new List<KeyValuePair<string, List<Graph>>>();
            m_Dimensions = new Dictionary<string, int>();
            m_Sources = new ObservableCollection<CompareSource>();
            m_Sources.CollectionChanged += OnSourcesChanged;

            m_AvailableOrdinates = new ObservableCollection<CheckedListItem<string>>();
            m_AvailableSources = new ObservableCollection<CheckedListItem<string>>();

            m_Imperical2MetricalConverter = new ImperialToMetricalConverter();
            m_Metrical2ImperialConverter = new MetricalToImperialConverter();
        }        

        public void AddSource(CompareSource source)
        {
            m_Sources.Add(source);
        }

        public void RemoveSource(string source)
        {
            m_Sources.Remove(m_Sources.Where(s => s.Title.Equals(source)).First());
        }

        public void SetMetrica(Metrica metrica)
        {            
            if (m_Metrica != metrica)
            {
                UnitConverter converter = null;
                switch (metrica)
                {
                    case Metrica.Metric:
                        converter = m_Imperical2MetricalConverter;
                        break;
                    case Metrica.Imperial:
                        converter = m_Metrical2ImperialConverter;
                        break;
                    default:
                        throw new InvalidOperationException("convert error");
                }

                foreach (var source in m_Sources)
                {
                    var data = source.getValues();
                    var units = source.getUnits();
                    var toConvert = data.Where(v => Array.IndexOf(converter.getConvertableUnits(), units[v.Key]) >= 0).ToArray();
                    foreach (var keyValue in toConvert)
                    {
                        string newUnit = converter.Convert(data[keyValue.Key], units[keyValue.Key]);

                        var toUpdateX = m_Graphs.SelectMany(g => g.Value).Where(graph =>
                            graph.GraphName.Equals(source.Title + "_" + graph.YAxis + "(" + (graph.XAxis ?? "") + ")")
                            && graph.XAxis != null 
                            && string.Equals(graph.XAxis, keyValue.Key));

                        var toUpdateY = m_Graphs.SelectMany(g => g.Value).Where(graph =>
                            graph.GraphName.Equals(source.Title + "_" + graph.YAxis + "(" + (graph.XAxis ?? "") + ")")
                            && graph.YAxis != null 
                            && string.Equals(graph.YAxis, keyValue.Key));

                        foreach (Graph graph in toUpdateX)
                        {
                            graph.X_Unit = newUnit;
                            for (int i = 0; i < keyValue.Value.Count(); i++)
                                graph.PointPairs[i].X = keyValue.Value[i];
                        }
                        foreach (Graph graph in toUpdateY)
                        {
                            graph.Y_Unit = newUnit;
                            for (int i = 0; i < keyValue.Value.Count(); i++)
                                graph.PointPairs[i].Y = keyValue.Value[i];
                        }

                        units[keyValue.Key] = newUnit;
                    }
                }
                m_Metrica = metrica;
                Redraw();
            }
        }

        #region UI
        private void BuildGraph(string x, string y)
        {
            var validSources = m_Sources.Where(s => (string.IsNullOrWhiteSpace(x) || s.getValues().ContainsKey(x)) && s.getValues().ContainsKey(y) 
                && m_AvailableSources.Where(a => a.IsChecked).Select(a => a.Item).Contains(s.Title));
                                       
            foreach (var source in validSources)
            {
                var title = source.Title;
                var x_values = string.IsNullOrWhiteSpace(x) ? null : source.getValues()[x].ToArray();
                var y_values = source.getValues()[y].ToArray();
                // x.Length == y.Length implies from data catching procedure
                int dataSize = x_values.Length;
                //var color = GraphSettings.CurveLines[m_Sources.IndexOf(source)].LineColor;

                Graph graph = null;
                if (string.IsNullOrWhiteSpace(x))
                {
                    var points = new PointPairList(new double[dataSize], y_values);
                    graph = new Graph(points, title + "_" + y + "(" + (x ?? "") + ")", x, y, null, source.getUnits()[y]);
                    
                }
                else
                {
                    var points = new PointPairList(x_values, y_values);
                    graph = new Graph(points, title + "_" + y + "(" + x + ")", x, y, source.getUnits()[x], source.getUnits()[y]);
                }

                //graph.Color = color;
                //if (ordinatesCount > 1)
                //    graph.DashStyle = (System.Drawing.Drawing2D.DashStyle)ordinatesCount;

                var graphsForCurrentSource = m_Graphs.Where(g => g.Key.Equals(source.Title));
                if (graphsForCurrentSource == null || graphsForCurrentSource.Count() == 0)
                {
                    List<Graph> newGraphList = new List<Graph>();
                    newGraphList.Add(graph);
                    KeyValuePair<string, List<Graph>> newKeyValue = new KeyValuePair<string, List<Graph>>(source.Title, newGraphList);
                    m_Graphs.Add(newKeyValue);
                }
                else
                {
                    var targetList = graphsForCurrentSource.First().Value;
                    targetList.Add(graph);
                }
            }
        }
        
        private void SetAbscissa(string abscissa)
        {
            m_Abs = abscissa;
            var currentOrdinates = m_Graphs.Count > 0 ? m_Graphs.First().Value.Select(t => t.YAxis).ToArray() : null;
            m_Graphs.Clear();
            if (currentOrdinates != null)
                foreach (var ordinate in currentOrdinates)
                    BuildGraph(m_Abs, ordinate);
            if (m_Graphs.Count > 0)
                Redraw();
        }

        private void AddOrdinate(string ordinate)
        {
            var ordinatesCount = m_Graphs.Count > 0
                ? m_Graphs.First().Value.Count
                : 0;
            if (ordinatesCount == MAX_ORDINATES)
            {
                m_AvailableOrdinates.Where(o => o.Item.Equals(ordinate)).First().IsChecked = false;
                MessageBox.Show("only " + MAX_ORDINATES + " ordinates allowed");
                return;
            }

            BuildGraph(m_Abs, ordinate);
            if (m_Abs != null)
                Redraw();
        }

        private void RemoveOrdinate(string ordinate)
        {
            foreach (var keyValue in m_Graphs)
                keyValue.Value.RemoveAll(g => g.YAxis.Equals(ordinate));
            Redraw();
        }
                
        private void Redraw()
        {
            if (GraphControl == null)
                return;
            GraphControl.GraphPane.CurveList.Clear();
            GraphPane pane = GraphControl.GraphPane;
            pane.YAxisList.Clear();

            //var graphs = m_Graphs.SelectMany(g => g.Value).ToList();
            if (m_Graphs.SelectMany(g => g.Value).Count() == 0)
            {
                pane.XAxis.Title.Text = "";
                return;
            }
            
            var yAxises = new Dictionary<string, int>();

            int color = 0;
            foreach (var keyValue in m_Graphs)
            {
                var lineColor = GraphSettings.CurveLines[color++].LineColor;
                for (int i = 0; i < keyValue.Value.Count(); i++) 
                {
                    Graph graph = keyValue.Value[i];
                    var line = graph.GetLineItem();
                    if (GraphSettings.Smooth)
                    {
                        var curList = (IPointList)line.Points.Clone();
                        var average = new MovingAverage(GraphSettings.SmoothPeriod);

                        switch (GraphSettings.SmoothType)
                        {
                            case SmoothModel.Average:
                                for (int j = 0; j < curList.Count; j++)
                                {
                                    average.Push(curList[j].Y);
                                    curList[j].Y = average.Average;
                                }
                                break;
                            case SmoothModel.Median:
                                for (int j = 0; j < curList.Count; j++)
                                {
                                    average.Push(curList[i].Y);
                                    curList[j].Y = average.Median;
                                }
                                break;
                            default:
                                break;
                        }

                        line.Points = curList;
                    }
                    var yAxisLabel = graph.YAxis + (string.IsNullOrEmpty(graph.Y_Unit) ? "" : (", " + graph.Y_Unit));
                    if (!yAxises.ContainsKey(yAxisLabel))
                        yAxises.Add(yAxisLabel, pane.AddYAxis(yAxisLabel));
                    line.YAxisIndex = yAxises[yAxisLabel];
                    line.Line.Style = (System.Drawing.Drawing2D.DashStyle)i;
                    line.Line.Color = lineColor;
                    pane.CurveList.Add(line);
                }
            }

            pane.XAxis.Title.Text = m_Graphs.First().Value.First().XAxis
                + (string.IsNullOrEmpty(m_Graphs.First().Value.First().X_Unit) ? "" : (", " + m_Graphs.First().Value.First().X_Unit));
            pane.Title.Text = "";

            GraphControl.AxisChange();

            LegendRedraw(GraphSettings.LegendVisible);
            AxisTitleRedraw(GraphSettings.AxisLabelVisible);

            #region AxisGrids
            pane.XAxis.MajorGrid.IsVisible = GraphSettings.MajorGrid;
            pane.YAxis.MajorGrid.IsVisible = GraphSettings.MajorGrid;
            pane.XAxis.MinorGrid.IsVisible = GraphSettings.MinorGrid;
            pane.YAxis.MinorGrid.IsVisible = GraphSettings.MinorGrid;
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

            GraphControl.Invalidate();
            GraphControl.Refresh();
        }

        private void AxisTitleRedraw(bool showAxis)
        {
            var myPane = GraphControl.GraphPane;
            myPane.XAxis.Title.IsVisible = showAxis;
            myPane.XAxis.Title.FontSpec.Size = GraphSettings.AxisFontSize;
            myPane.XAxis.Scale.FontSpec.Size = GraphSettings.AxisFontSize;

            int i = 0;
            foreach (var axis in myPane.YAxisList)
            {
                axis.Title.IsVisible = showAxis;
                axis.Title.FontSpec.Size = GraphSettings.AxisFontSize;
                axis.Scale.FontSpec.Size = GraphSettings.AxisFontSize;
                if (GraphSettings.AxisColorAsCurve)
                    axis.Color = GraphSettings.CurveLines[i++].LineColor;
            }
            GraphControl.Invalidate();
        }

        private void LegendRedraw(bool showLegend)
        {
            var myPane = GraphControl.GraphPane;
            foreach (var curve in myPane.CurveList)
            {
                if (curve.Label.FontSpec == null)
                {
                    FontSpec font = new FontSpec("Arial", GraphSettings.AxisFontSize, System.Drawing.Color.Black, false, false, false);
                    curve.Label.FontSpec = font;
                }
                else
                    curve.Label.FontSpec.Angle = GraphSettings.AxisFontSize;
                curve.Label.IsVisible = showLegend;
            }
            GraphControl.Invalidate();
        }

        #endregion

        #region events
        private void OnSourcesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            ObservableCollection<CompareSource> obsSender = sender as ObservableCollection<CompareSource>;

            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (var newItem in e.NewItems)
                {
                    var newSource = m_Sources[m_Sources.IndexOf((CompareSource)newItem)];
                    
                    int titleNum = 1;
                    
                    if (string.IsNullOrEmpty(newSource.Title.Trim()))
                        newSource.Title = DEFAULT_NAME;

                    string title = newSource.Title;
                    while (m_Sources.Where(s => s.Title.Equals(newSource.Title)).Count() > 1)
                        newSource.Title = title + titleNum++;

                    newSource.getValues().Keys.ToList().ForEach(
                        k =>
                        {
                            if (m_Dimensions.ContainsKey(k))
                                m_Dimensions[k]++;
                            else
                            {
                                m_Dimensions.Add(k, 1);
                                var newOrdinateCheckbox = new CheckedListItem<string>(k);
                                newOrdinateCheckbox.PropertyChanged += OnOrdinateCheckChanged;
                                m_AvailableOrdinates.Add(newOrdinateCheckbox);
                                CbAbscissa.Items.Add(k);
                            }
                        });

                    switch (m_Metrica)
                    {
                        case Metrica.Metric:
                            foreach (var keyValue in newSource.getValues())
                            {
                                string newUnit = m_Imperical2MetricalConverter.Convert(keyValue.Value, newSource.getUnits()[keyValue.Key]);
                                newSource.getUnits()[keyValue.Key] = newUnit;
                            }
                            break;
                        case Metrica.Imperial:
                            foreach (var keyValue in newSource.getValues())
                            {
                                string newUnit = m_Metrical2ImperialConverter.Convert(keyValue.Value, newSource.getUnits()[keyValue.Key]);
                                newSource.getUnits()[keyValue.Key] = newUnit;
                            }
                            break;
                        default:
                            throw new InvalidOperationException("invalid metrica");
                    }
                    var newSourceCheckbox = new CheckedListItem<string>(newSource.Title);
                    newSourceCheckbox.PropertyChanged += OnSourceCheckChanged;
                    m_AvailableSources.Add(newSourceCheckbox);
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (var removedItem in e.OldItems)
                {
                    var removedSource = (CompareSource)removedItem;

                    foreach (string dimension in removedSource.getValues().Keys.ToList())
                    {
                        int count = m_Dimensions[dimension];
                        if (count == 1)
                        {
                            m_Dimensions.Remove(dimension);
                            CbAbscissa.Items.Remove(dimension);

                            var ordinatesToRemove = m_AvailableOrdinates.Where(o => o.Item.Equals(dimension)).ToList();
                            foreach (var item in ordinatesToRemove)
                                m_AvailableOrdinates.Remove(item);
                        }
                        else
                            m_Dimensions[dimension]--;
                    }

                    m_Graphs.RemoveAll(g => g.Key.Equals(removedSource.Title));

                    var toRemove = m_AvailableSources.Where(s => s.Item.Equals(removedSource.Title)).ToList();
                    foreach (var item in toRemove)
                        m_AvailableSources.Remove(item);
                }
                Redraw();
            }
        }

        private void OnOrdinateCheckChanged(object sender, PropertyChangedEventArgs args)
        {
            CheckedListItem<string> item = sender as CheckedListItem<string>;
            if (item.IsChecked)
                AddOrdinate(item.Item);
            else
                RemoveOrdinate(item.Item);
        }

        private void OnSourceCheckChanged(object sender, PropertyChangedEventArgs args)
        {
            CheckedListItem<string> item = sender as CheckedListItem<string>;
            if (item.IsChecked)
            {
                var checkedOrds = m_AvailableOrdinates.Where(o => o.IsChecked).ToList();
                var ordinatesCount = checkedOrds.Count();
                if (ordinatesCount == 0)
                    return;

                m_AvailableOrdinates.Where(o => o.IsChecked).ToList().ForEach(c =>
                    {
                        var source = m_Sources.Where(s => s.Title.Equals(item.Item)).First();
                        //var color = GraphSettings.CurveLines[m_Sources.IndexOf(source)].LineColor;
                        var title = item.Item;
                        var x_values = string.IsNullOrEmpty(m_Abs) ? null : source.getValues()[m_Abs].ToArray();
                        var y_values = source.getValues()[c.Item].ToArray();
                        // x.Length == y.Length implies from data catching procedure
                        int dataSize = x_values.Length;

                        var x = m_Abs;
                        var y = c.Item;
                        Graph graph = null;
                        if (string.IsNullOrWhiteSpace(x))
                        {
                            var points = new PointPairList(new double[dataSize], y_values);
                            graph = new Graph(points, title + "_" + y + "(" + (x ?? "") + ")", x, y, null, source.getUnits()[y]);

                        }
                        else
                        {
                            var points = new PointPairList(x_values, y_values);
                            graph = new Graph(points, title + "_" + y + "(" + x + ")", x, y, source.getUnits()[x], source.getUnits()[y]);
                        }

                        //graph.Color = color;
                        //graph.DashStyle = (System.Drawing.Drawing2D.DashStyle)(ordinatesCount - 1);

                        var graphsForCurrentSource = m_Graphs.Where(g => g.Key.Equals(source.Title));
                        if (graphsForCurrentSource == null || graphsForCurrentSource.Count() == 0)
                        {
                            List<Graph> newGraphList = new List<Graph>();
                            newGraphList.Add(graph);
                            var newKeyValue = new KeyValuePair<string, List<Graph>>(title, newGraphList);
                            m_Graphs.Add(newKeyValue);
                        }
                        else
                        {
                            var targetList = graphsForCurrentSource.First().Value;
                            targetList.Add(graph);
                        }
                    }
                );
            }
            else
            {
                m_Graphs.RemoveAll(g => g.Key.Equals(item.Item));           
            }
            Redraw();
        }

        private void OnAbscissaChanged(object sender, SelectionChangedEventArgs e)
        {
            SetAbscissa(CbAbscissa.SelectedItem == null ? null : CbAbscissa.SelectedItem.ToString());
        }
        
        #endregion

    }
}
