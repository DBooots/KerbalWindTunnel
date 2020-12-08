using System;
using System.Collections.Generic;
using System.Linq;
using Smooth.Pools;
using UnityEngine;

namespace KerbalWindTunnel.FARVesselCache
{
    class FARVesselCache : AeroPredictor, VesselCache.IReleasable
    {
        public List<VesselCache.SimulatedEngine> engines = new List<VesselCache.SimulatedEngine>();

        public override float Mass => totalMass;
        public override float Area => throw new NotImplementedException();
        public override bool ThreadSafe { get { return true; } }
        private static readonly Pool<FARVesselCache> pool = new Pool<FARVesselCache>(Create, Reset);
        public List<InstantConditionSimulationWrapper> simulators = new List<InstantConditionSimulationWrapper>(Threading.ThreadPool.ThreadCount);

        public static bool accountForControls = false;

        public float totalMass = 0;
        public float dryMass = 0;

        public static int PoolSize
        {
            get { return pool.Size; }
        }

        private static FARVesselCache Create()
        {
            return new FARVesselCache();
        }

        public void Release()
        {
            pool.Release(this);
        }

        private static void Reset(FARVesselCache obj)
        {
            VesselCache.SimulatedEngine.Release(obj.engines);
            obj.simulators.Clear();
            obj.engines.Clear();
        }

        public static FARVesselCache Borrow(IShipconstruct v, CelestialBody body)
        {
            FARVesselCache vessel;
            lock (pool)
                vessel = pool.Borrow();
            vessel.Init(v, body);
            return vessel;
        }

        private InstantConditionSimulationWrapper GetAvailableSimulator()
        {
            int i = 0;
            while(!System.Threading.Monitor.TryEnter(simulators[i]))
            {
                //i = i + 1;
                if (i >= simulators.Count)
                    i = 0;
            }
            return simulators[i];
        }

        public void Init(IShipconstruct v, CelestialBody body)
        {
            FARHook.UpdateCurrentBody(body);

            int threads = Threading.ThreadPool.ThreadCount;
            simulators.Clear();
            for (int i = 0; i < threads; i++)
                simulators.Add(new InstantConditionSimulationWrapper()); //(InstantConditionSimulationWrapper.Borrow());

            List<Part> oParts = v.Parts;
            int count = oParts.Count;

            bool lgWarning = false;
            int stage = 0;
            for (int i = 0; i < count; i++)
            {
                if (!lgWarning)
                {
                    ModuleWheels.ModuleWheelDeployment gear = oParts[i].FindModuleImplementing<ModuleWheels.ModuleWheelDeployment>();
                    bool forcedRetract = !oParts[i].ShieldedFromAirstream && gear != null && gear.Position > 0;

                    if (forcedRetract)
                        lgWarning = true;
                }

                totalMass += oParts[i].mass + oParts[i].GetResourceMass();
                dryMass += oParts[i].mass;
                CoM += (oParts[i].mass + oParts[i].GetResourceMass()) * oParts[i].transform.TransformPoint(oParts[i].CoMOffset);
                CoM_dry += (oParts[i].mass) * oParts[i].transform.TransformPoint(oParts[i].CoMOffset);

                if (oParts[i].inverseStage > stage)
                {
                    VesselCache.SimulatedEngine.Release(engines);
                    engines.Clear();
                    stage = oParts[i].inverseStage;
                }
                if (oParts[i].inverseStage >= stage)
                {
                    MultiModeEngine multiMode = oParts[i].FindModuleImplementing<MultiModeEngine>();
                    if (multiMode != null)
                    {
                        engines.Add(VesselCache.SimulatedEngine.Borrow(oParts[i].FindModulesImplementing<ModuleEngines>().Find(engine => engine.engineID == multiMode.mode), this));
                    }
                    else
                    {
                        ModuleEngines engine = oParts[i].FindModulesImplementing<ModuleEngines>().FirstOrDefault();
                        if (engine != null)
                            engines.Add(VesselCache.SimulatedEngine.Borrow(engine, this));
                    }
                }
            }
        }

