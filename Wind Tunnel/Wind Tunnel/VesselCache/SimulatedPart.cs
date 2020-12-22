using System;
using System.Collections.Generic;
using KerbalWindTunnel.Extensions;
using Smooth.Pools;
using UnityEngine;

namespace KerbalWindTunnel.VesselCache
{
    public class SimulatedPart
    {
        protected internal DragCubeList cubes = new DragCubeList();

        public AeroPredictor vessel;

        public string name = "";
        public float totalMass = 0;
        public float dryMass = 0;
        public bool shieldedFromAirstream;
        public bool noDrag;
        public bool hasLiftModule;
        public Part.DragModel dragModel;
        private float minimum_drag;
        private float maximum_drag;
        private Vector3 dragReferenceVector;
        internal bool cubesNone;
        private float bodyLiftMultiplier;

        internal SimCurves simCurves;

        private Quaternion vesselToPart;
        private Quaternion partToVessel;
        public Vector3 CoM, CoL, CoP;
        public Vector3 transformPosition;

        private static readonly Pool<SimulatedPart> pool = new Pool<SimulatedPart>(Create, Reset);

        public static int PoolSize
        {
            get { return pool.Size; }
        }

        private static SimulatedPart Create()
        {
            SimulatedPart part = new SimulatedPart();
            lock (part.cubes)
            {
                part.cubes.BodyLiftCurve = new PhysicsGlobals.LiftingSurfaceCurve();
                part.cubes.SurfaceCurves = new PhysicsGlobals.SurfaceCurvesList();
            }
            return part;
        }

        public void Release()
        {
            lock (pool)
                pool.Release(this);
        }

        public static void Release(List<SimulatedPart> objList)
        {
            for (int i = 0; i < objList.Count; ++i)
            {
                objList[i].Release();
            }
        }

        private static void Reset(SimulatedPart obj)
        {
            foreach (DragCube cube in obj.cubes.Cubes)
            {
                DragCubePool.Instance.Release(cube);
            }
            obj.simCurves.Release();
        }

        public static SimulatedPart Borrow(Part p, SimulatedVessel vessel)
        {
            SimulatedPart part;
            lock (pool)
                part = pool.Borrow();
            part.vessel = vessel;
            part.Init(p);
            return part;
        }
        public static SimulatedPart BorrowClone(SimulatedPart part, SimulatedVessel vessel)
        {
            SimulatedPart clone;
            lock (pool)
                clone = pool.Borrow();
            clone.vessel = vessel;
            clone.InitClone(part);
            return clone;
        }

        protected void Init(Part part)
        {
            this.name = part.name;
            Rigidbody rigidbody = part.rb;

            //totalMass = rigidbody == null ? 0 : rigidbody.mass; // TODO : check if we need to use this or the one without the childMass
            totalMass = part.mass + part.GetResourceMass();
            dryMass = part.mass;
            shieldedFromAirstream = part.ShieldedFromAirstream;

            noDrag = rigidbody == null && !PhysicsGlobals.ApplyDragToNonPhysicsParts;
            hasLiftModule = part.hasLiftModule;
            bodyLiftMultiplier = part.bodyLiftMultiplier;
            dragModel = part.dragModel;
            cubesNone = part.DragCubes.None;

            CoM = part.transform.TransformPoint(part.CoMOffset);
            CoP = part.transform.TransformPoint(part.CoPOffset);
            CoL = part.transform.TransformPoint(part.CoLOffset);
            transformPosition = part.transform.position;

            switch (dragModel)
            {
                case Part.DragModel.CYLINDRICAL:
                case Part.DragModel.CONIC:
                    maximum_drag = part.maximum_drag;
                    minimum_drag = part.minimum_drag;
                    dragReferenceVector = part.partTransform.TransformDirection(part.dragReferenceVector);
                    break;
                case Part.DragModel.SPHERICAL:
                    maximum_drag = part.maximum_drag;
                    break;
                case Part.DragModel.CUBE:
                    if (cubesNone)
                        maximum_drag = part.maximum_drag;
                    break;
            }

            simCurves = SimCurves.Borrow(null);

            //cubes = new DragCubeList();
            ModuleWheels.ModuleWheelDeployment wheelDeployment = part.FindModuleImplementing<ModuleWheels.ModuleWheelDeployment>();
            bool forcedRetract = !shieldedFromAirstream && wheelDeployment != null && wheelDeployment.Position > 0;
            float gearPosition = 0;

            if(forcedRetract)
            {
                gearPosition = wheelDeployment.Position;
                lock (wheelDeployment)
                {
                    lock (part.DragCubes)
                    {
                        part.DragCubes.SetCubeWeight("Retracted", 1);
                        part.DragCubes.SetCubeWeight("Deployed", 0);

                        lock (this.cubes)
                            CopyDragCubesList(part.DragCubes, cubes);

                        part.DragCubes.SetCubeWeight("Retracted", 1 - gearPosition);
                        part.DragCubes.SetCubeWeight("Deployed", gearPosition);
                    }
                }
            }

            else
            {
                lock (this.cubes)
                    lock (part.DragCubes)
                        CopyDragCubesList(part.DragCubes, cubes);
            }

            // Rotation to convert the vessel space vesselVelocity to the part space vesselVelocity
            partToVessel = part.transform.rotation;
            vesselToPart = Quaternion.Inverse(partToVessel);
        }
        protected void InitClone(SimulatedPart part)
        {
            this.name = part.name;

            totalMass = part.totalMass;
            dryMass = part.dryMass;
            shieldedFromAirstream = part.shieldedFromAirstream;

            noDrag = part.noDrag;
            hasLiftModule = part.hasLiftModule;
            bodyLiftMultiplier = part.bodyLiftMultiplier;
            dragModel = part.dragModel;
            cubesNone = part.cubesNone;

            CoM = part.CoM;
            CoP = part.CoP;
            CoL = part.CoL;
            transformPosition = part.transformPosition;

            maximum_drag = part.maximum_drag;
            minimum_drag = part.minimum_drag;
            dragReferenceVector = part.dragReferenceVector;

            simCurves = SimCurves.Borrow(null);

            lock (this.cubes)
                CopyDragCubesList(part.cubes, cubes, true);

            // Rotation to convert the vessel space vesselVelocity to the part space vesselVelocity
            partToVessel = part.partToVessel;
            vesselToPart = part.vesselToPart;
        }

