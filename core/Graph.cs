using System;
using System.Collections.Generic;
using Microsoft.Office.Interop.Excel;
using ZedGraph;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace WarThunderParser
{
    [Serializable]
    public class Graph:ICloneable
    {
        public PointPairList PointPairs { get; set; }
        public string CurveName { get; set; }
        public string X_Unit { get; set; }
        public string Y_Unit { get; set; }
        public DashStyle DashStyle { get; set; }
        public Color Color { get; set; }
        private string _graphName;
        public string GraphName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_graphName)) return CurveName;
                return _graphName;
            }
            set
            {
                _graphName = value;
            }
        }

        public string XAxis { get;  set; }
        public string YAxis { get;  set; }
        readonly Random _rnd = new Random();
        
        public LineItem GetLineItem(Color color, SymbolType symbolType, float lineWidth)
        {
            var result = new LineItem(GraphName, PointPairs, color, symbolType);
            result.Line.Width = lineWidth;
            if (DashStyle > 0)
            {
                result.Line.DashOn = 4f;
                result.Line.Style = DashStyle;
            }
            return result;
        }
        public LineItem GetLineItem()
        {
            return GetLineItem(2.0f);
        }
        public LineItem GetLineItem(float lineWidth)
        {
            
            var color = System.Drawing.Color.FromArgb(_rnd.Next(256), _rnd.Next(256), _rnd.Next(256));
            var result = new LineItem(ToString(), PointPairs, color, SymbolType.None);
            result.Line.Width = lineWidth;
            if (DashStyle > 0)
            {
                result.Line.DashOn = 4f;
                result.Line.Style = DashStyle;
            }
            if (Color != null)
                result.Color = Color;
                
            return result;
        }

        public override string ToString()
        {
            return !string.IsNullOrEmpty(GraphName) ? GraphName : CurveName;
        }

        public override bool Equals(object obj)
        {
            var graph2 = obj as Graph;
            if (graph2 == null)
                return false;
            for (int i = 0; i < graph2.PointPairs.Count; i++)
            {
                if ((graph2.PointPairs[i].X != PointPairs[i].X) || (graph2.PointPairs[i].Y != PointPairs[i].Y))
                    return false;
            }
            return true;
        }

        public override int GetHashCode()
        {
            return PointPairs.GetHashCode();
        }

        public Graph(PointPairList points, string curveName, string xAxis, string yAxis, string x_unit = null, string y_unit = null)
        {
            XAxis = xAxis;
            YAxis = yAxis;
            X_Unit = x_unit;
            Y_Unit = y_unit;
            PointPairs = points;
            CurveName = curveName;
        }
        public Graph(PointPairList points, string curveName, string graphName, string xAxis, string yAxis, string x_unit = null, string y_unit = null)
            : this(points, curveName, xAxis, yAxis, x_unit, y_unit)
        {
            GraphName = graphName;
        }

        public object Clone()
        {
            var result = new Graph(PointPairs, CurveName,_graphName, XAxis, YAxis, X_Unit, Y_Unit);
            result.DashStyle = DashStyle;
            result.Color = Color;
            return result;
        }
    }

    public struct CurveLine
    {
        public float Thikness;
        public SymbolType Symbol;
        public Color LineColor;

        public CurveLine(float thikness, Color lineColor, SymbolType symbolType)
        {
            Thikness = thikness;
            LineColor = lineColor;
            Symbol = symbolType;
        }
    }
    public enum SmoothModel{Average, Median}

    public class GraphSettings : ICloneable
    {
        private const int MinSmoothPeriod = 0;
        private const int MaxSmoothPeriod = 500;
        public bool MajorGrid = true;
        public bool MinorGrid = false;
        public bool Smooth = true;
        public SmoothModel SmoothType = SmoothModel.Average;
        private int _smoothPeriod = 10;
        public int SmoothPeriod
        {
            get { return _smoothPeriod; }
            set
            {
                if ((value < MinSmoothPeriod) || (value > MaxSmoothPeriod))
                    throw new ArgumentException("Выборка должна лежать в пределах от " + MinSmoothPeriod + " до " + MaxSmoothPeriod + ".");
                _smoothPeriod = value;
            }
        }
        public bool LegendVisible { get; set; }
        public bool AxisLabelVisible { get; set; }
        public int AxisFontSize { get; set; }
        public bool AxisColorAsCurve { get; set; }

        public List<CurveLine> CurveLines;
        
        public GraphSettings()
        {
            LegendVisible = true;
            AxisLabelVisible = true;
            AxisFontSize = 10;
            AxisColorAsCurve = false;

            CurveLines = new List<CurveLine>
            {
                new CurveLine(2.0f, Color.Blue, SymbolType.None),
                new CurveLine(2.0f, Color.Red, SymbolType.None),
                new CurveLine(2.0f, Color.Green, SymbolType.None),
                new CurveLine(2.0f, Color.DeepSkyBlue, SymbolType.None),
                new CurveLine(2.0f, Color.Chartreuse, SymbolType.None),
                new CurveLine(2.0f, Color.Magenta, SymbolType.None),
                new CurveLine(2.0f, Color.DarkBlue, SymbolType.None),
                new CurveLine(2.0f, Color.DeepPink, SymbolType.None),
            };
        }

        public object Clone()
        {
            var resultSettings = new GraphSettings
            {
                CurveLines = new List<CurveLine>(this.CurveLines),
                MajorGrid = this.MajorGrid,
                MinorGrid = this.MinorGrid,
                Smooth = this.Smooth,
                SmoothPeriod = this.SmoothPeriod,
                SmoothType = this.SmoothType,
                LegendVisible = this.LegendVisible,
                AxisLabelVisible = this.AxisLabelVisible,
                AxisFontSize = this.AxisFontSize,
                AxisColorAsCurve = this.AxisColorAsCurve
            };
            return resultSettings;
        }

    }
}
