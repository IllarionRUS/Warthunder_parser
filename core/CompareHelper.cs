using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using WarThunderParser.Controls;
using WarThunderParser.Utils;
using ZedGraph;

namespace WarThunderParser.Core
{
    public struct CompareSource
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
        //todo add binds
        private const int MAX_ORDINATES = 5; //because 5 default line styles
        private readonly string DEFAULT_NAME = Properties.Resources.default_plot_title;        

        private List<Graph> m_Graphs;
        private GraphSettings m_GraphSettings;
        private ObservableCollection<CompareSource> m_Sources;

        private string m_Abs;
        private Metrica m_Metrica;
        private Dictionary<string, int> m_Dimensions;
        private ObservableCollection<CheckedListItem<string>> m_AvailableOrdinates,  m_AvailableSources;

        private ImperialToMetricalConverter m_Imperical2MetricalConverter;
        private MetricalToImperialConverter m_Metrical2ImperialConverter;

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
                value.ItemsSource = m_Dimensions;
            }
        }
        public ComboBox CbAbscissa { get; set; }

        public CompareHelper()
        {
            m_Metrica = Metrica.Metric;
            m_Graphs = new List<Graph>();
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

        public void RemoveSource(CompareSource source)
        {
            m_Sources.Remove(source);
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

                        var toUpdateX = m_Graphs.Where(graph =>
                            graph.GraphName.Equals(source.Title + "_" + graph.YAxis + "(" + graph.XAxis ?? "" + ")")
                            && graph.XAxis != null 
                            && string.Equals(graph.XAxis, keyValue.Key));

                        var toUpdateY = m_Graphs.Where(graph =>
                            graph.GraphName.Equals(source.Title + "_" + graph.YAxis + "(" + graph.XAxis ?? "" + ")")
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
            var validSources = m_Sources.Where(s => (string.IsNullOrWhiteSpace(x) || s.getValues().ContainsKey(x)) && s.getValues().ContainsKey(y));
            var ordinatesCount = m_Graphs.Select(g => g.YAxis).Distinct().Count();
            if (ordinatesCount > MAX_ORDINATES)
                throw new InvalidOperationException("only 5 ordinates allowed");

            int i = 0;
            foreach (var source in validSources)
            {
                var title = source.Title;
                var x_values = source.getValues()[x].ToArray();
                var y_values = source.getValues()[y].ToArray();
                // x.Length == y.Length implies from data catching procedure
                int dataSize = x_values.Length;
                var color = GraphSettings.CurveLines[i ++].LineColor;

                Graph graph = null;
                if (string.IsNullOrWhiteSpace(x))
                {
                    var points = new PointPairList(new double[dataSize], y_values);
                    graph = new Graph(points, title + "_" + y + "(" + x ?? "" + ")", x, y, null, source.getUnits()[y]);
                    
                }
                else
                {
                    var points = new PointPairList(x_values, y_values);
                    graph = new Graph(points, title + "_" + y + "(" + x + ")", x, y, source.getUnits()[x], source.getUnits()[y]);
                }

                graph.Color = color;
                if (ordinatesCount > 1)
                    graph.DashStyle = (System.Drawing.Drawing2D.DashStyle)ordinatesCount;
                m_Graphs.Add(graph);
            }
            Redraw();
        }
        
        public void SetAbscissa(string abscissa)
        {
            m_Abs = abscissa;
            var currentOrdinates = m_Graphs.Select(t => t.YAxis).ToArray();
            m_Graphs.Clear();
            foreach (var ordinate in currentOrdinates)
                BuildGraph(m_Abs, ordinate);
            if (m_Graphs.Count > 0)
                Redraw();
        }

        public void AddOrdinate(string ordinate)
        {
            BuildGraph(m_Abs, ordinate);
            if (m_Abs != null)
                Redraw();
        }

        public void RemoveOrdinate(string ordinate)
        {
            m_Graphs.RemoveAll(g => g.YAxis.Equals(ordinate));
            Redraw();
        }
                
        private void Redraw()
        {
            if (GraphControl == null)
                return;
            GraphControl.GraphPane.CurveList.Clear();
            GraphPane pane = GraphControl.GraphPane;
            pane.YAxisList.Clear();
            if (m_Graphs.Count == 0)
            {
                pane.XAxis.Title.Text = "";
                return;
            }

            var yAxises = new Dictionary<string, int>();
            for (int k = 0; k < m_Graphs.Count; k++)
            {
                Graph graph = m_Graphs[k];
                var line = graph.GetLineItem();
                if (GraphSettings.Smooth)
                {
                    var curList = (IPointList)line.Points.Clone();
                    var average = new MovingAverage(GraphSettings.SmoothPeriod);

                    switch (GraphSettings.SmoothType)
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
                            break;
                    }

                    line.Points = curList;
                }
                var yAxisLabel = graph.YAxis + (string.IsNullOrEmpty(graph.Y_Unit) ? "" : (", " + graph.Y_Unit));
                if (!yAxises.ContainsKey(yAxisLabel))
                    yAxises.Add(yAxisLabel, pane.AddYAxis(yAxisLabel));
                line.YAxisIndex = yAxises[yAxisLabel];
                pane.CurveList.Add(line);
            }

            pane.XAxis.Title.Text = m_Graphs.Last().XAxis
                + (string.IsNullOrEmpty(m_Graphs.Last().X_Unit) ? "" : (", " + m_Graphs.Last().X_Unit));
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

        #region collections events
        private void OnSourcesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            ObservableCollection<CompareSource> obsSender = sender as ObservableCollection<CompareSource>;

            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (var newItem in e.NewItems)
                {
                    var newSource = (CompareSource)newItem;

                    int titleNum = 1;
                    if (string.IsNullOrEmpty(newSource.Title.Trim()))
                        newSource.Title = DEFAULT_NAME;
                    while (m_Sources.Where(s => s.Title.Equals(newSource.Title)).Count() > 0)
                        newSource.Title = newSource.Title + titleNum++;

                    newSource.getValues().Keys.ToList().ForEach(
                        k =>
                        {
                            if (m_Dimensions.ContainsKey(k))
                                m_Dimensions[k]++;
                            else
                            {
                                m_Dimensions.Add(k, 1);
                                m_AvailableOrdinates.Add(new CheckedListItem<string>(k));
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
                    m_AvailableSources.Add(new CheckedListItem<string>(newSource.Title));
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

                            var ordinatesToRemove = m_AvailableOrdinates.Where(o => o.Item.Equals(dimension));
                            foreach (var item in ordinatesToRemove)
                                m_AvailableOrdinates.Remove(item);
                        }
                        else
                            m_Dimensions[dimension]--;
                    }

                    m_Graphs.RemoveAll(g => g.GraphName.Equals(removedSource.Title + "_" + g.YAxis + "(" + g.XAxis ?? "" + ")"));

                    var toRemove = m_AvailableSources.Where(s => s.Item.Equals(removedSource.Title));
                    foreach (var item in toRemove)
                        m_AvailableSources.Remove(item);
                }
                Redraw();
            }
        }
        #endregion

    }
}
