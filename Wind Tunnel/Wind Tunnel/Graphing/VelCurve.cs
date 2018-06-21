using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;

namespace KerbalWindTunnel.Graphing
{
    public static class VelCurve
    {
        public static VelPoint[] VelPoints = new VelPoint[0];
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
        private static Dictionary<Conditions, VelPoint[]> cache = new Dictionary<Conditions, VelPoint[]>();

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
            VelPoints = new VelPoint[0];
        }

        public static void Calculate(AeroPredictor vessel, CelestialBody body, float altitude, float lowerBound = 0, float upperBound = 2000, float step = 50)
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
                WindTunnel.Instance.StartCoroutine(Processing(calculationManager, newConditions, vessel, WindTunnelWindow.Instance.rootSolver));
            }
            else
            {
                currentConditions = newConditions;
                calculationManager.Status = CalculationManager.RunStatus.Completed;
                valuesSet = true;
            }
        }

        private static IEnumerator Processing(CalculationManager manager, Conditions conditions, AeroPredictor vessel, RootSolvers.RootSolver solver)
        {
            int numPts = (int)Math.Ceiling((conditions.upperBound - conditions.lowerBound) / conditions.step);
            VelPoint[] newVelPoints = new VelPoint[numPts + 1];
            float trueStep = (conditions.upperBound - conditions.lowerBound) / numPts;
            CalculationManager.State[] results = new CalculationManager.State[numPts + 1];

            for (int i = 0; i <= numPts; i++)
            {
                //newAoAPoints[i] = new AoAPoint(vessel, conditions.body, conditions.altitude, conditions.speed, conditions.lowerBound + trueStep * i);
                GenData genData = new GenData(vessel, conditions, conditions.lowerBound + trueStep * i, solver, manager);
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
                valuesSet = true;
            }
        }

        private static void GenerateVelPoint(object obj)
        {
            GenData data = (GenData)obj;
            if (data.storeState.manager.Cancelled)
                return;
            data.storeState.StoreResult(new VelPoint(data.vessel, data.conditions.body, data.conditions.altitude, data.speed, data.solver));
        }

        private struct GenData
        {
            public readonly Conditions conditions;
            public readonly AeroPredictor vessel;
            public readonly CalculationManager.State storeState;
            public readonly float speed;
            public readonly RootSolvers.RootSolver solver;

            public GenData(AeroPredictor vessel, Conditions conditions, float speed, RootSolvers.RootSolver solver, CalculationManager manager)
            {
                this.vessel = vessel;
                this.conditions = conditions;
                this.speed = speed;
                this.solver = solver;
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

            public VelPoint(AeroPredictor vessel, CelestialBody body, float altitude, float speed, RootSolvers.RootSolver solver)
            {
                this.altitude = altitude;
                this.speed = speed;
                float atmPressure, atmDensity, mach, gravParameter, radius;
                bool oxygenAvailable;
                lock (body)
                {
                    atmPressure = (float)body.GetPressure(altitude);
                    atmDensity = (float)Extensions.KSPClassExtensions.GetDensity(body, altitude);
                    mach = (float)(speed / body.GetSpeedOfSound(atmPressure, atmDensity));
                    oxygenAvailable = body.atmosphereContainsOxygen;
                    gravParameter = (float)body.gravParameter;
                    radius = (float)body.Radius;
                }
                this.mach = mach;
                this.dynamicPressure = 0.0005f * atmDensity * speed * speed;
                float weight = (vessel.Mass * gravParameter / ((radius + altitude) * (radius + altitude))); // TODO: Minus centrifugal force...
                Vector3 thrustForce = vessel.GetThrustForce(mach, atmDensity, atmPressure, oxygenAvailable);
                AoA_level = solver.Solve(
                    (aoa) => AeroPredictor.GetLiftForceMagnitude(
                        vessel.GetLiftForce(body, speed, altitude, aoa, mach, atmDensity) + thrustForce, aoa)
                        - weight, 0, WindTunnelWindow.Instance.solverSettings);
                AoA_max = float.NaN;
                Thrust_available = thrustForce.magnitude;
                if (!float.IsNaN(AoA_level))
                {
                    drag = AeroPredictor.GetDragForceMagnitude(vessel.GetAeroForce(body, speed, altitude, AoA_level, mach, atmDensity), AoA_level);
                    Thrust_excess = -drag - AeroPredictor.GetDragForceMagnitude(thrustForce, AoA_level);
                    LDRatio = Mathf.Abs(weight / drag);
                    dLift = (vessel.GetLiftForceMagnitude(body, speed, altitude, AoA_level + WindTunnelWindow.AoAdelta) -
                        vessel.GetLiftForceMagnitude(body, speed, altitude, AoA_level)) / (WindTunnelWindow.AoAdelta * 180 / Mathf.PI);
                }
                else
                {
                    drag = float.PositiveInfinity;
                    Thrust_excess = float.NegativeInfinity;
                    LDRatio = 0;
                    dLift = 0;
                }
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
