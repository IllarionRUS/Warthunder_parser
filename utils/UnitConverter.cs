using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WarThunderParser.Utils
{
    public abstract class UnitConverter
    {
        private string[] m_Units;

        public UnitConverter(string[] convertingUnits)
        {
            m_Units = convertingUnits;
        }

        internal abstract KeyValuePair<string, double> GetNewUnitAndFactor(string unit);

        public string Convert(IList<double> values, string unit)
        {
            if (m_Units.Contains(unit))
            {
                var unitAndFactor = GetNewUnitAndFactor(unit);

                for (int i = 0; i < values.Count(); i++)
                    values[i] *= unitAndFactor.Value;

                return unitAndFactor.Key;
            }
            return unit;
        }

        public string[] getConvertableUnits()
        {
            return m_Units.Clone() as string[];
        }

    }
}
