using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WarThunderParser.Core;

namespace WarThunderParser.Utils
{
    public class MetricalToImperialConverter : UnitConverter
    {
        public MetricalToImperialConverter() : base(Consts.Unit.Metrical)
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
}
