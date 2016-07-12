using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WarThunderParser.Core
{
    public partial class DataProcessingHelper
    {
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

        private void CalcTurnTime()
        {
            if (m_DataSize == 0)
                return;
            if (!m_Data.ContainsKey(Consts.Value.Compass) || m_Data[Consts.Value.Compass].Count < 2)
                return;

            var compass = m_Data[Consts.Value.Compass];

            var turnsData = new List<List<KeyValuePair<double, double>>>(); // <turns<time, compass>>

            double currentCompass = compass[0];
            var nextTurnData = new List<KeyValuePair<double, double>>();
            for (int i = 1; i < m_DataSize - 1; i++)
            {
                nextTurnData.Add(new KeyValuePair<double, double>(m_Data[Consts.Value.Time][i - 1], currentCompass));
                if ((currentCompass > compass[i] && currentCompass < compass[i + 1]) // turn counter clock
                        || (currentCompass > compass[i] && compass[i] < compass[i + 1])) // turn by clock
                {
                    turnsData.Add(nextTurnData);
                    nextTurnData = new List<KeyValuePair<double, double>>();
                }
                currentCompass = compass[i];
            }

            int turnsCount = turnsData.Count;
            if (turnsCount > 0)
            {
                var turnTime = new List<double>(m_DataSize);
                for (int i = 1; i < turnsCount; i++)
                {
                    
                }
            }
        }

        private void CalcTurnRadius()
        {
            if (m_DataSize == 0)
                return;
            // todo
        }

        private void CalcAccelerationTime(int speed)
        {
            if (m_DataSize == 0)
                return;
            // todo
        }

        private long CalcTakeoffDistance()
        {

        }

        private long CalcLandingDistance()
        {

        }

    }
}
