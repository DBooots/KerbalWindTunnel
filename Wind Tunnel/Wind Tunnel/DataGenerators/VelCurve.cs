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
    public class VelCurve : DataSetGenerator
    {
        public VelPoint[] VelPoints = new VelPoint[0];
        public Conditions currentConditions = Conditions.Blank;
        private Dictionary<Conditions, VelPoint[]> cache = new Dictionary<Conditions, VelPoint[]>();
        private VelPoint[] primaryProgress = null;

        public VelCurve()
        {
            graphables.Clear();
            Vector2[] blank = new Vector2[0];
            graphables.Add(new LineGraph(blank) { Name = "Level AoA", YUnit = "°", StringFormat = "F2", Color = Color.green });
            graphables.Add(new LineGraph(blank) { Name = "Max Lift AoA", YUnit = "°", StringFormat = "F2", Color = Color.green });
            graphables.Add(new LineGraph(blank) { Name = "Thrust Available", YName = "Force", YUnit = "kN", StringFormat = "N0", Color = Color.green });
            graphables.Add(new LineGraph(blank) { Name = "Lift/Drag Ratio", YUnit = "", StringFormat = "F2", Color = Color.green });
            graphables.Add(new LineGraph(blank) { Name = "Drag", YName = "Force", YUnit = "kN", StringFormat = "N0", Color = Color.green });
            graphables.Add(new LineGraph(blank) { Name = "Lift Slope", YUnit = "m^2/°", StringFormat = "F3", Color = Color.green });
            graphables.Add(new LineGraph(blank) { Name = "Excess Thrust", YName = "Force", YUnit = "kN", StringFormat = "N0", Color = Color.green });
            graphables.Add(new LineGraph(blank) { Name = "Pitch Input", YUnit = "", StringFormat = "F3", Color = Color.green });
            graphables.Add(new LineGraph(blank) { Name = "Max Lift", YName = "Force", YUnit = "kN", StringFormat = "N0", Color = Color.green });
            graphables.Add(new LineGraph(blank) { Name = "Excess Acceleration", YUnit = "g", StringFormat = "N2", Color = Color.green });

            var e = graphables.GetEnumerator();
            while (e.MoveNext())
            {
                e.Current.XUnit = "m/s";
                e.Current.XName = "Airspeed";
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
            VelPoints = new VelPoint[0];
        }

        public void Calculate(CelestialBody body, float altitude, float lowerBound = 0, float upperBound = 2000, float step = 50)
        {
            Conditions newConditions = new Conditions(body, altitude, lowerBound, upperBound, step);
            if (newConditions.Equals(currentConditions))
            {
                valuesSet = true;
                return;
            }

            Cancel();

            if (!cache.TryGetValue(newConditions, out VelPoints))
            {
                this.cancellationTokenSource = new CancellationTokenSource();
                WindTunnel.Instance.StartCoroutine(Processing(newConditions));
            }
            else
            {
                currentConditions = newConditions;
                UpdateGraphs();
                valuesSet = true;
            }
        }

        private void UpdateGraphs()
        {
            float left = currentConditions.lowerBound;
            float right = currentConditions.upperBound;
            Func<VelPoint, float> scale = (pt) => 1;
            if (WindTunnelSettings.UseCoefficients)
                scale = (pt) => 1 / pt.dynamicPressure;
            ((LineGraph)graphables["Level AoA"]).SetValues(VelPoints.Select(pt => pt.AoA_level * Mathf.Rad2Deg).ToArray(), left, right);
            ((LineGraph)graphables["Max Lift AoA"]).SetValues(VelPoints.Select(pt => pt.AoA_max * Mathf.Rad2Deg).ToArray(), left, right);
            ((LineGraph)graphables["Thrust Available"]).SetValues(VelPoints.Select(pt => pt.Thrust_available).ToArray(), left, right);
            ((LineGraph)graphables["Lift/Drag Ratio"]).SetValues(VelPoints.Select(pt => pt.LDRatio).ToArray(), left, right);
            ((LineGraph)graphables["Drag"]).SetValues(VelPoints.Select(pt => pt.drag * scale(pt)).ToArray(), left, right);
            ((LineGraph)graphables["Lift Slope"]).SetValues(VelPoints.Select(pt => pt.dLift / pt.dynamicPressure).ToArray(), left, right);
            ((LineGraph)graphables["Excess Thrust"]).SetValues(VelPoints.Select(pt => pt.Thrust_excess).ToArray(), left, right);
            ((LineGraph)graphables["Pitch Input"]).SetValues(VelPoints.Select(pt => pt.pitchInput).ToArray(), left, right);
            ((LineGraph)graphables["Max Lift"]).SetValues(VelPoints.Select(pt => pt.Lift_max * scale(pt)).ToArray(), left, right);
            ((LineGraph)graphables["Excess Acceleration"]).SetValues(VelPoints.Select(pt => pt.Accel_excess).ToArray(), left, right);
        }

        private IEnumerator Processing(Conditions conditions)
        {
            int numPts = (int)Math.Ceiling((conditions.upperBound - conditions.lowerBound) / conditions.step);
            primaryProgress = new VelPoint[numPts + 1];
            float trueStep = (conditions.upperBound - conditions.lowerBound) / numPts;

            CancellationTokenSource closureCancellationTokenSource = this.cancellationTokenSource;
            stopwatch.Reset();
            stopwatch.Start();

            task = Task.Factory.StartNew<VelPoint[]>(
                () =>
                {
                    try
                    {
                        //OrderablePartitioner<EnvelopePoint> partitioner = Partitioner.Create(primaryProgress, true);
                        Parallel.For<AeroPredictor>(0, primaryProgress.Length, new ParallelOptions() { CancellationToken = closureCancellationTokenSource.Token },
                            WindTunnelWindow.Instance.GetAeroPredictor,
                            (index, state, predictor) =>
                        {
                            primaryProgress[index] = new VelPoint(predictor, conditions.body, conditions.altitude, conditions.lowerBound + trueStep * index);
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
                    Debug.LogError("Wind tunnel task faulted");
                    Debug.LogException(task.Exception);
                }
                else if (task.Status == TaskStatus.Canceled)
                    Debug.Log("Wind tunnel task was canceled.");
                yield break;
            }

            if (!closureCancellationTokenSource.IsCancellationRequested)
            {
                VelPoints = primaryProgress;
                currentConditions = conditions;
                UpdateGraphs();
                valuesSet = true;
            }
        }

        public override void OnAxesChanged(float xMin, float xMax, float yMin, float yMax, float zMin, float zMax)
        {
            const float variance = 0.75f;
            const int numPts = 125;
            if (!currentConditions.Contains(currentConditions.Modify(lowerBound: xMin, upperBound: xMax)))
            {
                Calculate(currentConditions.body, currentConditions.altitude, xMin, xMax, (xMax - xMin) / numPts);
            }
            else if (currentConditions.step > (xMax - xMin / numPts) / variance)
            {
                Calculate(currentConditions.body, currentConditions.altitude, xMin, xMax, (xMax - xMin) / numPts);
            }
        }

        public struct VelPoint
        {
            public readonly float AoA_level;
            public readonly float AoA_max;
            public readonly float Thrust_available;
            public readonly float Thrust_excess;
            public readonly float Accel_excess;
            public readonly float drag;
            public readonly float altitude;
            public readonly float speed;
            public readonly float LDRatio;
            public readonly float mach;
            public readonly float dynamicPressure;
            public readonly float dLift;
            public readonly float pitchInput;
            public readonly float Lift_max;
            public readonly bool completed;

            public VelPoint(AeroPredictor vessel, CelestialBody body, float altitude, float speed)
            {
                this.altitude = altitude;
                this.speed = speed;
                AeroPredictor.Conditions conditions = new AeroPredictor.Conditions(body, speed, altitude);
                float gravParameter, radius;
                gravParameter = (float)body.gravParameter;
                radius = (float)body.Radius;
                this.mach = conditions.mach;
                this.dynamicPressure = 0.0005f * conditions.atmDensity * speed * speed;
                float weight = (vessel.Mass * gravParameter / ((radius + altitude) * (radius + altitude))) - (vessel.Mass * speed * speed / (radius + altitude));
                Vector3 thrustForce = vessel.GetThrustForce(conditions);
                AoA_max = vessel.GetMaxAoA(conditions, out Lift_max);
                AoA_level = Math.Min(vessel.GetAoA(conditions, weight), AoA_max);
                pitchInput = vessel.GetPitchInput(conditions, AoA_level);
                Thrust_available = thrustForce.magnitude;
                Vector3 force = vessel.GetAeroForce(conditions, AoA_level, pitchInput);
                drag = AeroPredictor.GetDragForceMagnitude(force, AoA_level);
                Thrust_excess = -drag - AeroPredictor.GetDragForceMagnitude(thrustForce, AoA_level);
                Accel_excess = Thrust_excess / vessel.Mass / WindTunnelWindow.gAccel;
                LDRatio = Math.Abs(AeroPredictor.GetLiftForceMagnitude(force, AoA_level) / drag);
                dLift = (vessel.GetLiftForceMagnitude(conditions, AoA_level + WindTunnelWindow.AoAdelta, pitchInput) -
                    vessel.GetLiftForceMagnitude(conditions, AoA_level, pitchInput)) / (WindTunnelWindow.AoAdelta * Mathf.Rad2Deg);
                completed = true;
            }

            public override string ToString()
            {
                return String.Format("Altitude:\t{0:N0}m\n" + "Speed:\t{1:N0}m/s\n" + "Mach:\t{7:N2}\n" + "Level Flight AoA:\t{2:N2}°\n" +
                        "Excess Thrust:\t{3:N0}kN\n" +
                        "Max Lift AoA:\t{4:N2}°\n" + "Lift/Drag Ratio:\t{6:N0}\n" + "Available Thrust:\t{5:N0}kN",
                        altitude, speed, AoA_level * Mathf.Rad2Deg,
                        Thrust_excess,
                        AoA_max * Mathf.Rad2Deg, Thrust_available, LDRatio,
                        mach);
            }
        }

        public struct Conditions : IEquatable<Conditions>
        {
            public readonly CelestialBody body;
            public readonly float altitude;
            public readonly float lowerBound;
            public readonly float upperBound;
            public readonly float step;

            public static readonly Conditions Blank = new Conditions(null, 0, 0, 0, 0);

            public Conditions(CelestialBody body, float altitude, float lowerBound, float upperBound, float step)
            {
                this.body = body;
                if (body != null && altitude > body.atmosphereDepth)
                    altitude = (float)body.atmosphereDepth;
                this.altitude = altitude;
                this.lowerBound = lowerBound;
                this.upperBound = upperBound;
                this.step = step;
            }

            public Conditions Modify(CelestialBody body = null, float altitude = float.NaN, float lowerBound = float.NaN, float upperBound = float.NaN, float step = float.NaN)
             => Conditions.Modify(this, body, altitude, lowerBound, upperBound, step);
            public static Conditions Modify(Conditions conditions, CelestialBody body = null, float altitude = float.NaN, float lowerBound = float.NaN, float upperBound = float.NaN, float step = float.NaN)
            {
                if (body == null) body = conditions.body;
                if (float.IsNaN(altitude)) altitude = conditions.altitude;
                if (float.IsNaN(lowerBound)) lowerBound = conditions.lowerBound;
                if (float.IsNaN(upperBound)) upperBound = conditions.upperBound;
                if (float.IsNaN(step)) step = conditions.step;
                return new Conditions(body, altitude, lowerBound, upperBound, step);
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
                    this.lowerBound == conditions.lowerBound &&
                    this.upperBound == conditions.upperBound &&
                    this.step == conditions.step;
            }

            public override int GetHashCode()
            {
                return Extensions.HashCode.Of(this.body).And(this.altitude).And(this.lowerBound).And(this.upperBound).And(this.step);
            }
        }
    }
}
