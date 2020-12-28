using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Graphing;
using KerbalWindTunnel.Extensions;
using System.Collections.Concurrent;

namespace KerbalWindTunnel.DataGenerators
{
    public class EnvelopeSurf : DataSetGenerator
    {
        #region EnvelopeSurf
        protected static readonly ColorMap Jet_Dark_Positive = new ColorMap(ColorMap.Jet_Dark) { Filter = (v) => v >= 0 && !float.IsNaN(v) && !float.IsInfinity(v) };

        public EnvelopePoint[,] envelopePoints = new EnvelopePoint[0, 0];
        public Conditions currentConditions = Conditions.Blank;
        //private Dictionary<Conditions, EnvelopePoint[,]> cache = new Dictionary<Conditions, EnvelopePoint[,]>();
        private ConcurrentDictionary<SurfCoords, EnvelopePoint> cache = new ConcurrentDictionary<SurfCoords, EnvelopePoint>();
        
        public int[,] resolution = { { 10, 10 }, { 40, 120 }, { 80, 180 }, { 160, 360 } };

        public EnvelopeSurf()
        {
            graphables.Clear();

            float bottom = 0, top = 0, left = 0, right = 0;
            float[,] blank = new float[0, 0];

            graphables.Add(new SurfGraph(blank, left, right, bottom, top) { Name = "Excess Thrust", ZUnit = "kN", StringFormat = "N0", Color = Jet_Dark_Positive });
            graphables.Add(new SurfGraph(blank, left, right, bottom, top) { Name = "Excess Acceleration", ZUnit = "g", StringFormat = "N2", Color = Jet_Dark_Positive });
            graphables.Add(new SurfGraph(blank, left, right, bottom, top) { Name = "Thrust Available", ZUnit = "kN", StringFormat = "N0", Color = ColorMap.Jet_Dark });
            graphables.Add(new SurfGraph(blank, left, right, bottom, top) { Name = "Level AoA", ZUnit = "°", StringFormat = "F2", Color = ColorMap.Jet_Dark });
            graphables.Add(new SurfGraph(blank, left, right, bottom, top) { Name = "Max Lift AoA", ZUnit = "°", StringFormat = "F2", Color = ColorMap.Jet_Dark });
            graphables.Add(new SurfGraph(blank, left, right, bottom, top) { Name = "Max Lift", ZUnit = "kN", StringFormat = "N0", Color = ColorMap.Jet_Dark });
            graphables.Add(new SurfGraph(blank, left, right, bottom, top) { Name = "Lift/Drag Ratio", ZUnit = "", StringFormat = "F2", Color = ColorMap.Jet_Dark });
            graphables.Add(new SurfGraph(blank, left, right, bottom, top) { Name = "Drag", ZUnit = "kN", StringFormat = "N0", Color = ColorMap.Jet_Dark });
            graphables.Add(new SurfGraph(blank, left, right, bottom, top) { Name = "Lift Slope", ZUnit = "/°", StringFormat = "F3", Color = ColorMap.Jet_Dark });
            graphables.Add(new SurfGraph(blank, left, right, bottom, top) { Name = "Pitch Input", ZUnit = "", StringFormat = "F2", Color = ColorMap.Jet_Dark });
            graphables.Add(new SurfGraph(blank, left, right, bottom, top) { Name = "Fuel Burn Rate", ZUnit = "kg/s", StringFormat = "F3", Color = ColorMap.Jet_Dark });
            graphables.Add(new SurfGraph(blank, left, right, bottom, top) { Name = "Fuel Economy", ZUnit = "kg/100 km", StringFormat = "F2", Color = ColorMap.Jet_Dark });
            //graphables.Add(new SurfGraph(blank, left, right, bottom, top) { Name = "Stability Derivative", ZUnit = "kNm/deg", StringFormat = "F3", Color = ColorMap.Jet_Dark });
            //graphables.Add(new SurfGraph(blank, left, right, bottom, top) { Name = "Stability Range", ZUnit = "deg", StringFormat = "F2", Color = ColorMap.Jet_Dark });
            //graphables.Add(new SurfGraph(blank, left, right, bottom, top) { Name = "Stability Score", ZUnit = "kNm-deg", StringFormat = "F1", Color = ColorMap.Jet_Dark });
            graphables.Add(new OutlineMask(blank, left, right, bottom, top) { Name = "Envelope Mask", ZUnit = "kN", StringFormat = "N0", Color = Color.grey, LineWidth = 2, LineOnly = true, MaskCriteria = (v) => !float.IsNaN(v) && !float.IsInfinity(v) && v >= 0 });
            graphables.Add(new MetaLineGraph(new Vector2[0])              { Name = "Fuel-Optimal Path", StringFormat = "N0", Color = Color.black, LineWidth = 3, MetaFields = new string[] { "Climb Angle", "Climb Rate", "Fuel Used", "Time" }, MetaStringFormats = new string[] { "N1", "N0", "N3", "N1" }, MetaUnits = new string[] { "°", "m/s", "units", "s" } });
            graphables.Add(new MetaLineGraph(new Vector2[0])              { Name = "Time-Optimal Path", StringFormat = "N0", Color = Color.white, LineWidth = 3, MetaFields = new string[] { "Climb Angle", "Climb Rate", "Time" }, MetaStringFormats = new string[] { "N1", "N0", "N1" }, MetaUnits = new string[] { "°", "m/s", "s" } });

            var e = graphables.GetEnumerator();
            while (e.MoveNext())
            {
                e.Current.XUnit = "m/s";
                e.Current.XName = "Speed";
                e.Current.YUnit = "m";
                e.Current.YName = "Altitude";
                e.Current.Visible = false;
            }
        }

