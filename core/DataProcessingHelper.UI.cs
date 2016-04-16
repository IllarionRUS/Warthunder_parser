using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Data;
using ZedGraph;

namespace WarThunderParser.Core
{

    public partial class DataProcessingHelper
    {
        public void Redraw()
        {
            if (GraphControl == null)
                return;
            GraphControl.GraphPane.CurveList.Clear();
            GraphPane pane = GraphControl.GraphPane;
            pane.YAxisList.Clear();
            if (Graphs.Count == 0)
            {
                pane.XAxis.Title.Text = "";
                return;
            }

            var yAxises = new Dictionary<string, int>();
            for (int k = 0; k < Graphs.Count; k++)
            {
                Graph graph = Graphs[k];
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
            
            pane.XAxis.Title.Text = Graphs.Last().XAxis 
                + (string.IsNullOrEmpty(Graphs.Last().X_Unit) ? "" : (", " + Graphs.Last().X_Unit));
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

        private void UpdateDataGrid()
        {
            DataGrid.Columns.Clear();
            DataGrid.AutoGenerateColumns = false;
            if (m_Data == null || m_DataSize == 0)
                return;

            ObservableCollection<double[]> tableValues = new ObservableCollection<double[]>();
            for (int i = 0; i < m_DataSize; i++)
            {
                var snapshot = new double[m_Data.Count()];
                int j = 0;
                foreach (var keyValue in m_Data)
                {
                    snapshot[j++] = keyValue.Value[i];
                }
                tableValues.Add(snapshot);
            }
            DataGrid.ItemsSource = tableValues;
            var keys = m_Data.Keys.ToList();
            for (int i = 0; i < m_Data.Count; i++)
            {
                var dataColumn = new DataGridTextColumn
                {
                    Header = keys[i]
                        + (string.IsNullOrEmpty(m_Units[keys[i]])
                            ? ""
                            : ", " + m_Units[keys[i]]),
                    Binding = new Binding("[" + i +"]") { StringFormat = "N4" }                    
                };
               
                DataGrid.Columns.Add(dataColumn);
            }
        }

    }
}
