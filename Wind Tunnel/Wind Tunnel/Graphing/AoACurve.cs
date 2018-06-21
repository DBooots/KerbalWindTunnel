using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace KerbalWindTunnel.Graphing
{
    public static class AoACurve
    {
        public static AoAPoint[] AoAPoints = new AoAPoint[0];
        private static CalculationManager calculationManager = new CalculationManager();
        public static CalculationManager.RunStatus Status
        {
            get
            {
                CalculationManager.RunStatus status = calculationManager.Status;
                if (status == CalculationManager.RunStatus.Completed && !valuesSet)
                    return CalculationManager.RunStatus.Running;
                if (status == CalculationManager.RunStatus.PreStart && valuesSet)
                    return CalculationManager.RunStatus.Completed;
                return status;
            }
        }
        public static float PercentComplete
        {
            get { return calculationManager.PercentComplete; }
        }
        private static bool valuesSet = false;
        public static Conditions currentConditions = Conditions.Blank;
        private static Dictionary<Conditions, AoAPoint[]> cache = new Dictionary<Conditions, AoAPoint[]>();

        public static void Cancel()
        {
            calculationManager.Cancel();
            calculationManager = new CalculationManager();
            valuesSet = false;
        }
        public static void Clear()
        {
            calculationManager.Cancel();
            calculationManager = new CalculationManager();
            currentConditions = Conditions.Blank;
            cache.Clear();
            AoAPoints = new AoAPoint[0];
        }

        public static void Calculate(AeroPredictor vessel, CelestialBody body, float altitude, float speed, float lowerBound = -20f, float upperBound = 20f, float step = 0.5f)
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
                WindTunnel.Instance.StartCoroutine(Processing(calculationManager, newConditions, vessel));
            }
            else
            {
                currentConditions = newConditions;
                calculationManager.Status = CalculationManager.RunStatus.Completed;
                valuesSet = true;
            }
        }

        private static IEnumerator Processing(CalculationManager manager, Conditions conditions, AeroPredictor vessel)
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

            public AoAPoint(AeroPredictor vessel, CelestialBody body, float altitude, float speed, float AoA)
            {
                this.altitude = altitude;
                this.speed = speed;
                this.AoA = AoA;
                float atmDensity, atmPressure;
                lock (body)
                {
                    atmPressure = (float)body.GetPressure(altitude);
                    atmDensity = (float)Extensions.KSPClassExtensions.GetDensity(body, altitude);
                    this.mach = (float)(speed / body.GetSpeedOfSound(atmPressure, atmDensity));
                }
                this.dynamicPressure = 0.0005f * atmDensity * speed * speed;
                Vector3 force = AeroPredictor.ToFlightFrame(vessel.GetAeroForce(body, speed, altitude, AoA), AoA);
                Lift = force.y;
                Drag = -force.z;
                LDRatio = Mathf.Abs(Lift / Drag);
                dLift = (vessel.GetLiftForceMagnitude(body, speed, altitude, AoA + WindTunnelWindow.AoAdelta) - Lift) /
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