        public override float PercentComplete
        {
            get
            {
                if (Status == TaskStatus.RanToCompletion)
                    return 1;
                return (float)progressNumerator / progressDenominator;
            }
        }

        public override float InternalPercentComplete
        {
            get
            {
                if (InternalStatus == TaskStatus.RanToCompletion)
                    return 1;
                return (float)progressNumerator / progressDenominator;
            }
        }

        private int progressNumerator = -1;
        private int progressDenominator = 1;

        public override void Clear()
        {
            base.Clear();
            currentConditions = Conditions.Blank;
            cache.Clear();
            progressNumerator = -1;
            progressDenominator = 1;
            envelopePoints = new EnvelopePoint[0, 0];

            ((LineGraph)graphables["Fuel-Optimal Path"]).SetValues(new Vector2[0]);
            ((LineGraph)graphables["Time-Optimal Path"]).SetValues(new Vector2[0]);
        }

        public void Calculate(CelestialBody body, float lowerBoundSpeed = 0, float upperBoundSpeed = 2000, float lowerBoundAltitude = 0, float upperBoundAltitude = 60000, float stepSpeed = 50f, float stepAltitude = 500)
        {
#if ENABLE_PROFILER
            UnityEngine.Profiling.Profiler.BeginSample("EnvelopeSurf.Calculate");
#endif
            // Set up calculation conditions and bounds
            Conditions newConditions;
            newConditions = new Conditions(body, lowerBoundSpeed, upperBoundSpeed, stepSpeed, lowerBoundAltitude, upperBoundAltitude, stepAltitude);

            if (currentConditions.Equals(newConditions) && Status != TaskStatus.WaitingToRun)
                return;

            Cancel();

            // Generate 'coarse' cache
            float firstStepSpeed = (newConditions.upperBoundSpeed - newConditions.lowerBoundSpeed) / resolution[0, 0];
            float firstStepAltitude = (newConditions.upperBoundAltitude - newConditions.lowerBoundAltitude) / resolution[0, 1];
            EnvelopePoint[] preliminaryData = new EnvelopePoint[(resolution[0, 0] + 1) * (resolution[0, 1] + 1)];
            AeroPredictor aeroPredictorToClone = WindTunnelWindow.Instance.GetAeroPredictor();
            if (aeroPredictorToClone is VesselCache.SimulatedVessel simVessel)
                simVessel.InitMaxAoA(newConditions.body, (newConditions.upperBoundAltitude - newConditions.lowerBoundAltitude) * 0.25f + newConditions.lowerBoundAltitude);

            // Probably won't run in parallel because it's very short.
            // But the UI will hang waiting for this to complete, so a self-triggering CancellationToken is provided with a life span of 5 seconds.
            try
            {
                Parallel.For(0, preliminaryData.Length, new ParallelOptions() { CancellationToken = new CancellationTokenSource(5000).Token },
                    () => WindTunnelWindow.GetUnitySafeAeroPredictor(aeroPredictorToClone), (index, state, predictor) =>
                         {
                             int x = index % (resolution[0, 0] + 1), y = index / (resolution[0, 0] + 1);
                             EnvelopePoint result = new EnvelopePoint(predictor, newConditions.body, y * firstStepAltitude + newConditions.lowerBoundAltitude, x * firstStepSpeed + newConditions.lowerBoundSpeed);
                             preliminaryData[index] = result;
                             cache[new SurfCoords(result.speed, result.altitude)] = result;
                             return predictor;
                         }, (predictor) => (predictor as VesselCache.IReleasable)?.Release());
            }
            catch (OperationCanceledException)
            {
                Debug.LogError("Wind Tunnel: Initial pass timed out.");
            }
            catch (AggregateException ex)
            {
                Debug.LogError("Wind Tunnel: Initial pass threw an inner exception.");
                Debug.LogException(ex.InnerException);
            }

            if (aeroPredictorToClone is VesselCache.IReleasable releaseable)
                releaseable.Release();

            cancellationTokenSource = new CancellationTokenSource();

            WindTunnel.Instance.StartCoroutine(Processing(newConditions, preliminaryData.To2Dimension(resolution[0, 0] + 1)));

#if ENABLE_PROFILER
            UnityEngine.Profiling.Profiler.EndSample();
#endif
        }

