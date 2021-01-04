using System;
using System.Collections.Generic;
using System.Linq;
using KerbalWindTunnel.Extensions;
using Smooth.Pools;
using UnityEngine;

namespace KerbalWindTunnel.VesselCache
{
    public class SimulatedVessel : AeroPredictor, IReleasable
    {
        public static bool accountForControls = false;

        /*RootSolverSettings pitchInputSolverSettings = new RootSolverSettings(
            RootSolver.LeftBound(-1),
            RootSolver.RightBound(1),
            RootSolver.LeftGuessBound(-0.25f),
            RootSolver.RightGuessBound(0.25f),
            RootSolver.ShiftWithGuess(true),
            RootSolver.Tolerance(0.01f));

        RootSolverSettings coarseAoASolverSettings = new RootSolverSettings(
            WindTunnelWindow.Instance.solverSettings,
            RootSolver.Tolerance(1 * Mathf.PI / 180));

        RootSolverSettings fineAoASolverSettings = new RootSolverSettings(
            WindTunnelWindow.Instance.solverSettings,
            RootSolver.LeftGuessBound(-2 * Mathf.PI / 180),
            RootSolver.RightGuessBound(2 * Mathf.PI / 180),
            RootSolver.ShiftWithGuess(true));*/
        
        public PartCollection partCollection;

        private int count;
        public float totalMass = 0;
        public float dryMass = 0;
        public float relativeWingArea = 0;
        public int stage = 0;

        public FloatCurve maxAoA = null;
        public static List<float> AoAMachs = null;

        public override bool ThreadSafe => true;

        public override float Mass { get => totalMass; }

        public override bool ThrustIsConstantWithAoA => partCollection.partCollections.Count == 0;

        public override float Area => relativeWingArea;

        public Vector3 GetAeroForce(Conditions conditions, float AoA, float pitchInput, out Vector3 torque, Vector3 torquePoint)
        {
            return partCollection.GetAeroForce(InflowVect(AoA) * conditions.speed, conditions, pitchInput, out torque, torquePoint);
        }
        public override Vector3 GetAeroForce(Conditions conditions, float AoA, float pitchInput = 0)
        {
#if ENABLE_PROFILER
            UnityEngine.Profiling.Profiler.BeginSample("SimulatedVessel.GetAeroForce(Conditions, float, float)");
#endif
            var value = GetAeroForce(conditions, AoA, pitchInput, out _, Vector3.zero);
#if ENABLE_PROFILER
            UnityEngine.Profiling.Profiler.EndSample();
#endif
            return value;
        }

        public Vector3 GetLiftForce(Conditions conditions, float AoA, float pitchInput, out Vector3 torque, Vector3 torquePoint)
        {
            return partCollection.GetLiftForce(InflowVect(AoA) * conditions.speed, conditions, pitchInput, out torque, torquePoint);
        }
        public override Vector3 GetLiftForce(Conditions conditions, float AoA, float pitchInput = 0)
        {
#if ENABLE_PROFILER
            UnityEngine.Profiling.Profiler.BeginSample("SimulatedVessel.GetLiftFoce(Conditions, float, float)");
#endif
            var value = GetLiftForce(conditions, AoA, pitchInput, out _, Vector3.zero);
#if ENABLE_PROFILER
            UnityEngine.Profiling.Profiler.EndSample();
#endif
            return value;
        }

        // TODO: Add ITorqueProvider and thrust effect on torque
        public override float GetAoA(Conditions conditions, float offsettingForce, bool useThrust = true, bool dryTorque = false, float guess = float.NaN, float pitchInputGuess = float.NaN, bool lockPitchInput = false, float tolerance = 0.0003f)
        {
#if ENABLE_PROFILER
            UnityEngine.Profiling.Profiler.BeginSample("SimulatedVessel.GetAoA(Conditions, float, bool, bool, float, float, bool)");
#endif
            Vector3 thrustForce = useThrust ? this.GetThrustForce(conditions) : Vector3.zero;
            float value;

            if (!accountForControls)
                value = base.GetAoA(conditions, offsettingForce, useThrust, dryTorque, guess, 0, true, tolerance);
            else if (lockPitchInput)
                value = base.GetAoA(conditions, offsettingForce, useThrust, dryTorque, guess, pitchInputGuess, lockPitchInput, tolerance);
            else
            {
                float approxAoA = GetAoA(conditions, offsettingForce, useThrust, dryTorque, guess, pitchInputGuess, true, 1 * Mathf.Deg2Rad);
                value = base.GetAoA(conditions, offsettingForce, useThrust, dryTorque, approxAoA, pitchInputGuess, lockPitchInput, tolerance);
            }
#if ENABLE_PROFILER
            UnityEngine.Profiling.Profiler.EndSample();
#endif
            return value;
        }

