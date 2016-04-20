using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using ZedGraph;

namespace WarThunderParser.Core
{
    public struct CompareSource
    {
        private string m_Title;
        private Dictionary<string, List<double>> m_Values;
        private Dictionary<string, string> m_Units;

        public CompareSource(string title, Dictionary<string, List<double>> values, Dictionary<string, string> units)
        {
            m_Title = title;
            m_Values = values;
            m_Units = units;
        }

        internal string getTitle()
        {
            return m_Title;
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
        private List<Graph> m_Graphs;
        private GraphSettings m_GraphSettings;
        private ObservableCollection<CompareSource> m_Sources;

        private string m_Abs;

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

        public CompareHelper()
        {
            m_Graphs = new List<Graph>();
            m_Sources = new ObservableCollection<CompareSource>();
            m_Sources.CollectionChanged += OnSourcesChanged;
        }

        private void OnSourcesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            //todo synchronize time, axis, and units

            foreach (var newItem in e.NewItems)
            {

            }

            foreach(var editedOrRemovedItem in e.OldItems)
            {

            }
            Redraw();
        }

        private void BuildGraph(string x, string y)
        {
            var validSources = m_Sources.Where(s => (string.IsNullOrWhiteSpace(x) || s.getValues().ContainsKey(x)) && s.getValues().ContainsKey(y));

            foreach (var source in validSources)
            {
                var x_values = source.getValues()[x].ToArray();
                var y_values = source.getValues()[y].ToArray();
                // x.Length == y.Length implies from data catching procedure
                int dataSize = x_values.Length;
                
                if (string.IsNullOrWhiteSpace(x))
                {
                    var points = new PointPairList(new double[dataSize], y_values);
                    Graph graph = new Graph(points, y + "(" + x + ")", x, y, null, source.getUnits()[y]);
                    // todo set line type
                    m_Graphs.Add(graph);
                }
                else
                {
                    var points = new PointPairList(x_values, y_values);
                    Graph graph = new Graph(points, y + "(" + x + ")", x, y, source.getUnits()[x], source.getUnits()[y]);
                    // todo set line type
                    m_Graphs.Add(graph);
                }
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

    }
}
