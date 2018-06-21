using System;
using System.Collections.Generic;
using System.Linq;
using Smooth.Pools;
using UnityEngine;

namespace KerbalWindTunnel.VesselCache
{
    public class SimulatedVessel : AeroPredictor
    {
        public List<SimulatedPart> parts = new List<SimulatedPart>();
        public List<SimulatedLiftingSurface> surfaces = new List<SimulatedLiftingSurface>();
        public List<SimulatedEngine> engines = new List<SimulatedEngine>();

        private int count;
        public float totalMass = 0;

        private SimCurves simCurves;

        public override bool ThreadSafe { get { return true; } }

        public override float Mass { get { return totalMass; } }

        public override Vector3 GetAeroForce(CelestialBody body, float speed, float altitude, float AoA)
        {
            float atmDensity, mach;
            lock (body)
            {
                float atmPressure = (float)body.GetPressure(altitude);
                atmDensity = (float)Extensions.KSPClassExtensions.GetDensity(body, altitude);
                mach = (float)(speed / body.GetSpeedOfSound(atmPressure, atmDensity));
            }

            float pseudoReDragMult;
            lock (simCurves.DragCurvePseudoReynolds)
                pseudoReDragMult = simCurves.DragCurvePseudoReynolds.Evaluate(atmDensity * speed);

            return this.GetAeroForce(body, speed, altitude, AoA, mach, atmDensity, pseudoReDragMult);
        }
        public override Vector3 GetAeroForce(CelestialBody body, float speed, float altitude, float AoA, float mach)
        {
            float pseudoReDragMult, atmDensity;
            lock (body)
                atmDensity = (float)Extensions.KSPClassExtensions.GetDensity(body, altitude);
            lock (simCurves.DragCurvePseudoReynolds)
                pseudoReDragMult = simCurves.DragCurvePseudoReynolds.Evaluate(atmDensity * speed);

            return this.GetAeroForce(body, speed, altitude, AoA, mach, atmDensity, pseudoReDragMult);
        }
        public override Vector3 GetAeroForce(CelestialBody body, float speed, float altitude, float AoA, float mach, float atmDensity)
        {
            float pseudoReDragMult;
            lock (simCurves.DragCurvePseudoReynolds)
                pseudoReDragMult = simCurves.DragCurvePseudoReynolds.Evaluate(atmDensity * speed);
            return this.GetAeroForce(body, speed, altitude, AoA, mach, atmDensity, pseudoReDragMult);
        }
        public override Vector3 GetAeroForce(CelestialBody body, float speed, float altitude, float AoA, float mach, float atmDensity, float pseudoReDragMult)
        {
            Vector3 aeroForce = Vector3.zero;
            Vector3 inflow = InflowVect(AoA);

            for (int i = parts.Count - 1; i >= 0; i--)
            {
                if (parts[i].shieldedFromAirstream)
                    continue;
                aeroForce += parts[i].GetAero(inflow, mach, pseudoReDragMult);
            }
            for (int i = surfaces.Count - 1; i >= 0; i--)
            {
                if (surfaces[i].part.shieldedFromAirstream)
                    continue;
                aeroForce += surfaces[i].GetForce(inflow, mach);
            }
            return aeroForce * 0.0005f * atmDensity * speed * speed;
        }

        public override Vector3 GetLiftForce(CelestialBody body, float speed, float altitude, float AoA)
        {
            float mach, atmDensity;
            lock (body)
            {
                float atmPressure = (float)body.GetPressure(altitude);
                atmDensity = (float)Extensions.KSPClassExtensions.GetDensity(body, altitude);
                mach = (float)(speed / body.GetSpeedOfSound(atmPressure, atmDensity));
            }
            /*float atmPressure = simCurves.GetPressure(altitude);
            float atmDensity = simCurves.GetDensity(altitude);
            mach = (float)(speed / body.GetSpeedOfSound(atmPressure, atmDensity));*/

            return this.GetLiftForce(body, speed, altitude, AoA, mach, atmDensity);
        }
        public override Vector3 GetLiftForce(CelestialBody body, float speed, float altitude, float AoA, float mach, float atmDensity)
        {
            Vector3 aeroForce = Vector3.zero;
            Vector3 inflow = InflowVect(AoA);

            for (int i = parts.Count - 1; i >= 0; i--)
            {
                aeroForce += parts[i].GetLift(inflow, mach);
            }
            for (int i = surfaces.Count - 1; i >= 0; i--)
            {
                aeroForce += surfaces[i].GetLift(inflow, mach);
            }
            return aeroForce * 0.0005f * atmDensity * speed * speed;
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

            List<Part> oParts = v.Parts;
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

            int stage = 0;
            for (int i = 0; i < count; i++)
            {
                SimulatedPart simulatedPart = SimulatedPart.Borrow(oParts[i]);
                parts.Add(simulatedPart);
                totalMass += simulatedPart.totalMass;

                if (!oParts[i].ShieldedFromAirstream)
                {
                    ModuleWheels.ModuleWheelDeployment gear = oParts[i].FindModuleImplementing<ModuleWheels.ModuleWheelDeployment>();
                    if (gear != null && gear.Position == gear.deployedPosition)
                    {
                        // Display a message on screen that drag may not be accurate.
                    }
                }

                ModuleLiftingSurface liftingSurface = oParts[i].FindModuleImplementing<ModuleLiftingSurface>();
                if (liftingSurface != null)
                {
                    surfaces.Add(SimulatedLiftingSurface.Borrow(liftingSurface, simulatedPart));
                    parts[i].hasLiftModule = true;
                }

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

        /*public Vector3 Drag(Vector3 localVelocity, float dynamicPressurekPa, float mach)
        {
            Vector3 drag = Vector3.zero;

            float dragFactor = dynamicPressurekPa * PhysicsGlobals.DragCubeMultiplier * PhysicsGlobals.DragMultiplier;

            for (int i = 0; i < count; i++)
            {
                drag += parts[i].Drag(localVelocity, dragFactor, mach);
            }

            return -localVelocity.normalized * drag.magnitude;
        }

        public Vector3 Lift(Vector3 localVelocity, float dynamicPressurekPa, float mach)
        {
            Vector3 lift = Vector3.zero;

            float liftFactor = dynamicPressurekPa * simCurves.LiftMachCurve.Evaluate(mach);

            for (int i = 0; i < count; i++)
            {
                lift += parts[i].Lift(localVelocity, liftFactor);
            }
            return lift;
        }*/
    }
}
