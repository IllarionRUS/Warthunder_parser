using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using ZedGraph;

namespace WarThunderParser.Core
{
    public partial class DataProcessingHelper
    {
        public FdrManager RecordersManager { get; private set; }
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
        public CollectSettings CollectSettings
        {
            get { return m_CollectSettings; }
            set
            {
                m_CollectSettings = value;
                Redraw();
            }
        }
        
        private string m_Abs;
        private List<Graph> m_Graphs;

        private GraphSettings m_GraphSettings;
        private CollectSettings m_CollectSettings;

        public DataProcessingHelper(FdrManager mngr)
        {
            RecordersManager = mngr;

            m_Graphs = new List<Graph>();

            RecordersManager.OnDataCollected += onDataCollected;
            RecordersManager.OnStartDataCollecting += onStartDataCollecting;
            RecordersManager.OnRecorderFailure += onRecorderFailure;
            RecordersManager.OnTotalFailure += onTotalFailure;
        }

        public void BuildGraph(string x, string y)
        {
            if (RecordersManager.State != ManagerState.DataCollected)
                return;

            if (string.IsNullOrWhiteSpace(x))
            {
                var points = new PointPairList(new double[m_DataSize], m_Data[y].ToArray());
                Graph graph = new Graph(points, y + "(" + x + ")", x, y);
                m_Graphs.Add(graph);
            }
            else if (m_Data.ContainsKey(x) && m_Data.ContainsKey(y))
            {
                var points = new PointPairList(m_Data[x].ToArray(), m_Data[y].ToArray());
                Graph graph = new Graph(points, y + "(" + x + ")", x, y);
                m_Graphs.Add(graph);              
            }
        }

        //x
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

        //y
        public void AddOrdinate(string ordinate)
        {
            BuildGraph(m_Abs, ordinate);
            if (m_Abs != null)
                Redraw();
        }

        public void RemoveOrdinate(string ordinate)
        {
            m_Graphs.RemoveAll(graph => graph.YAxis.Equals(ordinate));
            Redraw();
        }
        
        private void Clear()
        {
            m_DataSize = 0;
            m_Data.Clear();
            m_Units.Clear();
            m_Graphs.Clear();

            if (GraphControl != null)
            {
                GraphControl.GraphPane.CurveList.Clear();
                GraphPane pane = GraphControl.GraphPane;
                if (pane != null)
                {
                    pane.Title.Text = "";
                    if (pane.XAxis != null)
                        pane.XAxis.Title.Text = "";
                    if (pane.YAxis != null)
                        pane.YAxis.Title.Text = "";
                }
            }
            
            
        }

        #region recorders manager events
        private void onStartDataCollecting(FdrManagerEventArgs args)
        {
            Clear();
        }

        private void onDataCollected(FdrManagerEventArgs args)
        {
            Clear();
            CollectData();
        }       

        private void onRecorderFailure(FdrRecorderFailureEventArgs args)
        {

        }

        private void onTotalFailure(FdrManagerEventArgs args)
        {
            Clear();
        }
        #endregion

    }
}
