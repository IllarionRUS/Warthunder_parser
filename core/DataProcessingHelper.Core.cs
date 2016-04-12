using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WarThunderParser.core
{
    public partial class DataProcessingHelper
    {
        private Dictionary<string, List<double>> m_Data = new Dictionary<string, List<double>>();
        private int m_DataSize;

        private void collectData()
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
                    try {
                        m_Data.Add(name, approximated[Array.IndexOf(recorder.Names, name)]);
                    } catch (Exception) {
                        // TODO
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

        public string[] getCollectedMeasuresNames()
        {
            return m_Data == null
                ? null
                : m_Data.Keys.ToArray();
        }

    }
}
