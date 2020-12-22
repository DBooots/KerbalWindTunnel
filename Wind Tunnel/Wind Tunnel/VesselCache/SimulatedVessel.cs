using System;
using System.Collections.Generic;
using System.Linq;
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

        //public List<SimulatedPart> parts = new List<SimulatedPart>();
        //public List<SimulatedLiftingSurface> surfaces = new List<SimulatedLiftingSurface>();
        //public List<SimulatedControlSurface> ctrls = new List<SimulatedControlSurface>();
        //public List<SimulatedEngine> engines = new List<SimulatedEngine>();
        public PartCollection partCollection;

        private int count;
        public float totalMass = 0;
        public float dryMass = 0;
        public float relativeWingArea = 0;
        public int stage = 0;

        public override bool ThreadSafe { get { return true; } }

        public override float Mass { get { return totalMass; } }

        public override float Area => relativeWingArea;

        public Vector3 GetAeroForce(Conditions conditions, float AoA, float pitchInput, out Vector3 torque, Vector3 torquePoint)
        {
            Vector3 aeroForce;
            Vector3 inflow = InflowVect(AoA) * conditions.speed;

            aeroForce = partCollection.GetAeroForce(inflow, conditions, pitchInput, out torque, torquePoint);

            float Q = 0.0005f * conditions.atmDensity * conditions.speed * conditions.speed;
            torque *= Q;
            return aeroForce * Q;
        }
        public override Vector3 GetAeroForce(Conditions conditions, float AoA, float pitchInput = 0)
        {
            return GetAeroForce(conditions, AoA, pitchInput, out _, Vector3.zero);
        }
        
        public Vector3 GetLiftForce(Conditions conditions, float AoA, float pitchInput, out Vector3 torque, Vector3 torquePoint)
        {
            Vector3 aeroForce;
            Vector3 inflow = InflowVect(AoA) * conditions.speed;

            aeroForce = partCollection.GetAeroForce(inflow, conditions, pitchInput, out torque, torquePoint);
            
            float Q = 0.0005f * conditions.atmDensity * conditions.speed * conditions.speed;
            torque *= Q;
            return aeroForce * Q;
        }
        public override Vector3 GetLiftForce(Conditions conditions, float AoA, float pitchInput = 0)
        {
            return GetLiftForce(conditions, AoA, pitchInput, out _, Vector3.zero);
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

        // TODO: Add ITorqueProvider and thrust effect on torque
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

        public static int PoolSize
        {
            get { return pool.Size; }
        }

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
                // I'm not sure what effect calling ScreenMessages from a background thread will be.
                // Fortunately, anyone using this mod probably has at least one wing.
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

            partCollection = PartCollection.BorrowClone(this, vessel);
        }
    }
}
