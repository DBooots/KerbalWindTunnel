using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KerbalWindTunnel.Threading;
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
            float bottom = currentConditions.lowerBoundAltitude;
            float top = currentConditions.upperBoundAltitude;
            float left = currentConditions.lowerBoundSpeed;
            float right = currentConditions.upperBoundSpeed;
            Func<EnvelopePoint, float> scale = (pt) => 1;
            if (WindTunnelSettings.UseCoefficients)
                scale = (pt) => 1 / pt.dynamicPressure;
            SurfGraph newSurfGraph;
            newSurfGraph = new SurfGraph(envelopePoints.SelectToArray(pt => pt.Thrust_excess), left, right, bottom, top) { Name = "Excess Thrust", ZUnit = "kN", StringFormat = "N0", Color = Jet_Dark_Positive, ZAxisScale = (v) => v >= 0 ? v : 0 };
            float maxThrustExcess = newSurfGraph.ZMax;
            newSurfGraph.ColorFunc = (x, y, z) => z / maxThrustExcess;
            newSurfGraph.ZAxisScaler = maxThrustExcess / Axis.GetMax(0, newSurfGraph.ZMax);
            graphs.Add("Excess Thrust", newSurfGraph);
            newSurfGraph = new SurfGraph(envelopePoints.SelectToArray(pt => pt.Accel_excess), left, right, bottom, top) { Name = "Excess Acceleration", ZUnit = "g", StringFormat = "N2", Color = Jet_Dark_Positive, ZAxisScale = (v) => v >= 0 ? v : 0 };
            float maxAccelExcess = newSurfGraph.ZMax;
            newSurfGraph.ColorFunc = (x, y, z) => z / maxAccelExcess;
            newSurfGraph.ZAxisScaler = maxAccelExcess / Axis.GetMax(0, newSurfGraph.ZMax);
            graphs.Add("Excess Acceleration", newSurfGraph);
            graphs.Add("Thrust Available", new SurfGraph(envelopePoints.SelectToArray(pt => pt.Thrust_available), left, right, bottom, top, true) { Name = "Thrust Available", ZUnit = "kN", StringFormat = "N0", Color = ColorMap.Jet_Dark });
            graphs.Add("Level AoA", new SurfGraph(envelopePoints.SelectToArray(pt => pt.AoA_level * Mathf.Rad2Deg), left, right, bottom, top, true) { Name = "Level AoA", ZUnit = "°", StringFormat = "F2", Color = ColorMap.Jet_Dark });
            graphs.Add("Max Lift AoA", new SurfGraph(envelopePoints.SelectToArray(pt => pt.AoA_max * Mathf.Rad2Deg), left, right, bottom, top, true) { Name = "Max Lift AoA", ZUnit = "°", StringFormat = "F2", Color = ColorMap.Jet_Dark });
            graphs.Add("Max Lift", new SurfGraph(envelopePoints.SelectToArray(pt => pt.Lift_max), left, right, bottom, top, true) { Name = "Max Lift", ZUnit = "kN", StringFormat = "N0", Color = ColorMap.Jet_Dark });
            graphs.Add("Lift/Drag Ratio", new SurfGraph(envelopePoints.SelectToArray(pt => pt.LDRatio), left, right, bottom, top, true) { Name = "Lift/Drag Ratio", ZUnit = "", StringFormat = "F2", Color = ColorMap.Jet_Dark });
            graphs.Add("Drag", new SurfGraph(envelopePoints.SelectToArray(pt => pt.drag * scale(pt)), left, right, bottom, top, true) { Name = "Drag", ZUnit = "kN", StringFormat = "N0", Color = ColorMap.Jet_Dark });
            graphs.Add("Lift Slope", new SurfGraph(envelopePoints.SelectToArray(pt => pt.dLift / pt.dynamicPressure), left, right, bottom, top, true) { Name = "Lift Slope", ZUnit = "m^2/°", StringFormat = "F3", Color = ColorMap.Jet_Dark });
            graphs.Add("Pitch Input", new SurfGraph(envelopePoints.SelectToArray(pt => pt.pitchInput), left, right, bottom, top, true) { Name = "Pitch Input", ZUnit = "", StringFormat = "F2", Color = ColorMap.Jet_Dark });
            graphs.Add("Envelope Mask", new OutlineMask(envelopePoints.SelectToArray(pt => pt.Thrust_excess), left, right, bottom, top) { Name = "Envelope Mask", ZUnit = "kN", StringFormat = "N0", Color = Color.grey, LineWidth = 2, LineOnly = true, MaskCriteria = (v) => !float.IsNaN(v) && !float.IsInfinity(v) && v >= 0 });

            var e = graphs.GetEnumerator();
            while (e.MoveNext())
            {
                e.Current.Value.XUnit = "m/s";
                e.Current.Value.XName = "Speed";
                e.Current.Value.YUnit = "m";
                e.Current.Value.YName = "Altitude";
            }
        }

        private void UpdateGraphs()
        {
            float bottom = currentConditions.lowerBoundAltitude;
            float top = currentConditions.upperBoundAltitude;
            float left = currentConditions.lowerBoundSpeed;
            float right = currentConditions.upperBoundSpeed;
            Func<EnvelopePoint, float> scale = (pt) => 1;
            if (WindTunnelSettings.UseCoefficients)
                scale = (pt) => 1 / pt.dynamicPressure;

            ((SurfGraph)graphs["Excess Thrust"]).SetValues(envelopePoints.SelectToArray(pt => pt.Thrust_excess), left, right, bottom, top);
            float maxThrustExcess = ((SurfGraph)graphs["Excess Thrust"]).ZMax;
            ((SurfGraph)graphs["Excess Thrust"]).ColorFunc = (x, y, z) => z / maxThrustExcess;
            ((SurfGraph)graphs["Excess Thrust"]).ZAxisScaler = maxThrustExcess / Axis.GetMax(0, maxThrustExcess);
            ((SurfGraph)graphs["Excess Acceleration"]).SetValues(envelopePoints.SelectToArray(pt => pt.Accel_excess), left, right, bottom, top);
            float maxAccelExcess = ((SurfGraph)graphs["Excess Acceleration"]).ZMax;
            ((SurfGraph)graphs["Excess Acceleration"]).ColorFunc = (x, y, z) => z / maxAccelExcess;
            ((SurfGraph)graphs["Excess Acceleration"]).ZAxisScaler = maxAccelExcess / Axis.GetMax(0, maxAccelExcess);
            ((SurfGraph)graphs["Thrust Available"]).SetValues(envelopePoints.SelectToArray(pt => pt.Thrust_available), left, right, bottom, top, true);
            ((SurfGraph)graphs["Level AoA"]).SetValues(envelopePoints.SelectToArray(pt => pt.AoA_level * Mathf.Rad2Deg), left, right, bottom, top, true);
            ((SurfGraph)graphs["Max Lift AoA"]).SetValues(envelopePoints.SelectToArray(pt => pt.AoA_max * Mathf.Rad2Deg), left, right, bottom, top, true);
            ((SurfGraph)graphs["Max Lift"]).SetValues(envelopePoints.SelectToArray(pt => pt.Lift_max), left, right, bottom, top, true);
            ((SurfGraph)graphs["Lift/Drag Ratio"]).SetValues(envelopePoints.SelectToArray(pt => pt.LDRatio), left, right, bottom, top, true);
            ((SurfGraph)graphs["Drag"]).SetValues(envelopePoints.SelectToArray(pt => pt.drag * scale(pt)), left, right, bottom, top, true);
            ((SurfGraph)graphs["Lift Slope"]).SetValues(envelopePoints.SelectToArray(pt => pt.dLift / pt.dynamicPressure), left, right, bottom, top, true);
            ((SurfGraph)graphs["Pitch Input"]).SetValues(envelopePoints.SelectToArray(pt => pt.pitchInput), left, right, bottom, top, true);
            ((OutlineMask)graphs["Envelope Mask"]).SetValues(envelopePoints.SelectToArray(pt => pt.Thrust_excess), left, right, bottom, top);
        }

        private IEnumerator Processing(CalculationManager manager, Conditions conditions, AeroPredictor vessel)
        {
            int numPtsX = (int)Math.Ceiling((conditions.upperBoundSpeed - conditions.lowerBoundSpeed) / conditions.stepSpeed);
            int numPtsY = (int)Math.Ceiling((conditions.upperBoundAltitude - conditions.lowerBoundAltitude) / conditions.stepAltitude);
            EnvelopePoint[,] newEnvelopePoints = new EnvelopePoint[numPtsX + 1, numPtsY + 1];
            
            GenData rootData = new GenData(vessel, conditions, 0, 0, manager);
            ThreadPool.QueueUserWorkItem(SetupInBackground, rootData);

            while (!manager.Completed)
            {
                //Debug.Log(manager.PercentComplete + "% done calculating...");
                if (manager.Status == CalculationManager.RunStatus.Cancelled)
                    yield break;
                yield return 0;
            }
            
            newEnvelopePoints = ((CalculationManager.State[,])rootData.storeState.Result)
                .SelectToArray(pt => (EnvelopePoint)pt.Result);

            if (!manager.Cancelled)
            {
                //cache.Add(conditions, newEnvelopePoints);
                AddToCache(conditions, newEnvelopePoints);
                envelopePoints = newEnvelopePoints;
                currentConditions = conditions;
                GenerateGraphs();
                valuesSet = true;
            }

            float stepSpeed = conditions.stepSpeed, stepAltitude = conditions.stepAltitude;
            for(int i = 2; i <=2; i++)
            {
                yield return 0;

                CalculationManager backgroundManager = new CalculationManager();
                manager.OnCancelCallback += backgroundManager.Cancel;
                conditions = new Conditions(conditions.body, conditions.lowerBoundSpeed, conditions.upperBoundSpeed,
                    stepSpeed / i, conditions.lowerBoundAltitude, conditions.upperBoundAltitude, stepAltitude / i);
                CalculationManager.State[,] prevResults = ((CalculationManager.State[,])rootData.storeState.Result).SelectToArray(p => p);
                rootData = new GenData(vessel, conditions, 0, 0, backgroundManager);
                ThreadPool.QueueUserWorkItem(ContinueInBackground, new object[] { rootData, prevResults });
                while (!backgroundManager.Completed)
                {
                    if (manager.Status == CalculationManager.RunStatus.Cancelled)
                    {
                        backgroundManager.Cancel();
                        yield break;
                    }
                    yield return 0;
                }

                newEnvelopePoints = ((CalculationManager.State[,])rootData.storeState.Result)
                    .SelectToArray(pt => (EnvelopePoint)pt.Result);

                if (!manager.Cancelled)
                {
                    //cache.Add(conditions, newEnvelopePoints);
                    AddToCache(conditions, newEnvelopePoints);
                    envelopePoints = newEnvelopePoints;
                    currentConditions = conditions;
                    UpdateGraphs();
                    valuesSet = true;
                }
            }
        }

        private bool AddToCache(Conditions conditions, EnvelopePoint[,] data)
        {
            if (cache.ContainsKey(conditions) && cache[conditions].Length > data.Length)
                return false;
            cache[conditions] = data;
            return true;
        }

        private static void SetupInBackground(object obj)
        {
            GenData rootData = (GenData)obj;
            Conditions conditions = rootData.conditions;
            CalculationManager manager = rootData.storeState.manager;
            CalculationManager seedManager = new CalculationManager();
            CalculationManager.State[,] results = null;
            
            Conditions seedConditions = new Conditions(conditions.body, conditions.lowerBoundSpeed, conditions.upperBoundSpeed, 10, conditions.lowerBoundAltitude, conditions.upperBoundAltitude, 10);
            GenerateLevel(seedConditions, seedManager, ref results, rootData.vessel);

            seedManager.WaitForCompletion();

            GenerateLevel(conditions, manager, ref results, rootData.vessel);

            if (rootData.storeState.manager.Cancelled)
                return;
            rootData.storeState.StoreResult(results);
        }

        private static void ContinueInBackground(object obj)
        {
            object[] inObj = (object[])obj;
            GenData rootData = (GenData)inObj[0];
            CalculationManager.State[,] results = (CalculationManager.State[,])inObj[1];
            CalculationManager manager = rootData.storeState.manager;
            GenerateLevel(rootData.conditions, manager, ref results, rootData.vessel);
            if (rootData.storeState.manager.Cancelled)
                return;
            rootData.storeState.StoreResult(results);
        }

        private static void GenerateLevel(Conditions conditions, CalculationManager manager, ref CalculationManager.State[,] results, AeroPredictor vessel)
        {
            float[,] AoAs_guess = null, maxAs_guess = null, pitchIs_guess = null;
            if (results != null)
            {
                AoAs_guess = results.SelectToArray(pt => ((EnvelopePoint)pt.Result).AoA_level);
                maxAs_guess = results.SelectToArray(pt => ((EnvelopePoint)pt.Result).AoA_max);
                pitchIs_guess = results.SelectToArray(pt => ((EnvelopePoint)pt.Result).pitchInput);
            }
            int numPtsX = (int)Math.Ceiling((conditions.upperBoundSpeed - conditions.lowerBoundSpeed) / conditions.stepSpeed);
            int numPtsY = (int)Math.Ceiling((conditions.upperBoundAltitude - conditions.lowerBoundAltitude) / conditions.stepAltitude);
            float trueStepX = (conditions.upperBoundSpeed - conditions.lowerBoundSpeed) / numPtsX;
            float trueStepY = (conditions.upperBoundAltitude - conditions.lowerBoundAltitude) / numPtsY;
            results = new CalculationManager.State[numPtsX + 1, numPtsY + 1];

            for (int j = 0; j <= numPtsY; j++)
            {
                for (int i = 0; i <= numPtsX; i++)
                {
                    if (manager.Cancelled)
                        return;
                    float x = (float)i / numPtsX;
                    float y = (float)j / numPtsY;
                    float aoa_guess = AoAs_guess != null ? AoAs_guess.Lerp2(x, y) : float.NaN;
                    float maxA_guess = maxAs_guess != null ? maxAs_guess.Lerp2(x, y) : float.NaN;
                    float pi_guess = pitchIs_guess != null ? pitchIs_guess.Lerp2(x, y) : float.NaN;
                    GenData genData = new GenData(vessel, conditions, conditions.lowerBoundSpeed + trueStepX * i, conditions.lowerBoundAltitude + trueStepY * j, manager, aoa_guess, maxA_guess, pi_guess);
                    results[i, j] = genData.storeState;
                    ThreadPool.QueueUserWorkItem(GenerateSurfPoint, genData);
                }
            }
        }

        private static void GenerateSurfPoint(object obj)
        {
            GenData data = (GenData)obj;
            if (data.storeState.manager.Cancelled)
                return;
            //Debug.Log("Starting point: " + data.altitude + "/" + data.speed);
            EnvelopePoint result = new EnvelopePoint(data.vessel, data.conditions.body, data.altitude, data.speed, data.AoA_guess, data.maxA_guess, data.pitchI_guess);
            //Debug.Log("Point solved: " + data.altitude + "/" + data.speed);

            data.storeState.StoreResult(result);
        }

        private struct GenData
        {
            public readonly Conditions conditions;
            public readonly AeroPredictor vessel;
            public readonly CalculationManager.State storeState;
            public readonly float speed;
            public readonly float altitude;
            public readonly float AoA_guess;
            public readonly float maxA_guess;
            public readonly float pitchI_guess;

            public GenData(AeroPredictor vessel, Conditions conditions, float speed, float altitude, CalculationManager manager, float AoA_guess = float.NaN, float maxA_guess = float.NaN, float pitchI_guess = float.NaN)
            {
                this.vessel = vessel;
                this.conditions = conditions;
                this.speed = speed;
                this.altitude = altitude;
                this.storeState = manager.GetStateToken();
                this.AoA_guess = AoA_guess;
                this.maxA_guess = maxA_guess;
                this.pitchI_guess = pitchI_guess;
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

            public EnvelopePoint(AeroPredictor vessel, CelestialBody body, float altitude, float speed, float AoA_guess = float.NaN, float maxA_guess = float.NaN, float pitchI_guess = float.NaN)
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
                //AoA_max = vessel.GetMaxAoA(conditions, out Lift_max, maxA_guess);
                if (float.IsNaN(maxA_guess))
                    AoA_max = vessel.GetMaxAoA(conditions, out Lift_max, maxA_guess);
                else
                {
                    AoA_max = maxA_guess;
                    Lift_max = AeroPredictor.GetLiftForceMagnitude(vessel.GetAeroForce(conditions, AoA_max, 1) + thrustForce, AoA_max);
                }

                AoA_level = vessel.GetAoA(conditions, weight, guess: AoA_guess, pitchInputGuess: 0, lockPitchInput: true);
                if (AoA_level < AoA_max)
                    pitchInput = vessel.GetPitchInput(conditions, AoA_level, guess: pitchI_guess);
                else
                    pitchInput = 1;

                Thrust_available = thrustForce.magnitude;
                force = vessel.GetAeroForce(conditions, AoA_level, pitchInput);
                liftforce = AeroPredictor.ToFlightFrame(force, AoA_level); //vessel.GetLiftForce(body, speed, altitude, AoA_level, mach, atmDensity);
                drag = AeroPredictor.GetDragForceMagnitude(force, AoA_level);
                float lift = AeroPredictor.GetLiftForceMagnitude(force, AoA_level);
                Thrust_excess = -drag - AeroPredictor.GetDragForceMagnitude(thrustForce, AoA_level);
                if (weight > Lift_max)// AoA_level >= AoA_max)
                {
                    Thrust_excess = Lift_max - weight;
                    AoA_level = AoA_max;
                }
                Accel_excess = (Thrust_excess / vessel.Mass / WindTunnelWindow.gAccel);
                LDRatio = Mathf.Abs(lift / drag);
                dLift = (vessel.GetLiftForceMagnitude(conditions, AoA_level + WindTunnelWindow.AoAdelta, pitchInput) -
                    vessel.GetLiftForceMagnitude(conditions, AoA_level, pitchInput)) / (WindTunnelWindow.AoAdelta * Mathf.Rad2Deg);
            }

            public override string ToString()
            {
                return String.Format("Altitude:\t{0:N0}m\n" + "Speed:\t{1:N0}m/s\n" + "Mach:\t{9:N2}\n" + "Level Flight AoA:\t{2:N2}°\n" +
                        "Excess Thrust:\t{3:N0}kN\n" + "Excess Acceleration:\t{4:N2}g\n" + "Max Lift Force:\t{5:N0}kN\n" +
                        "Max Lift AoA:\t{6:N2}°\n" + "Lift/Drag Ratio:\t{8:N2}\n" + "Available Thrust:\t{7:N0}kN",
                        altitude, speed, AoA_level * Mathf.Rad2Deg,
                        Thrust_excess, Accel_excess, Lift_max,
                        AoA_max * Mathf.Rad2Deg, Thrust_available, LDRatio,
                        mach);
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

            public bool Equals(Conditions conditions)
            {
                return this.body == conditions.body &&
                    this.lowerBoundSpeed == conditions.lowerBoundSpeed &&
                    this.upperBoundSpeed == conditions.upperBoundSpeed &&
                    this.lowerBoundAltitude == conditions.lowerBoundAltitude &&
                    this.upperBoundAltitude == conditions.upperBoundAltitude;
            }

            public override int GetHashCode()
            {
                return Extensions.HashCode.Of(this.body).And(this.lowerBoundSpeed).And(this.upperBoundSpeed).And(this.lowerBoundAltitude).And(this.upperBoundAltitude);
            }
        }
    }
}
