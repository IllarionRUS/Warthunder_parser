using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using ZedGraph;

namespace WarThunderParser.core
{
    public partial class DataProcessingHelper
    {
        public ZedGraphControl GraphControl { get; set; }
        public GraphSettings GraphSettings { get; set; }
        public FdrManager RecordersManager { get; private set; }
        public CollectSettings CollectSettings { get; set; }
        
        private string m_Abs;
        private List<Graph> m_Graphs;

        public DataProcessingHelper(FdrManager mngr)
        {
            RecordersManager = mngr;

            m_Graphs = new List<Graph>();

            RecordersManager.OnDataCollected += onDataCollected;
            RecordersManager.OnStartDataCollecting += onStartDataCollecting;
            RecordersManager.OnRecorderFailure += onRecorderFailure;
            RecordersManager.OnTotalFailure += onTotalFailure;
        }

        public void buildGraph(string x, string y)
        {
            if (RecordersManager.State != ManagerState.DataCollected)
                return;

            if (m_Data.ContainsKey(x) && m_Data.ContainsKey(y))
            {
                var points = new PointPairList(m_Data[x].ToArray(), m_Data[y].ToArray());
                Graph graph = new Graph(points, y + "(" + x + ")", x, y);
                m_Graphs.Add(graph);              
            }
        }

        //x
        public void setAbscissa(string abscissa)
        {
            m_Abs = abscissa;
            var currentOrdinates = m_Graphs.Select(t => t.YAxis).ToArray();
            m_Graphs.Clear();
            foreach (var ordinate in currentOrdinates)
                buildGraph(m_Abs, ordinate);
            if (currentOrdinates.Length > 0)
                redraw();
        }

        //y
        public void addOrdinate(string ordinate)
        {
            buildGraph(m_Abs, ordinate);
            if (m_Abs != null)
                redraw();
        }

        public void removeOrdinate(string ordinate)
        {
            m_Graphs.RemoveAll(graph => graph.YAxis.Equals(ordinate));
            redraw();
        }

        #region ui
        private void redraw()
        {
            GraphControl.GraphPane.CurveList.Clear();
            GraphPane pane = GraphControl.GraphPane;
            pane.YAxisList.Clear();
            var yAxises = new Dictionary<string, int>();
            for (int k = 0; k < m_Graphs.Count; k++)
            {
                Graph graph = m_Graphs[k];
                LineItem line = k < GraphSettings.CurveLines.Count 
                    ? graph.GetLineItem(GraphSettings.CurveLines[k].LineColor, SymbolType.None, 2.0f) 
                    : graph.GetLineItem();
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
                            throw new InvalidEnumArgumentException();
                    }

                    line.Points = curList;
                }
                if (!yAxises.ContainsKey(graph.YAxis))
                    yAxises.Add(graph.YAxis, pane.AddYAxis(graph.YAxis));
                line.YAxisIndex = yAxises[graph.YAxis];
                pane.CurveList.Add(line);
            }
            pane.XAxis.Title.Text = m_Graphs.Last().XAxis;
            pane.Title.Text = "";

            GraphControl.AxisChange();
            //TODO
            //LegendRedraw(_isLegendVisible);
            //AxisTitleRedraw(_isAxisLabelVisible);

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

        private void clear()
        {
            m_DataSize = 0;
            m_Data.Clear();
            m_Graphs.Clear();
            GraphControl.GraphPane.CurveList.Clear();
            GraphPane pane = GraphControl.GraphPane;
            pane.Title.Text = "";
            pane.XAxis.Title.Text = "";
            pane.YAxis.Title.Text = "";
        }
        #endregion

        #region recorders manager events
        private void onStartDataCollecting(FdrManagerEventArgs args)
        {
            clear();
        }

        private void onDataCollected(FdrManagerEventArgs args)
        {
            clear();
            collectData();
        }       

        private void onRecorderFailure(FdrRecorderFailureEventArgs args)
        {

        }

        private void onTotalFailure(FdrManagerEventArgs args)
        {
            clear();
        }
        #endregion

    }
}
