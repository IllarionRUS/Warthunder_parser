using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WarThunderParser.Core
{
    public enum Metrica
    {
        Metric,
        Imperial
    }

    public class Consts
    {
        public static class Value
        {
            public const string Type = "type";
            public const string Time = "Time";            
            public const string TAS = "TAS";
            public const string IAS = "IAS";
            public const string Acceleration_TAS = "Acceleration_TAS";
            public const string Acceleration_IAS = "Acceleration_IAS";
            public const string Altitudes = "altitude";
            public const string Compass = "compass";
            public const string TurnTime = "Turn_Time";
        }

        public static class Unit
        {
            public const string Time_Ms = "ms";
            public const string Time_S = "s";
            public const string Alt_M = "m";
            public const string Alt_I = "ft";            
            public const string Speed_M = "km/h";
            public const string Speed_I = "mil/h";
            public const string Climb_M = "m/s";
            public const string Climb_I = "ft/min";
            public const string Acc_M = "km/h^2";
            public const string Acc_I = "mil/h^2";

            public static readonly string[] Imperial = new string[] { Alt_I, Speed_I, Climb_I, Acc_I };
            public static readonly string[] Metrical = new string[] { Alt_M, Speed_M, Climb_M, Acc_M };
        }
    }
}