        public override void UpdateGraphs()
        {
            float bottom = currentConditions.lowerBoundAltitude;
            float top = currentConditions.upperBoundAltitude;
            float left = currentConditions.lowerBoundSpeed;
            float right = currentConditions.upperBoundSpeed;
            float invArea = 1f / WindTunnelWindow.Instance.CommonPredictor.Area;
            Func<EnvelopePoint, float> scale = (pt) => 1f;
            if (WindTunnelSettings.UseCoefficients)
            {
                scale = (pt) => 1f / pt.dynamicPressure * invArea;
                ((SurfGraph)graphables["Drag"]).ZUnit = "";
                ((SurfGraph)graphables["Drag"]).StringFormat = "F3";
                ((SurfGraph)graphables["Max Lift"]).ZUnit = "";
                ((SurfGraph)graphables["Max Lift"]).StringFormat = "F3";
            }
            else
            {
                ((SurfGraph)graphables["Drag"]).ZUnit = "kN";
                ((SurfGraph)graphables["Drag"]).StringFormat = "N0";
                ((SurfGraph)graphables["Max Lift"]).ZUnit = "kN";
                ((SurfGraph)graphables["Max Lift"]).StringFormat = "N0";
            }

            ((SurfGraph)graphables["Excess Thrust"]).SetValues(envelopePoints.SelectToArray(pt => pt.Thrust_excess), left, right, bottom, top);
            ((SurfGraph)graphables["Excess Acceleration"]).SetValues(envelopePoints.SelectToArray(pt => pt.Accel_excess), left, right, bottom, top);
            ((SurfGraph)graphables["Thrust Available"]).SetValues(envelopePoints.SelectToArray(pt => pt.Thrust_available), left, right, bottom, top);
            ((SurfGraph)graphables["Level AoA"]).SetValues(envelopePoints.SelectToArray(pt => pt.AoA_level * Mathf.Rad2Deg), left, right, bottom, top);
            ((SurfGraph)graphables["Max Lift AoA"]).SetValues(envelopePoints.SelectToArray(pt => pt.AoA_max * Mathf.Rad2Deg), left, right, bottom, top);
            ((SurfGraph)graphables["Max Lift"]).SetValues(envelopePoints.SelectToArray(pt => pt.Lift_max * scale(pt)), left, right, bottom, top);
            ((SurfGraph)graphables["Lift/Drag Ratio"]).SetValues(envelopePoints.SelectToArray(pt => pt.LDRatio), left, right, bottom, top);
            ((SurfGraph)graphables["Drag"]).SetValues(envelopePoints.SelectToArray(pt => pt.drag * scale(pt)), left, right, bottom, top);
            ((SurfGraph)graphables["Lift Slope"]).SetValues(envelopePoints.SelectToArray(pt => pt.dLift / pt.dynamicPressure * invArea), left, right, bottom, top);
            ((SurfGraph)graphables["Pitch Input"]).SetValues(envelopePoints.SelectToArray(pt => pt.pitchInput), left, right, bottom, top);
            ((SurfGraph)graphables["Fuel Burn Rate"]).SetValues(envelopePoints.SelectToArray(pt => pt.fuelBurnRate), left, right, bottom, top);
            //((SurfGraph)graphables["Stability Derivative"]).SetValues(envelopePoints.SelectToArray(pt => pt.stabilityDerivative), left, right, bottom, top);
            //((SurfGraph)graphables["Stability Range"]).SetValues(envelopePoints.SelectToArray(pt => pt.stabilityRange), left, right, bottom, top);
            //((SurfGraph)graphables["Stability Score"]).SetValues(envelopePoints.SelectToArray(pt => pt.stabilityScore), left, right, bottom, top);

            float[,] economy = envelopePoints.SelectToArray(pt => pt.fuelBurnRate / pt.speed * 1000 * 100);
            SurfGraph toModify = (SurfGraph)graphables["Fuel Economy"];
            toModify.SetValues(economy, left, right, bottom, top);

            try
            {
                int stallpt = EnvelopeLine.CoordLocator.GenerateCoordLocators(envelopePoints.SelectToArray(pt => pt.Thrust_excess)).First(0, 0, c => c.value >= 0);
                float minEconomy = economy[stallpt, 0] / 3;
                toModify.ZMax = minEconomy;
            }
            catch (InvalidOperationException)
            {
                Debug.LogError("The vessel cannot maintain flight at ground level. Fuel Economy graph will be weird.");
            }
            ((OutlineMask)graphables["Envelope Mask"]).SetValues(envelopePoints.SelectToArray(pt => pt.Thrust_excess), left, right, bottom, top);
        }

