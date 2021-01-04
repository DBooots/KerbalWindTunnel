using System;
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
        public bool ignoresAllControls;
        public bool deployed;
        public float deployAngle;
        public Quaternion inputRotation = Quaternion.identity;
        public float deploymentDirection = 1;

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
            surface.vessel = part.vessel;
            surface.Init(module, part);
            return surface;
        }
        public static SimulatedControlSurface BorrowClone(SimulatedControlSurface surface, SimulatedPart part)
        {
            SimulatedControlSurface clone = pool.Borrow();
            clone.vessel = surface.vessel;
            clone.InitClone(surface, part);
            return clone;
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
            this.ignoresAllControls = surface.ignorePitch && surface.ignoreRoll && surface.ignoreYaw;
            this.maxAuthority = 150f; // surface.maxAuthority is private. Hopefully its value never changes.
            
            this.deployed = surface.deploy;
            if (deployed)
            {
                if (!surface.displaceVelocity)
                {
                    if (!surface.usesMirrorDeploy)
                    {
                        // Annoying that we have to calculate vessel CoM here just for this, but it can affect assymetric vessels with flaps
                        // or vessels where the entire vessel has been shifted off the centerline.
                        // Because the SimulatedVessel is still being constructed at this point, we can't just take its CoM value.
                        Vector3 CoM = Vector3.zero;
                        foreach (Part p in surface.part.ship.parts)
                            CoM += p.transform.TransformPoint(p.CoMOffset) * (p.mass + p.GetResourceMass());
                        CoM /= surface.part.ship.GetTotalMass();
                        this.deployAngle = (surface.deployInvert ? -1 : 1) *
                            Mathf.Sign((Quaternion.Inverse(surface.part.ship.rotation) * (surface.transform.position - CoM)).x);
                    }
                    else
                        this.deployAngle = surface.deployInvert ^ surface.partDeployInvert ^ surface.mirrorDeploy ? -1 : 1;
                    this.deployAngle *= -1;
                }
                else
                {
                    this.deployAngle = surface.deployInvert ^ surface.partDeployInvert ? -1 : 1;
                }

                try
                {
                    if (bool.TryParse(surface.part.variants.SelectedVariant.GetExtraInfoValue("reverseDeployDirection"), out bool flipDeployDirection))
                        this.deployAngle *= flipDeployDirection ? -1 : 1;
                }
                catch (NullReferenceException) { }

                this.deployAngle *= surface.deployAngle;
            }
            else
                this.deployAngle = 0;
        }
        protected void InitClone(SimulatedControlSurface surface, SimulatedPart part)
        {
            base.InitClone(surface, part);
            this.authorityLimiter = surface.authorityLimiter;
            this.ctrlSurfaceRange = surface.ctrlSurfaceRange;
            this.rotationAxis = surface.rotationAxis;
            this.ignorePitch = surface.ignorePitch;
            this.ignoresAllControls = surface.ignoresAllControls;
            this.maxAuthority = surface.maxAuthority;
            this.deployed = surface.deployed;
            this.deployAngle = surface.deployAngle;
            this.inputRotation = surface.inputRotation;
            this.deploymentDirection = surface.deploymentDirection;
        }

        public override Vector3 GetLift(Vector3 velocityVect, float mach)
        {
            return GetLift(velocityVect, mach, 0);
        }
        public override Vector3 GetLift(Vector3 velocityVect, float mach, out Vector3 torque, Vector3 torquePoint)
        {
            return GetLift(velocityVect, mach, 0, out torque, torquePoint);
        }
        public Vector3 GetLift(Vector3 velocityVect, float mach, float pitchInput, out Vector3 torque, Vector3 torquePoint)
        {
            bool isAheadOfCoM;
            if (torquePoint == vessel.CoM_dry)
                isAheadOfCoM = part.transformPosition.z > vessel.CoM_dry.z;
            else
                isAheadOfCoM = part.transformPosition.z > vessel.CoM.z;

            GetForce(velocityVect, mach, pitchInput, 0, out Vector3 lift, out _, isAheadOfCoM, true);
            torque = Vector3.Cross(lift, part.CoL - torquePoint);
            return lift;
        }
        public Vector3 GetLift(Vector3 velocityVect, float mach, float pitchInput)
        {
            bool isAheadOfCoM = part.transformPosition.z > vessel.CoM.z;

            GetForce(velocityVect, mach, pitchInput, 0, out Vector3 liftForce, out _, isAheadOfCoM, true);
            return liftForce;
        }

        /*
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
        public override Vector3 GetForce(Vector3 velocityVect, float mach, out Vector3 torque, Vector3 torquePoint)
        {
            Vector3 result = base.GetForce(velocityVect, mach, out torque, torquePoint);
            // Air Density: 1.225kg/m3
            // rho/rho_0 assumed to be 0.5
            // Speed of sound assumed to be ~300m/s
            float pseudoReDragMult;
            lock (part.simCurves.DragCurvePseudoReynolds)
                pseudoReDragMult = part.simCurves.DragCurvePseudoReynolds.Evaluate((1.225f * 0.5f) * (300f * mach));
            result += part.GetAero(velocityVect, mach, pseudoReDragMult, out Vector3 pTorque, torquePoint);
            torque += pTorque;
            return result;
        }*/

        public Vector3 GetDrag(Vector3 velocityVect, float mach, float pseudoReDragMult)
            => GetDrag(velocityVect, mach, 0, pseudoReDragMult);
        public Vector3 GetDrag(Vector3 velocityVect, float mach, float pitchInput, float pseudoReDragMult)
        {
            bool isAheadOfCoM = part.transformPosition.z > vessel.CoM.z;

            GetForce(velocityVect, mach, pitchInput, pseudoReDragMult, out _, out Vector3 drag, isAheadOfCoM, false);
            return drag;
        }
        public Vector3 GetDrag(Vector3 velocityVect, float mach, float pseudoReDragMult, out Vector3 torque, Vector3 torquePoint)
            => GetDrag(velocityVect, mach, 0, pseudoReDragMult, out torque, torquePoint);
        public Vector3 GetDrag(Vector3 velocityVect, float mach, float pitchInput, float pseudoReDragMult, out Vector3 torque, Vector3 torquePoint)
        {
            Vector3 drag = GetDrag(velocityVect, mach, pitchInput, pseudoReDragMult);
            torque = Vector3.Cross(drag, part.CoP - torquePoint);
            return drag;
        }

        public Vector3 GetForce(Vector3 velocityVect, float mach, float pseudoReDragMult)
        {
            return GetForce(velocityVect, mach, 0, pseudoReDragMult);
        }
        public Vector3 GetForce(Vector3 velocityVect, float mach, float pseudoReDragMult, out Vector3 torque, Vector3 torquePoint)
        {
            return GetForce(velocityVect, mach, 0, pseudoReDragMult, out torque, torquePoint);
        }
        public Vector3 GetForce(Vector3 velocityVect, float mach, float pitchInput, float pseudoReDragMult, out Vector3 torque, Vector3 torquePoint)
        {
            bool isAheadOfCoM;
            if (torquePoint == vessel.CoM_dry)
                isAheadOfCoM = part.transformPosition.z > vessel.CoM_dry.z;
            else
                isAheadOfCoM = part.transformPosition.z > vessel.CoM.z;

            GetForce(velocityVect, mach, pitchInput, pseudoReDragMult, out Vector3 liftForce, out Vector3 dragForce, isAheadOfCoM, !useInternalDragModel);
            torque = Vector3.Cross(liftForce, part.CoL - torquePoint);
            torque += Vector3.Cross(dragForce, part.CoP - torquePoint);
            return liftForce + dragForce;
        }
        public Vector3 GetForce(Vector3 velocityVect, float mach, float pitchInput, float pseudoReDragMult)
        {
            bool isAheadOfCoM = part.transformPosition.z > vessel.CoM.z;

            GetForce(velocityVect, mach, pitchInput, pseudoReDragMult, out Vector3 liftForce, out Vector3 dragForce, isAheadOfCoM, !useInternalDragModel);
            return liftForce + dragForce;
        }

        private void GetForce(Vector3 velocityVect, float mach, float pitchInput, float pseudoReDragMult, out Vector3 liftForce, out Vector3 dragForce, bool isAheadOfCoM, bool? liftOnly = null)
        {
            if (liftOnly == null)
                liftOnly = !this.useInternalDragModel;
            // Assumes no roll input required.
            // Assumes no yaw input required.
            float surfaceInput = 0;
            if (!ignorePitch && pitchInput != 0)
            {
                Vector3 input = inputRotation * new Vector3(pitchInput, 0, 0);
                surfaceInput = Vector3.Dot(input, rotationAxis);
                surfaceInput *= authorityLimiter * 0.01f;
                surfaceInput = Mathf.Clamp(surfaceInput, -1, 1);
                if (isAheadOfCoM)
                    surfaceInput *= -1;
                surfaceInput *= ctrlSurfaceRange;
            }
            if (deployed)
            {
                surfaceInput += deployAngle * deploymentDirection;
                //surfaceInput = Mathf.Clamp(surfaceInput, -1.5f, 1.5f);
            }

            Vector3 relLiftVector;
            if (surfaceInput != 0)
                relLiftVector = Quaternion.AngleAxis(surfaceInput, rotationAxis) * liftVector;
            else
                relLiftVector = liftVector;

            float dot = Vector3.Dot(velocityVect, relLiftVector);
            float absdot = omnidirectional ? Math.Abs(dot) : Mathf.Clamp01(dot);
            lock (this.liftCurve)
                liftForce = -relLiftVector * Math.Sign(dot) * liftCurve.Evaluate(absdot) * liftMachCurve.Evaluate(mach) * deflectionLiftCoeff * PhysicsGlobals.LiftMultiplier;
            if (perpendicularOnly)
                liftForce = Vector3.ProjectOnPlane(liftForce, -velocityVect);
            liftForce *= 1000;
            if ((bool)liftOnly)
            {
                dragForce = Vector3.zero;
                return;
            }

            lock (this.dragCurve)
                dragForce = -velocityVect * dragCurve.Evaluate(absdot) * dragMachCurve.Evaluate(mach) * deflectionLiftCoeff * PhysicsGlobals.LiftDragMultiplier;
            dragForce *= 1000;

            if ((this.part.dragModel == Part.DragModel.DEFAULT || this.part.dragModel == Part.DragModel.CUBE) && !this.part.cubesNone && pseudoReDragMult > 0)
            {
                lock (this.part.cubes)
                {
                    // TODO: Check if neutral needs clamping. It can't be above 1 because math, but will negative values be an issue?
                    // So, since surfaceInput is already multiplied by authorityLimiter%, one would think it isn't needed again here.
                    // But, detailed testing shows it is required.
                    this.part.cubes.SetCubeWeight("neutral", (this.maxAuthority - Math.Abs(surfaceInput * this.authorityLimiter)) * 0.01f);
                    this.part.cubes.SetCubeWeight("fullDeflectionPos", Mathf.Clamp01(surfaceInput * this.authorityLimiter * 0.01f));
                    this.part.cubes.SetCubeWeight("fullDeflectionNeg", Mathf.Clamp01(-surfaceInput * this.authorityLimiter * 0.01f));

                    dragForce += this.part.GetAero(velocityVect, mach, pseudoReDragMult);
                }
            }
        }
    }
}
