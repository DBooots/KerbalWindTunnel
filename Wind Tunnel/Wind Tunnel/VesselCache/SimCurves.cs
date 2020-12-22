using Smooth.Pools;
using KerbalWindTunnel.Extensions;

namespace KerbalWindTunnel.VesselCache
{
    // FloatCurve (Unity Animation curve) are not thread safe so we need a local copy of the curves for the thread
    public class SimCurves
    {
        private static readonly Pool<SimCurves> pool = new Pool<SimCurves>(Create, Reset);

        private SimCurves()
        {
        }

        public static int PoolSize
        {
            get { return pool.Size; }
        }

        private static SimCurves Create()
        {
            return new SimCurves();
        }

        public virtual void Release()
        {
            pool.Release(this);
        }

        private static void Reset(SimCurves obj)
        {
        }

        public static SimCurves Borrow(CelestialBody newBody)
        {
            SimCurves curve;
            lock (pool)
                curve = pool.Borrow();
            curve.Setup(newBody);
            return curve;
        }

        private void Setup(CelestialBody newBody)
        {
            // No point in copying those again if we already have them loaded
            if (!loaded)
            {
                DragCurvePseudoReynolds = PhysicsGlobals.DragCurvePseudoReynolds.Clone();

                DragCurveCd = PhysicsGlobals.DragCurveCd.Clone();
                DragCurveCdPower = PhysicsGlobals.DragCurveCdPower.Clone();
                DragCurveMultiplier = PhysicsGlobals.DragCurveMultiplier.Clone();

                DragCurveSurface = PhysicsGlobals.SurfaceCurves.dragCurveSurface.Clone();
                DragCurveTail = PhysicsGlobals.SurfaceCurves.dragCurveTail.Clone();
                DragCurveTip = PhysicsGlobals.SurfaceCurves.dragCurveTip.Clone();

                LiftCurve = PhysicsGlobals.BodyLiftCurve.liftCurve.Clone();
                LiftMachCurve = PhysicsGlobals.BodyLiftCurve.liftMachCurve.Clone();
                DragCurve = PhysicsGlobals.BodyLiftCurve.dragCurve.Clone();
                DragMachCurve = PhysicsGlobals.BodyLiftCurve.dragMachCurve.Clone();

                SpaceTemperature = PhysicsGlobals.SpaceTemperature;
                loaded = true;
            }

            if (newBody != body)
            {
                body = newBody;
                if (body != null)
                {
                    AtmospherePressureCurve = newBody.atmospherePressureCurve.Clone();
                    AtmosphereTemperatureSunMultCurve = newBody.atmosphereTemperatureSunMultCurve.Clone();
                    LatitudeTemperatureBiasCurve = newBody.latitudeTemperatureBiasCurve.Clone();
                    LatitudeTemperatureSunMultCurve = newBody.latitudeTemperatureSunMultCurve.Clone();
                    AtmosphereTemperatureCurve = newBody.atmosphereTemperatureCurve.Clone();
                    AxialTemperatureSunMultCurve = newBody.axialTemperatureSunMultCurve.Clone();
                }
            }
        }

        public float GetPressure(float altitude)
        {
            if (!body.atmosphere)
            {
                return 0;
            }

            if (altitude >= body.atmosphereDepth)
            {
                return 0;
            }
            if (!body.atmosphereUsePressureCurve)
            {
                return (float)(body.atmospherePressureSeaLevel * System.Math.Pow(1 - body.atmosphereTemperatureLapseRate * altitude / body.atmosphereTemperatureSeaLevel, body.atmosphereGasMassLapseRate));
            }
            if (!body.atmospherePressureCurveIsNormalized)
            {
                float res = 0;
                lock(this.AtmospherePressureCurve)
                    res = this.AtmospherePressureCurve.Evaluate(altitude);
                return res;
            }
            float result = 0;
            lock (this.AtmospherePressureCurve)
                result = this.AtmospherePressureCurve.Evaluate((float)(altitude / body.atmosphereDepth));
            return UnityEngine.Mathf.Lerp(0f, (float)body.atmospherePressureSeaLevel, result);
        }

        public float GetDensity(float altitude)
        {
            if (!body.atmosphere)
                return 0;

            double pressure = GetPressure(altitude);
            double temp = GetTemperature(altitude);

            return (float)body.GetDensity(pressure, temp); //(float)FlightGlobals.getAtmDensity(pressure, temp, body);
        }

        public float GetTemperature(float altitude)
        {
            double temperature = 0;
            if (altitude >= body.atmosphereDepth)
            {
                return (float)this.SpaceTemperature;
            }
            if (!body.atmosphereUseTemperatureCurve)
            {
                temperature = body.atmosphereTemperatureSeaLevel - body.atmosphereTemperatureLapseRate * altitude;
            }
            else
            {
                lock (this.AtmosphereTemperatureCurve)
                    temperature = !body.atmosphereTemperatureCurveIsNormalized ?
                        this.AtmosphereTemperatureCurve.Evaluate((float)altitude) :
                        UtilMath.Lerp(this.SpaceTemperature, body.atmosphereTemperatureSeaLevel, this.AtmosphereTemperatureCurve.Evaluate((float)(altitude / body.atmosphereDepth)));
            }
            lock (this.AtmosphereTemperatureSunMultCurve)
                temperature += this.AtmosphereTemperatureSunMultCurve.Evaluate(altitude)
                                 * (this.LatitudeTemperatureBiasCurve.Evaluate(0)
                                    + this.LatitudeTemperatureSunMultCurve.Evaluate(0)
                                    + this.AxialTemperatureSunMultCurve.Evaluate(0));

            return (float)temperature;
        }

        private bool loaded = false;

        private CelestialBody body;

        public FloatCurve DragCurvePseudoReynolds { get; private set; }

        public FloatCurve LiftCurve { get; private set; }

        public FloatCurve LiftMachCurve { get; private set; }

        public FloatCurve DragCurve { get; private set; }

        public FloatCurve DragMachCurve { get; private set; }

        public FloatCurve DragCurveTail { get; private set; }

        public FloatCurve DragCurveSurface { get; private set; }

        public FloatCurve DragCurveTip { get; private set; }

        public FloatCurve DragCurveCd { get; private set; }

        public FloatCurve DragCurveCdPower { get; private set; }

        public FloatCurve DragCurveMultiplier { get; private set; }

        public FloatCurve AtmospherePressureCurve { get; private set; }

        public FloatCurve AtmosphereTemperatureSunMultCurve { get; private set; }

        public FloatCurve LatitudeTemperatureBiasCurve { get; private set; }

        public FloatCurve LatitudeTemperatureSunMultCurve { get; private set; }

        public FloatCurve AxialTemperatureSunMultCurve { get; private set; }

        public FloatCurve AtmosphereTemperatureCurve { get; private set; }

        public double SpaceTemperature { get; private set; }

    }
}
