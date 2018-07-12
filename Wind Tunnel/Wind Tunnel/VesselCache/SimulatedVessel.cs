using System;
using System.Collections.Generic;
using System.Linq;
using KerbalWindTunnel.RootSolvers;
using Smooth.Pools;
using UnityEngine;

namespace KerbalWindTunnel.VesselCache
{
    public class SimulatedVessel : AeroPredictor
    {
        public static bool accountForControls = false;

        RootSolverSettings pitchInputSolverSettings = new RootSolverSettings(
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
            RootSolver.ShiftWithGuess(true));
        
        public List<SimulatedPart> parts = new List<SimulatedPart>();
        public List<SimulatedLiftingSurface> surfaces = new List<SimulatedLiftingSurface>();
        public List<SimulatedControlSurface> ctrls = new List<SimulatedControlSurface>();
        public List<SimulatedEngine> engines = new List<SimulatedEngine>();

        private int count;
        public float totalMass = 0;
        public Vector3 CoM;

        private SimCurves simCurves;

        public override bool ThreadSafe { get { return true; } }

        public override float Mass { get { return totalMass; } }
        public Vector3 GetAeroForce(Conditions conditions, float AoA, float pitchInput, out Vector3 torque)
        {
            Vector3 aeroForce = Vector3.zero;
            Vector3 inflow = InflowVect(AoA);
            torque = Vector3.zero;

            for (int i = parts.Count - 1; i >= 0; i--)
            {
                if (parts[i].shieldedFromAirstream)
                    continue;
                aeroForce += parts[i].GetAero(inflow, conditions.mach, conditions.pseudoReDragMult, out Vector3 pTorque);
                torque += pTorque;
            }
            for (int i = surfaces.Count - 1; i >= 0; i--)
            {
                if (surfaces[i].part.shieldedFromAirstream)
                    continue;
                aeroForce += surfaces[i].GetForce(inflow, conditions.mach, out Vector3 pTorque);
                torque += pTorque;
            }
            for (int i = ctrls.Count - 1; i >=0; i--)
            {
                if (ctrls[i].part.shieldedFromAirstream)
                    continue;
                aeroForce += ctrls[i].GetForce(inflow, conditions.mach, pitchInput, conditions.pseudoReDragMult, out Vector3 pTorque);
                torque += pTorque;
            }
            float Q = 0.0005f * conditions.atmDensity * conditions.speed * conditions.speed;
            torque *= Q;
            return aeroForce * Q;
        }
        public override Vector3 GetAeroForce(Conditions conditions, float AoA, float pitchInput = 0)
        {
            return GetAeroForce(conditions, AoA, pitchInput, out _);
        }
        
        public Vector3 GetLiftForce(Conditions conditions, float AoA, float pitchInput, out Vector3 torque)
        {
            Vector3 aeroForce = Vector3.zero;
            Vector3 inflow = InflowVect(AoA);
            torque = Vector3.zero;

            for (int i = parts.Count - 1; i >= 0; i--)
            {
                if (parts[i].shieldedFromAirstream)
                    continue;
                aeroForce += parts[i].GetLift(inflow, conditions.mach, out Vector3 pTorque);
                torque += pTorque;
            }
            for (int i = surfaces.Count - 1; i >= 0; i--)
            {
                if (surfaces[i].part.shieldedFromAirstream)
                    continue;
                aeroForce += surfaces[i].GetLift(inflow, conditions.mach, out Vector3 pTorque);
                torque += pTorque;
            }
            for (int i = ctrls.Count - 1; i >= 0; i--)
            {
                if (ctrls[i].part.shieldedFromAirstream)
                    continue;
                aeroForce += ctrls[i].GetLift(inflow, conditions.mach, pitchInput, out Vector3 pTorque);
                torque += pTorque;
            }
            float Q = 0.0005f * conditions.atmDensity * conditions.speed * conditions.speed;
            torque *= Q;
            return aeroForce * Q;
        }
        public override Vector3 GetLiftForce(Conditions conditions, float AoA, float pitchInput = 0)
        {
            return GetLiftForce(conditions, AoA, pitchInput, out _);
        }

