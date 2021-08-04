using System;
using System.Collections.Generic;
using System.Linq;
using Smooth.Pools;
using UnityEngine;
using KerbalWindTunnel.Extensions;

namespace KerbalWindTunnel.VesselCache
{
    public class SimulatedEngine
    {
        private static readonly Pool<SimulatedEngine> pool = new Pool<SimulatedEngine>(Create, Reset);

        public AeroPredictor vessel;

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
        public Vector3 thrustPoint;
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
        public static SimulatedEngine Borrow(ModuleEngines module, AeroPredictor vessel)
        {
            SimulatedEngine engine = pool.Borrow();
            engine.vessel = vessel;
            engine.Init(module, null);
            return engine;
        }
        public static SimulatedEngine BorrowClone(SimulatedEngine engine, SimulatedPart part)
        {
            SimulatedEngine clone = pool.Borrow();
            clone.vessel = part?.vessel;
            clone.InitClone(engine, part);
            return clone;
        }

        protected void Init(ModuleEngines engine, SimulatedPart part)
        {
            this.flameoutBar = engine.flameoutBar;
            this.atmChangeFlow = engine.atmChangeFlow;
            this.useAtmCurve = engine.useAtmCurve;
            this.useAtmCurveIsp = engine.useAtmCurveIsp;
            this.atmCurve = engine.atmCurve.Clone();
            this.atmCurveIsp = engine.atmCurveIsp.Clone();
            this.useVelCurve = engine.useVelCurve;
            this.useVelCurveIsp = engine.useVelCurveIsp;
            this.velCurve = engine.velCurve.Clone();
            this.velCurveIsp = engine.velCurveIsp.Clone();
            this.flowMultCap = engine.flowMultCap;
            this.flowMultCapSharpness = engine.flowMultCapSharpness;
            this.atmosphereCurve = engine.atmosphereCurve.Clone();
            this.useThrustCurve = engine.useThrustCurve;
            this.thrustCurve = engine.thrustCurve.Clone();
            this.useThrottleIspCurve = engine.useThrottleIspCurve;
            this.throttleIspCurve = engine.throttleIspCurve.Clone();
            this.throttleIspCurveAtmStrength = engine.throttleIspCurveAtmStrength.Clone();
            this.requiresOxygen = engine.propellants.Any(propellant => propellant.name == "IntakeAir");
            this.g = engine.g;
            this.multIsp = engine.multIsp;
            this.maxFuelFlow = engine.maxFuelFlow;
            this.multFlow = engine.multFlow;
            this.thrustPercentage = engine.thrustPercentage;
            this.thrustVector = Vector3.zero;
            float thrustTransformMultiplierSum = 0;
            this.thrustPoint = Vector3.zero;
            for (int i = engine.thrustTransforms.Count - 1; i >= 0; i--)
            {
                this.thrustVector -= engine.thrustTransforms[i].forward * engine.thrustTransformMultipliers[i];
                this.thrustPoint += engine.thrustTransforms[i].position * engine.thrustTransformMultipliers[i];
                thrustTransformMultiplierSum += engine.thrustTransformMultipliers[i];
            }
            this.thrustPoint /= thrustTransformMultiplierSum;
            this.CLAMP = engine.CLAMP;
            this.stage = engine.part.inverseStage;
            this.part = part;
        }
        protected void InitClone(SimulatedEngine engine, SimulatedPart part)
        {
            this.flameoutBar = engine.flameoutBar;
            this.atmChangeFlow = engine.atmChangeFlow;
            this.useAtmCurve = engine.useAtmCurve;
            this.useAtmCurveIsp = engine.useAtmCurveIsp;
            this.atmCurve = engine.atmCurve.Clone();
            this.atmCurveIsp = engine.atmCurveIsp.Clone();
            this.useVelCurve = engine.useVelCurve;
            this.useVelCurveIsp = engine.useVelCurveIsp;
            this.velCurve = engine.velCurve.Clone();
            this.velCurveIsp = engine.velCurveIsp.Clone();
            this.flowMultCap = engine.flowMultCap;
            this.flowMultCapSharpness = engine.flowMultCapSharpness;
            this.atmosphereCurve = engine.atmosphereCurve.Clone();
            this.useThrustCurve = engine.useThrustCurve;
            this.thrustCurve = engine.thrustCurve.Clone();
            this.useThrottleIspCurve = engine.useThrottleIspCurve;
            this.throttleIspCurve = engine.throttleIspCurve.Clone();
            this.throttleIspCurveAtmStrength = engine.throttleIspCurveAtmStrength.Clone();
            this.requiresOxygen = engine.requiresOxygen;
            this.g = engine.g;
            this.multIsp = engine.multIsp;
            this.maxFuelFlow = engine.maxFuelFlow;
            this.multFlow = engine.multFlow;
            this.thrustPercentage = engine.thrustPercentage;
            this.thrustVector = engine.thrustVector;
            this.thrustPoint = engine.thrustPoint;
            this.CLAMP = engine.CLAMP;
            this.stage = engine.stage;
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

#if DEBUG
            if (!requiresOxygen)
                Debug.LogFormat("Fuel: {0:F3}, ISP: {1:F1}, Thrust: {2:F2}", fuelBurnRate, isp, fuelBurnRate * g * multIsp * thrustPercentage / 100f);
#endif
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