        public Vector3 GetAero(Vector3 velocityVect, float mach, float pseudoReDragMult)
        {
            return GetAero(velocityVect, mach, pseudoReDragMult, out _, Vector3.zero);
        }
        public Vector3 GetAero(Vector3 velocityVect, float mach, float pseudoReDragMult, out Vector3 torque, Vector3 torquePoint)
        {
            torque = Vector3.zero;
            if (shieldedFromAirstream || noDrag)
                return Vector3.zero;
            Vector3 dragVectorDirLocal = -(vesselToPart * velocityVect);
            Vector3 drag = -velocityVect;
            Vector3 liftV = Vector3.zero;

            switch (dragModel)
            {
                case Part.DragModel.SPHERICAL:
                    drag *= maximum_drag;
                    break;
                case Part.DragModel.CYLINDRICAL:
                    drag *= Mathf.Lerp(minimum_drag, maximum_drag, Math.Abs(Vector3.Dot(dragReferenceVector, velocityVect)));
                    break;
                case Part.DragModel.CONIC:
                    drag *= Mathf.Lerp(minimum_drag, maximum_drag, Vector3.Angle(dragReferenceVector, velocityVect) / 180f);
                    break;
                case Part.DragModel.DEFAULT:
                case Part.DragModel.CUBE:
                    if (cubesNone)
                        drag *= maximum_drag;
                    else
                    {
                        lock (this.cubes)
                        {
                            cubes.SetDrag(dragVectorDirLocal, mach);

                            drag *= cubes.AreaDrag * PhysicsGlobals.DragCubeMultiplier;
                            if (!hasLiftModule)
                            {
                                // direction of the lift in a vessel centric reference
                                liftV = partToVessel * (cubes.LiftForce * bodyLiftMultiplier);

                                liftV = Vector3.ProjectOnPlane(liftV, velocityVect) * PhysicsGlobals.BodyLiftMultiplier * cubes.BodyLiftCurve.liftMachCurve.Evaluate(mach);
                            }
                        }
                    }
                    break;
                default:
                    drag = Vector3.zero;
                    break;
            }

            drag *= PhysicsGlobals.DragMultiplier * pseudoReDragMult;
            //Debug.Log(name + ": " + drag.magnitude / pseudoReDragMult);
            torque = Vector3.Cross(liftV, CoL - torquePoint) + Vector3.Cross(drag, CoP - torquePoint);
            return drag + liftV;
        }

        public Vector3 GetLift(Vector3 velocityVect, float mach, out Vector3 torque, Vector3 torquePoint)
        {
            Vector3 lift = GetLift(velocityVect, mach);
            torque = Vector3.Cross(lift, CoL - torquePoint);
            return lift;
        }
        public Vector3 GetLift(Vector3 velocityVect, float mach)
        {
            if (shieldedFromAirstream || hasLiftModule || cubesNone || dragModel != Part.DragModel.CUBE)
                return Vector3.zero;

            Vector3 dragVectorDirLocal = -(vesselToPart * velocityVect);
            Vector3 liftV = Vector3.zero;

            lock (this.cubes)
            {
                cubes.SetDrag(dragVectorDirLocal, mach);

                // direction of the lift in a vessel centric reference
                liftV = partToVessel * (cubes.LiftForce * bodyLiftMultiplier);

                liftV = KSPClassExtensions.ProjectOnPlaneSafe(liftV, velocityVect) * PhysicsGlobals.BodyLiftMultiplier * cubes.BodyLiftCurve.liftMachCurve.Evaluate(mach);
            }

            return liftV;
        }

