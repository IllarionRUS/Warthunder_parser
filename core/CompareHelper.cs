using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using WarThunderParser.Controls;
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
        private const int MAX_ORDINATES = 5; //because 5 default line styles
        private const string DEFAULT_NAME = "plot";        

        private List<Graph> m_Graphs;
        private GraphSettings m_GraphSettings;
        private ObservableCollection<CompareSource> m_Sources;

        private Metrica m_Metrica;
        private Dictionary<string, int> m_Dimensions;
        private ObservableCollection<CheckedListItem<string>> m_AvailableOrdinates,  m_AvailableSources;

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
                value.ItemsSource = m_ValuesNames;
            }
        }
        public ComboBox CbAbscissa { get; set; }

        public CompareHelper()
        {
            m_Metrica = Metrica.Metric;
            m_Graphs = new List<Graph>();
            m_Sources = new ObservableCollection<CompareSource>();
            m_Sources.CollectionChanged += OnSourcesChanged;

            m_AvailableOrdinates = new ObservableCollection<CheckedListItem<string>>();
            m_AvailableSources = new ObservableCollection<CheckedListItem<string>>();
        }

        private void OnSourcesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            //todo source.name must be unique
            //todo synchronize axis and units

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
                            // todo
                            break;
                        case Metrica.Imperial:
                            // todo
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
                    
                    foreach(string dimension in removedSource.getValues().Keys.ToList())
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

                    m_Graphs.RemoveAll(g => g.GraphName.Equals(removedSource.Title + "_" + g.YAxis + "(" + g.XAxis + ")"));

                    var toRemove = m_AvailableSources.Where(s => s.Item.Equals(removedSource.Title));
                    foreach (var item in toRemove)
                        m_AvailableSources.Remove(item);                   
                }
                Redraw();
            }
                            
        }

        public void SetMetrica(Metrica metrica)
        {
            if (m_Metrica != metrica)
            {
                //todo
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
                    graph = new Graph(points, title + "_" + y + "(" + x + ")", x, y, null, source.getUnits()[y]);
                    
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

        }

        public void AddOrdinate(string ordinate)
        {

        }

        public void RemoveOrdinate(string ordinate)
        {

        }
                
        private void Redraw()
        {

        }

        private void ClearPlot()
        {
            m_Graphs.Clear();
        }
        #endregion

        #region collections events
        #endregion

    }
}
