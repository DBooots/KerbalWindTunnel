using System;
using KerbalWindTunnel.Extensions.Reflection;

namespace KerbalWindTunnel.FARVesselCache
{
    public static partial class FARAeroUtil
    {
        private static Func<double, double, double, double, double, double, double> skinFrictionDrag_method1;
        private static Func<double, double, double> skinFrictionDrag_method2;
        private static Func<CelestialBody, Vector3d, double, double> getTemperature_method;
        private static Func<CelestialBody, Vector3d, double, double> getAdiabaticIndex_method;
        private static Func<double, double, double, double, double> calculateOswaldsEfficiencyNitaScholz_method;
        private static Func<double, double, double> calculateSinMaxShockAngle_method;
        private static Func<double, double, double, double> calculateSinWeakObliqueShockAngle_method;
        private static Action<CelestialBody> updateCurrentActiveBody_method;
        private static Func<double, double, double, double, double, double, double> calculateReynoldsNumber_method;

        //public static FloatCurve PrandtlMeyerMach;
        //public static FloatCurve PrandtlMeyerAngle;
        //public static double maxPrandtlMeyerTurnAngle;

        public const double lat = 0;
        public const double lon = 0;
        public const double ut = 0;

        public static double SkinFrictionDrag(double density, double lengthScale, double vel, double machNumber, double externalTemp, double gamma)
            => skinFrictionDrag_method1(density, lengthScale, vel, machNumber, externalTemp, gamma);
        public static double SkinFrictionDrag(double reynoldsNumber, double machNumber)
            => skinFrictionDrag_method2(reynoldsNumber, machNumber);
        public static double GetTemperature(AeroPredictor.Conditions conditions)
            => getTemperature_method(conditions.body, new Vector3d(lat, lon, conditions.altitude), ut);
        public static double GetAdiabaticIndex(AeroPredictor.Conditions conditions)
            => getAdiabaticIndex_method(conditions.body, new Vector3d(lat, lon, conditions.altitude), ut);
        public static double CalculateOswaldsEfficiencyNitaScholz(double AR, double CosSweepAngle, double Cd0, double taperRatio)
            => calculateOswaldsEfficiencyNitaScholz_method(AR, CosSweepAngle, Cd0, taperRatio);
        public static double CalculateSinMaxShockAngle(double MachNumber, double gamma)
            => calculateSinMaxShockAngle_method(MachNumber, gamma);
        public static double CalculateSinWeakObliqueShockAngle(double MachNumber, double gamma, double deflectionAngle)
            => calculateSinWeakObliqueShockAngle_method(MachNumber, gamma, deflectionAngle);
        public static double CalculateReynoldsNumber(double density, double lengthScale, double vel, double machNumber, double externalTemp, double gamma)
            => calculateReynoldsNumber_method(density, lengthScale, vel, machNumber, externalTemp, gamma);

        public static void UpdateCurrentActiveBody(CelestialBody body)
        {
            updateCurrentActiveBody_method(body);
            PrandtlMeyerAngle.ClearCache(body);
            //PrandtlMeyerMach = prandtlMeyerMach_get().Clone();
            //PrandtlMeyerAngle = prandtlMeyerAngle_get().Clone();
        }

        public static bool InitializeMethods(Type FARAeroUtilType, Type FARAtmosphereType)
        {
            skinFrictionDrag_method1 = FARAeroUtilType.StaticMethod<Func<double, double, double, double, double, double, double>>("SkinFrictionDrag");
            skinFrictionDrag_method2 = FARAeroUtilType.StaticMethod<Func<double, double, double>>("SkinFrictionDrag");
            getTemperature_method = FARAtmosphereType.StaticMethod<Func<CelestialBody, Vector3d, double, double>>("GetTemperature");
            getAdiabaticIndex_method = FARAtmosphereType.StaticMethod<Func<CelestialBody, Vector3d, double, double>>("GetAdiabaticIndex");
            calculateOswaldsEfficiencyNitaScholz_method = FARAeroUtilType.StaticMethod<Func<double, double, double, double, double>>("CalculateOswaldsEfficiencyNitaScholz");
            calculateSinMaxShockAngle_method = FARAeroUtilType.StaticMethod<Func<double, double, double>>("CalculateSinMaxShockAngle");
            calculateSinWeakObliqueShockAngle_method = FARAeroUtilType.StaticMethod<Func<double, double, double, double>>("CalculateSinWeakObliqueShockAngle");
            //prandtlMeyerMach_get = FARAeroUtilType.StaticMethod<Func<FloatCurve>>(FARAeroUtilType.GetProperty("PrandtlMeyerMach", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).GetGetMethod());
            //prandtlMeyerAngle_get = FARAeroUtilType.StaticMethod<Func<FloatCurve>>(FARAeroUtilType.GetProperty("PrandtlMeyerAngle", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).GetGetMethod());
            //prandtlMeyerMach_set = FARAeroUtilType.StaticFieldSet<FloatCurve>(FARAeroUtilType.GetField("prandtlMeyerMach", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
            //prandtlMeyerAngle_set = FARAeroUtilType.StaticFieldSet<FloatCurve>(FARAeroUtilType.GetField("prandtlMeyerAngle", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
            updateCurrentActiveBody_method = FARAeroUtilType.StaticMethod<Action<CelestialBody>>("UpdateCurrentActiveBody");
            calculateReynoldsNumber_method = FARAeroUtilType.StaticMethod<Func<double, double, double, double, double, double, double>>("CalculateReynoldsNumber");

            return true;
        }
    }
}
