using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using KerbalWindTunnel.Graphing;
using KerbalWindTunnel.Extensions;

namespace KerbalWindTunnel.DataGenerators
{
    public class EnvelopeSurf : DataSetGenerator
    {
        protected static readonly ColorMap Jet_Dark_Positive = new ColorMap(ColorMap.Jet_Dark) { Filter = (v) => v >= 0 && !float.IsNaN(v) && !float.IsInfinity(v) };

        public EnvelopePoint[,] envelopePoints = new EnvelopePoint[0, 0];
        public Conditions currentConditions = Conditions.Blank;
        private Dictionary<Conditions, EnvelopePoint[,]> cache = new Dictionary<Conditions, EnvelopePoint[,]>();
        
        public override void Clear()
        {
            base.Clear();
            currentConditions = Conditions.Blank;
            cache.Clear();
            envelopePoints = new EnvelopePoint[0,0];
        }

        public void Calculate(AeroPredictor vessel, CelestialBody body, float lowerBoundSpeed = 0, float upperBoundSpeed = 2000, float stepSpeed = 50f, float lowerBoundAltitude = 0, float upperBoundAltitude = 60000, float stepAltitude = 500)
        {
            Conditions newConditions = new Conditions(body, lowerBoundSpeed, upperBoundSpeed, stepSpeed, lowerBoundAltitude, upperBoundAltitude, stepAltitude);
            if (newConditions.Equals(currentConditions))
            {
                valuesSet = true;
                return;
            }

            Cancel();

            if (!cache.TryGetValue(newConditions, out envelopePoints))
            {
                WindTunnel.Instance.StartCoroutine(Processing(calculationManager, newConditions, vessel, WindTunnelWindow.Instance.rootSolver));
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
            float bottom = currentConditions.lowerBoundAltitude;
            float top = currentConditions.upperBoundAltitude;
            float left = currentConditions.lowerBoundSpeed;
            float right = currentConditions.upperBoundSpeed;
            SurfGraph newSurfGraph;
            newSurfGraph = new SurfGraph(envelopePoints.SelectToArray(pt => pt.Thrust_excess), left, right, bottom, top) { Name = "Excess Thrust", Unit = "kN", StringFormat = "N0", Color = Jet_Dark_Positive };
            float maxThrustExcess = newSurfGraph.ZMax;
            newSurfGraph.ColorFunc = (x, y, z) => z / maxThrustExcess;
            graphs.Add("Excess Thrust", newSurfGraph);
            newSurfGraph = new SurfGraph(envelopePoints.SelectToArray(pt => pt.Accel_excess), left, right, bottom, top) { Name = "Excess Acceleration", Unit = "g", StringFormat = "N2", Color = Jet_Dark_Positive };
            float maxAccelExcess = newSurfGraph.ZMax;
            newSurfGraph.ColorFunc = (x, y, z) => z / maxAccelExcess;
            graphs.Add("Excess Acceleration", newSurfGraph);
            graphs.Add("Thrust Available", new SurfGraph(envelopePoints.SelectToArray(pt => pt.Thrust_available), left, right, bottom, top) { Name = "Thrust Available", Unit = "kN", StringFormat = "N0", Color = ColorMap.Jet_Dark });
            graphs.Add("Level AoA", new SurfGraph(envelopePoints.SelectToArray(pt => pt.AoA_level * 180 / Mathf.PI), left, right, bottom, top) { Name = "Level AoA", Unit = "°", StringFormat = "F2", Color = ColorMap.Jet_Dark });
            graphs.Add("Max Lift AoA", new SurfGraph(envelopePoints.SelectToArray(pt => pt.AoA_max * 180 / Mathf.PI), left, right, bottom, top) { Name = "Max Lift AoA", Unit = "°", StringFormat = "F2", Color = ColorMap.Jet_Dark });
            graphs.Add("Max Lift", new SurfGraph(envelopePoints.SelectToArray(pt => pt.Lift_max), left, right, bottom, top) { Name = "Max Lift", Unit = "kN", StringFormat = "N0", Color = ColorMap.Jet_Dark });
            graphs.Add("Lift/Drag Ratio", new SurfGraph(envelopePoints.SelectToArray(pt => pt.LDRatio), left, right, bottom, top) { Name = "Lift/Drag Ratio", Unit = "", StringFormat = "F2", Color = ColorMap.Jet_Dark });
            graphs.Add("Drag", new SurfGraph(envelopePoints.SelectToArray(pt => pt.drag), left, right, bottom, top) { Name = "Drag", Unit = "kN", StringFormat = "N0", Color = ColorMap.Jet_Dark });
            graphs.Add("Lift Slope", new SurfGraph(envelopePoints.SelectToArray(pt => pt.dLift / pt.dynamicPressure), left, right, bottom, top) { Name = "Lift Slope", Unit = "m^2/°", StringFormat = "F3", Color = ColorMap.Jet_Dark });
            graphs.Add("Pitch Input", new SurfGraph(envelopePoints.SelectToArray(pt => pt.pitchInput), left, right, bottom, top) { Name = "Pitch Input", Unit = "", StringFormat = "F2", Color = ColorMap.Jet_Dark });
        }

        private IEnumerator Processing(CalculationManager manager, Conditions conditions, AeroPredictor vessel, RootSolvers.RootSolver solver)
        {
            int numPtsX = (int)Math.Ceiling((conditions.upperBoundSpeed - conditions.lowerBoundSpeed) / conditions.stepSpeed);
            int numPtsY = (int)Math.Ceiling((conditions.upperBoundAltitude - conditions.lowerBoundAltitude) / conditions.stepAltitude);
            EnvelopePoint[,] newEnvelopePoints = new EnvelopePoint[numPtsX + 1, numPtsY + 1];
            float trueStepX = (conditions.upperBoundSpeed - conditions.lowerBoundSpeed) / numPtsX;
            float trueStepY = (conditions.upperBoundAltitude - conditions.lowerBoundAltitude) / numPtsY;
            CalculationManager.State[,] results = new CalculationManager.State[numPtsX + 1, numPtsY + 1];

            for (int j = 0; j <= numPtsY; j++)
            {
                for (int i = 0; i <= numPtsX; i++)
                {
                    //newAoAPoints[i] = new AoAPoint(vessel, conditions.body, conditions.altitude, conditions.speed, conditions.lowerBound + trueStep * i);
                    GenData genData = new GenData(vessel, conditions, conditions.lowerBoundSpeed + trueStepX * i, conditions.lowerBoundAltitude + trueStepY * j, solver, manager);
                    results[i, j] = genData.storeState;
                    ThreadPool.QueueUserWorkItem(GenerateSurfPoint, genData);
                }
            }

            while (!manager.Completed)
            {
                //Debug.Log(manager.PercentComplete + "% done calculating...");
                if (manager.Status == CalculationManager.RunStatus.Cancelled)
                    yield break;
                yield return 0;
            }

            for (int j = 0; j <= numPtsY; j++)
            {
                for (int i = 0; i <= numPtsX; i++)
                {
                    newEnvelopePoints[i, j] = (EnvelopePoint)results[i, j].Result;
                }
            }
            if (!manager.Cancelled)
            {
                cache.Add(conditions, newEnvelopePoints);
                envelopePoints = newEnvelopePoints;
                currentConditions = conditions;
                GenerateGraphs();
                valuesSet = true;
            }
        }

        private static void GenerateSurfPoint(object obj)
        {
            GenData data = (GenData)obj;
            if (data.storeState.manager.Cancelled)
                return;
            //Debug.Log("Starting point: " + data.altitude + "/" + data.speed);
            EnvelopePoint result = new EnvelopePoint(data.vessel, data.conditions.body, data.altitude, data.speed, data.solver);
            //Debug.Log("Point solved: " + data.altitude + "/" + data.speed);

            data.storeState.StoreResult(result);
        }

        private struct GenData
        {
            public readonly Conditions conditions;
            public readonly AeroPredictor vessel;
            public readonly CalculationManager.State storeState;
            public readonly RootSolvers.RootSolver solver;
            public readonly float speed;
            public readonly float altitude;

            public GenData(AeroPredictor vessel, Conditions conditions, float speed, float altitude, RootSolvers.RootSolver solver, CalculationManager manager)
            {
                this.vessel = vessel;
                this.conditions = conditions;
                this.speed = speed;
                this.altitude = altitude;
                this.solver = solver;
                this.storeState = manager.GetStateToken();
            }
        }
        public struct EnvelopePoint
        {
            public readonly float AoA_level;
            public readonly float Thrust_excess;
            public readonly float Accel_excess;
            public readonly float Lift_max;
            public readonly float AoA_max;
            public readonly float Thrust_available;
            public readonly float altitude;
            public readonly float speed;
            public readonly float LDRatio;
            public readonly Vector3 force;
            public readonly Vector3 liftforce;
            public readonly float mach;
            public readonly float dynamicPressure;
            public readonly float dLift;
            public readonly float drag;
            public readonly float pitchInput;

            public EnvelopePoint(AeroPredictor vessel, CelestialBody body, float altitude, float speed, RootSolvers.RootSolver solver, float AoA_guess = 0)
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
                AoA_level = vessel.GetAoA(solver, conditions, weight, out pitchInput, true);
                AoA_max = float.NaN;
                Lift_max = float.NaN;
                Thrust_available = thrustForce.magnitude;
                if (!float.IsNaN(AoA_level))
                {
                    force = vessel.GetAeroForce(conditions, AoA_level, pitchInput);
                    liftforce = AeroPredictor.ToFlightFrame(force, AoA_level); //vessel.GetLiftForce(body, speed, altitude, AoA_level, mach, atmDensity);
                    drag = AeroPredictor.GetDragForceMagnitude(force, AoA_level);
                    Thrust_excess = -drag - AeroPredictor.GetDragForceMagnitude(thrustForce, AoA_level);
                    Accel_excess = (Thrust_excess / vessel.Mass / WindTunnelWindow.gAccel);
                    LDRatio = Mathf.Abs(weight / drag);
                    dLift = (vessel.GetLiftForceMagnitude(conditions, AoA_level + WindTunnelWindow.AoAdelta, pitchInput) -
                        vessel.GetLiftForceMagnitude(conditions, AoA_level, pitchInput)) / (WindTunnelWindow.AoAdelta * 180 / Mathf.PI);
                }
                else
                {
                    force = vessel.GetAeroForce(conditions, 30f * Mathf.PI / 180);
                    drag = AeroPredictor.GetDragForceMagnitude(force, 30f * Mathf.PI / 180);
                    liftforce = force; // vessel.GetLiftForce(body, speed, altitude, 30, mach, atmDensity);
                    Thrust_excess = float.NegativeInfinity;
                    Accel_excess = float.NegativeInfinity;
                    LDRatio = 0;// weight / AeroPredictor.GetDragForceMagnitude(force, 30);
                    dLift = 0;
                }
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

            public static readonly Conditions Blank = new Conditions(null, 0, 0, 0, 0, 0, 0);

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
        }
    }
}
