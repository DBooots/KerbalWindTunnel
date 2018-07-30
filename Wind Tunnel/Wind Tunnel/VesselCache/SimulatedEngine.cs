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

        public SimulatedVessel vessel;

        public float flameoutBar;
        public bool atmChangeFlow;
        public bool useAtmCurve;
        public bool useAtmCurveIsp;
        public FloatCurve atmCurve;
        public FloatCurve atmCurveIsp;
        public bool useVelCurve;
        public bool useVelCurveIsp;
        public FloatCurve velCurve;
        public FloatCurve velCurveIsp;
        public bool useThrustCurve;
        public FloatCurve thrustCurve;
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
            engine.vessel = part.vessel;
            engine.Init(module, part);
            return engine;
        }

        protected void Init(ModuleEngines engine, SimulatedPart part)
        {
            this.flameoutBar = engine.flameoutBar;
            this.atmChangeFlow = engine.atmChangeFlow;
            this.useAtmCurve = engine.useAtmCurve;
            this.useAtmCurveIsp = engine.useAtmCurveIsp;
            this.atmCurve = new FloatCurve(engine.atmCurve.Curve.keys);
            this.atmCurveIsp = new FloatCurve(engine.atmCurveIsp.Curve.keys);
            this.useVelCurve = engine.useVelCurve;
            this.useVelCurveIsp = engine.useVelCurveIsp;
            this.velCurve = new FloatCurve(engine.velCurve.Curve.keys);
            this.velCurveIsp = new FloatCurve(engine.velCurveIsp.Curve.keys);
            this.flowMultCap = engine.flowMultCap;
            this.flowMultCapSharpness = engine.flowMultCapSharpness;
            this.atmosphereCurve = new FloatCurve(engine.atmosphereCurve.Curve.keys);
            this.useThrustCurve = engine.useThrustCurve;
            this.thrustCurve = new FloatCurve(engine.thrustCurve.Curve.keys);
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
            return GetThrust(mach, atmDensity, atmPressure, oxygenPresent, out _);
        }
        public Vector3 GetThrust(float mach, float atmDensity, float atmPressure, bool oxygenPresent, out float fuelBurnRate)
        {
            atmPressure *= 0.00986923267f;
            fuelBurnRate = 0;
            if (requiresOxygen && !oxygenPresent)
                return Vector3.zero;

            fuelBurnRate = GetFuelBurnRate(mach, atmDensity);
            if (fuelBurnRate <= 0)
                return Vector3.zero;
            
            float isp = 0;
            lock (atmosphereCurve)
                isp = atmosphereCurve.Evaluate(atmPressure);
            if (useThrottleIspCurve)
                lock (throttleIspCurve)
                    isp *= Mathf.Lerp(1f, throttleIspCurve.Evaluate(1), throttleIspCurveAtmStrength.Evaluate(atmPressure));
            if (useAtmCurveIsp)
                lock (atmCurveIsp)
                    isp *= atmCurveIsp.Evaluate(atmDensity * 40 / 49);
            if (useVelCurveIsp)
                lock (velCurveIsp)
                    isp *= velCurveIsp.Evaluate(mach);


            if (!requiresOxygen)
                Debug.Log(string.Format("Fuel: {0}, ISP: {1}, Thrust: {2}", fuelBurnRate, isp, fuelBurnRate * g * multIsp * thrustPercentage / 100f));
            return thrustVector * fuelBurnRate * g * multIsp * isp * (thrustPercentage / 100f);
        }

        public float GetFuelBurnRate(float mach, float atmDensity)
        {
            float flowMultiplier = 1;
            if (atmChangeFlow)
            {
                if (useAtmCurve)
                    lock (atmCurve)
                        flowMultiplier = atmCurve.Evaluate(atmDensity * 40 / 49);
                else
                    flowMultiplier = atmDensity * 40 / 49;
            }
            if (useThrustCurve)
                lock (thrustCurve)
                    flowMultiplier *= thrustCurve.Evaluate(1f);
            if (useVelCurve)
                lock (velCurve)
                    flowMultiplier *= velCurve.Evaluate(mach);
            if (flowMultiplier > flowMultCap)
            {
                float excess = flowMultiplier - flowMultCap;
                flowMultiplier = flowMultCap + excess / (flowMultCapSharpness + excess / flowMultCap);
            }
            if (flowMultiplier < CLAMP)
                flowMultiplier = CLAMP;

            if (flowMultiplier < flameoutBar)
                return 0;
            return flowMultiplier * maxFuelFlow * multFlow;
        }
    }
}