        /*public virtual Vector3 Drag(Vector3 vesselVelocity, float dragFactor, float mach)
        {
            if (shieldedFromAirstream || noDrag)
                return Vector3.zero;

            Vector3 dragVectorDirLocal = -(vesselToPart * vesselVelocity).normalized;

            cubes.SetDrag(-dragVectorDirLocal, mach);

            Vector3 drag = -vesselVelocity.normalized * cubes.AreaDrag * dragFactor;

            return drag;
        }

        public virtual Vector3 Lift(Vector3 vesselVelocity, float liftFactor)
        {
            if (shieldedFromAirstream || hasLiftModule)
                return Vector3.zero;

            // direction of the lift in a vessel centric reference
            Vector3 liftV = partToVessel * (cubes.LiftForce * bodyLiftMultiplier * liftFactor);

            Vector3 liftVector = Vector3.ProjectOnPlane(liftV, -vesselVelocity);

            return liftVector;
        }*/

        public static class DragCubePool
        {
            public static Pool<DragCube> Instance { get; } = new Pool<DragCube>(
                () => new DragCube(), cube => { });
        }

        protected void CopyDragCubesList(DragCubeList source, DragCubeList dest, bool sourceIsSet = false)
        {
            if (!sourceIsSet)
            {
                source.ForceUpdate(true, true);
                source.SetDragWeights();
                source.SetPartOcclusion();
            }

            dest.ClearCubes();

            dest.SetPart(source.Part);

            dest.None = source.None;

            // Procedural need access to part so things gets bad quick.
            dest.Procedural = false;

            for (int i = 0; i < source.Cubes.Count; i++)
            {
                DragCube c;
                lock (DragCubePool.Instance)
                    c = DragCubePool.Instance.Borrow();
                CopyDragCube(source.Cubes[i], c);
                dest.Cubes.Add(c);
            }

            dest.SetDragWeights();

            for (int i = 0; i < 6; i++)
            {
                dest.WeightedArea[i] = source.WeightedArea[i];
                dest.WeightedDrag[i] = source.WeightedDrag[i];
                dest.AreaOccluded[i] = source.AreaOccluded[i];
                dest.WeightedDepth[i] = source.WeightedDepth[i];
            }

            dest.SetDragWeights();

            dest.BodyLiftCurve = new PhysicsGlobals.LiftingSurfaceCurve();
            dest.SurfaceCurves = new PhysicsGlobals.SurfaceCurvesList();

            dest.DragCurveCd = simCurves.DragCurveCd.Clone();
            dest.DragCurveCdPower = simCurves.DragCurveCdPower.Clone();
            dest.DragCurveMultiplier = simCurves.DragCurveMultiplier.Clone();

            dest.BodyLiftCurve.liftCurve = simCurves.LiftCurve.Clone();
            dest.BodyLiftCurve.dragCurve = simCurves.DragCurve.Clone();
            dest.BodyLiftCurve.dragMachCurve = simCurves.DragMachCurve.Clone();
            dest.BodyLiftCurve.liftMachCurve = simCurves.LiftMachCurve.Clone();

            dest.SurfaceCurves.dragCurveMultiplier = simCurves.DragCurveMultiplier.Clone();
            dest.SurfaceCurves.dragCurveSurface = simCurves.DragCurveSurface.Clone();
            dest.SurfaceCurves.dragCurveTail = simCurves.DragCurveTail.Clone();
            dest.SurfaceCurves.dragCurveTip = simCurves.DragCurveTip.Clone();

            dest.SetPartOcclusion();
        }

        protected static void CopyDragCube(DragCube source, DragCube dest)
        {
            dest.Name = source.Name;
            dest.Weight = source.Weight;
            dest.Center = source.Center;
            dest.Size = source.Size;
            for (int i = 0; i < source.Drag.Length; i++)
            {
                dest.Drag[i] = source.Drag[i];
                dest.Area[i] = source.Area[i];
                dest.Depth[i] = source.Depth[i];
                dest.DragModifiers[i] = source.DragModifiers[i];
            }
        }

        protected void SetCubeWeight(string name, float newWeight)
        {
            lock (cubes)
            {
                int count = cubes.Cubes.Count;
                if (count == 0)
                {
                    return;
                }

                bool noChange = true;
                for (int i = count - 1; i >= 0; i--)
                {
                    if (cubes.Cubes[i].Name == name && cubes.Cubes[i].Weight != newWeight)
                    {
                        cubes.Cubes[i].Weight = newWeight;
                        noChange = false;
                    }
                }

                if (noChange)
                    return;

                cubes.SetDragWeights();
            }
        }
    }
}