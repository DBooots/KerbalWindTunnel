using System.Collections.Generic;
using KerbalWindTunnel.Extensions;
using Smooth.Pools;
using UnityEngine;

namespace KerbalWindTunnel.VesselCache
{
    public class SimulatedControlSurface : SimulatedLiftingSurface
    {
        private static readonly Pool<SimulatedControlSurface> pool = new Pool<SimulatedControlSurface>(Create, Reset);

        /*public Vector3 liftVector;
        public bool omnidirectional;
        public bool perpendicularOnly;
        public FloatCurve liftCurve;
        public FloatCurve liftMachCurve;
        public FloatCurve dragCurve;
        public FloatCurve dragMachCurve;
        public float deflectionLiftCoeff;
        public bool useInternalDragModel;
        public SimulatedPart part;*/
        public float authorityLimiter;
        public float maxAuthority;
        public float ctrlSurfaceRange;
        public Vector3 rotationAxis;
        public bool ignorePitch;
        public Quaternion inputRotation = Quaternion.identity;

        private static SimulatedControlSurface Create()
        {
            SimulatedControlSurface surface = new SimulatedControlSurface();
            return surface;
        }

        private static void Reset(SimulatedControlSurface surface) { }

        override public void Release()
        {
            part.Release();
            pool.Release(this);
        }

        public static void Release(List<SimulatedControlSurface> objList)
        {
            for (int i = 0; i < objList.Count; ++i)
            {
                objList[i].Release();
            }
        }

        public static SimulatedControlSurface Borrow(ModuleControlSurface module, SimulatedPart part)
        {
            SimulatedControlSurface surface = pool.Borrow();
            surface.Init(module, part);
            return surface;
        }

        protected void Init(ModuleControlSurface surface, SimulatedPart part)
        {
            base.Init(surface, part);

            this.deflectionLiftCoeff = surface.deflectionLiftCoeff * surface.ctrlSurfaceArea;
            this.authorityLimiter = surface.authorityLimiter;
            this.ctrlSurfaceRange = surface.ctrlSurfaceRange;
            // TODO: Incorporate transformName if required.
            this.rotationAxis = surface.transform.rotation * Vector3.right;
            this.ignorePitch = surface.ignorePitch;
            this.maxAuthority = 150f; // surface.maxAuthority is private. Hopefully its value never changes.
        }

        public override Vector3 GetLift(Vector3 velocityVect, float mach)
        {
            return GetLift(velocityVect, mach, 0);
        }
        public override Vector3 GetLift(Vector3 velocityVect, float mach, out Vector3 torque, bool dryTorque = false)
        {
            return GetLift(velocityVect, mach, 0, out torque, dryTorque);
        }
        public Vector3 GetLift(Vector3 velocityVect, float mach, float pitchInput, out Vector3 torque, bool dryTorque = false)
        {
            Vector3 lift = GetLift(velocityVect, mach, pitchInput);
            if (vessel != null)
                torque = Vector3.Cross(lift, part.CoL - (dryTorque ? vessel.CoM_dry : vessel.CoM));
            else
                torque = Vector3.Cross(lift, part.CoL);
            return lift;
        }
        public Vector3 GetLift(Vector3 velocityVect, float mach, float pitchInput)
        {
            // Assumes no roll input required.
            // Assumes no yaw input required.
            Vector3 input = inputRotation * new Vector3(!this.ignorePitch ? pitchInput : 0, 0, 0);
            float surfaceInput = Vector3.Dot(input, rotationAxis);
            surfaceInput *= this.authorityLimiter * 0.01f;
            surfaceInput = Mathf.Clamp(surfaceInput, -1, 1);

            Vector3 relLiftVector = Quaternion.AngleAxis(ctrlSurfaceRange * surfaceInput, rotationAxis) * liftVector;

            float dot = Vector3.Dot(velocityVect, relLiftVector);
            float absdot = omnidirectional ? Mathf.Abs(dot) : Mathf.Clamp01(dot);
            Vector3 lift = Vector3.zero;
            lock (this.liftCurve)
                lift = -relLiftVector * Mathf.Sign(dot) * liftCurve.Evaluate(absdot) * liftMachCurve.Evaluate(mach) * deflectionLiftCoeff * PhysicsGlobals.LiftMultiplier;
            if (perpendicularOnly)
                lift = Vector3.ProjectOnPlane(lift, -velocityVect);
            return lift * 1000;
        }