        public override float GetAoA(RootSolver solver, Conditions conditions, float offsettingForce, bool useThrust = true)
        {
            return this.GetAoA(solver, conditions, offsettingForce, out _, useThrust);
        }

        // TODO: Add ITorqueProvider and thrust effect on torque
        public override float GetAoA(RootSolver solver, Conditions conditions, float offsettingForce, out float pitchInput, bool useThrust = true)
        {
            Vector3 thrustForce = useThrust ? this.GetThrustForce(conditions) : Vector3.zero;

            pitchInput = 0;
            if (!accountForControls)
                return solver.Solve(
                    (aoa) => AeroPredictor.GetLiftForceMagnitude(this.GetLiftForce(conditions, aoa, 0) + thrustForce, aoa) - offsettingForce,
                    0, WindTunnelWindow.Instance.solverSettings);

            float pi = 0;
            float approxAoA = solver.Solve(
                (aoa) => AeroPredictor.GetLiftForceMagnitude(this.GetLiftForce(conditions, aoa, pi) + thrustForce, aoa) - offsettingForce,
                0, coarseAoASolverSettings);
            float result = solver.Solve((aoa) =>
            {
                float netF = -offsettingForce;
                pi = solver.Solve((input) =>
                {
                    netF = AeroPredictor.GetLiftForceMagnitude(this.GetAeroForce(conditions, aoa, input, out Vector3 torque) + thrustForce, aoa) - offsettingForce;
                    return torque.x;
                }, pi, pitchInputSolverSettings);
                return netF;
            }, 0, fineAoASolverSettings);

            pitchInput = pi;
            return result;
        }

        // TODO: Add ITorqueProvider and thrust effect on torque
        public override float GetPitchInput(RootSolver solver, Conditions conditions, float AoA)
        {
            float pi = solver.Solve((input) => this.GetAeroTorque(conditions, AoA, input).x,
                0, pitchInputSolverSettings);
            return pi;
        }
        
        public override Vector3 GetAeroTorque(Conditions conditions, float AoA, float pitchInput = 0)
        {
            GetAeroForce(conditions, AoA, pitchInput, out Vector3 torque);
            return torque;
        }
        
