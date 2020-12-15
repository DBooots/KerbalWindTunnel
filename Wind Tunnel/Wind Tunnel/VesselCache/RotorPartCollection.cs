using Smooth.Pools;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KerbalWindTunnel.VesselCache
{
    public class RotorPartCollection : PartCollection
    {
        public Vector3 axis;
        public float angularVelocity;
        public float fuelConsumption;

        public override Vector3 GetAeroForce(Vector3 inflow, AeroPredictor.Conditions conditions, float pitchInput, out Vector3 torque, Vector3 torquePoint)
        {
            Vector3 aeroForce = Vector3.zero;
            torque = Vector3.zero;
            int rotationCount = WindTunnelSettings.Instance.rotationCount;
            float Q = 0.0005f * conditions.atmDensity;

            // The root part is the rotor hub, so since the rotating mesh is usually cylindrical we
            // only need to evaluate this part once.
            if (!parts[0].shieldedFromAirstream)
            {
                float localMach = inflow.magnitude;
                float localVelFactor = localMach * localMach;
                float localPRDM;
                lock (PhysicsGlobals.DragCurvePseudoReynolds)
                    localPRDM = PhysicsGlobals.DragCurvePseudoReynolds.Evaluate(conditions.atmDensity * localMach);
                localMach /= conditions.speedOfSound;
                aeroForce += parts[0].GetAero(inflow.normalized, localMach, localPRDM, out Vector3 pTorque, origin) * localVelFactor * Q;
                torque += pTorque * localVelFactor * Q;
            }

            for (int r = 0; r < rotationCount; r++)
            {
                Quaternion rotation = Quaternion.AngleAxis(360f / rotationCount * r, axis);
                Vector3 rTorque = Vector3.zero;
                Vector3 rAeroForce = Vector3.zero;
                // Rotate inflow
                Vector3 rotatedInflow = rotation * inflow;
                // Calculate forces
                for (int i = parts.Count - 1; i >= 1; i--)
                {
                    if (parts[i].shieldedFromAirstream)
                        continue;
                    Vector3 partMotion = Vector3.Cross(axis, (parts[i].transformPosition - origin)) * angularVelocity + rotatedInflow;
                    //Vector3 partMotion = Vector3.Cross((parts[i].transformPosition - origin), axis) * angularVelocity + rotatedInflow;
                    Vector3 partInflow = partMotion.normalized;
                    float localMach = partMotion.magnitude;
                    float localVelFactor = localMach * localMach;
                    float localPRDM;
                    lock (PhysicsGlobals.DragCurvePseudoReynolds)
                        localPRDM = PhysicsGlobals.DragCurvePseudoReynolds.Evaluate(conditions.atmDensity * localMach);
                    localMach /= conditions.speedOfSound;
                    rAeroForce += parts[i].GetAero(partInflow, localMach, localPRDM, out Vector3 pTorque, origin) * localVelFactor;
                    rTorque += pTorque * localVelFactor;
                }
                for (int i = surfaces.Count - 1; i >= 0; i--)
                {
                    if (surfaces[i].part.shieldedFromAirstream)
                        continue;
                    Vector3 partMotion = Vector3.Cross(axis, (surfaces[i].part.transformPosition + surfaces[i].velocityOffset - origin)) * angularVelocity + rotatedInflow;
                    //Vector3 partMotion = Vector3.Cross((surfaces[i].part.transformPosition + surfaces[i].velocityOffset - origin), axis) * angularVelocity + rotatedInflow;
                    Vector3 partInflow = partMotion.normalized;
                    float localMach = partMotion.magnitude;
                    float localVelFactor = localMach * localMach;
                    localMach /= conditions.speedOfSound;
                    rAeroForce += surfaces[i].GetForce(partInflow, localMach, out Vector3 pTorque, origin) * localVelFactor;
                    rTorque += pTorque * localVelFactor;
                    if (conditions.altitude <= 50 && conditions.speed >= 15 && conditions.speed < 25 && Vector3.Angle(inflow, Vector3.forward) < 10)
                    {
                        if (i == surfaces.Count - 1)
                            Debug.LogFormat("\nAxis {0}\tAngularVelocity {1}\nSpeed {2}\tAltitude {3}", axis, angularVelocity, conditions.speed, conditions.altitude);
                        Debug.LogFormat("Surface {4}\tInflow {0}\tPartMotion {1}\tTorque {2}\tAeroForce{3}", rotatedInflow, partMotion, Quaternion.AngleAxis(-360f / rotationCount * r, axis) * pTorque, surfaces[i].GetForce(inflow + partMotion, conditions.mach, out _, origin), i);
                    }
                }
                for (int i = ctrls.Count - 1; i >= 0; i--)
                {
                    if (ctrls[i].part.shieldedFromAirstream)
                        continue;
                    Vector3 partMotion = Vector3.Cross(axis, (ctrls[i].part.transformPosition + ctrls[i].velocityOffset - origin)) * angularVelocity + rotatedInflow;
                    //Vector3 partMotion = Vector3.Cross((ctrls[i].part.transformPosition + ctrls[i].velocityOffset - origin), axis) * angularVelocity + rotatedInflow;
                    Vector3 partInflow = partMotion.normalized;
                    float localMach = partMotion.magnitude;
                    float localVelFactor = localMach * localMach;
                    float localPRDM;
                    lock (PhysicsGlobals.DragCurvePseudoReynolds)
                        localPRDM = PhysicsGlobals.DragCurvePseudoReynolds.Evaluate(conditions.atmDensity * localMach);
                    localMach /= conditions.speedOfSound;
                    rAeroForce += ctrls[i].GetForce(partInflow, localMach, pitchInput, localPRDM, out Vector3 pTorque, origin) * localVelFactor;
                    rTorque += pTorque * localVelFactor;
                    if (conditions.altitude <= 50 && conditions.speed >= 15 && conditions.speed < 25 && Vector3.Angle(inflow, Vector3.forward) < 10)
                    {
                        if (i == ctrls.Count - 1 && surfaces.Count == 0)
                        {
                            Debug.LogFormat("\nAxis {0}\tAngularVelocity {1}\nSpeed {2}\tAltitude {3}\tRotated Inflow {4}", axis, angularVelocity, conditions.speed, conditions.altitude, rotatedInflow);

                            float surfaceInput = 0;
                            if (!ctrls[i].ignorePitch)
                            {
                                Vector3 input = ctrls[i].inputRotation * new Vector3(!ctrls[i].ignorePitch ? pitchInput : 0, 0, 0);
                                surfaceInput = Vector3.Dot(input, ctrls[i].rotationAxis);
                                surfaceInput *= ctrls[i].authorityLimiter * 0.01f;
                                surfaceInput = Mathf.Clamp(surfaceInput, -1, 1);
                            }
                            if (ctrls[i].deployed)
                            {
                                surfaceInput += ctrls[i].deployAngle;
                                surfaceInput = Mathf.Clamp(surfaceInput, -1.5f, 1.5f);
                            }
                            surfaceInput *= ctrls[i].deflectionDirection;

                            Vector3 relLiftVector;
                            if (surfaceInput != 0)
                                relLiftVector = Quaternion.AngleAxis(ctrls[i].ctrlSurfaceRange * surfaceInput, ctrls[i].rotationAxis) * ctrls[i].liftVector;
                            else
                                relLiftVector = ctrls[i].liftVector;

                            float dot = Vector3.Dot(partInflow, relLiftVector);
                            float absdot = ctrls[i].omnidirectional ? Math.Abs(dot) : Mathf.Clamp01(dot);
                            Vector3 lift = Vector3.zero;
                            lock (ctrls[i].liftCurve)
                                lift = -relLiftVector * Math.Sign(dot) * ctrls[i].liftCurve.Evaluate(absdot) * ctrls[i].liftMachCurve.Evaluate(localMach) * ctrls[i].deflectionLiftCoeff * PhysicsGlobals.LiftMultiplier;
                            if (ctrls[i].perpendicularOnly)
                                lift = Vector3.ProjectOnPlane(lift, -partInflow);
                            Debug.LogFormat("Inflow vector: {3}\tBase lift vector: {0}\tCtrl lift vector: {1}\tDot: {2}\tAoA: {4}", ctrls[i].liftVector, relLiftVector, dot, partInflow, Mathf.Acos(-dot) * Mathf.Rad2Deg);
                            Debug.LogFormat("Lifting surface forces: {0} (without part drag)", lift * 1000 * Q * localVelFactor);
                        }
                        Debug.LogFormat("Ctrl {3}\tPartMotion {0}\tTorque {1}\tAeroForce{2}", partMotion, Quaternion.AngleAxis(-360f / rotationCount * r, axis) * pTorque * Q, ctrls[i].GetForce(inflow + partMotion, conditions.mach, pitchInput, conditions.pseudoReDragMult, out _, origin) * localVelFactor * Q, i);
                    }
                }

                rTorque *= Q;
                rAeroForce *= Q;

                for (int i = partCollections.Count - 1; i >= 0; i--)
                {
                    //Vector3 partMotion = Vector3.Cross(axis, (parts[i].transformPosition - origin)) * angularVelocity;
                    Vector3 partMotion = Vector3.Cross((parts[i].transformPosition - origin), axis) * angularVelocity;
                    rAeroForce += partCollections[i].GetAeroForce(rotatedInflow + partMotion, conditions, pitchInput, out Vector3 pTorque, origin);
                    rTorque += pTorque;
                }

                // Rotate torque backwards
                rTorque = Quaternion.AngleAxis(-360f / rotationCount * r, axis) * rTorque;

                torque += rTorque;
                aeroForce += rAeroForce;
            }

            aeroForce /= rotationCount;
            torque /= rotationCount;
            if (conditions.altitude <= 50 && conditions.speed >= 15 && conditions.speed < 25 && Vector3.Angle(inflow, Vector3.forward) < 10)
            {
                Debug.LogFormat("Aeroforce {0}\tTorque {1}", aeroForce, torque);
            }
            torque += Vector3.Cross(aeroForce, origin - torquePoint);

            return aeroForce;
        }

        public override Vector3 GetLiftForce(Vector3 inflow, AeroPredictor.Conditions conditions, float pitchInput, out Vector3 torque, Vector3 torquePoint)
        {
            Vector3 aeroForce = Vector3.zero;
            torque = Vector3.zero;
            int rotationCount = WindTunnelSettings.Instance.rotationCount;
            float Q = 0.0005f * conditions.atmDensity;

            // The root part is the rotor hub, so since the rotating mesh is usually cylindrical we
            // only need to evaluate this part once.
            if (!parts[0].shieldedFromAirstream)
            {
                float localMach = inflow.magnitude;
                float localVelFactor = localMach * localMach;
                float localPRDM;
                lock (PhysicsGlobals.DragCurvePseudoReynolds)
                    localPRDM = PhysicsGlobals.DragCurvePseudoReynolds.Evaluate(conditions.atmDensity * localMach);
                localMach /= conditions.speedOfSound;
                aeroForce += parts[0].GetAero(inflow.normalized, localMach, localPRDM, out Vector3 pTorque, origin) * localVelFactor * Q;
                torque += pTorque * localVelFactor * Q;
            }

            for (int r = 0; r < rotationCount; r++)
            {
                Quaternion rotation = Quaternion.AngleAxis(360f / rotationCount * r, axis);
                Vector3 rTorque = Vector3.zero;
                Vector3 rAeroForce = Vector3.zero;
                // Rotate inflow
                Vector3 rotatedInflow = rotation * inflow;
                // Calculate forces
                for (int i = parts.Count - 1; i >= 1; i--)
                {
                    if (parts[i].shieldedFromAirstream)
                        continue;
                    //Vector3 partMotion = Vector3.Cross(axis, (parts[i].transformPosition - origin)) * angularVelocity + rotatedInflow;
                    Vector3 partMotion = Vector3.Cross((parts[i].transformPosition - origin), axis) * angularVelocity + rotatedInflow;
                    Vector3 partInflow = partMotion.normalized;
                    float localMach = partMotion.magnitude;
                    float localVelFactor = localMach * localMach;
                    localMach /= conditions.speedOfSound;
                    rAeroForce += parts[i].GetLift(partInflow, localMach, out Vector3 pTorque, torquePoint) * localVelFactor;
                    rTorque += pTorque * localVelFactor;
                }
                for (int i = surfaces.Count - 1; i >= 0; i--)
                {
                    if (surfaces[i].part.shieldedFromAirstream)
                        continue;
                    //Vector3 partMotion = Vector3.Cross(axis, (surfaces[i].part.transformPosition + surfaces[i].velocityOffset - origin)) * angularVelocity + rotatedInflow;
                    Vector3 partMotion = Vector3.Cross((surfaces[i].part.transformPosition + surfaces[i].velocityOffset - origin), axis) * angularVelocity + rotatedInflow;
                    Vector3 partInflow = partMotion.normalized;
                    float localMach = partMotion.magnitude;
                    float localVelFactor = localMach * localMach;
                    localMach /= conditions.speedOfSound;
                    rAeroForce += surfaces[i].GetLift(partInflow, localMach, out Vector3 pTorque, torquePoint) * localVelFactor;
                    rTorque += pTorque * localVelFactor;
                }
                for (int i = ctrls.Count - 1; i >= 0; i--)
                {
                    if (ctrls[i].part.shieldedFromAirstream)
                        continue;
                    //Vector3 partMotion = Vector3.Cross(axis, (ctrls[i].part.transformPosition + ctrls[i].velocityOffset - origin)) * angularVelocity + rotatedInflow;
                    Vector3 partMotion = Vector3.Cross((ctrls[i].part.transformPosition + ctrls[i].velocityOffset - origin), axis) * angularVelocity + rotatedInflow;
                    Vector3 partInflow = partMotion.normalized;
                    float localMach = partMotion.magnitude;
                    float localVelFactor = localMach * localMach;
                    localMach /= conditions.speedOfSound;
                    rAeroForce += ctrls[i].GetLift(partInflow, localMach, pitchInput, out Vector3 pTorque, torquePoint) * localVelFactor;
                    rTorque += pTorque * localVelFactor;
                }

                rTorque *= Q;
                rAeroForce *= Q;

                for (int i = partCollections.Count - 1; i >= 0; i--)
                {
                    //Vector3 partMotion = Vector3.Cross(axis, partCollections[i].origin - this.origin) * angularVelocity;
                    Vector3 partMotion = Vector3.Cross(partCollections[i].origin - this.origin, axis) * angularVelocity;
                    rAeroForce += partCollections[i].GetLiftForce(rotatedInflow + partMotion, conditions, pitchInput, out Vector3 pTorque, torquePoint);
                    rTorque += pTorque;
                }

                // Rotate torque backwards
                rTorque = Quaternion.AngleAxis(-360f / rotationCount * r, axis) * rTorque;

                torque += rTorque;
                aeroForce += rAeroForce;
            }
            aeroForce /= rotationCount;
            torque /= rotationCount;
            torque += Vector3.Cross(aeroForce, origin - torquePoint);

            return aeroForce;
        }

        public override Vector3 GetThrustForce(float mach, float atmDensity, float atmPressure, bool oxygenPresent)
        {
            if (engines.Count == 0 && partCollections.Count == 0)
                return Vector3.zero;

            Vector3 thrust = base.GetThrustForce(mach, atmDensity, atmPressure, oxygenPresent);

            // eliminate components normal to the axis of rotation.
            thrust = Vector3.Project(thrust, axis);

            return thrust;
        }

        public override float GetFuelBurnRate(float mach, float atmDensity, float atmPressure, bool oxygenPresent)
        {
            // Assumes all rotors are running at max torque.
            // An over-estimation, sure, but the closest I'll get without simulating the aero forces at that point,
            // which requires information this function can't get.
            //
            // Users should be aware, though, and de-tune their rotors to provide only sufficient torque throughout
            // their target flight regime.
            return base.GetFuelBurnRate(mach, atmDensity, atmPressure, oxygenPresent) + fuelConsumption;
        }

        #region Pool Methods

        private static readonly Pool<RotorPartCollection> pool = new Pool<RotorPartCollection>(Create, Reset);

        new public static int PoolSize
        {
            get { return pool.Size; }
        }

        private static RotorPartCollection Create()
        {
            return new RotorPartCollection();
        }

        public override void Release()
        {
            lock (pool)
                pool.Release(this);
        }

        private static void Reset(RotorPartCollection obj)
        {
            PartCollection.Reset(obj);
        }

        new public static RotorPartCollection Borrow(PartCollection parentCollection, Part originPart)
        {
            RotorPartCollection collection = BorrowWithoutAdding(parentCollection?.parentVessel, originPart);
            collection.parentCollection = parentCollection;
            collection.AddPart(originPart);
            return collection;
        }

        new public static RotorPartCollection Borrow(SimulatedVessel vessel, Part originPart)
        {
            RotorPartCollection collection;
            lock (pool)
                collection = pool.Borrow();
            collection.parentVessel = vessel;

            collection.Init(originPart);

            collection.AddPart(originPart);
            return collection;
        }

        public static RotorPartCollection BorrowWithoutAdding(SimulatedVessel vessel, Part originPart)
        {
            RotorPartCollection collection;
            lock (pool)
                collection = pool.Borrow();
            collection.parentVessel = vessel;

            collection.Init(originPart);
            return collection;
        }

        private void Init(Part part)
        {
            Expansions.Serenity.ModuleRoboticServoRotor rotorModule = part.FindModuleImplementing<Expansions.Serenity.ModuleRoboticServoRotor>();
            
            Transform rotorTransform = part.FindModelTransform(rotorModule.servoTransformName);

            origin = rotorTransform.position;

            axis = rotorTransform.TransformVector(rotorModule.GetMainAxis());
            if (rotorModule.rotateCounterClockwise ^ rotorModule.inverted)
                axis *= -1;
            angularVelocity = rotorModule.rpmLimit / 60 * 2 * Mathf.PI;
            fuelConsumption = rotorModule.LFPerkN * rotorModule.maxTorque;
        }
        #endregion
    }
}
