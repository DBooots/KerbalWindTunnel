using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Graphing;
using System.Threading.Tasks;
using System.Threading;

namespace KerbalWindTunnel.DataGenerators
{
    public class AoACurve : DataSetGenerator
    {
        public AoAPoint[] AoAPoints = new AoAPoint[0];
        public Conditions currentConditions = Conditions.Blank;
        public float AverageLiftSlope { get; private set; } = -1;
        private Dictionary<Conditions, AoAPoint[]> cache = new Dictionary<Conditions, AoAPoint[]>();
        private AoAPoint[] primaryProgress = null;

        public AoACurve()
        {
            graphables.Clear();
            Vector2[] blank = new Vector2[0];
            graphables.Add(new LineGraph(blank) { Name = "Lift", YName = "Force", YUnit = "kN", StringFormat = "N0", Color = Color.green });
            graphables.Add(new LineGraph(blank) { Name = "Drag", YName = "Force", YUnit = "kN", StringFormat = "N0", Color = Color.green });
            graphables.Add(new LineGraph(blank) { Name = "Lift/Drag Ratio", YUnit = "", StringFormat = "F2", Color = Color.green });
            graphables.Add(new LineGraph(blank) { Name = "Lift Slope", YUnit = "/°", StringFormat = "F3", Color = Color.green });
            IGraphable[] pitch = new IGraphable[] {
                new LineGraph(blank) { Name = "Pitch Input (Wet)", YUnit = "", StringFormat = "F2", Color = Color.green },
                new LineGraph(blank) { Name = "Pitch Input (Dry)", YUnit = "", StringFormat = "F2", Color = Color.yellow }
            };
            //graphables.Add(pitch[0]);
            //graphables.Add(pitch[1]);
            graphables.Add(new GraphableCollection(pitch) { Name = "Pitch Input" });
            IGraphable[] torque = new IGraphable[] {
                new LineGraph(blank) { Name = "Torque (Wet)", YUnit = "kNm", StringFormat = "N0", Color = Color.green },
                new LineGraph(blank) { Name = "Torque (Dry)", YUnit = "kNm", StringFormat = "N0", Color = Color.yellow }
            };
            //graphables.Add(torque[0]);
            //graphables.Add(torque[1]);
            graphables.Add(new GraphableCollection(torque) { Name = "Torque" });

            var e = graphables.GetEnumerator();
            while (e.MoveNext())
            {
                e.Current.XUnit = "°";
                e.Current.XName = "Angle of Attack";
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
            AoAPoints = new AoAPoint[0];
            AverageLiftSlope = -1;
        }

        public void Calculate(CelestialBody body, float altitude, float speed, float lowerBound = -20f, float upperBound = 20f, float step = 0.5f)
        {
            Conditions newConditions = new Conditions(body, altitude, speed, lowerBound, upperBound, step);
            if (newConditions.Equals(currentConditions))
            {
                valuesSet = true;
                return;
            }

            Cancel();

            if (!cache.TryGetValue(newConditions, out AoAPoints))
            {
                this.cancellationTokenSource = new CancellationTokenSource();
                WindTunnelWindow.Instance.StartCoroutine(Processing(newConditions));
            }
            else
            {
                currentConditions = newConditions;
                UpdateGraphs();
                valuesSet = true;
            }
        }

        public override void UpdateGraphs()
        {
            AverageLiftSlope = AoAPoints.Select(pt => pt.dLift / pt.dynamicPressure).Where(v => !float.IsNaN(v) && !float.IsInfinity(v)).Average();
            if (WindTunnelSettings.UseCoefficients)
                AverageLiftSlope /= WindTunnelWindow.Instance.CommonPredictor.Area;

            float left = currentConditions.lowerBound * Mathf.Rad2Deg;
            float right = currentConditions.upperBound * Mathf.Rad2Deg;
            Func<AoAPoint, float> scale = (pt) => 1;
            float invArea = 1f / WindTunnelWindow.Instance.CommonPredictor.Area;
            if (WindTunnelSettings.UseCoefficients)
            {
                scale = (pt) => 1 / pt.dynamicPressure * invArea;
                graphables["Lift"].YName = graphables["Drag"].YName = "Coefficient";
                graphables["Lift"].YUnit = graphables["Drag"].YUnit = "";
                graphables["Lift"].DisplayName = "Lift Coefficient";
                graphables["Drag"].DisplayName = "Drag Coefficient";
                ((LineGraph)graphables["Lift"]).StringFormat = ((LineGraph)graphables["Drag"]).StringFormat = "N2";
            }
            else
            {
                graphables["Lift"].YName = graphables["Drag"].YName = "Force";
                graphables["Lift"].YUnit = graphables["Drag"].YUnit = "kN";
                graphables["Lift"].DisplayName = "Lift";
                graphables["Drag"].DisplayName = "Drag";
                ((LineGraph)graphables["Lift"]).StringFormat = ((LineGraph)graphables["Drag"]).StringFormat = "N0";
            }
            ((LineGraph)graphables["Lift"]).SetValues(AoAPoints.Select(pt => pt.Lift * scale(pt)).ToArray(), left, right);
            ((LineGraph)graphables["Drag"]).SetValues(AoAPoints.Select(pt => pt.Drag * scale(pt)).ToArray(), left, right);
            ((LineGraph)graphables["Lift/Drag Ratio"]).SetValues(AoAPoints.Select(pt => pt.LDRatio).ToArray(), left, right);
            ((LineGraph)graphables["Lift Slope"]).SetValues(AoAPoints.Select(pt => pt.dLift / pt.dynamicPressure * invArea).ToArray(), left, right);
            ((LineGraph)((GraphableCollection)graphables["Pitch Input"])["Pitch Input (Wet)"]).SetValues(AoAPoints.Select(pt => pt.pitchInput).ToArray(), left, right);
            ((LineGraph)((GraphableCollection)graphables["Pitch Input"])["Pitch Input (Dry)"]).SetValues(AoAPoints.Select(pt => pt.pitchInput_dry).ToArray(), left, right);
            ((LineGraph)((GraphableCollection)graphables["Torque"])["Torque (Wet)"]).SetValues(AoAPoints.Select(pt => pt.torque).ToArray(), left, right);
            ((LineGraph)((GraphableCollection)graphables["Torque"])["Torque (Dry)"]).SetValues(AoAPoints.Select(pt => pt.torque_dry).ToArray(), left, right);
        }

        private IEnumerator Processing(Conditions conditions)
        {
            int numPts = (int)Math.Ceiling((conditions.upperBound - conditions.lowerBound) / conditions.step);
            primaryProgress = new AoAPoint[numPts + 1];
            float trueStep = (conditions.upperBound - conditions.lowerBound) / numPts;

            CancellationTokenSource closureCancellationTokenSource = this.cancellationTokenSource;
            stopwatch.Reset();
            stopwatch.Start();

            task = Task.Factory.StartNew<AoAPoint[]>(
                () =>
                {
                    try
                    {
                        //OrderablePartitioner<EnvelopePoint> partitioner = Partitioner.Create(primaryProgress, true);
                        Parallel.For<AeroPredictor>(0, primaryProgress.Length, new ParallelOptions() { CancellationToken = closureCancellationTokenSource.Token },
                            WindTunnelWindow.Instance.GetAeroPredictor,
                            (index, state, predictor) =>
                        {
                            primaryProgress[index] = new AoAPoint(predictor, conditions.body, conditions.altitude, conditions.speed, conditions.lowerBound + trueStep * index);
                            return predictor;
                        }, (predictor) => (predictor as VesselCache.IReleasable)?.Release());

                        closureCancellationTokenSource.Token.ThrowIfCancellationRequested();
                        return cache[conditions] = primaryProgress;
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
            closureCancellationTokenSource.Token);

            while (task.Status < TaskStatus.RanToCompletion)
            {
                //Debug.Log(manager.PercentComplete + "% done calculating...");
                yield return 0;
            }

            if (task.Status > TaskStatus.RanToCompletion)
            {
                if (task.Status == TaskStatus.Faulted)
                {
                    Debug.LogError("Wind tunnel task faulted (AoA)");
                    Debug.LogException(task.Exception);
                }
                else if (task.Status == TaskStatus.Canceled)
                    Debug.Log("Wind tunnel task was canceled. (AoA)");
                yield break;
            }
            
            if (!closureCancellationTokenSource.IsCancellationRequested)
            {
                AoAPoints = primaryProgress;
                currentConditions = conditions;
                UpdateGraphs();
                valuesSet = true;
            }
        }

        public override void OnAxesChanged(float xMin, float xMax, float yMin, float yMax, float zMin, float zMax)
        {
            const float variance = 0.5f;
            const int numPts = 80;
            xMin = (xMin < -180 ? -180 : xMin) * Mathf.Deg2Rad;
            xMax = (xMax > 180 ? 180 : xMax) * Mathf.Deg2Rad;
            float step = Math.Min(2 * Mathf.Deg2Rad, (xMax - xMin) / numPts * Mathf.Deg2Rad);
            if (!currentConditions.Contains(currentConditions.Modify(lowerBound: xMin, upperBound: xMax)))
            {
                Calculate(currentConditions.body, currentConditions.altitude, currentConditions.speed, xMin, xMax, step);
            }
            else if (currentConditions.step > step / variance)
            {
                Calculate(currentConditions.body, currentConditions.altitude, currentConditions.speed, xMin, xMax, step);
            }
        }

        public struct AoAPoint
        {
            public readonly float Lift;
            public readonly float Drag;
            public readonly float LDRatio;
            public readonly float altitude;
            public readonly float speed;
            public readonly float AoA;
            public readonly float dynamicPressure;
            public readonly float dLift;
            public readonly float mach;
            public readonly float pitchInput;
            public readonly float pitchInput_dry;
            public readonly float torque;
            public readonly float torque_dry;
            public readonly bool completed;
            private readonly float area;

            public AoAPoint(AeroPredictor vessel, CelestialBody body, float altitude, float speed, float AoA)
            {
                this.altitude = altitude;
                this.speed = speed;
                AeroPredictor.Conditions conditions = new AeroPredictor.Conditions(body, speed, altitude);
                this.AoA = AoA;
                this.mach = conditions.mach;
                this.dynamicPressure = 0.0005f * conditions.atmDensity * speed * speed;
                this.pitchInput = vessel.GetPitchInput(conditions, AoA);
                this.pitchInput_dry = vessel.GetPitchInput(conditions, AoA, true);
                Vector3 force = AeroPredictor.ToFlightFrame(vessel.GetAeroForce(conditions, AoA, pitchInput), AoA);
                torque = vessel.GetAeroTorque(conditions, AoA).x;
                torque_dry = vessel.GetAeroTorque(conditions, AoA, 0, true).x;
                Lift = force.y;
                Drag = -force.z;
                LDRatio = Math.Abs(Lift / Drag);
                dLift = (vessel.GetLiftForceMagnitude(conditions, AoA + WindTunnelWindow.AoAdelta, pitchInput) - Lift) /
                    (WindTunnelWindow.AoAdelta * Mathf.Rad2Deg);
                area = vessel.Area;
                completed = true;
            }

            public override string ToString()
            {
                if (WindTunnelSettings.UseCoefficients)
                {
                    float coefMod = 1f / dynamicPressure / area;
                    return String.Format("Altitude:\t{0:N0}m\n" + "Speed:\t{1:N0}m/s\n" + "Mach:\t{6:N2}\n" + "AoA:\t{2:N2}°\n" +
                        "Lift Coefficient:\t{3:N2}\n" + "Drag Coefficient:\t{4:N2}\n" + "Lift/Drag Ratio:\t{5:N2}\n" + "Pitch Input:\t{7:F3}\n" + 
                        "Wing Area:\t{8:F2}",
                        altitude, speed, AoA * Mathf.Rad2Deg,
                        Lift * coefMod, Drag * coefMod, LDRatio, mach, pitchInput,
                        area);
                }
                else
                    return String.Format("Altitude:\t{0:N0}m\n" + "Speed:\t{1:N0}m/s\n" + "Mach:\t{6:N2}\n" + "AoA:\t{2:N2}°\n" +
                            "Lift:\t{3:N0}kN\n" + "Drag:\t{4:N0}kN\n" + "Lift/Drag Ratio:\t{5:N2}\n" + "Pitch Input:\t{7:F3}",
                            altitude, speed, AoA * Mathf.Rad2Deg,
                            Lift, Drag, LDRatio, mach, pitchInput);
            }
        }

        public struct Conditions : IEquatable<Conditions>
        {
            public readonly CelestialBody body;
            public readonly float altitude;
            public readonly float speed;
            public readonly float lowerBound;
            public readonly float upperBound;
            public readonly float step;

            public static readonly Conditions Blank = new Conditions(null, 0, 0, 0, 0, 0);

            public Conditions(CelestialBody body, float altitude, float speed, float lowerBound, float upperBound, float step)
            {
                this.body = body;
                if (body != null && altitude > body.atmosphereDepth)
                    altitude = (float)body.atmosphereDepth;
                this.altitude = altitude;
                this.speed = speed;
                this.lowerBound = lowerBound;
                this.upperBound = upperBound;
                this.step = step;
            }

            public Conditions Modify(CelestialBody body = null, float altitude = float.NaN, float speed = float.NaN, float lowerBound = float.NaN, float upperBound = float.NaN, float step = float.NaN)
             => Conditions.Modify(this, body, altitude, speed, lowerBound, upperBound, step);
            public static Conditions Modify(Conditions conditions, CelestialBody body = null, float altitude = float.NaN, float speed = float.NaN, float lowerBound = float.NaN, float upperBound = float.NaN, float step = float.NaN)
            {
                if (body == null) body = conditions.body;
                if (float.IsNaN(altitude)) altitude = conditions.altitude;
                if (float.IsNaN(speed)) speed = conditions.speed;
                if (float.IsNaN(lowerBound)) lowerBound = conditions.lowerBound;
                if (float.IsNaN(upperBound)) upperBound = conditions.upperBound;
                if (float.IsNaN(step)) step = conditions.step;
                return new Conditions(body, altitude, speed, lowerBound, upperBound, step);
            }

            public bool Contains(Conditions conditions)
            {
                return this.lowerBound <= conditions.lowerBound &&
                    this.upperBound >= conditions.upperBound;
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
                    this.altitude == conditions.altitude &&
                    this.speed == conditions.speed &&
                    this.lowerBound == conditions.lowerBound &&
                    this.upperBound == conditions.upperBound &&
                    this.step == conditions.step;
            }

            public override int GetHashCode()
            {
                return Extensions.HashCode.Of(this.body).And(this.altitude).And(this.speed).And(this.lowerBound).And(this.upperBound).And(this.step);
            }
        }
    }
}
