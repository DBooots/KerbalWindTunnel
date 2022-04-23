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

        public PartCollection parentCollection;

        public List<SimulatedPart> parts = new List<SimulatedPart>();
        public List<SimulatedLiftingSurface> surfaces = new List<SimulatedLiftingSurface>();
        public List<SimulatedControlSurface> ctrls = new List<SimulatedControlSurface>();
        public List<SimulatedEngine> engines = new List<SimulatedEngine>();
        public List<PartCollection> partCollections = new List<PartCollection>();
        public Vector3 origin;

        #region AeroPredictor Methods

        public virtual Vector3 GetAeroForce(Vector3 inflow, AeroPredictor.Conditions conditions, float pitchInput, out Vector3 torque, Vector3 torquePoint)
        {
            Vector3 aeroForce = GetAeroForceStatic(inflow, conditions, out torque, torquePoint);
            aeroForce += GetAeroForceDynamic(inflow, conditions, pitchInput, out Vector3 pTorque, torquePoint);
            torque += pTorque;
            return aeroForce;
        }

        public virtual Vector3 GetAeroForceStatic(Vector3 inflow, AeroPredictor.Conditions conditions, out Vector3 torque, Vector3 torquePoint)
        {
            Vector3 aeroForce = Vector3.zero;
            torque = Vector3.zero;

            if (inflow.sqrMagnitude > 0)
            {
                Vector3 normalizedInflow = inflow.normalized;

                for (int i = parts.Count - 1; i >= 0; i--)
                {
                    if (parts[i].shieldedFromAirstream)
                        continue;
                    aeroForce += parts[i].GetAero(normalizedInflow, conditions.mach, conditions.pseudoReDragMult, out Vector3 pTorque, torquePoint);
                    torque += pTorque;
                }
                for (int i = surfaces.Count - 1; i >= 0; i--)
                {
                    if (surfaces[i].part.shieldedFromAirstream)
                        continue;
                    aeroForce += surfaces[i].GetForce(normalizedInflow, conditions.mach, out Vector3 pTorque, torquePoint);
                    torque += pTorque;
                }
                for (int i = ctrls.Count - 1; i >= 0; i--)
                {
                    if (ctrls[i].part.shieldedFromAirstream)
                        continue;
                    if (!ctrls[i].ignorePitch)
                        continue;
                    aeroForce += ctrls[i].GetForce(normalizedInflow, conditions.mach, conditions.pseudoReDragMult, out Vector3 pTorque, torquePoint);
                    torque += pTorque;
                }

                float Q = 0.0005f * conditions.atmDensity * inflow.sqrMagnitude;
                torque *= Q;
                aeroForce *= Q;
            }

            for (int i = partCollections.Count - 1; i >= 0; i--)
            {
                aeroForce += partCollections[i].GetAeroForceStatic(inflow, conditions, out Vector3 pTorque, torquePoint);
                torque += pTorque;
            }

            return aeroForce;
        }

        public virtual Vector3 GetAeroForceDynamic(Vector3 inflow, AeroPredictor.Conditions conditions, float pitchInput, out Vector3 torque, Vector3 torquePoint)
        {
            Vector3 aeroForce = Vector3.zero;
            torque = Vector3.zero;

            if (inflow.sqrMagnitude > 0)
            {
                Vector3 normalizedInflow = inflow.normalized;

                for (int i = ctrls.Count - 1; i >= 0; i--)
                {
                    if (ctrls[i].part.shieldedFromAirstream)
                        continue;
                    if (ctrls[i].ignorePitch)
                        continue;
                    aeroForce += ctrls[i].GetForce(normalizedInflow, conditions.mach, pitchInput, conditions.pseudoReDragMult, out Vector3 pTorque, torquePoint);
                    torque += pTorque;
                }

                float Q = 0.0005f * conditions.atmDensity * inflow.sqrMagnitude;
                torque *= Q;
                aeroForce *= Q;
            }

            for (int i = partCollections.Count - 1; i >= 0; i--)
            {
                aeroForce += partCollections[i].GetAeroForceDynamic(inflow, conditions, pitchInput, out Vector3 pTorque, torquePoint);
                torque += pTorque;
            }

            return aeroForce;
        }

        public virtual Vector3 GetLiftForce(Vector3 inflow, AeroPredictor.Conditions conditions, float pitchInput, out Vector3 torque, Vector3 torquePoint)
        {
            Vector3 aeroForce = Vector3.zero;
            torque = Vector3.zero;

            if (inflow.sqrMagnitude > 0)
            {
                Vector3 normalizedInflow = inflow.normalized;

                for (int i = parts.Count - 1; i >= 0; i--)
                {
                    if (parts[i].shieldedFromAirstream)
                        continue;
                    aeroForce += parts[i].GetLift(normalizedInflow, conditions.mach, out Vector3 pTorque, torquePoint);
                    torque += pTorque;
                }
                for (int i = surfaces.Count - 1; i >= 0; i--)
                {
                    if (surfaces[i].part.shieldedFromAirstream)
                        continue;
                    aeroForce += surfaces[i].GetLift(normalizedInflow, conditions.mach, out Vector3 pTorque, torquePoint);
                    torque += pTorque;
                }
                for (int i = ctrls.Count - 1; i >= 0; i--)
                {
                    if (ctrls[i].part.shieldedFromAirstream)
                        continue;
                    aeroForce += ctrls[i].GetLift(normalizedInflow, conditions.mach, pitchInput, out Vector3 pTorque, torquePoint);
                    torque += pTorque;
                }

                float Q = 0.0005f * conditions.atmDensity * inflow.sqrMagnitude;
                torque *= Q;
                aeroForce *= Q;
            }

            for (int i = partCollections.Count - 1; i >= 0; i--)
            {
                aeroForce += partCollections[i].GetLiftForce(inflow, conditions, pitchInput, out Vector3 pTorque, torquePoint);
                torque += pTorque;
            }

            return aeroForce;
        }

        public virtual Vector3 GetAeroTorque(Vector3 inflow, AeroPredictor.Conditions conditions, Vector3 torquePoint, float pitchInput = 0)
        {
            GetAeroForce(inflow, conditions, pitchInput, out Vector3 torque, torquePoint);
            return torque;
        }

        public virtual void GetAeroCombined(Vector3 inflow, AeroPredictor.Conditions conditions, float pitchInput, out Vector3 forces, out Vector3 torques, Vector3 torquePoint)
        {
            forces = GetAeroForce(inflow, conditions, pitchInput, out torques, torquePoint);
        }

        public virtual Vector3 GetThrustForce(Vector3 inflow, AeroPredictor.Conditions conditions)
        {
            Vector3 thrust = Vector3.zero;
            for (int i = engines.Count - 1; i >= 0; i--)
            {
                thrust += engines[i].GetThrust(conditions.mach, conditions.atmDensity, conditions.atmPressure, conditions.oxygenAvailable);
            }
            for (int i = partCollections.Count - 1; i >= 0; i--)
            {
                thrust += partCollections[i].GetThrustForce(inflow, conditions);
            }
            return thrust;
        }

        public virtual Vector3 GetThrustForce(Vector3 inflow, AeroPredictor.Conditions conditions, out Vector3 torque, Vector3 torquePoint)
        {
            Vector3 thrust = Vector3.zero;
            torque = Vector3.zero;
            for (int i = engines.Count - 1; i >= 0; i--)
            {
                Vector3 eThrust = engines[i].GetThrust(conditions.mach, conditions.atmDensity, conditions.atmPressure, conditions.oxygenAvailable);
                thrust += eThrust;
                torque += Vector3.Cross(eThrust, engines[i].thrustPoint - torquePoint);
            }
            for (int i = partCollections.Count - 1; i >= 0; i--)
            {
                thrust += partCollections[i].GetThrustForce(inflow, conditions, out Vector3 pTorque, torquePoint);
                torque += pTorque;
            }
            return thrust;
        }

        public virtual float GetFuelBurnRate(Vector3 inflow, AeroPredictor.Conditions conditions)
        {
            float burnRate = 0;
            for (int i = engines.Count - 1; i >= 0; i--)
            {
                burnRate += engines[i].GetFuelBurnRate(conditions.mach, conditions.atmDensity);
            }
            for (int i = partCollections.Count - 1; i >= 0; i--)
            {
                burnRate += partCollections[i].GetFuelBurnRate(inflow, conditions);
            }
            return burnRate;
        }

        #endregion

        #region Pool Methods

        private static readonly Pool<PartCollection> pool = new Pool<PartCollection>(Create, Reset);

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

        public static PartCollection Borrow(PartCollection parentCollection, Part originPart)
        {
            PartCollection collection = Borrow(parentCollection?.parentVessel, originPart);
            collection.parentCollection = parentCollection;
            return collection;
        }

        public static PartCollection Borrow(SimulatedVessel vessel, Part originPart)
        {
            PartCollection collection;
            lock (pool)
                collection = pool.Borrow();
            collection.parentVessel = vessel;
            collection.AddPart(originPart);
            return collection;
        }

        public static PartCollection BorrowWithoutAdding(SimulatedVessel vessel)
        {
            PartCollection collection;
            lock (pool)
                collection = pool.Borrow();
            collection.parentVessel = vessel;
            return collection;
        }

        public static PartCollection BorrowClone(SimulatedVessel vessel, SimulatedVessel vesselToClone)
        {
            PartCollection clone;
            lock (pool)
                clone = pool.Borrow();
            clone.parentVessel = vessel;
            clone.InitClone(vesselToClone.partCollection);
            return clone;
        }
        public static PartCollection BorrowClone(PartCollection collection, PartCollection parentCollection)
        {
            PartCollection clone;
            if (collection is RotorPartCollection)
                clone = RotorPartCollection.DirectBorrow();
            else
                lock (pool)
                    clone = pool.Borrow();
            clone.parentVessel = parentCollection.parentVessel;
            clone.parentCollection = parentCollection;
            clone.InitClone(collection);
            return clone;
        }

        public virtual void AddPart(Part part)
        {
            if (parts.Count > 0 && part.HasModuleImplementing<Expansions.Serenity.ModuleRoboticServoRotor>())
            {
                Expansions.Serenity.ModuleRoboticServoRotor rotorModule = part.FindModuleImplementing<Expansions.Serenity.ModuleRoboticServoRotor>();
                if (rotorModule.servoIsMotorized && rotorModule.rpmLimit != 0)
                {
                    partCollections.Add(RotorPartCollection.Borrow(this, part));
                    return;
                }
            }

            SimulatedPart simulatedPart = SimulatedPart.Borrow(part, parentVessel);
            parts.Add(simulatedPart);

            parentVessel.totalMass += simulatedPart.totalMass;
            parentVessel.dryMass += simulatedPart.dryMass;
            parentVessel.CoM += simulatedPart.totalMass * simulatedPart.CoM;
            parentVessel.CoM_dry += simulatedPart.dryMass * simulatedPart.CoM;

            ModuleLiftingSurface liftingSurface = part.FindModuleImplementing<ModuleLiftingSurface>();
            if (liftingSurface != null)
            {
                part.hasLiftModule = true;
                SimulatedLiftingSurface surface;
                
                if (liftingSurface is ModuleControlSurface ctrlSurface && (!ctrlSurface.ignorePitch || ctrlSurface.deploy))
                {
                    surface = SimulatedControlSurface.Borrow(ctrlSurface, simulatedPart);
                    ctrls.Add((SimulatedControlSurface)surface);

                    // Controls change their drag cubes with deployment and so we can't precalculate them.
                    // The effect of their drag cubes is captured in the methods for SimulatedControlSurface
                    parts.Remove(simulatedPart);

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

            for (int i = part.children.Count - 1; i >= 0; i--)
            {
                AddPart(part.children[i]);
            }
        }

        protected virtual void InitClone(PartCollection collection)
        {
            origin = collection.origin;
            parts.AddRange(collection.parts.Select(p => SimulatedPart.BorrowClone(p, parentVessel)));

            Dictionary<SimulatedPart, SimulatedPart> ctrlParts = new Dictionary<SimulatedPart, SimulatedPart>((int)(collection.ctrls.Count * 1.5f));
            foreach (SimulatedControlSurface ctrl in collection.ctrls)
            {
                SimulatedPart clonedPart = SimulatedPart.BorrowClone(ctrl.part, parentVessel);
                ctrls.Add(SimulatedControlSurface.BorrowClone(ctrl, clonedPart));
                ctrlParts.Add(ctrl.part, clonedPart);
            }
            foreach (SimulatedLiftingSurface surf in collection.surfaces)
            {
                int partIndex = collection.parts.FindIndex(p => p == surf.part);
                SimulatedLiftingSurface clonedSurface;
                if (partIndex >= 0)
                    clonedSurface = SimulatedLiftingSurface.BorrowClone(surf, parts[partIndex]);
                else
                    clonedSurface = SimulatedLiftingSurface.BorrowClone(surf, ctrlParts[surf.part]);
                surfaces.Add(clonedSurface);
            }
            foreach (SimulatedEngine engine in collection.engines)
            {
                int partIndex = collection.parts.FindIndex(p => p == engine.part);
                SimulatedEngine clonedEngine;
                if (partIndex >= 0)
                    clonedEngine = SimulatedEngine.BorrowClone(engine, parts[partIndex]);
                else
                    clonedEngine = SimulatedEngine.BorrowClone(engine, ctrlParts[engine.part]);
                engines.Add(clonedEngine);
            }
            partCollections.AddRange(collection.partCollections.Select(c => PartCollection.BorrowClone(c, this)));
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