        private IEnumerator Processing(Conditions conditions, EnvelopePoint[,] prelimData)
        {
            CancellationToken closureCancellationToken = this.cancellationTokenSource.Token;

            EnvelopePoint[] closureProgress = new EnvelopePoint[conditions.Resolution];
            progressNumerator = 0;
            progressDenominator = conditions.Resolution;
            int cachedCount = 0;

            stopwatch.Reset();
            stopwatch.Start();

            AeroPredictor aeroPredictorToClone = WindTunnelWindow.Instance.GetAeroPredictor();
            if (aeroPredictorToClone is VesselCache.SimulatedVessel simVessel)
                simVessel.InitMaxAoA(conditions.body, (conditions.upperBoundAltitude - conditions.lowerBoundAltitude) * 0.25f + conditions.lowerBoundAltitude);

            task = new Task<EnvelopePoint[,]>(
                () =>
                {
                    float[,] AoAs_guess = null, maxAs_guess = null, pitchIs_guess = null;
                    AoAs_guess = prelimData.SelectToArray(pt => pt.AoA_level);
                    maxAs_guess = prelimData.SelectToArray(pt => pt.AoA_max);
                    pitchIs_guess = prelimData.SelectToArray(pt => pt.pitchInput);

                    try
                    {
                        //OrderablePartitioner<EnvelopePoint> partitioner = Partitioner.Create(primaryProgress, true);
                        Parallel.For<AeroPredictor>(0, closureProgress.Length, new ParallelOptions() { CancellationToken = closureCancellationToken },
                            () => WindTunnelWindow.GetUnitySafeAeroPredictor(aeroPredictorToClone),
                            (index, state, predictor) =>
                        {
                            int x = index % conditions.XResolution, y = index / conditions.XResolution;
                            SurfCoords coords = new SurfCoords(x * conditions.stepSpeed + conditions.lowerBoundSpeed,
                                y * conditions.stepAltitude + conditions.lowerBoundAltitude);

                            EnvelopePoint result;
                            if (!cache.TryGetValue(coords, out result))
                            {
                                result = new EnvelopePoint(predictor, conditions.body, y * conditions.stepAltitude + conditions.lowerBoundAltitude, x * conditions.stepSpeed + conditions.lowerBoundSpeed);
                                closureCancellationToken.ThrowIfCancellationRequested();
                                cache[coords] = result;
                            }
                            else
                                Interlocked.Increment(ref cachedCount);
                            closureProgress[index] = result;
                            Interlocked.Increment(ref progressNumerator);
                            return predictor;
                        }, (predictor) => (predictor as VesselCache.IReleasable)?.Release());

                        closureCancellationToken.ThrowIfCancellationRequested();
                        Debug.LogFormat("Wind Tunnel - Data run finished. {0} of {1} ({2:F0}%) retrieved from cache.", cachedCount, closureProgress.Length, (float)cachedCount / closureProgress.Length * 100);

                        return closureProgress.To2Dimension(conditions.XResolution);
                    }
                    catch (OperationCanceledException) { return null; }
                    catch (AggregateException aggregateException)
                    {
                        bool onlyCancelled = true;
                        foreach (var ex in aggregateException.Flatten().InnerExceptions)
                        {
                            if (ex is OperationCanceledException)
                                continue;
                            Debug.LogException(ex);
                            onlyCancelled = false;
                        }
                        if (!onlyCancelled)
                            throw new AggregateException(aggregateException.Flatten().InnerExceptions.Where(ex => !(ex is OperationCanceledException)));
                        return null;
                    }
                },
            closureCancellationToken, TaskCreationOptions.LongRunning);

            task.Start();

            while (task.Status < TaskStatus.RanToCompletion)
            {
                //Debug.Log(manager.PercentComplete + "% done calculating...");
                yield return 0;
            }

            if (aeroPredictorToClone is VesselCache.IReleasable releaseable)
                releaseable.Release();

            //timer.Stop();
            //Debug.Log("Time taken: " + timer.ElapsedMilliseconds / 1000f);

            if (task.Status > TaskStatus.RanToCompletion)
            {
                if (task.Status == TaskStatus.Faulted)
                {
                    Debug.LogError("Wind tunnel task faulted (Envelope)");
                    Debug.LogException(task.Exception);
                }
                else if (task.Status == TaskStatus.Canceled)
                    Debug.Log("Wind tunnel task was canceled. (Envelope)");
                yield break;
            }

            if (!closureCancellationToken.IsCancellationRequested)
            {
                envelopePoints = ((Task<EnvelopePoint[,]>)task).Result;
                currentConditions = conditions;
                UpdateGraphs();
                EnvelopeLine.CalculateOptimalLines(conditions, WindTunnelWindow.Instance.TargetSpeed, WindTunnelWindow.Instance.TargetAltitude, 0, 0, envelopePoints, closureCancellationToken, graphables);
                valuesSet = true;
            }

            if (cachedCount < closureProgress.Length)
                yield return 0;

            if (!closureCancellationToken.IsCancellationRequested)
            {
                Conditions newConditions;
                for (int i = 1; i <= resolution.GetUpperBound(0); i++)
                {
                    if (resolution[i,0] + 1 > conditions.XResolution || resolution[i,1] + 1 > conditions.YResolution)
                    {
                        newConditions = conditions.Modify(
                            stepSpeed: Math.Min((conditions.upperBoundSpeed - conditions.lowerBoundSpeed) / resolution[i, 0], conditions.stepSpeed),
                            stepAltitude: Math.Min((conditions.upperBoundAltitude - conditions.lowerBoundAltitude) / resolution[i, 1], conditions.stepAltitude));
                        Debug.LogFormat("Wind Tunnel graphing higher res at: {0} by {1}.", newConditions.XResolution, newConditions.YResolution);
                        WindTunnel.Instance.StartCoroutine(Processing(newConditions, envelopePoints));
                        yield break;
                    }
                }

                Debug.Log("Wind Tunnel Graph reached maximum resolution");
            }
        }