        // TODO: Add ITorqueProvider and thrust effect on torque
        public override float GetPitchInput(Conditions conditions, float AoA, bool dryTorque = false, float guess = float.NaN, float tolerance = 0.0003f)
        {
#if ENABLE_PROFILER
            UnityEngine.Profiling.Profiler.BeginSample("SimulatedVessel.GetPitchInput(Conditions, float, bool, float)");
#endif
            Vector3 inflow = InflowVect(AoA) * conditions.speed;
            partCollection.GetAeroForceStatic(inflow, conditions, out Vector3 staticTorque, dryTorque ? CoM_dry : CoM);
            float staticPitchTorque = staticTorque.x;
            float value;
            Accord.Math.Optimization.BrentSearch solver = new Accord.Math.Optimization.BrentSearch((input) =>
            {
                partCollection.GetAeroForceDynamic(inflow, conditions, (float)input, out Vector3 torque, dryTorque ? CoM_dry : CoM);
                return torque.x + staticPitchTorque;
            }, -0.3, 0.3, tolerance);
            if (solver.FindRoot())
                value = (float)solver.Solution;
            else
            {
                solver.LowerBound = -1;
                solver.UpperBound = 1;
                if (solver.FindRoot())
                    value = (float)solver.Solution;
                else if (this.GetAeroTorque(conditions, AoA, 0, dryTorque).x > 0)
                    value = -1;
                else
                    value = 1;
            }
#if ENABLE_PROFILER
            UnityEngine.Profiling.Profiler.EndSample();
#endif
            return value;
        }

        // Since, on Kerbin at least, speed of sound doesn't vary with altitude
        public override float GetMaxAoA(Conditions conditions, out float lift, float guess = float.NaN, float tolerance = 0.0003F)
        {
#if ENABLE_PROFILER
            UnityEngine.Profiling.Profiler.BeginSample("SimulatedVessel.GetMaxAoA(Conditions, float, float, float)");
#endif
            // Rotating parts ruin everything... Because their mach number is the sum of the inflow and their rotation,
            // the FloatCurve method isn't valid.
            if (partCollection.partCollections.Count > 0)
                return base.GetMaxAoA(conditions, out lift, guess, tolerance);

            if (!(conditions.body.bodyName.Equals("Kerbin", StringComparison.InvariantCultureIgnoreCase) ||
                conditions.body.bodyName.Equals("Laythe", StringComparison.InvariantCultureIgnoreCase)))
                return base.GetMaxAoA(conditions, out lift, guess, tolerance);
            if (maxAoA == null)
                InitMaxAoA(conditions.body, conditions.altitude);
            float aoa = maxAoA.Evaluate(conditions.mach);
            lift = GetLiftForceMagnitude(conditions, aoa, 1);
#if ENABLE_PROFILER
            UnityEngine.Profiling.Profiler.EndSample();
#endif
            return aoa;
        }

        public override Vector3 GetAeroTorque(Conditions conditions, float AoA, float pitchInput = 0, bool dryTorque = false)
        {
            GetAeroForce(conditions, AoA, pitchInput, out Vector3 torque, dryTorque ? CoM_dry : CoM);
            return torque;
        }
        
        public override void GetAeroCombined(Conditions conditions, float AoA, float pitchInput, out Vector3 forces, out Vector3 torques, bool dryTorque = false)
        {
            forces = GetAeroForce(conditions, AoA, pitchInput, out torques, dryTorque ? CoM_dry : CoM);
        }

        public override Vector3 GetThrustForce(float mach, float atmDensity, float atmPressure, bool oxygenPresent)
        {
            return partCollection.GetThrustForce(mach, atmDensity, atmPressure, oxygenPresent);
        }
        
        public override float GetFuelBurnRate(float mach, float atmDensity, float atmPressure, bool oxygenPresent)
        {
            return partCollection.GetFuelBurnRate(mach, atmDensity, atmPressure, oxygenPresent);
        }

        private static readonly Pool<SimulatedVessel> pool = new Pool<SimulatedVessel>(Create, Reset);

        private static SimulatedVessel Create()
        {
            return new SimulatedVessel();
        }

        public void Release()
        {
            lock (pool)
                pool.Release(this);
        }

        private static void Reset(SimulatedVessel obj)
        {
            obj.partCollection.Release();
            obj.partCollection = null;
            obj.maxAoA = null;
        }

        public static SimulatedVessel Borrow(IShipconstruct v)
        {
            SimulatedVessel vessel;
            // This lock is more expansive than it needs to be.
            // There is still a race condition within Init that causes
            // extra drag in the simulation, but this section is not a
            // performance bottleneck and so further refinement is #TODO.
            lock (pool)
            {
                vessel = pool.Borrow();
                vessel.Init(v);
            }
            return vessel;
        }

        public static SimulatedVessel BorrowClone(SimulatedVessel vessel)
        {
            SimulatedVessel clone;
            lock (pool)
                clone = pool.Borrow();
            clone.InitClone(vessel);
            return clone;
        }

