using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Smooth.Pools;
using UnityEngine;

namespace KerbalWindTunnel.VesselCache
{
    public class SimulatedEngine
    {
        private static readonly Pool<SimulatedEngine> pool = new Pool<SimulatedEngine>(Create, Reset);

        public bool atmChangeFlow;
        public bool useAtmCurve;
        public FloatCurve atmCurve;
        public bool useVelCurve;
        public FloatCurve velCurve;
        public float flowMultCap;
        public float flowMultCapSharpness;
        public FloatCurve atmosphereCurve;
        public bool useThrottleIspCurve;
        public FloatCurve throttleIspCurve;
        public FloatCurve throttleIspCurveAtmStrength;
        public bool requiresOxygen;
        public float g;
        public float multIsp;
        public float maxFuelFlow;
        public float multFlow;
        public float thrustPercentage;
        public Vector3 thrustVector;
        public float CLAMP;
        public int stage;
        public SimulatedPart part;

        private static SimulatedEngine Create()
        {
            SimulatedEngine engine = new SimulatedEngine();
            return engine;
        }

        private static void Reset(SimulatedEngine simulatedEngine) { }

        public void Release()
        {
            pool.Release(this);
        }

        public static void Release(List<SimulatedEngine> objList)
        {
            for (int i = 0; i < objList.Count; ++i)
            {
                objList[i].Release();
            }
        }

        public static SimulatedEngine Borrow(ModuleEngines module, SimulatedPart part)
        {
            SimulatedEngine engine = pool.Borrow();
            engine.Init(module, part);
            return engine;
        }

        protected void Init(ModuleEngines engine, SimulatedPart part)
        {
            this.atmChangeFlow = engine.atmChangeFlow;
            this.useAtmCurve = engine.useAtmCurve;
            this.atmCurve = new FloatCurve(engine.atmCurve.Curve.keys);
            this.useVelCurve = engine.useVelCurve;
            this.velCurve = new FloatCurve(engine.velCurve.Curve.keys);
            this.flowMultCap = engine.flowMultCap;
            this.flowMultCapSharpness = engine.flowMultCapSharpness;
            this.atmosphereCurve = new FloatCurve(engine.atmosphereCurve.Curve.keys);
            this.useThrottleIspCurve = engine.useThrottleIspCurve;
            this.throttleIspCurve = new FloatCurve(engine.throttleIspCurve.Curve.keys);
            this.throttleIspCurveAtmStrength = new FloatCurve(engine.throttleIspCurveAtmStrength.Curve.keys);
            this.requiresOxygen = engine.propellants.Any(propellant => propellant.name == "IntakeAir");
            this.g = engine.g;
            this.multIsp = engine.multIsp;
            this.maxFuelFlow = engine.maxFuelFlow;
            this.multFlow = engine.multFlow;
            this.thrustPercentage = engine.thrustPercentage;
            this.thrustVector = Vector3.zero;
            for (int i = engine.thrustTransforms.Count - 1; i >= 0; i--)
            {
                this.thrustVector -= engine.thrustTransforms[i].forward * engine.thrustTransformMultipliers[i];
            }
            this.CLAMP = engine.CLAMP;
            this.stage = engine.part.inverseStage;
            this.part = part;
        }

        public Vector3 GetThrust(float mach, float atmDensity, float atmPressure, bool oxygenPresent)
        {
            if (requiresOxygen && !oxygenPresent)
                return Vector3.zero;
            Vector3 thrust = thrustVector;
            float thrustLevel = 1;
            if (atmChangeFlow)
            {
                if (useAtmCurve)
                    lock (this.atmCurve)
                        thrustLevel *= atmCurve.Evaluate(atmDensity * (40f / 49f));
                else
                    thrustLevel *= atmDensity * (40f / 49f);
            }
            if (useVelCurve)
                lock (this.velCurve)
                    thrustLevel *= velCurve.Evaluate(mach);
            if (thrustLevel > flowMultCap)
                thrustLevel = flowMultCap + (thrustLevel - flowMultCap) / (flowMultCapSharpness + thrustLevel / flowMultCap - 1);
            thrustLevel = Mathf.Max(thrustLevel, CLAMP);

            float isp = 0;
            lock (this.atmosphereCurve)
                isp = atmosphereCurve.Evaluate(atmPressure);
            if (useThrottleIspCurve)
                lock (throttleIspCurve)
                    isp *= Mathf.Lerp(1f, throttleIspCurve.Evaluate(1), throttleIspCurveAtmStrength.Evaluate(atmPressure));

            return thrust * g * multIsp * maxFuelFlow * multFlow * (thrustPercentage / 100f) * isp * thrustLevel;
        }

        public float GetFuelBurnRate(float mach, float atmDensity, float atmPressure, bool oxygenPresent)
        {
            throw new NotImplementedException();
        }
    }
}
