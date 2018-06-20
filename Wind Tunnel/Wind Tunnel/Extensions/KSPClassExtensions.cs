using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KerbalWindTunnel.Extensions
{
    public static class KSPClassExtensions
    {
        /*
        From Trajectories
        Copyright 2014, Youen Toupin
        This method is part of Trajectories, under MIT license.
        StockAeroUtil by atomicfury
        */
        /// <summary>
        /// Gets the air density (rho) for the specified altitude on the specified body.
        /// This is an approximation, because actual calculations, taking sun exposure into account to compute air temperature, require to know the actual point on the body where the density is to be computed (knowing the altitude is not enough).
        /// However, the difference is small for high altitudes, so it makes very little difference for trajectory prediction.
        /// From StockAeroUtil.cs from Trajectories
        /// </summary>
        /// <param name="body"></param>
        /// <param name="altitude">Altitude above sea level (in meters)</param>
        /// <returns></returns>
        public static double GetDensity(this CelestialBody body, double altitude)
        {
            if (!body.atmosphere)
                return 0;

            if (altitude > body.atmosphereDepth)
                return 0;

            double pressure = body.GetPressure(altitude);

            // get an average day/night temperature at the equator
            double sunDot = 0.5;
            float sunAxialDot = 0;
            double atmosphereTemperatureOffset = (double)body.latitudeTemperatureBiasCurve.Evaluate(0)
                + (double)body.latitudeTemperatureSunMultCurve.Evaluate(0) * sunDot
                + (double)body.axialTemperatureSunMultCurve.Evaluate(sunAxialDot);
            double temperature = // body.GetFullTemperature(altitude, atmosphereTemperatureOffset);
                body.GetTemperature(altitude)
                + (double)body.atmosphereTemperatureSunMultCurve.Evaluate((float)altitude) * atmosphereTemperatureOffset;


            return body.GetDensity(pressure, temperature);
        }

        public static FloatCurve Clone(this FloatCurve inCurve)
        {
            return new FloatCurve(inCurve.Curve.keys);
        }

        public static UnityEngine.Vector3 ProjectOnPlaneSafe(UnityEngine.Vector3 vector, UnityEngine.Vector3 planeNormal)
        {
            return vector - UnityEngine.Vector3.Dot(vector, planeNormal) / planeNormal.sqrMagnitude * planeNormal;
        }
    }
}