        public override void GetAeroCombined(Conditions conditions, float AoA, float pitchInput, out Vector3 forces, out Vector3 torques)
        {
            forces = GetAeroForce(conditions, AoA, pitchInput, out torques);
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
        
        public override float GetFuelBurnRate(float mach, float atmDensity, float atmPressure, bool oxygenPresent)
        {
            float burnRate = 0;
            for (int i = engines.Count - 1; i >= 0; i--)
            {
                burnRate += engines[i].GetFuelBurnRate(mach, atmDensity, atmPressure, oxygenPresent);
            }
            return burnRate;
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
            pool.Release(this);
        }

        private static void Reset(SimulatedVessel obj)
        {
            SimulatedPart.Release(obj.parts);
            obj.parts.Clear();
            SimulatedLiftingSurface.Release(obj.surfaces);
            obj.surfaces.Clear();
            SimulatedControlSurface.Release(obj.ctrls);
            obj.ctrls.Clear();
            SimulatedEngine.Release(obj.engines);
            obj.engines.Clear();
        }

        public static SimulatedVessel Borrow(IShipconstruct v, SimCurves simCurves)
        {
            SimulatedVessel vessel = pool.Borrow();
            vessel.Init(v, simCurves);
            return vessel;
        }

        private void Init(IShipconstruct v, SimCurves _simCurves)
        {
            totalMass = 0;
            CoM = Vector3.zero;

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

            simCurves = _simCurves;

            if (parts.Capacity < count)
                parts.Capacity = count;

            bool lgWarning = false;
            int stage = 0;
            for (int i = 0; i < count; i++)
            {
                ModuleWheels.ModuleWheelDeployment gear = oParts[i].FindModuleImplementing<ModuleWheels.ModuleWheelDeployment>();
                bool forcedRetract = !oParts[i].ShieldedFromAirstream && gear != null && gear.Position > 0;
                float gearPosition = 0;

                if (forcedRetract)
                {
                    gearPosition = gear.Position;
                    oParts[i].DragCubes.SetCubeWeight("Retracted", 1);
                    oParts[i].DragCubes.SetCubeWeight("Deployed", 0);
                    lgWarning = true;
                }

                SimulatedPart simulatedPart = SimulatedPart.Borrow(oParts[i]);
                parts.Add(simulatedPart);
                totalMass += simulatedPart.totalMass;
                CoM += simulatedPart.totalMass * simulatedPart.CoM;

                if (forcedRetract)
                {
                    oParts[i].DragCubes.SetCubeWeight("Retracted", 1 - gearPosition);
                    oParts[i].DragCubes.SetCubeWeight("Deployed", gearPosition);
                }

                ModuleLiftingSurface liftingSurface = oParts[i].FindModuleImplementing<ModuleLiftingSurface>();
                if (liftingSurface != null)
                {
                    parts[i].hasLiftModule = true;
                    if (liftingSurface is ModuleControlSurface ctrlSurface)
                    {
                        ctrls.Add(SimulatedControlSurface.Borrow(ctrlSurface, simulatedPart));
                        variableDragParts_ctrls.Add(simulatedPart);
                        if (ctrlSurface.ctrlSurfaceArea < 1)
                            surfaces.Add(SimulatedLiftingSurface.Borrow(ctrlSurface, simulatedPart));
                    }
                    else
                        surfaces.Add(SimulatedLiftingSurface.Borrow(liftingSurface, simulatedPart));
                }

                List<ITorqueProvider> torqueProviders = oParts[i].FindModulesImplementing<ITorqueProvider>();
                // TODO: Add them to a list.

                if(oParts[i].inverseStage > stage)
                {
                    SimulatedEngine.Release(engines);
                    engines.Clear();
                    stage = oParts[i].inverseStage;
                }
                if (oParts[i].inverseStage >= stage)
                {
                    MultiModeEngine multiMode = oParts[i].FindModuleImplementing<MultiModeEngine>();
                    if (multiMode != null)
                    {
                        engines.Add(SimulatedEngine.Borrow(oParts[i].FindModulesImplementing<ModuleEngines>().Find(engine => engine.engineID == multiMode.mode), simulatedPart));
                    }
                    else
                    {
                        ModuleEngines engine = oParts[i].FindModulesImplementing<ModuleEngines>().FirstOrDefault();
                        if (engine != null)
                            engines.Add(SimulatedEngine.Borrow(engine, simulatedPart));
                    }
                }
            }
            CoM /= totalMass;

            if (lgWarning)
                ScreenMessages.PostScreenMessage("Landing gear deployed, results may not be accurate.", 5, ScreenMessageStyle.UPPER_CENTER);

            for (int i = 0; i < count; i++)
            {
                parts[i].CoM -= this.CoM;
                parts[i].CoL -= this.CoM;
                parts[i].CoP -= this.CoM;
            }

            parts.RemoveAll(part => variableDragParts_ctrls.Contains(part));
        }

        /*public static SimulatedVessel BorrowAndClone(SimulatedVessel v)
        {
            SimulatedVessel clonedVessel = pool.Borrow();
            clonedVessel.CloneFrom(v);
            return clonedVessel;
        }
        private void CloneFrom(SimulatedVessel v)
        {
            int num;
            num = v.parts.Count;
            for (int i = 0; i < num; i++)
            {
                this.parts.Add(v.parts[i].Clone());
            }
            num = v.surfaces.Count;
            for (int i = 0; i < num; i++)
            {
                this.surfaces.Add(v.surfaces[i].Clone());
            }
            num = v.engines.Count;
            for (int i = 0; i < num; i++)
            {
                this.engines.Add(v.engines[i].Clone());
            }
            this.totalMass = v.totalMass;
            this.simCurves = v.simCurves;
            this.count = v.count;
        }

        public override AeroPredictor Clone()
        {
            return SimulatedVessel.BorrowAndClone(this);
        }*/
    }
}
