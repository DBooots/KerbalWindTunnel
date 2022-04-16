using System;
using System.Collections.Generic;
using KerbalWindTunnel.Extensions;
using Smooth.Pools;
using UnityEngine;

namespace KerbalWindTunnel.VesselCache
{
    public class SimulatedLiftingSurface
    {
        private static readonly Pool<SimulatedLiftingSurface> pool = new Pool<SimulatedLiftingSurface>(Create, Reset);

        public AeroPredictor vessel;

        public Vector3 liftVector;
        public bool omnidirectional;
        public bool perpendicularOnly;
        public FloatCurve liftCurve;
        public FloatCurve liftMachCurve;
        public FloatCurve dragCurve;
        public FloatCurve dragMachCurve;
        public float deflectionLiftCoeff;
        public bool useInternalDragModel;
        public SimulatedPart part;
        public Vector3 velocityOffset;

        private static SimulatedLiftingSurface Create()
        {
            SimulatedLiftingSurface surface = new SimulatedLiftingSurface();
            return surface;
        }

        private static void Reset(SimulatedLiftingSurface surface) { }

        virtual public void Release()
        {
            pool.Release(this);
        }

        public static void Release(List<SimulatedLiftingSurface> objList)
        {
            for (int i = 0; i < objList.Count; ++i)
            {
                objList[i].Release();
            }
        }

        public static SimulatedLiftingSurface Borrow(ModuleLiftingSurface module, SimulatedPart part)
        {
            SimulatedLiftingSurface surface = pool.Borrow();
            surface.vessel = part.vessel;
            surface.Init(module, part);
            return surface;
        }
        public static SimulatedLiftingSurface BorrowClone(SimulatedLiftingSurface surface, SimulatedPart part)
        {
            SimulatedLiftingSurface clone = pool.Borrow();
            clone.vessel = part.vessel;
            clone.InitClone(surface, part);
            return clone;
        }

        protected void Init(ModuleLiftingSurface surface, SimulatedPart part)
        {
            surface.SetupCoefficients(Vector3.forward, out _, out this.liftVector, out _, out _);
            this.omnidirectional = surface.omnidirectional;
            this.perpendicularOnly = surface.perpendicularOnly;
            this.liftCurve = surface.liftCurve.Clone();
            this.liftMachCurve = surface.liftMachCurve.Clone();
            this.dragCurve = surface.dragCurve.Clone();
            this.dragMachCurve = surface.dragMachCurve.Clone();
            this.deflectionLiftCoeff = surface.deflectionLiftCoeff;
            this.useInternalDragModel = surface.useInternalDragModel;
            this.part = part;
            if (surface.displaceVelocity)
                this.velocityOffset = surface.part.transform.TransformVector(surface.velocityOffset);
            else
                this.velocityOffset = Vector3.zero;

            if (surface is ModuleControlSurface ctrl)
                this.deflectionLiftCoeff *= (1 - ctrl.ctrlSurfaceArea);
        }

        protected void InitClone(SimulatedLiftingSurface surface, SimulatedPart part)
        {
            this.liftVector = surface.liftVector;
            this.omnidirectional = surface.omnidirectional;
            this.perpendicularOnly = surface.perpendicularOnly;
            this.liftCurve = surface.liftCurve.Clone();
            this.liftMachCurve = surface.liftMachCurve.Clone();
            this.dragCurve = surface.dragCurve.Clone();
            this.dragMachCurve = surface.dragMachCurve.Clone();
            this.deflectionLiftCoeff = surface.deflectionLiftCoeff;
            this.useInternalDragModel = surface.useInternalDragModel;
            this.velocityOffset = surface.velocityOffset;
            this.part = part;
        }