        public void CalculateOptimalLines(Conditions conditions, float exitSpeed, float exitAlt, float initialSpeed, float initialAlt)
        {
            if (cancellationTokenSource != null && cancellationTokenSource.IsCancellationRequested)
                return;
            EnvelopeLine.CalculateOptimalLines(conditions, exitSpeed, exitAlt, initialSpeed, initialAlt, envelopePoints, cancellationTokenSource.Token, graphables);
        }

        public override void OnAxesChanged(float xMin, float xMax, float yMin, float yMax, float zMin, float zMax)
        {
            float stepX = (xMax - xMin) / resolution[1, 0], stepY = (yMax - yMin) / resolution[1, 1];
            Calculate(currentConditions.body, xMin, xMax, yMin, yMax, Math.Min(stepX, currentConditions.stepSpeed), Math.Min(stepY, currentConditions.stepAltitude));
        }

        #endregion EnvelopeSurf

        private struct SurfCoords : IEquatable<SurfCoords>
        {
            public readonly int speed, altitude;

            public SurfCoords(float speed, float altitude)
            {
                this.speed = Mathf.RoundToInt(speed);
                this.altitude = Mathf.RoundToInt(altitude);
            }
            public SurfCoords(int speed, int altitude)
            {
                this.speed = speed;
                this.altitude = altitude;
            }
            public SurfCoords(EnvelopePoint point) : this(point.speed, point.altitude) { }

