using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WarThunderParser.Utils;

namespace WarThunderParser.Core
{
    public partial class DataProcessingHelper
    {
        private string m_Type;
        private Dictionary<string, List<double>> m_Data = new Dictionary<string, List<double>>();
        private Dictionary<string, string> m_Units = new Dictionary<string, string>();
        private int m_DataSize;

        // for proper recalcs on collectSettings change
        private DateTime? m_SynchTime;

        private MetricalToImperialConverter m_Metrical2ImperialConverter;
        private ImperialToMetricalConverter m_Imperial2MetricalConverter;

        private void CollectData()
        {
            m_Data.Clear();
            m_Units.Clear();
            List<FlightDataRecorder> recorders = RecordersManager.getFinishedRecorders();
            if (recorders == null)
                return;

            m_DataSize = int.MaxValue;

            var syncTime = m_SynchTime ?? new DateTime(0);
            foreach (var recorder in recorders)
                if (recorder.InitTime > syncTime)
                    syncTime = recorder.InitTime;
            m_SynchTime = syncTime;

            foreach (var recorder in recorders)
            {
                List<double>[] approximated = recorder.GetApproxList(syncTime, CollectSettings.InterpInterval);
                foreach (List<double> list in approximated)
                    m_DataSize = Math.Min(m_DataSize, list.Count);

                foreach (var name in recorder.Names)
                {
                    if (m_CollectSettings.AllowInputFilters && m_CollectSettings.FilterList.Contains(name))
                        continue;

                    if (m_Data.ContainsKey(name))
                        continue;

                    var unit = recorder.Unit(name);
                    if (string.Equals(name, Consts.Value.Time, StringComparison.InvariantCultureIgnoreCase))
                    {
                        approximated[Array.IndexOf(recorder.Names, name)] =
                            approximated[Array.IndexOf(recorder.Names, name)].Select(t => t / 1000).ToList();
                        unit = Consts.Unit.Time_S;
                    }
                    if (name.ToLower().StartsWith(Consts.Value.Altitudes))
                    {
                        if (m_CollectSettings.AltFeets)
                            unit = Consts.Unit.Alt_I;
                        else
                            unit = Consts.Unit.Alt_M;
                    }

                    m_Data.Add(name, approximated[Array.IndexOf(recorder.Names, name)]);
                    m_Units.Add(name, unit);  
                }

                if (recorder.TextData.ContainsKey(Consts.Value.Type))
                    m_Type = recorder.TextData[Consts.Value.Type];
            }
            if (m_Data.Count == 0)
            {
                m_DataSize = 0;
                return;
            }

            CalcAcceleration();

            foreach (var keyValue in m_Data)
            {
                var trimmedValues = keyValue.Value;
                if (trimmedValues.Count > m_DataSize)
                    trimmedValues.RemoveRange(m_DataSize, keyValue.Value.Count - m_DataSize);
                trimmedValues.TrimExcess();
            }
        }

        private void CalcAcceleration()
        {
            if (m_DataSize == 0)
                return;

            List<double> tas_Acc = new List<double>(m_DataSize);
            var ias_Acc = new List<double>(m_DataSize);

            bool isMetrical = string.Equals(m_Units[Consts.Value.TAS], Consts.Unit.Speed_M, StringComparison.InvariantCultureIgnoreCase);

            var tasValues = m_Data[Consts.Value.TAS];
            var iasValues = m_Data[Consts.Value.IAS];

            tas_Acc.Add(0d);
            ias_Acc.Add(0d);
            for (int i = 1; i < m_DataSize; i++)
            {
                tas_Acc.Add((tasValues[i] - tasValues[i - 1]) / (m_Data[Consts.Value.Time][i] - m_Data[Consts.Value.Time][i - 1]));
                ias_Acc.Add((iasValues[i] - iasValues[i - 1]) / (m_Data[Consts.Value.Time][i] - m_Data[Consts.Value.Time][i - 1]));

                /*
                if (isMetrical)
                    values[i - 1] *= 0.2778;
                else
                    values[i - 1] *= 1.4667;
                */

            }
            m_Data.Add(Consts.Value.Acceleration_TAS, tas_Acc);
            m_Data.Add(Consts.Value.Acceleration_IAS, ias_Acc);
            m_Units.Add(Consts.Value.Acceleration_TAS, isMetrical ? Consts.Unit.Acc_M : Consts.Unit.Acc_I);
            m_Units.Add(Consts.Value.Acceleration_IAS, isMetrical ? Consts.Unit.Acc_M : Consts.Unit.Acc_I);
        }

        public void ConvertToMetrical()
        {
            Convert(m_Imperial2MetricalConverter);
        }

        public void ConvertToImperial()
        {
            Convert(m_Metrical2ImperialConverter);
        }

        public string[] GetCollectedMeasuresNames()
        {
            return m_Data == null
                ? null
                : m_Data.Keys.ToArray();
        }

        private void Convert(UnitConverter converter)
        {
            var toConvert = m_Data.Where(d => Array.IndexOf(converter.getConvertableUnits(), m_Units[d.Key]) >= 0).ToArray();

            foreach (var keyValue in toConvert)
            {
                string newUnit = converter.Convert(m_Data[keyValue.Key], m_Units[keyValue.Key]);
                
                var toUpdateX = Graphs.Where(graph => graph.XAxis != null && string.Equals(graph.XAxis, keyValue.Key));
                var toUpdateY = Graphs.Where(graph => graph.YAxis != null && string.Equals(graph.YAxis, keyValue.Key));
                foreach (Graph graph in toUpdateX)
                {
                    graph.X_Unit = newUnit;
                    for (int i = 0; i < m_DataSize; i++)
                        graph.PointPairs[i].X = m_Data[keyValue.Key][i];
                }
                foreach (Graph graph in toUpdateY)
                {
                    graph.Y_Unit = newUnit;
                    for (int i = 0; i < m_DataSize; i++)
                        graph.PointPairs[i].Y = m_Data[keyValue.Key][i];
                }

                m_Units[keyValue.Key] = newUnit;
            }

            Redraw();
            var visibleColumns = DataGrid.Columns
                .Where(c => c.Visibility == System.Windows.Visibility.Visible)
                .Select(s => s.Header.ToString().Split(",".ToCharArray())[0])
                .ToList();
            UpdateDataGrid();
            foreach (var columnName in visibleColumns)
                ShowColumn(columnName);
        }