        public override void GetAeroCombined(Conditions conditions, float AoA, float pitchInput, out Vector3 forces, out Vector3 torques, bool dryTorque = false)
        {
            AoA *= Mathf.Rad2Deg;
            InstantConditionSimOutputWrapper output;
            InstantConditionSimulationWrapper simulator = GetAvailableSimulator();
            try
            {
                output = new InstantConditionSimOutputWrapper(simulator.ComputeNonDimensionalForces(AoA, 0, 0, 0, 0, 0, conditions.mach, pitchInput, true, true));
            }
            finally
            { System.Threading.Monitor.Exit(simulator); }
            
            float Q = 0.0005f * conditions.atmDensity * conditions.speed * conditions.speed;
            torques = new Vector3((float)output.Cm * (float)output.MAC, 0, 0) * Q * (float)output.Area;
            forces = new Vector3(0, (float)output.Cl, -(float)output.Cd) * Q * (float)output.Area;
        }

        public override Vector3 GetAeroForce(Conditions conditions, float AoA, float pitchInput = 0)
        {
            AoA *= Mathf.Rad2Deg;
            InstantConditionSimOutputWrapper output;
            InstantConditionSimulationWrapper simulator = GetAvailableSimulator();
            try
            {
                output = new InstantConditionSimOutputWrapper(simulator.ComputeNonDimensionalForces(AoA, 0, 0, 0, 0, 0, conditions.mach, pitchInput, true, true));
            }
            finally
            { System.Threading.Monitor.Exit(simulator); }

            float Q = 0.0005f * conditions.atmDensity * conditions.speed * conditions.speed;
            return new Vector3(0, (float)output.Cl, -(float)output.Cd) * Q * (float)output.Area;
        }

        public override Vector3 GetAeroTorque(Conditions conditions, float AoA, float pitchInput = 0, bool dryTorque = false)
        {
            AoA *= Mathf.Rad2Deg;
            InstantConditionSimOutputWrapper output;
            InstantConditionSimulationWrapper simulator =  GetAvailableSimulator();
            try
            {
                output = new InstantConditionSimOutputWrapper(simulator.ComputeNonDimensionalForces(AoA, 0, 0, 0, 0, 0, conditions.mach, pitchInput, true, true));
            }
            finally
            { System.Threading.Monitor.Exit(simulator); }

            float Q = 0.0005f * conditions.atmDensity * conditions.speed * conditions.speed;
            return new Vector3((float)output.Cm * (float)output.MAC, 0, 0) * Q * (float)output.Area;
        }

        // TODO: Add ITorqueProvider and thrust effect on torque
        public override float GetAoA(Conditions conditions, float offsettingForce, bool useThrust = true, bool dryTorque = false, float guess = float.NaN, float pitchInputGuess = float.NaN, bool lockPitchInput = false)
        {
            Vector3 thrustForce = useThrust ? this.GetThrustForce(conditions) : Vector3.zero;

            if (!accountForControls)
                return base.GetAoA(conditions, offsettingForce, useThrust, dryTorque, guess, 0, true);
            if (lockPitchInput)
                return base.GetAoA(conditions, offsettingForce, useThrust, dryTorque, guess, pitchInputGuess, lockPitchInput);

            float approxAoA = GetAoA(conditions, offsettingForce, useThrust, dryTorque, guess, pitchInputGuess, true, 1 * Mathf.Deg2Rad);
            return base.GetAoA(conditions, offsettingForce, useThrust, dryTorque, approxAoA, pitchInputGuess, lockPitchInput);
        }
        
        public override float GetFuelBurnRate(float mach, float atmDensity, float atmPressure, bool oxygenPresent)
        {
            float burnRate = 0;
            for (int i = engines.Count - 1; i >= 0; i--)
            {
                burnRate += engines[i].GetFuelBurnRate(mach, atmDensity);
            }
            return burnRate;
        }

        public override float GetPitchInput(Conditions conditions, float AoA, bool dryTorque = false, float guess = float.NaN)
        {
            Accord.Math.Optimization.BrentSearch solver = new Accord.Math.Optimization.BrentSearch((input) => this.GetAeroTorque(conditions, AoA, (float)input, dryTorque).x, -0.3, 0.3, 0.0001);
            if (solver.FindRoot())
                return (float)solver.Solution;
            solver.LowerBound = -1;
            solver.UpperBound = 1;
            if (solver.FindRoot())
                return (float)solver.Solution;
            if (this.GetAeroTorque(conditions, AoA, 0, dryTorque).x > 0)
                return -1;
            else
                return 1;
        }

        public override Vector3 GetThrustForce(float mach, float atmDensity, float atmPressure, bool oxygenPresent)
        {
            Vector3 thrust = Vector3.zero;
            for (int i = engines.Count - 1; i >= 0; i--)
            {
                thrust += engines[i].GetThrust(mach, atmDensity, atmPressure, oxygenPresent);
            }
            return thrust;
        }
    }
}