        private void Init(IShipconstruct v)
        {
            totalMass = 0;
            dryMass = 0;
            CoM = Vector3.zero;
            CoM_dry = Vector3.zero;
            relativeWingArea = 0;
            stage = 0;

            List<Part> oParts = v.Parts;
            List<SimulatedPart> variableDragParts_ctrls = new List<SimulatedPart>();
            count = oParts.Count;

            if (HighLogic.LoadedSceneIsEditor)
            {
                for (int i = 0; i < v.Parts.Count; i++)
                {
                    Part p = v.Parts[i];
                    if (p.dragModel == Part.DragModel.CUBE && !p.DragCubes.None)
                    {
                        DragCubeList cubes = p.DragCubes;
                        lock (cubes)
                        {
                            DragCubeList.CubeData p_drag_data = new DragCubeList.CubeData();

                            try
                            {
                                cubes.SetDragWeights();
                                cubes.SetPartOcclusion();
                                cubes.AddSurfaceDragDirection(-Vector3.forward, 0, ref p_drag_data);
                            }
                            catch (Exception)
                            {
                                cubes.SetDrag(Vector3.forward, 0);
                                cubes.ForceUpdate(true, true);
                                cubes.SetDragWeights();
                                cubes.SetPartOcclusion();
                                cubes.AddSurfaceDragDirection(-Vector3.forward, 0, ref p_drag_data);
                                //Debug.Log(String.Format("Trajectories: Caught NRE on Drag Initialization.  Should be fixed now.  {0}", e));
                            }
                        }
                    }
                }
            }

            bool lgWarning = false;
            for (int i = 0; i < count; i++)
            {
                if (!lgWarning)
                {
                    ModuleWheels.ModuleWheelDeployment gear = oParts[i].FindModuleImplementing<ModuleWheels.ModuleWheelDeployment>();
                    bool forcedRetract = !oParts[i].ShieldedFromAirstream && gear != null && gear.Position > 0;

                    if (forcedRetract)
                        lgWarning = true;
                }
            }

            // Recursively add all parts to collections
            // What a crazy change to make just to accomodate rotating parts!
            partCollection = PartCollection.BorrowWithoutAdding(this);
            partCollection.AddPart(oParts[0]);

            CoM /= totalMass;
            CoM_dry /= dryMass;

            partCollection.origin = CoM;

            if (relativeWingArea == 0)
            {
                ScreenMessages.PostScreenMessage("No wings found, using a reference area of 1.", 5, ScreenMessageStyle.UPPER_CENTER);
                relativeWingArea = 1;
            }

            //if (lgWarning)
                //ScreenMessages.PostScreenMessage("Landing gear deployed, results may not be accurate.", 5, ScreenMessageStyle.UPPER_CENTER);
        }

        private void InitClone(SimulatedVessel vessel)
        {
            totalMass = vessel.totalMass;
            dryMass = vessel.dryMass;
            CoM = vessel.CoM;
            CoM_dry = vessel.CoM_dry;
            relativeWingArea = vessel.relativeWingArea;
            stage = vessel.stage;
            count = vessel.count;
            maxAoA = vessel.maxAoA?.Clone();

            partCollection = PartCollection.BorrowClone(this, vessel);
        }

        public void InitMaxAoA(CelestialBody body, float altitude = 0)
        {
            // If there are rotating parts, this won't ever come in handy so it's not worth the time.
            if (partCollection.partCollections.Count > 0)
                return;
#if ENABLE_PROFILER
            UnityEngine.Profiling.Profiler.BeginSample("SimulatedVessel.InitMaxAoA()");
#endif
            maxAoA = new FloatCurve();
            FindAoAMachs();
            const float machStep = 0.002f;
            Conditions conditions = new Conditions(body, 0, altitude);

            for (int i = 0; i < AoAMachs.Count; i++)
            {
                float inTangent = 0;
                float outTangent = 0;

                Conditions stepConditions = new Conditions(body, conditions.speedOfSound * AoAMachs[i], altitude);
                float stepMaxAoA = base.GetMaxAoA(stepConditions, out _, 30 * Mathf.Deg2Rad);

                if (i > 0)
                {
                    Conditions inConditions = new Conditions(body, conditions.speedOfSound * (AoAMachs[i] - machStep), altitude);
                    inTangent = (stepMaxAoA - base.GetMaxAoA(inConditions, out _, 30 * Mathf.Deg2Rad)) / machStep;
                }
                if (i < AoAMachs.Count - 1)
                {
                    Conditions outConditions = new Conditions(body, conditions.speedOfSound * (AoAMachs[i] + machStep), altitude);
                    outTangent = (base.GetMaxAoA(outConditions, out _, 30 * Mathf.Deg2Rad) - stepMaxAoA) / machStep;
                }
                maxAoA.Add(AoAMachs[i], stepMaxAoA, inTangent, outTangent);
            }
#if ENABLE_PROFILER
            UnityEngine.Profiling.Profiler.EndSample();
#endif
        }

        private void FindAoAMachs()
        {
            if (AoAMachs != null)
                return;

            AoAMachs = new List<float>();
            foreach (var curve in PhysicsGlobals.LiftingSurfaceCurves.Values)
                foreach (Keyframe key in curve.liftMachCurve.Curve.keys)
                    if (!AoAMachs.Contains(key.time))
                        AoAMachs.Add(key.time);
            AoAMachs.Sort();
        }
    }
}
