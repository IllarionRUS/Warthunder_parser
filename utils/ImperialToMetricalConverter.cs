using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WarThunderParser.Core;

namespace WarThunderParser.Utils
{
    public class ImperialToMetricalConverter : UnitConverter
    {
        public ImperialToMetricalConverter() : base(Consts.Unit.Imperial)
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
}