        public Dictionary<string, List<double>> getData()
        {
            if (m_Data == null)
                return null;
            return m_Data.ToDictionary(entry => entry.Key, entry => new List<double>(entry.Value));
        }

        public Dictionary<string, string> getUnits()
        {
            if (m_Units == null)
                return null;
            return m_Units.ToDictionary(entry => entry.Key, entry => entry.Value);
        }

        public string getType()
        {
            return m_Type;
        }

        /*
        private abstract class UnitConverter
        {
            private DataProcessingHelper processingHelper;
            private string[] convertFrom;

            public UnitConverter(DataProcessingHelper processingHelper, string[] convertingUnits)
            {
                this.processingHelper = processingHelper;
                this.convertFrom = convertingUnits;
            }

            internal abstract KeyValuePair<string, double> GetNewUnitAndFactor(string unit);

            public void Convert()
            {
                var toConvert = processingHelper.m_Data.Where(d => Array.IndexOf(convertFrom, processingHelper.m_Units[d.Key]) >= 0).ToArray();

                foreach (var keyValue in toConvert)
                {
                    var values = keyValue.Value;
                    var newUnit = processingHelper.m_Units[keyValue.Key];
                    var unitAndFactor = GetNewUnitAndFactor(processingHelper.m_Units[keyValue.Key]);
                    newUnit = unitAndFactor.Key;
                    var factor = unitAndFactor.Value;

                    for (int i = 0; i < processingHelper.m_DataSize; i++)
                        processingHelper.m_Data[keyValue.Key][i] *= factor;

                    var toUpdateX = processingHelper.Graphs.Where(graph => graph.XAxis != null && string.Equals(graph.XAxis, keyValue.Key));
                    var toUpdateY = processingHelper.Graphs.Where(graph => graph.YAxis != null && string.Equals(graph.YAxis, keyValue.Key));
                    foreach (Graph graph in toUpdateX)
                    {
                        graph.X_Unit = newUnit;
                        for (int i = 0; i < processingHelper.m_DataSize; i++)
                            graph.PointPairs[i].X = values[i];
                    }
                    foreach (Graph graph in toUpdateY)
                    {
                        graph.Y_Unit = newUnit;
                        for (int i = 0; i < processingHelper.m_DataSize; i++)
                            graph.PointPairs[i].Y = values[i];
                    }

                    processingHelper.m_Units[keyValue.Key] = newUnit;
                }

                processingHelper.Redraw();
                var visibleColumns = processingHelper.DataGrid.Columns
                    .Where(c => c.Visibility == System.Windows.Visibility.Visible)
                    .Select(s => s.Header.ToString().Split(",".ToCharArray())[0])
                    .ToList();
                processingHelper.UpdateDataGrid();
                foreach (var columnName in visibleColumns)
                    processingHelper.ShowColumn(columnName);                
            }
        }

        private class ImperialToMetrical : UnitConverter
        {
            public ImperialToMetrical(DataProcessingHelper processingHelper) : base(processingHelper, Consts.Unit.Imperial)
            {
            }

            internal override KeyValuePair<string, double> GetNewUnitAndFactor(string unit)
            {
                string newUnit = unit;
                double factor = 1d;
                switch (unit)
                {
                    case Consts.Unit.Alt_I:
                        factor = 0.3048;
                        newUnit = Consts.Unit.Alt_M;
                        break;
                    case Consts.Unit.Speed_I:
                        factor = 1.6093;
                        newUnit = Consts.Unit.Speed_M;
                        break;
                    case Consts.Unit.Climb_I:
                        factor = 0.0051;
                        newUnit = Consts.Unit.Climb_M;
                        break;
                    case Consts.Unit.Acc_I:
                        factor = 0.3048;
                        newUnit = Consts.Unit.Acc_M;
                        break;
                    default:
                        throw new InvalidOperationException("convert error");
                }
                return new KeyValuePair<string, double>(newUnit, factor);
            }
        }

        private class MetricalToImperial : UnitConverter
        {
            public MetricalToImperial(DataProcessingHelper processingHelper) : base(processingHelper, Consts.Unit.Metrical)
            {
            }

            internal override KeyValuePair<string, double> GetNewUnitAndFactor(string unit)
            {
                string newUnit = unit;
                double factor = 1d;
                switch (unit)
                {
                    case Consts.Unit.Alt_M:
                        factor = 3.2808;
                        newUnit = Consts.Unit.Alt_I;
                        break;
                    case Consts.Unit.Speed_M:
                        factor = 0.6214;
                        newUnit = Consts.Unit.Speed_I;
                        break;
                    case Consts.Unit.Climb_M:
                        factor = 196.8504;
                        newUnit = Consts.Unit.Climb_I;
                        break;
                    case Consts.Unit.Acc_M:
                        factor = 3.2808;
                        newUnit = Consts.Unit.Acc_I;
                        break;
                    default:
                        throw new InvalidOperationException("convert error");
                }
                return new KeyValuePair<string, double>(newUnit, factor);
            }
        }
        */
    }
}
