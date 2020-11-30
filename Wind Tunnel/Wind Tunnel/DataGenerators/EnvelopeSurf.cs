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
    public partial class EnvelopeSurf : DataSetGenerator
    {
        #region EnvelopeSurf
        protected static readonly ColorMap Jet_Dark_Positive = new ColorMap(ColorMap.Jet_Dark) { Filter = (v) => v >= 0 && !float.IsNaN(v) && !float.IsInfinity(v) };

        public EnvelopePoint[,] envelopePoints = new EnvelopePoint[0, 0];
        public Conditions currentConditions = Conditions.Blank;
        private Dictionary<Conditions, EnvelopePoint[,]> cache = new Dictionary<Conditions, EnvelopePoint[,]>();
        private Dictionary<Conditions, KeyValuePair<Conditions, EnvelopePoint[]>> inProgressCache = new Dictionary<Conditions, KeyValuePair<Conditions, EnvelopePoint[]>>(Conditions.stepInsensitiveComparer);
        private EnvelopePoint[] primaryProgress = null;
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
            graphables.Add(new SurfGraph(blank, left, right, bottom, top) { Name = "Lift Slope", ZUnit = "m^2/°", StringFormat = "F3", Color = ColorMap.Jet_Dark });
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
                if (primaryProgress == null)
                    return -1;
                return (float)(primaryProgress.Count((pt) => pt.completed)) / primaryProgress.Count();
            }
        }

        public override void Clear()
        {
            base.Clear();
            currentConditions = Conditions.Blank;
            cache.Clear();
            inProgressCache.Clear();
            primaryProgress = null;
            envelopePoints = new EnvelopePoint[0, 0];

            ((LineGraph)graphables["Fuel-Optimal Path"]).SetValues(new Vector2[0]);
            ((LineGraph)graphables["Time-Optimal Path"]).SetValues(new Vector2[0]);
        }

        private bool TryGetBestCache(Conditions conditions, out Conditions bestConditions, out EnvelopePoint[,] points)
        {
            bestConditions = conditions;
            points = new EnvelopePoint[0, 0];
            Dictionary<Conditions, EnvelopePoint[,]>.Enumerator enumerator = cache.GetEnumerator();
            long bestResolution = 0;
            bool result = false;
            while (enumerator.MoveNext())
            {
                Conditions enumConditions = enumerator.Current.Key;
                if (Conditions.stepInsensitiveComparer.Equals(conditions, enumConditions))
                {
                    result = true;
                    long resolution = enumConditions.Resolution;
                    if (resolution > bestResolution)
                    {
                        bestResolution = resolution;
                        bestConditions = enumConditions;
                        points = enumerator.Current.Value;
                    }
                }
            }
            return result;
        }

        private bool TryGetCacheContaining(Conditions conditions, out Conditions containingConditions, out EnvelopePoint[,] points)
        {
            containingConditions = conditions;
            points = new EnvelopePoint[0, 0];
            Dictionary<Conditions, EnvelopePoint[,]>.Enumerator enumerator = cache.GetEnumerator();
            int bestNumPoints = 0;
            bool result = false;
            while (enumerator.MoveNext())
            {
                Conditions enumConditions = enumerator.Current.Key;
                if (enumConditions.Contains(conditions))
                {
                    result = true;
                    int numPoints = Mathf.FloorToInt((conditions.upperBoundSpeed - conditions.lowerBoundSpeed) / enumConditions.stepSpeed + 1) *
                        Mathf.FloorToInt((conditions.upperBoundAltitude - conditions.lowerBoundAltitude) / enumConditions.stepAltitude + 1);
                    if (numPoints > bestNumPoints)
                    {
                        bestNumPoints = numPoints;
                        containingConditions = enumConditions;
                        points = enumerator.Current.Value;
                    }
                }
            }
            return result;
        }

        public void Calculate(CelestialBody body, float lowerBoundSpeed = 0, float upperBoundSpeed = 2000, float stepSpeed = 50f, float lowerBoundAltitude = 0, float upperBoundAltitude = 60000, float stepAltitude = 500)
        {
            // Set up calculation conditions and bounds
            Conditions newConditions = new Conditions(body, lowerBoundSpeed, upperBoundSpeed, stepSpeed, lowerBoundAltitude, upperBoundAltitude, stepAltitude);

            if (currentConditions.Equals(newConditions) && Status != TaskStatus.WaitingToRun)
                return;

            Cancel();

            bool loadedCache = false;
            if (TryGetCacheContaining(newConditions, out currentConditions, out EnvelopePoint[,] cachedData) && cachedData.Length > (resolution[0, 0] + 1) * (resolution[0, 1] + 1))
            {
                loadedCache = true;
                envelopePoints = cachedData;
                UpdateGraphs();
            }
            if (loadedCache)
            {
                UpdateGraphs();
                valuesSet = true;

                if (inProgressCache.TryGetValue(newConditions, out KeyValuePair<Conditions, EnvelopePoint[]> cachedProgress) &&
                    cachedProgress.Key.Resolution < newConditions.Resolution)
                    inProgressCache.Remove(newConditions);
                else if (inProgressCache.TryGetValue(currentConditions, out cachedProgress) &&
                    cachedProgress.Key.Resolution < newConditions.Resolution)
                    inProgressCache.Remove(currentConditions);
            }
            else
            {
                // Generate 'coarse' cache
                float firstStepSpeed = (newConditions.upperBoundSpeed - newConditions.lowerBoundSpeed) / resolution[0, 0];
                float firstStepAltitude = (newConditions.upperBoundAltitude - newConditions.lowerBoundAltitude) / resolution[0, 1];
                EnvelopePoint[] preliminaryData = new EnvelopePoint[(resolution[0, 0] + 1) * (resolution[0, 1] + 1)];
                // Probably won't run in parallel because it's very short.
                // But the UI will hang waiting for this to complete, so a self-triggering CancellationToken is provided with a life span of 5 seconds.
                try
                {
                    Parallel.For(0, preliminaryData.Length, new ParallelOptions() { CancellationToken = new CancellationTokenSource(5000).Token },
                        WindTunnelWindow.Instance.GetAeroPredictor, (index, state, predictor) =>
                             {
                                 int x = index % (resolution[0, 0] + 1), y = index / (resolution[0, 0] + 1);
                                 preliminaryData[index] = new EnvelopePoint(predictor, newConditions.body, y * firstStepAltitude + newConditions.lowerBoundAltitude, x * firstStepSpeed + newConditions.lowerBoundSpeed);
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

                cache[newConditions] = preliminaryData.To2Dimension(resolution[0, 0] + 1);
            }
            
            WindTunnel.Instance.StartCoroutine(Processing(newConditions));
        }

        private void UpdateGraphs()
        {
            float bottom = currentConditions.lowerBoundAltitude;
            float top = currentConditions.upperBoundAltitude;
            float left = currentConditions.lowerBoundSpeed;
            float right = currentConditions.upperBoundSpeed;
            Func<EnvelopePoint, float> scale = (pt) => 1f;
            if (WindTunnelSettings.UseCoefficients)
            {
                scale = (pt) => 1f / pt.dynamicPressure;
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
            ((SurfGraph)graphables["Lift Slope"]).SetValues(envelopePoints.SelectToArray(pt => pt.dLift / pt.dynamicPressure), left, right, bottom, top);
            ((SurfGraph)graphables["Pitch Input"]).SetValues(envelopePoints.SelectToArray(pt => pt.pitchInput), left, right, bottom, top);
            ((SurfGraph)graphables["Fuel Burn Rate"]).SetValues(envelopePoints.SelectToArray(pt => pt.fuelBurnRate), left, right, bottom, top);
            //((SurfGraph)graphables["Stability Derivative"]).SetValues(envelopePoints.SelectToArray(pt => pt.stabilityDerivative), left, right, bottom, top);
            //((SurfGraph)graphables["Stability Range"]).SetValues(envelopePoints.SelectToArray(pt => pt.stabilityRange), left, right, bottom, top);
            //((SurfGraph)graphables["Stability Score"]).SetValues(envelopePoints.SelectToArray(pt => pt.stabilityScore), left, right, bottom, top);

            float[,] economy = envelopePoints.SelectToArray(pt => pt.fuelBurnRate / pt.speed * 1000 * 100);
            int stallpt = CoordLocator.GenerateCoordLocators(envelopePoints.SelectToArray(pt=>pt.Thrust_excess)).First(0, 0, c => c.value>=0);
            SurfGraph toModify = (SurfGraph)graphables["Fuel Economy"];
            toModify.SetValues(economy, left, right, bottom, top);
            float minEconomy = economy[stallpt, 0] / 3;
            toModify.ZMax = minEconomy;
            ((OutlineMask)graphables["Envelope Mask"]).SetValues(envelopePoints.SelectToArray(pt => pt.Thrust_excess), left, right, bottom, top);
        }

        private IEnumerator Processing(Conditions conditions)
        {
            Debug.Log("Wind Tunnel: Beginning CoRoutine");
            // Is there something to resume?
            bool resuming = false;
            if (valuesSet)
            {
                if (inProgressCache.TryGetValue(conditions, out KeyValuePair<Conditions, EnvelopePoint[]> cachedData))
                {
                    if (cachedData.Key.stepAltitude <= conditions.stepAltitude && cachedData.Key.stepSpeed <= conditions.stepSpeed)
                    {
                        resuming = true;
                        primaryProgress = cachedData.Value;
                        conditions = cachedData.Key;
                    }
                    else
                        inProgressCache.Remove(conditions);
                }
                /*else if (inProgressCache.TryGetValue(currentConditions, out cachedData))
                {
                    if (cachedData.Key.stepAltitude <= currentConditions.stepAltitude && cachedData.Key.stepSpeed <= currentConditions.stepSpeed)
                    {
                        resuming = true;
                        primaryProgress = cachedData.Value;
                    }
                    else
                        inProgressCache.Remove(currentConditions);
                }*/
            }

            int resolutionStep = 1;
            if (resuming)
            {
                for (resolutionStep = 1; resolutionStep <= resolution.GetUpperBound(0); resolutionStep++)
                {
                    if (resolution[resolutionStep, 0] < (conditions.upperBoundSpeed - conditions.lowerBoundSpeed) / conditions.stepSpeed ||
                        resolution[resolutionStep, 1] < (conditions.upperBoundAltitude - conditions.lowerBoundAltitude) / conditions.stepAltitude)
                        break;
                }
            }
            else
            {
                primaryProgress = new EnvelopePoint[conditions.Resolution];
                inProgressCache.Add(conditions, new KeyValuePair<Conditions, EnvelopePoint[]>(conditions, primaryProgress));
            }

            cancellationTokenSource = new CancellationTokenSource();
            stopwatch.Reset();
            stopwatch.Start();

            task = Task.Factory.StartNew<EnvelopePoint[,]>(
                () =>
                {
                    if (!TryGetCacheContaining(conditions, out Conditions cachedConditions, out EnvelopePoint[,] cachedData))
                        Debug.LogAssertion("Cache didn't have initialization data...");

                    float[,] AoAs_guess = null, maxAs_guess = null, pitchIs_guess = null;
                    AoAs_guess = cachedData.SelectToArray(pt => pt.AoA_level);
                    maxAs_guess = cachedData.SelectToArray(pt => pt.AoA_max);
                    pitchIs_guess = cachedData.SelectToArray(pt => pt.pitchInput);

                    try
                    {
                        //OrderablePartitioner<EnvelopePoint> partitioner = Partitioner.Create(primaryProgress, true);
                        Parallel.For<AeroPredictor>(0, primaryProgress.Length, new ParallelOptions() { CancellationToken = cancellationTokenSource.Token },
                            WindTunnelWindow.Instance.GetAeroPredictor,
                            (index, state, predictor) =>
                        {
                            int x = index % conditions.XResolution, y = index / conditions.XResolution;
                            primaryProgress[index] = new EnvelopePoint(predictor, conditions.body, y * conditions.stepAltitude + conditions.lowerBoundAltitude, x * conditions.stepSpeed + conditions.lowerBoundSpeed);
                            return predictor;
                        }, (predictor) => (predictor as VesselCache.IReleasable)?.Release());

                        cancellationTokenSource.Token.ThrowIfCancellationRequested();
                        return cache[conditions] = primaryProgress.To2Dimension(conditions.XResolution);
                    }
                    catch (AggregateException aggregateException)
                    {
                        foreach (var ex in aggregateException.Flatten().InnerExceptions)
                        {
                            Debug.LogException(ex);
                        }
                        throw aggregateException;
                    }
                },
            cancellationTokenSource.Token);

            while (task.Status < TaskStatus.RanToCompletion)
            {
                //Debug.Log(manager.PercentComplete + "% done calculating...");
                yield return 0;
            }
            //timer.Stop();
            //Debug.Log("Time taken: " + timer.ElapsedMilliseconds / 1000f);

            if (task.Status > TaskStatus.RanToCompletion)
            {
                if (task.Status == TaskStatus.Faulted)
                {
                    Debug.LogError("Wind tunnel task faulted");
                    Debug.LogException(task.Exception);
                }
                else if (task.Status == TaskStatus.Canceled)
                    Debug.Log("Wind tunnel task was canceled.");
                yield break;
            }

            if (!cancellationTokenSource.IsCancellationRequested)
            {
                envelopePoints = ((Task<EnvelopePoint[,]>)task).Result;
                currentConditions = conditions;
                UpdateGraphs();
                CalculateOptimalLines(conditions, WindTunnelWindow.Instance.TargetSpeed, WindTunnelWindow.Instance.TargetAltitude, 0, 0);
                valuesSet = true;
            }
            yield return 0;

            if (!cancellationTokenSource.IsCancellationRequested)
            {
                //Conditions nextConditions = conditions.Modify(stepSpeed: conditions.stepSpeed / 2, stepAltitude: conditions.stepAltitude / 2);
                //WindTunnel.Instance.StartCoroutine(RefinementProcessing(calculationManager, nextConditions, vessel, newEnvelopePoints, conditions));
            }
        }

        public override void OnAxesChanged(float xMin, float xMax, float yMin, float yMax, float zMin, float zMax)
        {
            float stepX = (xMax - xMin) / resolution[1, 0], stepY = (yMax - yMin) / resolution[1, 1];
            Calculate(currentConditions.body, xMin, xMax, stepX, yMin, yMax, stepY);
        }

        #endregion EnvelopeSurf

        public struct Conditions : IEquatable<Conditions>
        {
            public readonly CelestialBody body;
            public readonly float lowerBoundSpeed;
            public readonly float upperBoundSpeed;
            public readonly float stepSpeed;
            public readonly float lowerBoundAltitude;
            public readonly float upperBoundAltitude;
            public readonly float stepAltitude;
            public static IEqualityComparer<Conditions> stepInsensitiveComparer = new StepInsensitiveComparer();

            public static readonly Conditions Blank = new Conditions(null, 0, 0, 0f, 0, 0, 0f);

            public long Resolution
            {
                get
                {
                    return Mathf.RoundToInt((upperBoundAltitude - lowerBoundAltitude) / stepAltitude + 1) * Mathf.RoundToInt((upperBoundSpeed - lowerBoundSpeed) / stepSpeed + 1);
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
                if (obj.GetType() != typeof(Conditions))
                    return false;
                Conditions conditions = (Conditions)obj;
                return this.Equals(conditions);
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

            public override int GetHashCode()
            {
                return Extensions.HashCode.Of(this.body).And(this.lowerBoundSpeed).And(this.upperBoundSpeed).And(this.stepSpeed).And(this.lowerBoundAltitude).And(this.upperBoundAltitude).And(this.stepAltitude);
            }

            private class StepInsensitiveComparer : IEqualityComparer<Conditions>
            {
                public bool Equals(Conditions x, Conditions y)
                {
                    return x.body == y.body &&
                        x.lowerBoundSpeed == y.lowerBoundSpeed &&
                        x.upperBoundSpeed == y.upperBoundSpeed &&
                        x.lowerBoundAltitude == y.lowerBoundAltitude &&
                        x.upperBoundAltitude == y.upperBoundAltitude;
                }

                public int GetHashCode(Conditions obj)
                {
                    return Extensions.HashCode.Of(obj.body).And(obj.lowerBoundSpeed).And(obj.upperBoundSpeed).And(obj.lowerBoundAltitude).And(obj.upperBoundAltitude);
                }
            }
        }
    }
}
