using System;

namespace KerbalWindTunnel.FARVesselCache
{
    public class InstantConditionSimOutputWrapper
    {
        internal static Func<object, double> _getCl;
        internal static Func<object, double> _getCd;
        internal static Func<object, double> _getCm;
        internal static Func<object, double> _getCy;
        internal static Func<object, double> _getCn;
        internal static Func<object, double> _getC_roll;
        internal static Func<object, double> _getArea;
        internal static Func<object, double> _getMAC;

        private object trueObject;

        public InstantConditionSimOutputWrapper(object trueObject)
        {
            FARHook.Initiate();
            this.trueObject = trueObject;
        }

        public double Cl { get => _getCl(trueObject); }
        public double Cd { get => _getCd(trueObject); }
        public double Cm { get => _getCm(trueObject); }
        public double Cy { get => _getCy(trueObject); }
        public double Cn { get => _getCn(trueObject); }
        public double C_roll { get => _getC_roll(trueObject); }
        public double Area { get => _getArea(trueObject); }
        public double MAC { get => _getMAC(trueObject); }
    }
}
