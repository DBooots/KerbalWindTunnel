using Smooth.Pools;

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
            SimCurves curve = pool.Borrow();
            curve.Setup(newBody);
            return curve;
        }

        private void Setup(CelestialBody newBody)
        {
            // No point in copying those again if we already have them loaded
            if (!loaded)
            {
                dragCurvePseudoReynolds = new FloatCurve(PhysicsGlobals.DragCurvePseudoReynolds.Curve.keys);

                dragCurveCd = new FloatCurve(PhysicsGlobals.DragCurveCd.Curve.keys);
                dragCurveCdPower = new FloatCurve(PhysicsGlobals.DragCurveCdPower.Curve.keys);
                dragCurveMultiplier = new FloatCurve(PhysicsGlobals.DragCurveMultiplier.Curve.keys);

                dragCurveSurface = new FloatCurve(PhysicsGlobals.SurfaceCurves.dragCurveSurface.Curve.keys);
                dragCurveTail = new FloatCurve(PhysicsGlobals.SurfaceCurves.dragCurveTail.Curve.keys);
                dragCurveTip = new FloatCurve(PhysicsGlobals.SurfaceCurves.dragCurveTip.Curve.keys);

                liftCurve = new FloatCurve(PhysicsGlobals.BodyLiftCurve.liftCurve.Curve.keys);
                liftMachCurve = new FloatCurve(PhysicsGlobals.BodyLiftCurve.liftMachCurve.Curve.keys);
                dragCurve = new FloatCurve(PhysicsGlobals.BodyLiftCurve.dragCurve.Curve.keys);
                dragMachCurve = new FloatCurve(PhysicsGlobals.BodyLiftCurve.dragMachCurve.Curve.keys);

                spaceTemperature = PhysicsGlobals.SpaceTemperature;
                loaded = true;
            }

            if (newBody != body)
            {
                body = newBody;
                if (body != null)
                {
                    atmospherePressureCurve = new FloatCurve(newBody.atmospherePressureCurve.Curve.keys);
                    atmosphereTemperatureSunMultCurve = new FloatCurve(newBody.atmosphereTemperatureSunMultCurve.Curve.keys);
                    latitudeTemperatureBiasCurve = new FloatCurve(newBody.latitudeTemperatureBiasCurve.Curve.keys);
                    latitudeTemperatureSunMultCurve = new FloatCurve(newBody.latitudeTemperatureSunMultCurve.Curve.keys);
                    atmosphereTemperatureCurve = new FloatCurve(newBody.atmosphereTemperatureCurve.Curve.keys);
                    axialTemperatureSunMultCurve = new FloatCurve(newBody.axialTemperatureSunMultCurve.Curve.keys);
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

        private FloatCurve dragCurvePseudoReynolds;

        private FloatCurve liftCurve;
        private FloatCurve liftMachCurve;
        private FloatCurve dragCurve;
        private FloatCurve dragMachCurve;

        private FloatCurve dragCurveTail;
        private FloatCurve dragCurveSurface;
        private FloatCurve dragCurveTip;

        private FloatCurve dragCurveCd;
        private FloatCurve dragCurveCdPower;
        private FloatCurve dragCurveMultiplier;

        private FloatCurve atmospherePressureCurve;

        private FloatCurve atmosphereTemperatureSunMultCurve;

        private FloatCurve latitudeTemperatureBiasCurve;

        private FloatCurve latitudeTemperatureSunMultCurve;

        private FloatCurve axialTemperatureSunMultCurve;

        private FloatCurve atmosphereTemperatureCurve;

        private double spaceTemperature;

        public FloatCurve DragCurvePseudoReynolds
        {
            get { return dragCurvePseudoReynolds; }
        }

        public FloatCurve LiftCurve
        {
            get { return liftCurve; }
        }

        public FloatCurve LiftMachCurve
        {
            get { return liftMachCurve; }
        }

        public FloatCurve DragCurve
        {
            get { return dragCurve; }
        }

        public FloatCurve DragMachCurve
        {
            get { return dragMachCurve; }
        }

        public FloatCurve DragCurveTail
        {
            get { return dragCurveTail; }
        }

        public FloatCurve DragCurveSurface
        {
            get { return dragCurveSurface; }
        }

        public FloatCurve DragCurveTip
        {
            get { return dragCurveTip; }
        }

        public FloatCurve DragCurveCd
        {
            get { return dragCurveCd; }
        }

        public FloatCurve DragCurveCdPower
        {
            get { return dragCurveCdPower; }
        }

        public FloatCurve DragCurveMultiplier
        {
            get { return dragCurveMultiplier; }
        }

        public FloatCurve AtmospherePressureCurve
        {
            get { return atmospherePressureCurve; }
        }

        public FloatCurve AtmosphereTemperatureSunMultCurve
        {
            get { return atmosphereTemperatureSunMultCurve; }
        }

        public FloatCurve LatitudeTemperatureBiasCurve
        {
            get { return latitudeTemperatureBiasCurve; }
        }

        public FloatCurve LatitudeTemperatureSunMultCurve
        {
            get { return latitudeTemperatureSunMultCurve; }
        }

        public FloatCurve AxialTemperatureSunMultCurve
        {
            get { return axialTemperatureSunMultCurve; }
        }

        public FloatCurve AtmosphereTemperatureCurve
        {
            get { return atmosphereTemperatureCurve; }
        }

        public double SpaceTemperature
        {
            get { return spaceTemperature; }
        }

    }
}
