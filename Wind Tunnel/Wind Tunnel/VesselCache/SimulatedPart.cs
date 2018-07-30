using System.Collections.Generic;
using KerbalWindTunnel.Extensions;
using Smooth.Pools;
using UnityEngine;

namespace KerbalWindTunnel.VesselCache
{
    public class SimulatedPart
    {
        protected internal DragCubeList cubes = new DragCubeList();

        public SimulatedVessel vessel;

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

        private static readonly Pool<SimulatedPart> pool = new Pool<SimulatedPart>(Create, Reset);

        public static int PoolSize
        {
            get { return pool.Size; }
        }

        private static SimulatedPart Create()
        {
            SimulatedPart part = new SimulatedPart();
            part.cubes.BodyLiftCurve = new PhysicsGlobals.LiftingSurfaceCurve();
            part.cubes.SurfaceCurves = new PhysicsGlobals.SurfaceCurvesList();
            return part;
        }

        public void Release()
        {
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
            SimulatedPart part = pool.Borrow();
            part.vessel = vessel;
            part.Init(p);
            return part;
        }

        protected void Init(Part p)
        {
            this.name = p.name;
            Rigidbody rigidbody = p.rb;

            //totalMass = rigidbody == null ? 0 : rigidbody.mass; // TODO : check if we need to use this or the one without the childMass
            totalMass = p.mass + p.GetResourceMass();
            dryMass = p.mass;
            shieldedFromAirstream = p.ShieldedFromAirstream;

            noDrag = rigidbody == null && !PhysicsGlobals.ApplyDragToNonPhysicsParts;
            hasLiftModule = p.hasLiftModule;
            bodyLiftMultiplier = p.bodyLiftMultiplier;
            dragModel = p.dragModel;
            cubesNone = p.DragCubes.None;

            CoM = p.transform.TransformPoint(p.CoMOffset);
            CoP = p.transform.TransformPoint(p.CoPOffset);
            CoL = p.transform.TransformPoint(p.CoLOffset);

            switch (dragModel)
            {
                case Part.DragModel.CYLINDRICAL:
                case Part.DragModel.CONIC:
                    maximum_drag = p.maximum_drag;
                    minimum_drag = p.minimum_drag;
                    dragReferenceVector = p.partTransform.TransformDirection(p.dragReferenceVector);
                    break;
                case Part.DragModel.SPHERICAL:
                    maximum_drag = p.maximum_drag;
                    break;
                case Part.DragModel.CUBE:
                    if (cubesNone)
                        maximum_drag = p.maximum_drag;
                    break;
            }

            simCurves = SimCurves.Borrow(null);

            //cubes = new DragCubeList();
            ModuleWheels.ModuleWheelDeployment wheelDeployment = p.FindModuleImplementing<ModuleWheels.ModuleWheelDeployment>();
            bool forcedRetract = !shieldedFromAirstream && wheelDeployment != null && wheelDeployment.Position > 0;
            float gearPosition = 0;

            if(forcedRetract)
            {
                gearPosition = wheelDeployment.Position;
                p.DragCubes.SetCubeWeight("Retracted", 1);
                p.DragCubes.SetCubeWeight("Deployed", 0);
            }

            lock (this.cubes)
                CopyDragCubesList(p.DragCubes, cubes);

            if (forcedRetract)
            {
                p.DragCubes.SetCubeWeight("Retracted", 1 - gearPosition);
                p.DragCubes.SetCubeWeight("Deployed", gearPosition);
            }

            // Rotation to convert the vessel space vesselVelocity to the part space vesselVelocity
            // QuaternionD.LookRotation is not working...
            //partToVessel = Quaternion.LookRotation(p.vessel.GetTransform().InverseTransformDirection(p.transform.forward), p.vessel.GetTransform().InverseTransformDirection(p.transform.up));
            //vesselToPart = Quaternion.Inverse(partToVessel);
            partToVessel = p.transform.rotation;
            vesselToPart = Quaternion.Inverse(partToVessel);
            /*Debug.Log(p.name);
            Debug.Log(p.transform.rotation);
            Debug.Log(Quaternion.Inverse(p.transform.rotation));
            Debug.Log(Quaternion.LookRotation(p.transform.forward, p.transform.up));
            Debug.Log(p.transform.InverseTransformDirection(Vector3.forward) + " // " + Quaternion.Inverse(p.transform.rotation) * Vector3.forward + " // " + Quaternion.Inverse(Quaternion.LookRotation(p.transform.forward, p.transform.up)) * Vector3.forward);
            Debug.Log(p.DragCubes.None + " " + p.dragModel);
            Debug.Log("");*/
        }

        public Vector3 GetAero(Vector3 velocityVect, float mach, float pseudoReDragMult)
        {
            return GetAero(velocityVect, mach, pseudoReDragMult, out _);
        }
        public Vector3 GetAero(Vector3 velocityVect, float mach, float pseudoReDragMult, out Vector3 torque, bool dryTorque = false)
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
                    drag *= Mathf.Lerp(minimum_drag, maximum_drag, Mathf.Abs(Vector3.Dot(dragReferenceVector, velocityVect)));
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
            if (vessel != null)
                torque = Vector3.Cross(liftV, CoL - (dryTorque ? vessel.CoM_dry : vessel.CoM)) + Vector3.Cross(drag, CoP - (dryTorque ? vessel.CoM_dry : vessel.CoM));
            else
                torque = Vector3.Cross(liftV, CoL) + Vector3.Cross(drag, CoP);
            return drag + liftV;
        }

        public Vector3 GetLift(Vector3 velocityVect, float mach, out Vector3 torque, bool dryTorque = false)
        {
            Vector3 lift = GetLift(velocityVect, mach);
            if (vessel != null)
                torque = Vector3.Cross(lift, CoL - (dryTorque ? vessel.CoM_dry : vessel.CoM));
            else
                torque = Vector3.Cross(lift, CoL);
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
            private static readonly Pool<DragCube> _Instance = new Pool<DragCube>(
                () => new DragCube(), cube => { });


            public static Pool<DragCube> Instance { get { return _Instance; } }
        }

        protected void CopyDragCubesList(DragCubeList source, DragCubeList dest)
        {
            source.ForceUpdate(true, true);
            source.SetDragWeights();
            source.SetPartOcclusion();

            dest.ClearCubes();

            dest.SetPart(source.Part);

            dest.None = source.None;

            // Procedural need access to part so things gets bad quick.
            dest.Procedural = false;

            for (int i = 0; i < source.Cubes.Count; i++)
            {
                DragCube c = DragCubePool.Instance.Borrow();
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