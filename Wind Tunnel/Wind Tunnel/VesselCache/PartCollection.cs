using Smooth.Pools;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KerbalWindTunnel.VesselCache
{
    public class PartCollection
    {
        public SimulatedVessel parentVessel;
        public List<SimulatedPart> parts = new List<SimulatedPart>();
        public List<SimulatedLiftingSurface> surfaces = new List<SimulatedLiftingSurface>();
        public List<SimulatedControlSurface> ctrls = new List<SimulatedControlSurface>();
        public List<SimulatedEngine> engines = new List<SimulatedEngine>();
        public List<PartCollection> partCollections = new List<PartCollection>();
        public Vector3 origin;

        #region AeroPredictor Methods

        public virtual Vector3 GetAeroForce(Vector3 inflow, AeroPredictor.Conditions conditions, float AoA, float pitchInput, out Vector3 torque, Vector3 torquePoint)
        {
            Vector3 aeroForce = Vector3.zero;
            torque = Vector3.zero;

            for (int i = parts.Count - 1; i >= 0; i--)
            {
                if (parts[i].shieldedFromAirstream)
                    continue;
                aeroForce += parts[i].GetAero(inflow, conditions.mach, conditions.pseudoReDragMult, out Vector3 pTorque, torquePoint);
                torque += pTorque;
            }
            for (int i = surfaces.Count - 1; i >= 0; i--)
            {
                if (surfaces[i].part.shieldedFromAirstream)
                    continue;
                aeroForce += surfaces[i].GetForce(inflow, conditions.mach, out Vector3 pTorque, torquePoint);
                torque += pTorque;
            }
            for (int i = ctrls.Count - 1; i >= 0; i--)
            {
                if (ctrls[i].part.shieldedFromAirstream)
                    continue;
                aeroForce += ctrls[i].GetForce(inflow, conditions.mach, pitchInput, conditions.pseudoReDragMult, out Vector3 pTorque, torquePoint);
                torque += pTorque;
            }
            for (int i = partCollections.Count - 1; i >=0; i--)
            {
                aeroForce += partCollections[i].GetAeroForce(inflow, conditions, AoA, pitchInput, out Vector3 pTorque, torquePoint);
                torque += pTorque;
            }

            //float Q = 0.0005f * conditions.atmDensity * conditions.speed * conditions.speed;
            //torque *= Q;
            return aeroForce; // * Q;
        }

        public virtual Vector3 GetLiftForce(Vector3 inflow, AeroPredictor.Conditions conditions, float AoA, float pitchInput, out Vector3 torque, Vector3 torquePoint)
        {
            Vector3 aeroForce = Vector3.zero;
            torque = Vector3.zero;

            for (int i = parts.Count - 1; i >= 0; i--)
            {
                if (parts[i].shieldedFromAirstream)
                    continue;
                aeroForce += parts[i].GetLift(inflow, conditions.mach, out Vector3 pTorque, torquePoint);
                torque += pTorque;
            }
            for (int i = surfaces.Count - 1; i >= 0; i--)
            {
                if (surfaces[i].part.shieldedFromAirstream)
                    continue;
                aeroForce += surfaces[i].GetLift(inflow, conditions.mach, out Vector3 pTorque, torquePoint);
                torque += pTorque;
            }
            for (int i = ctrls.Count - 1; i >= 0; i--)
            {
                if (ctrls[i].part.shieldedFromAirstream)
                    continue;
                aeroForce += ctrls[i].GetLift(inflow, conditions.mach, pitchInput, out Vector3 pTorque, torquePoint);
                torque += pTorque;
            }
            for (int i = partCollections.Count - 1; i >= 0; i--)
            {
                aeroForce += partCollections[i].GetLiftForce(inflow, conditions, AoA, pitchInput, out Vector3 pTorque, torquePoint);
                torque += pTorque;
            }

            //float Q = 0.0005f * conditions.atmDensity * conditions.speed * conditions.speed;
            //torque *= Q;
            return aeroForce; // * Q;
        }

        public virtual Vector3 GetAeroTorque(Vector3 inflow, AeroPredictor.Conditions conditions, float AoA, Vector3 torquePoint, float pitchInput = 0)
        {
            GetAeroForce(inflow, conditions, AoA, pitchInput, out Vector3 torque, torquePoint);
            return torque;
        }

        public virtual void GetAeroCombined(Vector3 inflow, AeroPredictor.Conditions conditions, float AoA, float pitchInput, out Vector3 forces, out Vector3 torques, Vector3 torquePoint)
        {
            forces = GetAeroForce(inflow, conditions, AoA, pitchInput, out torques, torquePoint);
        }

        public virtual Vector3 GetThrustForce(float mach, float atmDensity, float atmPressure, bool oxygenPresent)
        {
            Vector3 thrust = Vector3.zero;
            for (int i = engines.Count - 1; i >= 0; i--)
            {
                thrust += engines[i].GetThrust(mach, atmDensity, atmPressure, oxygenPresent);
            }
            return thrust;
        }

        public virtual float GetFuelBurnRate(float mach, float atmDensity, float atmPressure, bool oxygenPresent)
        {
            float burnRate = 0;
            for (int i = engines.Count - 1; i >= 0; i--)
            {
                burnRate += engines[i].GetFuelBurnRate(mach, atmDensity);
            }
            return burnRate;
        }

        #endregion

        #region Pool Methods

        private static readonly Pool<PartCollection> pool = new Pool<PartCollection>(Create, Reset);

        public static int PoolSize
        {
            get { return pool.Size; }
        }

        private static PartCollection Create()
        {
            return new PartCollection();
        }

        public static void Release(List<PartCollection> objList)
        {
            for (int i = 0; i < objList.Count; ++i)
            {
                objList[i].Release();
            }
        }

        public virtual void Release()
        {
            lock (pool)
                pool.Release(this);
        }

        protected static void Reset(PartCollection obj)
        {
            SimulatedPart.Release(obj.parts);
            obj.parts.Clear();
            SimulatedLiftingSurface.Release(obj.surfaces);
            obj.surfaces.Clear();
            SimulatedControlSurface.Release(obj.ctrls);
            obj.ctrls.Clear();
            SimulatedEngine.Release(obj.engines);
            obj.engines.Clear();
            PartCollection.Release(obj.partCollections);
            obj.partCollections.Clear();
        }

        public static PartCollection Borrow(SimulatedVessel v, Part originPart)
        {
            PartCollection collection;
            // This lock is more expansive than it needs to be.
            // There is still a race condition within Init that causes
            // extra drag in the simulation, but this section is not a
            // performance bottleneck and so further refinement is #TODO.
            lock (pool)
            {
                collection = pool.Borrow();
                collection.parentVessel = v;
                collection.AddPart(originPart);
            }
            return collection;
        }

        protected void AddPart(Part part)
        {
            if (parts.Count > 0 && part.HasModuleImplementing<Expansions.Serenity.ModuleRoboticServoRotor>())
            {
                partCollections.Add(RotorPartCollection.Borrow(parentVessel, part));
                return;
            }

            SimulatedPart simulatedPart = SimulatedPart.Borrow(part, parentVessel);
            parts.Add(simulatedPart);

            parentVessel.totalMass += simulatedPart.totalMass;
            parentVessel.dryMass += simulatedPart.dryMass;
            parentVessel.CoM += simulatedPart.totalMass * simulatedPart.CoM;
            parentVessel.CoM_dry += simulatedPart.dryMass * simulatedPart.CoM;

            bool variableDragCube_Ctrl = false;

            ModuleLiftingSurface liftingSurface = part.FindModuleImplementing<ModuleLiftingSurface>();
            if (liftingSurface != null)
            {
                part.hasLiftModule = true;
                SimulatedLiftingSurface surface;
                if (liftingSurface is ModuleControlSurface ctrlSurface)
                {
                    surface = SimulatedControlSurface.Borrow(ctrlSurface, simulatedPart);
                    ctrls.Add((SimulatedControlSurface)surface);

                    // Controls change their drag cubes with deployment and so we can't precalculate them.
                    // The effect of their drag cubes is captured in the methods for SimulatedControlSurface
                    variableDragCube_Ctrl = true;

                    if (ctrlSurface.ctrlSurfaceArea < 1)
                    {
                        surface = SimulatedLiftingSurface.Borrow(ctrlSurface, simulatedPart);
                        surfaces.Add(surface);
                    }
                }
                else
                {
                    surface = SimulatedLiftingSurface.Borrow(liftingSurface, simulatedPart);
                    surfaces.Add(surface);
                }
                Math.Abs(0);
                parentVessel.relativeWingArea += surface.deflectionLiftCoeff * Math.Abs(surface.liftVector[1]);
            }

            List<ITorqueProvider> torqueProviders = part.FindModulesImplementing<ITorqueProvider>();
            // TODO: Add them to a list.

            if (part.inverseStage > parentVessel.stage)
            {
                // Recursively clear all engines - there's an earlier stage active.
                parentVessel.partCollection.ClearEngines();
                parentVessel.stage = part.inverseStage;
            }
            if (part.inverseStage >= parentVessel.stage)
            {
                MultiModeEngine multiMode = part.FindModuleImplementing<MultiModeEngine>();
                if (multiMode != null)
                {
                    engines.Add(SimulatedEngine.Borrow(part.FindModulesImplementing<ModuleEngines>().Find(engine => engine.engineID == multiMode.mode), simulatedPart));
                }
                else
                {
                    ModuleEngines engine = part.FindModulesImplementing<ModuleEngines>().FirstOrDefault();
                    if (engine != null)
                        engines.Add(SimulatedEngine.Borrow(engine, simulatedPart));
                }
            }

            if (variableDragCube_Ctrl)
            {
                simulatedPart.Release();
                parts.Remove(simulatedPart);
            }

            for (int i = part.children.Count - 1; i >= 0; i--)
            {
                AddPart(part.children[i]);
            }
        }

        private void ClearEngines()
        {
            SimulatedEngine.Release(engines);
            engines.Clear();
            for (int i = partCollections.Count - 1; i >= 0; i--)
                partCollections[i].ClearEngines();
        }

#endregion
    }
}
