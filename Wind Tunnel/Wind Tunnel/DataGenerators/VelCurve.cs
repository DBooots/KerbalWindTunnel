using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KerbalWindTunnel.Graphing;
using KerbalWindTunnel.Threading;

namespace KerbalWindTunnel.DataGenerators
{
    public class VelCurve : DataSetGenerator
    {
        public VelPoint[] VelPoints = new VelPoint[0];
        public Conditions currentConditions = Conditions.Blank;
        private Dictionary<Conditions, VelPoint[]> cache = new Dictionary<Conditions, VelPoint[]>();
        
        public override void Clear()
        {
            base.Clear();
            currentConditions = Conditions.Blank;
            cache.Clear();
            VelPoints = new VelPoint[0];
        }

        public void Calculate(AeroPredictor vessel, CelestialBody body, float altitude, float lowerBound = 0, float upperBound = 2000, float step = 50)
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
                WindTunnel.Instance.StartCoroutine(Processing(calculationManager, newConditions, vessel));
            }
            else
            {
                currentConditions = newConditions;
                calculationManager.Status = CalculationManager.RunStatus.Completed;
                GenerateGraphs();
                valuesSet = true;
            }
        }

        private void GenerateGraphs()
        {
            graphs.Clear();
            float left = currentConditions.lowerBound;
            float right = currentConditions.upperBound;
            Func<VelPoint, float> scale = (pt) => 1;
            if (WindTunnelSettings.UseCoefficients)
                scale = (pt) => 1 / pt.dynamicPressure;
            graphs.Add("Level AoA", new LineGraph(VelPoints.Select(pt => pt.AoA_level * Mathf.Rad2Deg).ToArray(), left, right) { Name = "Level AoA", YUnit = "°", StringFormat = "F2", Color = Color.green });
            graphs.Add("Max Lift AoA", new LineGraph(VelPoints.Select(pt => pt.AoA_max * Mathf.Rad2Deg).ToArray(), left, right) { Name = "Max Lift AoA", YUnit = "°", StringFormat = "F2", Color = Color.green });
            graphs.Add("Thrust Available", new LineGraph(VelPoints.Select(pt => pt.Thrust_available).ToArray(), left, right) { Name = "Thrust Available", YUnit = "kN", StringFormat = "N0", Color = Color.green });
            graphs.Add("Lift/Drag Ratio", new LineGraph(VelPoints.Select(pt => pt.LDRatio).ToArray(), left, right) { Name = "Lift/Drag Ratio", YUnit = "", StringFormat = "F2", Color = Color.green });
            graphs.Add("Drag", new LineGraph(VelPoints.Select(pt => pt.drag * scale(pt)).ToArray(), left, right) { Name = "Drag", YUnit = "kN", StringFormat = "N0", Color = Color.green });
            graphs.Add("Lift Slope", new LineGraph(VelPoints.Select(pt => pt.dLift / pt.dynamicPressure).ToArray(), left, right) { Name = "Lift Slope", YUnit = "m^2/°", StringFormat = "F3", Color = Color.green });
            graphs.Add("Excess Thrust", new LineGraph(VelPoints.Select(pt => pt.Thrust_excess).ToArray(), left, right) { Name = "Excess Thrust", YUnit = "kN", StringFormat = "N0", Color = Color.green });
            graphs.Add("Pitch Input", new LineGraph(VelPoints.Select(pt => pt.pitchInput).ToArray(), left, right) { Name = "Pitch Input", YUnit = "", StringFormat = "F3", Color = Color.green });
            //graphs.Add("Excess Acceleration", new LineGraph(VelPoints.Select(pt => pt.Accel_excess).ToArray(), left, right) { Name = "Excess Acceleration", Unit = "g", StringFormat = "N2", Color = Color.green });
            //graphs.Add("Max Lift", new LineGraph(VelPoints.Select(pt => pt.Lift_max * scale(pt)).ToArray(), left, right) { Name = "Max Lift", Unit = "kN", StringFormat = "N0", Color = Color.green });

            var e = graphs.GetEnumerator();
            while (e.MoveNext())
            {
                e.Current.Value.XUnit = "m/s";
                e.Current.Value.XName = "Airspeed";
            }
        }

        private IEnumerator Processing(CalculationManager manager, Conditions conditions, AeroPredictor vessel)
        {
            int numPts = (int)Math.Ceiling((conditions.upperBound - conditions.lowerBound) / conditions.step);
            VelPoint[] newVelPoints = new VelPoint[numPts + 1];
            float trueStep = (conditions.upperBound - conditions.lowerBound) / numPts;
            CalculationManager.State[] results = new CalculationManager.State[numPts + 1];

            for (int i = 0; i <= numPts; i++)
            {
                //newAoAPoints[i] = new AoAPoint(vessel, conditions.body, conditions.altitude, conditions.speed, conditions.lowerBound + trueStep * i);
                GenData genData = new GenData(vessel, conditions, conditions.lowerBound + trueStep * i, manager);
                results[i] = genData.storeState;
                ThreadPool.QueueUserWorkItem(GenerateVelPoint, genData);
            }

            while (!manager.Completed)
            {
                if (manager.Status == CalculationManager.RunStatus.Cancelled)
                    yield break;
                yield return 0;
            }

            for (int i = 0; i <= numPts; i++)
            {
                newVelPoints[i] = (VelPoint)results[i].Result;
            }
            if (!manager.Cancelled)
            {
                cache.Add(conditions, newVelPoints);
                VelPoints = newVelPoints;
                currentConditions = conditions;
                GenerateGraphs();
                valuesSet = true;
            }
        }

        private static void GenerateVelPoint(object obj)
        {
            GenData data = (GenData)obj;
            if (data.storeState.manager.Cancelled)
                return;
            data.storeState.StoreResult(new VelPoint(data.vessel, data.conditions.body, data.conditions.altitude, data.speed));
        }

        private struct GenData
        {
            public readonly Conditions conditions;
            public readonly AeroPredictor vessel;
            public readonly CalculationManager.State storeState;
            public readonly float speed;

            public GenData(AeroPredictor vessel, Conditions conditions, float speed, CalculationManager manager)
            {
                this.vessel = vessel;
                this.conditions = conditions;
                this.speed = speed;
                this.storeState = manager.GetStateToken();
            }
        }

        public struct VelPoint
        {
            public readonly float AoA_level;
            public readonly float AoA_max;
            public readonly float Thrust_available;
            public readonly float Thrust_excess;
            public readonly float drag;
            public readonly float altitude;
            public readonly float speed;
            public readonly float LDRatio;
            public readonly float mach;
            public readonly float dynamicPressure;
            public readonly float dLift;
            public readonly float pitchInput;
            public readonly float Lift_max;

            public VelPoint(AeroPredictor vessel, CelestialBody body, float altitude, float speed)
            {
                this.altitude = altitude;
                this.speed = speed;
                AeroPredictor.Conditions conditions = new AeroPredictor.Conditions(body, speed, altitude);
                float gravParameter, radius;
                lock (body)
                {
                    gravParameter = (float)body.gravParameter;
                    radius = (float)body.Radius;
                }
                this.mach = conditions.mach;
                this.dynamicPressure = 0.0005f * conditions.atmDensity * speed * speed;
                float weight = (vessel.Mass * gravParameter / ((radius + altitude) * (radius + altitude))); // TODO: Minus centrifugal force...
                Vector3 thrustForce = vessel.GetThrustForce(conditions);
                AoA_max = vessel.GetMaxAoA(conditions, out Lift_max);
                AoA_level = Mathf.Min(vessel.GetAoA(conditions, weight), AoA_max);
                pitchInput = vessel.GetPitchInput(conditions, AoA_level);
                Thrust_available = thrustForce.magnitude;
                Vector3 force = vessel.GetAeroForce(conditions, AoA_level, pitchInput);
                drag = AeroPredictor.GetDragForceMagnitude(force, AoA_level);
                Thrust_excess = -drag - AeroPredictor.GetDragForceMagnitude(thrustForce, AoA_level);
                LDRatio = Mathf.Abs(AeroPredictor.GetLiftForceMagnitude(force, AoA_level) / drag);
                dLift = (vessel.GetLiftForceMagnitude(conditions, AoA_level + WindTunnelWindow.AoAdelta, pitchInput) -
                    vessel.GetLiftForceMagnitude(conditions, AoA_level, pitchInput)) / (WindTunnelWindow.AoAdelta * Mathf.Rad2Deg);
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
