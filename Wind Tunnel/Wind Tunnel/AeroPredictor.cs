using System;
using UnityEngine;

namespace KerbalWindTunnel
{
    public abstract class AeroPredictor
    {
        public virtual bool ThreadSafe { get { return false; } }

        public abstract float Mass { get; }

        public virtual float GetAoA(RootSolvers.RootSolver solver, Conditions conditions, float offsettingForce, bool useThrust = true, bool dryTorque = false)
        {
            Vector3 thrustForce = useThrust ? this.GetThrustForce(conditions) : Vector3.zero;
            return solver.Solve(
                    (aoa) =>
                        GetLiftForceMagnitude(
                            this.GetLiftForce(conditions, aoa, GetPitchInput(solver, conditions, aoa, dryTorque)) + thrustForce, aoa)
                        - offsettingForce,
                    0, WindTunnelWindow.Instance.solverSettings);
        }
        public virtual float GetAoA(RootSolvers.RootSolver solver, Conditions conditions, float offsettingForce, out float pitchInput, bool useThrust = true, bool dryTorque = false)
        {
            Vector3 thrustForce = useThrust ? this.GetThrustForce(conditions) : Vector3.zero;
            float pi = 0;
            float result = solver.Solve(
                    (aoa) =>
                        GetLiftForceMagnitude(
                            this.GetLiftForce(conditions, aoa, pi = GetPitchInput(solver, conditions, aoa, dryTorque)) + thrustForce, aoa)
                        - offsettingForce,
                    0, WindTunnelWindow.Instance.solverSettings);
            pitchInput = pi;
            return result;
        }

        public abstract float GetPitchInput(RootSolvers.RootSolver solver, Conditions conditions, float AoA, bool dryTorque = false);
        
        public abstract Vector3 GetAeroForce(Conditions conditions, float AoA, float pitchInput = 0);

        public virtual Vector3 GetLiftForce(Conditions conditions, float AoA, float pitchInput = 0)
        {
            return GetAeroForce(conditions, AoA, pitchInput);
        }

        public abstract Vector3 GetAeroTorque(Conditions conditions, float AoA, float pitchInput = 0, bool dryTorque = false);
        
        public virtual void GetAeroCombined(Conditions conditions, float AoA, float pitchInput, out Vector3 forces, out Vector3 torques, bool dryTorque = false)
        {
            forces = GetAeroForce(conditions, AoA, pitchInput);
            torques = GetAeroTorque(conditions, AoA, pitchInput);
        }

        public virtual float GetLiftForceMagnitude(Conditions conditions, float AoA, float pitchInput = 0)
        {
            return GetLiftForceMagnitude(GetLiftForce(conditions, AoA, pitchInput), AoA);
        }
        public static float GetLiftForceMagnitude(Vector3 force, float AoA)
        {
            return (Quaternion.AngleAxis((AoA * Mathf.Rad2Deg), Vector3.left) * force).y;
        }

        public virtual float GetDragForceMagnitude(Conditions conditions, float AoA, float pitchInput = 0)
        {
            return GetDragForceMagnitude(GetAeroForce(conditions, AoA, pitchInput), AoA);
        }
        public static float GetDragForceMagnitude(Vector3 force, float AoA)
        {
            return -(Quaternion.AngleAxis((AoA * Mathf.Rad2Deg), Vector3.left) * force).z;
        }

        public virtual Vector3 GetThrustForce(Conditions conditions)
        {
            return GetThrustForce(conditions.mach, conditions.atmDensity, conditions.atmPressure, conditions.oxygenAvailable);
        }
        public virtual Vector3 GetThrustForce(Conditions conditions, float AoA)
        {
            return Quaternion.AngleAxis((AoA * Mathf.Rad2Deg), Vector3.left) * GetThrustForce(conditions);
        }

        public abstract Vector3 GetThrustForce(float mach, float atmDensity, float atmPressure, bool oxygenPresent);
        public virtual Vector3 GetThrustForce(float mach, float atmDensity, float atmPressure, bool oxygenPresent, float AoA)
        {
            return Quaternion.AngleAxis((AoA * Mathf.Rad2Deg), Vector3.left) * GetThrustForce(mach, atmDensity, atmPressure, oxygenPresent);
        }

        public virtual Vector2 GetThrustForce2D(Conditions conditions)
        {
            Vector3 thrustForce = GetThrustForce(conditions);
            return new Vector2(thrustForce.z, thrustForce.y);
        }
        public virtual Vector2 GetThrustForce2D(float mach, float atmDensity, float atmPressure, bool oxygenPresent)
        {
            Vector3 thrustForce = GetThrustForce(mach, atmDensity, atmPressure, oxygenPresent);
            return new Vector2(thrustForce.z, thrustForce.y);
        }
        public virtual Vector2 GetThrustForce2D(Conditions conditions, float AoA)
        {
            Vector3 thrustForce = GetThrustForce(conditions, AoA);
            return new Vector2(thrustForce.z, thrustForce.y);
        }
        public virtual Vector2 GetThrustForce2D(float mach, float atmDensity, float atmPressure, bool oxygenPresent, float AoA)
        {
            Vector3 thrustForce = GetThrustForce(mach, atmDensity, atmPressure, oxygenPresent, AoA);
            return new Vector2(thrustForce.z, thrustForce.y);
        }

        public virtual float GetFuelBurnRate(Conditions conditions)
        {
            return GetFuelBurnRate(conditions.mach, conditions.atmDensity, conditions.atmPressure, conditions.oxygenAvailable);
        }
        public abstract float GetFuelBurnRate(float mach, float atmDensity, float atmPressure, bool oxygenPresent);

        public static Vector3 ToFlightFrame(Vector3 force, float AoA)
        {
            return Quaternion.AngleAxis((AoA * Mathf.Rad2Deg), Vector3.left) * force;
        }
        public static Vector3 ToVesselFrame(Vector3 force, float AoA)
        {
            return Quaternion.AngleAxis((-AoA * Mathf.Rad2Deg), Vector3.left) * force;
        }

        public static Vector3 InflowVect(float AoA)
        {
            Vector3 vesselForward = Vector3d.forward;
            Vector3 vesselUp = Vector3d.up;
            return vesselForward * Mathf.Cos(-AoA) + vesselUp * Mathf.Sin(-AoA);
        }

        //public abstract AeroPredictor Clone();

        public struct Conditions
        {
            public readonly CelestialBody body;
            public readonly float speed;
            public readonly float altitude;
            public readonly float mach;
            public readonly float atmDensity;
            public readonly float atmPressure;
            public readonly float pseudoReDragMult;
            public readonly bool oxygenAvailable;

            public Conditions(CelestialBody body, float speed, float altitude)
            {
                this.body = body;
                this.speed = speed;
                this.altitude = altitude;
                
                lock (body)
                {
                    this.atmPressure = (float)body.GetPressure(altitude);
                    this.atmDensity = (float)Extensions.KSPClassExtensions.GetDensity(body, altitude);
                    this.mach = (float)(speed / body.GetSpeedOfSound(atmPressure, atmDensity));
                    this.oxygenAvailable = body.atmosphereContainsOxygen;
                }
                FloatCurve pseudoReynoldsCurve;
                lock (PhysicsGlobals.DragCurvePseudoReynolds)
                    pseudoReynoldsCurve = new FloatCurve(PhysicsGlobals.DragCurvePseudoReynolds.Curve.keys);
                this.pseudoReDragMult = pseudoReynoldsCurve.Evaluate(atmDensity * speed);
            }
        }
    }
}