            public override bool Equals(object obj)
            {
                if (obj is SurfCoords c)
                    return Equals(c);
                return false;
            }

            public bool Equals(SurfCoords obj)
            {
                return this.speed == obj.speed && this.altitude == obj.altitude;
            }

            public override int GetHashCode()
            {
                // I'm not expecting altitudes over 131 km or speeds over 8 km/s
                // (or negative values for either) so bit-shifting the values in this way
                // should equally weight the two inputs while returning a hash with the
                // same quality as the default uint/int hash
                // (which may or may not be just that number).
                // This means that there will be zero collisions within the expected range.
                return ((((uint)speed) << 17) | (uint)altitude).GetHashCode();
            }
        }

        public struct Conditions : IEquatable<Conditions>
        {
            public readonly CelestialBody body;
            public readonly float lowerBoundSpeed;
            public readonly float upperBoundSpeed;
            public readonly float stepSpeed;
            public readonly float lowerBoundAltitude;
            public readonly float upperBoundAltitude;
            public readonly float stepAltitude;

            public static readonly Conditions Blank = new Conditions(null, 0, 0, 0f, 0, 0, 0f);

            public int Resolution
            {
                get
                {
                    return XResolution * YResolution;
                }
            }

            public int XResolution { get => Mathf.RoundToInt((upperBoundSpeed - lowerBoundSpeed) / stepSpeed + 1); }
            public int YResolution { get => Mathf.RoundToInt((upperBoundAltitude - lowerBoundAltitude) / stepAltitude + 1); }

            public Conditions(CelestialBody body, float lowerBoundSpeed, float upperBoundSpeed, float stepSpeed, float lowerBoundAltitude, float upperBoundAltitude, float stepAltitude)
            {
                this.body = body;
                if (body != null && lowerBoundAltitude > body.atmosphereDepth)
                    lowerBoundAltitude = upperBoundAltitude = (float)body.atmosphereDepth;
                if (body != null && upperBoundAltitude > body.atmosphereDepth)
                    upperBoundAltitude = (float)body.atmosphereDepth;
                this.lowerBoundSpeed = lowerBoundSpeed;
                this.upperBoundSpeed = upperBoundSpeed;
                this.stepSpeed = stepSpeed;
                this.lowerBoundAltitude = lowerBoundAltitude;
                this.upperBoundAltitude = upperBoundAltitude;
                this.stepAltitude = stepAltitude;
            }
            public Conditions(CelestialBody body, float lowerBoundSpeed, float upperBoundSpeed, int speedPts, float lowerBoundAltitude, float upperBoundAltitude, int altitudePts) :
                this(body, lowerBoundSpeed, upperBoundSpeed, (upperBoundSpeed - lowerBoundSpeed) / (speedPts - 1), lowerBoundAltitude, upperBoundAltitude, (upperBoundAltitude - lowerBoundAltitude) / (altitudePts - 1))
            { }

