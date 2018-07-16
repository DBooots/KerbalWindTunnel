using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using KerbalWindTunnel.Graphing;

namespace KerbalWindTunnel.DataGenerators
{
    public class AoACurve : DataSetGenerator
    {
        public AoAPoint[] AoAPoints = new AoAPoint[0];
        public Conditions currentConditions = Conditions.Blank;
        private Dictionary<Conditions, AoAPoint[]> cache = new Dictionary<Conditions, AoAPoint[]>();

        public override void Clear()
        {
            base.Clear();
            currentConditions = Conditions.Blank;
            cache.Clear();
            AoAPoints = new AoAPoint[0];
        }

        public void Calculate(AeroPredictor vessel, CelestialBody body, float altitude, float speed, float lowerBound = -20f, float upperBound = 20f, float step = 0.5f)
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
                WindTunnelWindow.Instance.StartCoroutine(Processing(calculationManager, newConditions, vessel));
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
            float left = currentConditions.lowerBound * 180 / Mathf.PI;
            float right = currentConditions.upperBound * 180 / Mathf.PI;
            Func<AoAPoint, float> scale = (pt) => 1;
            if (WindTunnelSettings.useCoefficients)
                scale = (pt) => 1 / pt.dynamicPressure;
            graphs.Add("Lift", new LineGraph(AoAPoints.Select(pt => pt.Lift * scale(pt)).ToArray(), left, right) { Name = "Lift", Unit = "kN", StringFormat = "N0", Color = Color.green });
            graphs.Add("Drag", new LineGraph(AoAPoints.Select(pt => pt.Drag * scale(pt)).ToArray(), left, right) { Name = "Drag", Unit = "kN", StringFormat = "N0", Color = Color.green });
            graphs.Add("Lift/Drag Ratio", new LineGraph(AoAPoints.Select(pt => pt.LDRatio).ToArray(), left, right) { Name = "Lift/Drag Ratio", Unit = "", StringFormat = "F2", Color = Color.green });
            graphs.Add("Lift Slope", new LineGraph(AoAPoints.Select(pt => pt.dLift / pt.dynamicPressure).ToArray(), left, right) { Name = "Lift Slope", Unit = "m^2/°", StringFormat = "F3", Color = Color.green });
            graphs.Add("Pitch Input", new LineGraph(AoAPoints.Select(pt => pt.pitchInput).ToArray(), left, right) { Name = "Pitch Input", Unit = "", StringFormat = "F2", Color = Color.green });
        }

        private IEnumerator Processing(CalculationManager manager, Conditions conditions, AeroPredictor vessel)
        {
            int numPts = (int)Math.Ceiling((conditions.upperBound - conditions.lowerBound) / conditions.step);
            AoAPoint[] newAoAPoints = new AoAPoint[numPts + 1];
            float trueStep = (conditions.upperBound - conditions.lowerBound) / numPts;
            CalculationManager.State[] results = new CalculationManager.State[numPts + 1];

            for (int i = 0; i <= numPts; i++)
            {
                //newAoAPoints[i] = new AoAPoint(vessel, conditions.body, conditions.altitude, conditions.speed, conditions.lowerBound + trueStep * i);
                GenData genData = new GenData(vessel, conditions, conditions.lowerBound + trueStep * i, manager);
                results[i] = genData.storeState;
                ThreadPool.QueueUserWorkItem(GenerateAoAPoint, genData);
            }

            while (!manager.Completed)
            {
                if (manager.Status == CalculationManager.RunStatus.Cancelled)
                    yield break;
                yield return 0;
            }

            for(int i = 0; i <= numPts; i++)
            {
                newAoAPoints[i] = (AoAPoint)results[i].Result;
            }
            if (!manager.Cancelled)
            {
                cache.Add(conditions, newAoAPoints);
                AoAPoints = newAoAPoints;
                currentConditions = conditions;
                GenerateGraphs();
                valuesSet = true;
            }
        }

        private static void GenerateAoAPoint(object obj)
        {
            GenData data = (GenData)obj;
            if (data.storeState.manager.Cancelled)
                return;
            data.storeState.StoreResult(new AoAPoint(data.vessel, data.conditions.body, data.conditions.altitude, data.conditions.speed, data.AoA));
        }

        private struct GenData
        {
            public readonly Conditions conditions;
            public readonly AeroPredictor vessel;
            public readonly CalculationManager.State storeState;
            public readonly float AoA;

            public GenData(AeroPredictor vessel, Conditions conditions, float AoA, CalculationManager manager)
            {
                this.vessel = vessel;
                this.conditions = conditions;
                this.AoA = AoA;
                this.storeState = manager.GetStateToken();
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

            public AoAPoint(AeroPredictor vessel, CelestialBody body, float altitude, float speed, float AoA)
            {
                this.altitude = altitude;
                this.speed = speed;
                AeroPredictor.Conditions conditions = new AeroPredictor.Conditions(body, speed, altitude);
                this.AoA = AoA;
                this.mach = conditions.mach;
                this.dynamicPressure = 0.0005f * conditions.atmDensity * speed * speed;
                this.pitchInput = vessel.GetPitchInput(WindTunnelWindow.Instance.rootSolver, conditions, AoA);
                Vector3 force = AeroPredictor.ToFlightFrame(vessel.GetAeroForce(conditions, AoA, pitchInput), AoA);
                Lift = force.y;
                Drag = -force.z;
                LDRatio = Mathf.Abs(Lift / Drag);
                dLift = (vessel.GetLiftForceMagnitude(conditions, AoA + WindTunnelWindow.AoAdelta, pitchInput) - Lift) /
                    (WindTunnelWindow.AoAdelta * 180 / Mathf.PI);
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