        /// <summary>
        /// Use <see cref="GetForce(Vector3 velocityVect, float mach, float pseudoReDragMult)"/> instead.
        /// </summary>
        /// <param name="velocityVect"></param>
        /// <param name="mach"></param>
        /// <returns></returns>
        public override Vector3 GetForce(Vector3 velocityVect, float mach)
        {
            // Air Density: 1.225kg/m3
            // rho/rho_0 assumed to be 0.5
            // Speed of sound assumed to be ~300m/s
            float pseudoReDragMult;
            lock (part.simCurves.DragCurvePseudoReynolds)
                pseudoReDragMult = part.simCurves.DragCurvePseudoReynolds.Evaluate((1.225f * 0.5f) * (300f * mach));
            return base.GetForce(velocityVect, mach) + part.GetAero(velocityVect, mach, pseudoReDragMult);
        }
        public override Vector3 GetForce(Vector3 velocityVect, float mach, out Vector3 torque, bool dryTorque = false)
        {
            // Air Density: 1.225kg/m3
            // rho/rho_0 assumed to be 0.5
            // Speed of sound assumed to be ~300m/s
            float pseudoReDragMult;
            lock (part.simCurves.DragCurvePseudoReynolds)
                pseudoReDragMult = part.simCurves.DragCurvePseudoReynolds.Evaluate((1.225f * 0.5f) * (300f * mach));
            Vector3 result = base.GetForce(velocityVect, mach, out torque);
            result += part.GetAero(velocityVect, mach, pseudoReDragMult, out Vector3 pTorque, dryTorque);
            torque += pTorque;
            return result;
        }
        public Vector3 GetForce(Vector3 velocityVect, float mach, float pseudoReDragMult)
        {
            return GetForce(velocityVect, mach, 0, pseudoReDragMult);
        }
        public Vector3 GetForce(Vector3 velocityVect, float mach, float pseudoReDragMult, out Vector3 torque, bool dryTorque = false)
        {
            return GetForce(velocityVect, mach, 0, pseudoReDragMult, out torque, dryTorque);
        }
        public Vector3 GetForce(Vector3 velocityVect, float mach, float pitchInput, float pseudoReDragMult, out Vector3 torque, bool dryTorque = false)
        {
            // Assumes no roll input required.
            // Assumes no yaw input required.
            Vector3 input = inputRotation * new Vector3(!this.ignorePitch ? pitchInput : 0, 0, 0);
            float surfaceInput = Vector3.Dot(input, rotationAxis);
            surfaceInput *= this.authorityLimiter * 0.01f;
            surfaceInput = Mathf.Clamp(surfaceInput, -1, 1);

            Vector3 relLiftVector = Quaternion.AngleAxis(ctrlSurfaceRange * surfaceInput, rotationAxis) * liftVector;

            float dot = Vector3.Dot(velocityVect, relLiftVector);
            float absdot = omnidirectional ? Mathf.Abs(dot) : Mathf.Clamp01(dot);
            Vector3 lift = Vector3.zero;
            lock (this.liftCurve)
                lift = -relLiftVector * Mathf.Sign(dot) * liftCurve.Evaluate(absdot) * liftMachCurve.Evaluate(mach) * deflectionLiftCoeff * PhysicsGlobals.LiftMultiplier;
            if (perpendicularOnly)
                lift = Vector3.ProjectOnPlane(lift, -velocityVect);
            if (vessel != null)
                torque = Vector3.Cross(lift * 1000, part.CoL - (dryTorque ? vessel.CoM_dry : vessel.CoM));
            else
                torque = Vector3.Cross(lift * 1000, part.CoL);
            if (!useInternalDragModel)
                return lift * 1000;

            Vector3 drag = Vector3.zero;
            lock (this.dragCurve)
                drag = -velocityVect * dragCurve.Evaluate(absdot) * dragMachCurve.Evaluate(mach) * deflectionLiftCoeff * PhysicsGlobals.LiftDragMultiplier;

            Vector3 partDrag = Vector3.zero;
            if ((this.part.dragModel == Part.DragModel.DEFAULT || this.part.dragModel == Part.DragModel.CUBE) && !this.part.cubesNone && pseudoReDragMult > 0)
            {
                lock (this.part.cubes)
                {
                    // TODO: Check if neutral needs clamping. It can't be above 1 because math, but will negative values be an issue?
                    // So, since surfaceInput is already multiplied by authorityLimiter%, one would think it isn't needed again here.
                    // But, detailed testing shows it is required.
                    this.part.cubes.SetCubeWeight("neutral", (this.maxAuthority - Mathf.Abs(surfaceInput * this.authorityLimiter)) * 0.01f);
                    this.part.cubes.SetCubeWeight("fullDeflectionPos", Mathf.Clamp01(surfaceInput * this.authorityLimiter * 0.01f));
                    this.part.cubes.SetCubeWeight("fullDeflectionNeg", Mathf.Clamp01(-surfaceInput * this.authorityLimiter * 0.01f));
                    this.part.cubes.SetDragWeights();

                    partDrag = this.part.GetAero(velocityVect, mach, pseudoReDragMult);
                }
            }

            if (vessel != null)
                torque += Vector3.Cross(drag * 1000 + partDrag, part.CoP - (dryTorque ? vessel.CoM_dry : vessel.CoM));
            else
                torque += Vector3.Cross(drag * 1000 + partDrag, part.CoP);
            return (lift + drag) * 1000 + partDrag;
        }
        public Vector3 GetForce(Vector3 velocityVect, float mach, float pitchInput, float pseudoReDragMult)
        {
            // Assumes no roll input required.
            // Assumes no yaw input required.
            Vector3 input = inputRotation * new Vector3(!this.ignorePitch ? pitchInput : 0, 0, 0);
            float surfaceInput = Vector3.Dot(input, rotationAxis);
            surfaceInput *= this.authorityLimiter * 0.01f;
            surfaceInput = Mathf.Clamp(surfaceInput, -1, 1);

            Vector3 relLiftVector = Quaternion.AngleAxis(ctrlSurfaceRange * surfaceInput, rotationAxis) * liftVector;

            float dot = Vector3.Dot(velocityVect, relLiftVector);
            float absdot = omnidirectional ? Mathf.Abs(dot) : Mathf.Clamp01(dot);
            Vector3 lift = Vector3.zero;
            lock (this.liftCurve)
                lift = -relLiftVector * Mathf.Sign(dot) * liftCurve.Evaluate(absdot) * liftMachCurve.Evaluate(mach) * deflectionLiftCoeff * PhysicsGlobals.LiftMultiplier;
            if (perpendicularOnly)
                lift = Vector3.ProjectOnPlane(lift, -velocityVect);
            if (!useInternalDragModel)
                return lift * 1000;

            Vector3 drag = Vector3.zero;
            lock (this.dragCurve)
                drag = -velocityVect * dragCurve.Evaluate(absdot) * dragMachCurve.Evaluate(mach) * deflectionLiftCoeff * PhysicsGlobals.LiftDragMultiplier;

            Vector3 partDrag = Vector3.zero;
            if ((this.part.dragModel == Part.DragModel.DEFAULT || this.part.dragModel == Part.DragModel.CUBE) && !this.part.cubesNone && pseudoReDragMult > 0)
            {
                lock (this.part.cubes)
                {
                    // TODO: Check if neutral needs clamping. It can't be above 1 because math, but will negative values be an issue?
                    // So, since surfaceInput is already multiplied by authorityLimiter%, one would think it isn't needed again here.
                    // But, detailed testing shows it is required.
                    this.part.cubes.SetCubeWeight("neutral", (this.maxAuthority - Mathf.Abs(surfaceInput * this.authorityLimiter)) * 0.01f);
                    this.part.cubes.SetCubeWeight("fullDeflectionPos", Mathf.Clamp01(surfaceInput * this.authorityLimiter * 0.01f));
                    this.part.cubes.SetCubeWeight("fullDeflectionNeg", Mathf.Clamp01(-surfaceInput * this.authorityLimiter * 0.01f));

                    partDrag = this.part.GetAero(velocityVect, mach, pseudoReDragMult);
                }
            }

            return (lift + drag) * 1000 + partDrag;
        }
    }
}