        virtual public Vector3 GetLift(Vector3 velocityVect, float mach)
        {
            float dot = Vector3.Dot(velocityVect, liftVector);
            float absdot = omnidirectional ? Math.Abs(dot) : Mathf.Clamp01(dot);
            Vector3 lift;
            lock (this.liftCurve)
                lift = -liftVector * Math.Sign(dot) * liftCurve.Evaluate(absdot) * liftMachCurve.Evaluate(mach) * deflectionLiftCoeff * PhysicsGlobals.LiftMultiplier;
            if (perpendicularOnly)
                lift = Vector3.ProjectOnPlane(lift, -velocityVect);
            return lift * 1000;
        }
        virtual public Vector3 GetLift(Vector3 velocityVect, float mach, out Vector3 torque, Vector3 torquePoint)
        {
            Vector3 liftForce = GetLift(velocityVect, mach);
            torque = Vector3.Cross(liftForce, part.CoL - torquePoint);
            return liftForce;
        }

        virtual public Vector3 GetDrag(Vector3 velocityVect, float mach)
        {
            if (!useInternalDragModel)
                return Vector3.zero;
            float dot = Vector3.Dot(velocityVect, liftVector);
            float absdot = omnidirectional ? Math.Abs(dot) : Mathf.Clamp01(dot);
            Vector3 drag;
            lock (this.dragCurve)
                drag = -velocityVect * dragCurve.Evaluate(absdot) * dragMachCurve.Evaluate(mach) * deflectionLiftCoeff * PhysicsGlobals.LiftDragMultiplier;
            return drag * 1000;
        }
        virtual public Vector3 GetDrag(Vector3 velocityVect, float mach, out Vector3 torque, Vector3 torquePoint)
        {
            if (!useInternalDragModel)
                return torque = Vector3.zero;

            Vector3 dragForce = GetDrag(velocityVect, mach);
            torque = Vector3.Cross(dragForce, part.CoP - torquePoint);
            return dragForce;
        }

        virtual public Vector3 GetForce(Vector3 velocityVect, float mach)
        {
            float dot = Vector3.Dot(velocityVect, liftVector);
            float absdot = omnidirectional ? Math.Abs(dot) : Mathf.Clamp01(dot);
            Vector3 lift = Vector3.zero;
            lock (this.liftCurve)
                lift = -liftVector * Math.Sign(dot) * liftCurve.Evaluate(absdot) * liftMachCurve.Evaluate(mach) * deflectionLiftCoeff * PhysicsGlobals.LiftMultiplier;
            if (perpendicularOnly)
                lift = Vector3.ProjectOnPlane(lift, -velocityVect);
            if (!useInternalDragModel)
                return lift * 1000;
            Vector3 drag;
            lock (this.dragCurve)
                drag = -velocityVect * dragCurve.Evaluate(absdot) * dragMachCurve.Evaluate(mach) * deflectionLiftCoeff * PhysicsGlobals.LiftDragMultiplier;

            return (lift + drag) * 1000;
        }
        virtual public Vector3 GetForce(Vector3 velocityVect, float mach, out Vector3 torque, Vector3 torquePoint)
        {
            float dot = Vector3.Dot(velocityVect, liftVector);
            float absdot = omnidirectional ? Math.Abs(dot) : Mathf.Clamp01(dot);
            Vector3 lift = Vector3.zero;
            lock (this.liftCurve)
                lift = -liftVector * Math.Sign(dot) * liftCurve.Evaluate(absdot) * liftMachCurve.Evaluate(mach) * deflectionLiftCoeff * PhysicsGlobals.LiftMultiplier;
            if (perpendicularOnly)
                lift = Vector3.ProjectOnPlane(lift, -velocityVect);
            torque = Vector3.Cross(lift * 1000, part.CoL - torquePoint);
            if (!useInternalDragModel)
                return lift * 1000;

            Vector3 drag;
            lock (this.dragCurve)
                drag = -velocityVect * dragCurve.Evaluate(absdot) * dragMachCurve.Evaluate(mach) * deflectionLiftCoeff * PhysicsGlobals.LiftDragMultiplier;

            torque += Vector3.Cross(drag * 1000, part.CoP - torquePoint);
            return (lift + drag) * 1000;
        }
    }
}
