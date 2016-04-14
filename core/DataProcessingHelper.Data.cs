using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WarThunderParser.Core
{
    public partial class DataProcessingHelper
    {
        private Dictionary<string, List<double>> m_Data = new Dictionary<string, List<double>>();
        private Dictionary<string, string> m_Units = new Dictionary<string, string>();
        private int m_DataSize;

        private void CollectData()
        {
            m_Data.Clear();
            List<FlightDataRecorder> recorders = RecordersManager.getFinishedRecorders();
            if (recorders == null)
                return;

            m_DataSize = int.MaxValue;
            var syncTime = new DateTime(0);
            foreach (var recorder in recorders)
                if (recorder.InitTime > syncTime)
                    syncTime = recorder.InitTime;

            foreach (var recorder in recorders)
            {
                List<double>[] approximated = recorder.GetApproxList(syncTime, CollectSettings.InterpInterval);
                foreach (List<double> list in approximated)
                    m_DataSize = Math.Min(m_DataSize, list.Count);

                foreach (var name in recorder.Names)
                {
                    if (m_Data.ContainsKey(name))
                        continue;

                    var unit = recorder.Unit(name);
                    if (string.Equals(name, Consts.Value.Time, StringComparison.InvariantCultureIgnoreCase))
                    {
                        approximated[Array.IndexOf(recorder.Names, name)] =
                            approximated[Array.IndexOf(recorder.Names, name)].Select(t => t / 1000).ToList();
                        unit = Consts.Unit.Time_S;
                    }

                    m_Data.Add(name, approximated[Array.IndexOf(recorder.Names, name)]);
                    m_Units.Add(name, unit);

                    if (string.Equals(name, Consts.Value.TAS, StringComparison.InvariantCultureIgnoreCase))
                        CalcAcceleration();
                }
            }

            foreach (var keyValue in m_Data)
            {
                var trimmedValues = keyValue.Value;
                if (trimmedValues.Count > m_DataSize)
                    trimmedValues.RemoveRange(m_DataSize, keyValue.Value.Count - 1);
                trimmedValues.TrimExcess();
            }
        }

        private void CalcAcceleration()
        {
            if (m_DataSize == 0)
                return;

            List<double> values = new List<double>(m_DataSize);
            bool isMetrical = string.Equals(m_Units[Consts.Value.TAS], Consts.Unit.Speed_M, StringComparison.InvariantCultureIgnoreCase);

            var speedValues = m_Data[Consts.Value.TAS];
            for (int i = 1; i < m_DataSize; i++)
            {
                values.Add((speedValues[i] - speedValues[i - 1]) / (m_Data[Consts.Value.Time][i] - m_Data[Consts.Value.Time][i - 1]));
                
                /*
                if (isMetrical)
                    values[i - 1] *= 0.2778;
                else
                    values[i - 1] *= 1.4667;
                */

            }
            m_Data.Add(Consts.Value.Acceleration, values);
            m_Units.Add(Consts.Value.Acceleration, isMetrical ? Consts.Unit.Acc_M : Consts.Unit.Acc_I);
        }

        public void ConvertToMetrical()
        {
            var toConvert = m_Data.Where(d => Array.IndexOf(Consts.Unit.Imperial, m_Units[d.Key]) >= 0).ToArray();

            foreach (var keyValue in toConvert) {
                var values = keyValue.Value;
                switch (m_Units[keyValue.Key])
                {                    
                    case Consts.Unit.Alt_I:
                        values.ForEach(v => v *= 0.3048);
                        break;
                    case Consts.Unit.Speed_I:
                        values.ForEach(v => v *= 1.6093);
                        break;
                    case Consts.Unit.Climb_I:
                        values.ForEach(v => v *= 0.0051);
                        break;
                    case Consts.Unit.Acc_I:
                        values.ForEach(v => v *= 0.3048);
                        break;
                    default:
                        throw new InvalidOperationException("convert error");
                }
            }
            Redraw();
        }

        public void ConvertToImperial()
        {
            var toConvert = m_Data.Where(d => Array.IndexOf(Consts.Unit.Metrical, m_Units[d.Key]) >= 0).ToArray();

            foreach (var keyValue in toConvert)
            {
                var values = keyValue.Value;
                switch (m_Units[keyValue.Key])
                {
                    case Consts.Unit.Alt_M:
                        values.ForEach(v => v *= 3.2808);
                        break;
                    case Consts.Unit.Speed_M:
                        values.ForEach(v => v *= 0.6214);
                        break;
                    case Consts.Unit.Climb_M:
                        values.ForEach(v => v *= 196.8504);
                        break;
                    case Consts.Unit.Acc_M:
                        values.ForEach(v => v *= 3.2808);
                        break;
                    default:
                        throw new InvalidOperationException("convert error");
                }
            }
            Redraw();
        }

        public string[] GetCollectedMeasuresNames()
        {
            return m_Data == null
                ? null
                : m_Data.Keys.ToArray();
        }

    }
}
