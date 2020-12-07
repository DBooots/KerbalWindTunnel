using System;
using System.Collections.Generic;
using Smooth.Pools;

namespace KerbalWindTunnel.FARVesselCache
{
    public delegate TResult Func<T1, T2, T3, T4, T5, T6, T7, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7);
    public delegate TResult Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11);
    public delegate TResult Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13);
    public delegate void Action<T1, T2, T3, T4, T5, T6, T7>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7);

    public class InstantConditionSimulationWrapper
    {
        private static readonly Pool<InstantConditionSimulationWrapper> pool = new Pool<InstantConditionSimulationWrapper>(Create, Reset);
        
        private static InstantConditionSimulationWrapper Create()
        {
            return new InstantConditionSimulationWrapper();
        }
        private static void Reset(InstantConditionSimulationWrapper sim) { }
        public void Release()
        {
            lock (pool)
                pool.Release(this);
        }
        public static void Release(List<InstantConditionSimulationWrapper> objList)
        {
            for (int i = 0; i < objList.Count; ++i)
            {
                objList[i].Release();
            }
        }

        public static InstantConditionSimulationWrapper Borrow()
        {
            InstantConditionSimulationWrapper sim;
            lock (pool)
                sim = pool.Borrow();
            sim.Update();
            return sim;
        }

        private object trueObject;

        internal static Func<object, object> _iterationOutput = (o) => null;
        public object iterationOutput { get => _iterationOutput(trueObject); } //InstantConditionSimOutput

        internal static Func<object> _constructor = () => null;
        public InstantConditionSimulationWrapper()
        {
            FARHook.Initiate();
            trueObject = _constructor();
        }

        internal static Func<object, bool> _ready = (o) => false;
        public bool Ready { get => _ready(trueObject); }

        internal static Func<object, CelestialBody, double, double> _calculateAccelerationDueToGravity = (o, b, a) => 0;
        public double CalculateAccelerationDueToGravity(CelestialBody body, double alt)
            => _calculateAccelerationDueToGravity(trueObject, body, alt);

        internal static Func<object, object, bool, bool, object> _computeNonDimensionalForcesO = (o, i, c, r) => null;
        public object ComputeNonDimensionalForces(object input, bool clear, bool reset_stall = false) //InstantConditionSimOutput
            => _computeNonDimensionalForcesO(trueObject, input, clear, reset_stall);

        internal static Func<object, double, double, double, double, double, double, double, double, bool, bool, object> _computeNonDimensionalForcesS = (o, a, b, p, ad, bd, pd, m, pitch, c, r) => null;
        public object ComputeNonDimensionalForces(double alpha, double beta, double phi, double alphaDot, double betaDot, double phiDot, double machNumber, double pitchValue, bool clear, bool reset_stall = false) //InstantConditionSimOutput
        //=> _computeNonDimensionalForcesS(trueObject, alpha, beta, phi, alphaDot, betaDot, phiDot, machNumber, pitchValue, clear, reset_stall);
        {
            return FARHook.simulationType.GetMethod("ComputeNonDimensionalForces", new Type[] { typeof(double), typeof(double), typeof(double), typeof(double), typeof(double), typeof(double), typeof(double), typeof(double), typeof(bool), typeof(bool) }).Invoke(trueObject, new object[] { alpha, beta, phi, alphaDot, betaDot, phiDot, machNumber, pitchValue, clear, reset_stall });
        }

        internal static Func<object, double, double, double, double, double, double, double, double, int, bool, bool, bool, object> _computeNonDimensionalForcesE = (o, a, b, p, ad, bd, pd, m, pitch, flaps, s, c, r) => null;
        public object ComputeNonDimensionalForces(double alpha, double beta, double phi, double alphaDot, double betaDot, double phiDot, double machNumber, double pitchValue, int flaps, bool spoilers, bool clear, bool reset_stall = false) //InstantConditionSimOutput
            => _computeNonDimensionalForcesE(trueObject, alpha, beta, phi, alphaDot, betaDot, phiDot, machNumber, pitchValue, flaps, spoilers, clear, reset_stall);

        internal static Action<object, double, double, Vector3d, double, int, bool> _setState = (o, mbox, cl, com, pitch, flaps, spoilers) => { };
        public void SetState(double machNumber, double Cl, Vector3d CoM, double pitch, int flapSetting, bool spoilers)
            => _setState(trueObject, machNumber, Cl, CoM, pitch, flapSetting, spoilers);

        internal static Func<object, double, double> _functionIterateForAlpha = (o, alpha) => 0;
        public double FunctionIterateForAlpha(double alpha)
            => _functionIterateForAlpha(trueObject, alpha);

        internal static Func<object, double, double, Vector3d, double, int, bool, double> _computeRequiredAoA = (o, m, Cl, CoM, p, f, s) => 0;
        public double ComputeRequiredAoA(double machNumber, double Cl, Vector3d CoM, double pitch, int flapSetting, bool spoilers)
            => _computeRequiredAoA(trueObject, machNumber, Cl, CoM, pitch, flapSetting, spoilers);

        internal static Action<object> _update = (o) => { };
        public void Update()
            => _update(trueObject);
    }
}
