using System;
using UnityEngine;

namespace KerbalWindTunnel
{
    public abstract class AeroPredictor
    {
        public virtual bool ThreadSafe { get { return false; } }

        public abstract float Mass { get; }

        public abstract Vector3 GetAeroForce(CelestialBody body, float speed, float altitude, float AoA);
        public virtual Vector3 GetAeroForce(CelestialBody body, float speed, float altitude, float AoA, float mach)
        {
            return GetAeroForce(body, speed, altitude, AoA);
        }
        public virtual Vector3 GetAeroForce(CelestialBody body, float speed, float altitude, float AoA, float mach, float atmDensity)
        {
            return GetAeroForce(body, speed, altitude, AoA, mach);
        }
        public virtual Vector3 GetAeroForce(CelestialBody body, float speed, float altitude, float AoA, float mach, float atmDensity, float pseudoReDragMult)
        {
            return GetAeroForce(body, speed, altitude, AoA, mach, atmDensity);
        }
        public virtual Vector3 GetLiftForce(CelestialBody body, float speed, float altitude, float AoA)
        {
            return GetAeroForce(body, speed, altitude, AoA);
        }
        public virtual Vector3 GetLiftForce(CelestialBody body, float speed, float altitude,float AoA, float mach, float atmDensity)
        {
            return GetLiftForce(body, speed, altitude, AoA);
        }

        public virtual float GetLiftForceMagnitude(CelestialBody body, float speed, float altitude, float AoA)
        {
            return GetLiftForceMagnitude(GetLiftForce(body, speed, altitude, AoA), AoA);
        }
        public static float GetLiftForceMagnitude(Vector3 force, float AoA)
        {
            return (Quaternion.AngleAxis((AoA * 180 / Mathf.PI), Vector3.left) * force).y;
        }

        public virtual float GetDragForceMagnitude(CelestialBody body, float speed, float altitude, float AoA)
        {
            return GetDragForceMagnitude(GetAeroForce(body, speed, altitude, AoA), AoA);
        }
        public static float GetDragForceMagnitude(Vector3 force, float AoA)
        {
            return -(Quaternion.AngleAxis((AoA * 180 / Mathf.PI), Vector3.left) * force).z;
        }

        public abstract Vector3 GetThrustForce(float mach, float atmDensity, float atmPressure, bool oxygenPresent);
        public virtual Vector3 GetThrustForce(float mach, float atmDensity, float atmPressure, bool oxygenPresent, float AoA)
        {
            return Quaternion.AngleAxis((AoA * 180 / Mathf.PI), Vector3.left) * GetThrustForce(mach, atmDensity, atmPressure, oxygenPresent);
        }
        public virtual Vector2 GetThrustForce2D(float mach, float atmDensity, float atmPressure, bool oxygenPresent)
        {
            Vector3 thrustForce = GetThrustForce(mach, atmDensity, atmPressure, oxygenPresent);
            return new Vector2(thrustForce.z, thrustForce.y);
        }
        public virtual Vector2 GetThrustForce2D(float mach, float atmDensity, float atmPressure, bool oxygenPresent, float AoA)
        {
            Vector3 thrustForce = GetThrustForce(mach, atmDensity, atmPressure, oxygenPresent, AoA);
            return new Vector2(thrustForce.z, thrustForce.y);
        }

        public abstract float GetFuelBurnRate(float mach, float atmDensity, float atmPressure, bool oxygenPresent);

        public static Vector3 ToFlightFrame(Vector3 force, float AoA)
        {
            return Quaternion.AngleAxis((AoA * 180 / Mathf.PI), Vector3.left) * force;
        }
        public static Vector3 ToVesselFrame(Vector3 force, float AoA)
        {
            return Quaternion.AngleAxis((-AoA * 180 / Mathf.PI), Vector3.left) * force;
        }

        public static Vector3 InflowVect(float AoA)
        {
            Vector3 vesselForward = Vector3d.forward;
            Vector3 vesselUp = Vector3d.up;
            return vesselForward * Mathf.Cos(-AoA) + vesselUp * Mathf.Sin(-AoA);
        }

        //public abstract AeroPredictor Clone();
    }
}