            public Conditions Modify(CelestialBody body = null, float lowerBoundSpeed = float.NaN, float upperBoundSpeed = float.NaN, float stepSpeed = float.NaN, float lowerBoundAltitude = float.NaN, float upperBoundAltitude = float.NaN, float stepAltitude = float.NaN)
                => Conditions.Modify(this, body, lowerBoundSpeed, upperBoundSpeed, stepSpeed, lowerBoundAltitude, upperBoundAltitude, stepAltitude);
            public static Conditions Modify(Conditions conditions, CelestialBody body = null, float lowerBoundSpeed = float.NaN, float upperBoundSpeed = float.NaN, float stepSpeed = float.NaN, float lowerBoundAltitude = float.NaN, float upperBoundAltitude = float.NaN, float stepAltitude = float.NaN)
            {
                if (body == null) body = conditions.body;
                if (float.IsNaN(lowerBoundSpeed)) lowerBoundSpeed = conditions.lowerBoundSpeed;
                if (float.IsNaN(upperBoundSpeed)) upperBoundSpeed = conditions.upperBoundSpeed;
                if (float.IsNaN(stepSpeed)) stepSpeed = conditions.stepSpeed;
                if (float.IsNaN(lowerBoundAltitude)) lowerBoundAltitude = conditions.lowerBoundAltitude;
                if (float.IsNaN(upperBoundAltitude)) upperBoundAltitude = conditions.upperBoundAltitude;
                if (float.IsNaN(stepAltitude)) stepAltitude = conditions.stepAltitude;
                return new Conditions(body, lowerBoundSpeed, upperBoundSpeed, stepSpeed, lowerBoundAltitude, upperBoundAltitude, stepAltitude);
            }

            public bool Contains(Conditions conditions)
            {
                return this.lowerBoundSpeed <= conditions.lowerBoundSpeed &&
                    this.upperBoundSpeed >= conditions.upperBoundSpeed &&
                    this.lowerBoundAltitude <= conditions.lowerBoundAltitude &&
                    this.upperBoundAltitude >= conditions.upperBoundAltitude;
            }
            public bool Contains(float speed, float altitude)
            {
                return lowerBoundSpeed <= speed && speed <= upperBoundSpeed &&
                    lowerBoundAltitude <= altitude && altitude <= upperBoundAltitude &&
                    (speed - lowerBoundSpeed) % stepSpeed == 0 && (altitude - lowerBoundAltitude) % stepAltitude == 0;
            }
            public bool Contains(float speed, float altitude, out int x, out int y)
            {
                bool result = Contains(speed, altitude);
                if (result)
                {
                    x = (int)((speed - lowerBoundSpeed) / stepSpeed);
                    y = (int)((altitude - lowerBoundAltitude) / stepAltitude);
                }
                else
                {
                    x = -1; y = -1;
                }
                return result;
            }

            public override bool Equals(object obj)
            {
                if (obj == null)
                    return false;
                if (obj is Conditions conditions)
                    return Equals(conditions);
                return false;
            }

            public bool Equals(Conditions conditions)
            {
                return this.body == conditions.body &&
                    this.lowerBoundSpeed == conditions.lowerBoundSpeed &&
                    this.upperBoundSpeed == conditions.upperBoundSpeed &&
                    this.stepSpeed == conditions.stepSpeed &&
                    this.lowerBoundAltitude == conditions.lowerBoundAltitude &&
                    this.upperBoundAltitude == conditions.upperBoundAltitude &&
                    this.stepAltitude == conditions.stepAltitude;
            }

            public static bool operator ==(Conditions left, Conditions right)
            {
                return left.Equals(right);
            }
            public static bool operator !=(Conditions left, Conditions right) => !(left.Equals(right));

            public override int GetHashCode()
            {
                return Extensions.HashCode.Of(this.body).And(this.lowerBoundSpeed).And(this.upperBoundSpeed).And(this.stepSpeed).And(this.lowerBoundAltitude).And(this.upperBoundAltitude).And(this.stepAltitude);
            }

            private class StepInsensitiveComparer : EqualityComparer<Conditions>
            {
                public override bool Equals(Conditions x, Conditions y)
                {
                    return x.body == y.body &&
                        x.lowerBoundSpeed == y.lowerBoundSpeed &&
                        x.upperBoundSpeed == y.upperBoundSpeed &&
                        x.lowerBoundAltitude == y.lowerBoundAltitude &&
                        x.upperBoundAltitude == y.upperBoundAltitude;
                }

                public override int GetHashCode(Conditions obj)
                {
                    return Extensions.HashCode.Of(obj.body).And(obj.lowerBoundSpeed).And(obj.upperBoundSpeed).And(obj.lowerBoundAltitude).And(obj.upperBoundAltitude);
                }
            }
        }
    }
}
