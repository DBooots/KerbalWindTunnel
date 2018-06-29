using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KerbalWindTunnel.Graphing;
using KerbalWindTunnel.DataGenerators;
using KerbalWindTunnel.Extensions;
using UnityEngine;

namespace KerbalWindTunnel
{
    public partial class WindTunnelWindow
    {
        private bool showEnvelopeMask = true;
        private EnvelopeSurf.Conditions maskConditions = EnvelopeSurf.Conditions.Blank;
        //private string[][] graphSelections = new string[][] {
        //    new string[] { "Excess Thrust", "Excess Acceleration", "Thrust Available", "Level Flight AoA", "Max Lift AoA", "Max Lift Force" },
        //    new string[] {"Lift Force", "Drag Force", "Lift-Drag Ratio" },
        //    new string[]{"Level Flight AoA", "Max Lift AoA", "Thrust Available"}

        private void DrawGraph(GraphMode graphMode, GraphSelect graphSelect)
        {
            if (graphDirty)
            {
                if (this.vessel == null)
                    this.vessel = VesselCache.SimulatedVessel.Borrow(EditorLogic.fetch.ship, VesselCache.SimCurves.Borrow(body));
                    //this.vessel = new StockAero();

                switch (graphMode)
                {
                    case GraphMode.FlightEnvelope:
                        if (!graphRequested)
                        {
                            EnvelopeSurfGenerator.Calculate(vessel, body, 0, 2000, 10, 0, 25000, 100);
                            graphRequested = true;
                        }
                        switch (EnvelopeSurfGenerator.Status)
                        {
                            case CalculationManager.RunStatus.PreStart:
                            case CalculationManager.RunStatus.Cancelled:
                            case CalculationManager.RunStatus.Running:
                                DrawProgressBar(EnvelopeSurfGenerator.PercentComplete);
                                break;
                            case CalculationManager.RunStatus.Completed:
                                /*for (int x = 0; x <= EnvelopeSurf.envelopePoints.GetUpperBound(0); x++)
                                {
                                    for (int y = 0; y <= EnvelopeSurf.envelopePoints.GetUpperBound(1); y++)
                                    {
                                        EnvelopeSurf.EnvelopePoint pt = EnvelopeSurf.envelopePoints[x, y];
                                        //Debug.Log(String.Format("{0} / {1} / {2} / {3} / {4} / {5}", pt.altitude, pt.speed, pt.AoA_level, pt.Thrust_available, pt.Thrust_excess, pt.Accel_excess));
                                        if(pt.speed == 930 && pt.altitude == 22200)
                                        {
                                            Debug.Log("AoA Level:        " + pt.AoA_level * 180 / Mathf.PI);
                                            Debug.Log("Thrust Available: " + pt.Thrust_available);
                                            Debug.Log("Excess Thrust:    " + pt.Thrust_excess);
                                            Debug.Log("Excess Accel:     " + pt.Accel_excess);
                                            Debug.Log("Speed:            " + pt.speed);
                                            Debug.Log("Altitude:         " + pt.altitude);
                                            Debug.Log("Force:            " + pt.force);
                                            Debug.Log("LiftForce:        " + pt.liftforce);
                                        }
                                    }
                                }//*/
                                grapher.Clear();
                                switch (graphSelect)
                                {
                                    case GraphSelect.ExcessThrust:
                                        grapher.AddGraph(EnvelopeSurfGenerator.GetGraphableByName("Excess Thrust"));
                                        break;
                                    case GraphSelect.ExcessAcceleration:
                                        grapher.AddGraph(EnvelopeSurfGenerator.GetGraphableByName("Excess Acceleration"));
                                        break;
                                    case GraphSelect.ThrustAvailable:
                                        grapher.AddGraph(EnvelopeSurfGenerator.GetGraphableByName("Thrust Available"));
                                        break;
                                    case GraphSelect.LevelFlightAoA:
                                        grapher.AddGraph(EnvelopeSurfGenerator.GetGraphableByName("Level AoA"));
                                        break;
                                    case GraphSelect.MaxLiftAoA:
                                        grapher.AddGraph(EnvelopeSurfGenerator.GetGraphableByName("Max Lift AoA"));
                                        break;
                                    case GraphSelect.MaxLiftForce:
                                        grapher.AddGraph(EnvelopeSurfGenerator.GetGraphableByName("Max Lift"));
                                        break;
                                    case GraphSelect.LiftDragRatio:
                                        grapher.AddGraph(EnvelopeSurfGenerator.GetGraphableByName("Lift/Drag Ratio"));
                                        break;
                                    case GraphSelect.DragForce:
                                        grapher.AddGraph(EnvelopeSurfGenerator.GetGraphableByName("Drag"));
                                        break;
                                    case GraphSelect.LiftSlope:
                                        grapher.AddGraph(EnvelopeSurfGenerator.GetGraphableByName("Lift Slope"));
                                        break;
                                }
                                grapher.RecalculateLimits();

                                if (showEnvelopeMask && !maskConditions.Equals(EnvelopeSurfGenerator.currentConditions))
                                {
                                    ((SurfGraph)EnvelopeSurfGenerator.GetGraphableByName("Excess Thrust"))
                                        .DrawMask(ref maskTex, grapher.XMin, grapher.XMax, grapher.YMin, grapher.YMax,
                                        (v) => v >= 0 && !float.IsNaN(v) && !float.IsInfinity(v), Color.grey, true, 2);
                                    maskConditions = EnvelopeSurfGenerator.currentConditions;
                                }

                                graphDirty = false;
                                break;
                        }
                        break;
                    case GraphMode.AoACurves:
                        if (!graphRequested)
                        {
                            AoACurveGenerator.Calculate(vessel, body, Altitude, Speed, -20f * Mathf.PI / 180, 20f * Mathf.PI / 180, 0.5f * Mathf.PI / 180);
                            graphRequested = true;
                        }
                        switch (AoACurveGenerator.Status)
                        {
                            case CalculationManager.RunStatus.PreStart:
                            case CalculationManager.RunStatus.Cancelled:
                            case CalculationManager.RunStatus.Running:
                                DrawProgressBar(AoACurveGenerator.PercentComplete);
                                break;
                            case CalculationManager.RunStatus.Completed:
                                grapher.Clear();
                                switch (graphSelect)
                                {
                                    case GraphSelect.LiftForce:
                                        grapher.AddGraph(AoACurveGenerator.GetGraphableByName("Lift"));
                                        break;
                                    case GraphSelect.DragForce:
                                        grapher.AddGraph(AoACurveGenerator.GetGraphableByName("Drag"));
                                        break;
                                    case GraphSelect.LiftDragRatio:
                                        grapher.AddGraph(AoACurveGenerator.GetGraphableByName("Lift/Drag Ratio"));
                                        break;
                                    case GraphSelect.LiftSlope:
                                        grapher.AddGraph(AoACurveGenerator.GetGraphableByName("Lift Slope"));
                                        break;
                                }
                                graphDirty = false;
                                break;
                        }
                        break;
                    case GraphMode.VelocityCurves:
                        if (!graphRequested)
                        {
                            VelCurveGenerator.Calculate(vessel, body, Altitude, 0, 2000, 10);
                            graphRequested = true;
                        }
                        switch (VelCurveGenerator.Status)
                        {
                            case CalculationManager.RunStatus.PreStart:
                            case CalculationManager.RunStatus.Cancelled:
                            case CalculationManager.RunStatus.Running:
                                DrawProgressBar(VelCurveGenerator.PercentComplete);
                                break;
                            case CalculationManager.RunStatus.Completed:
                                grapher.Clear();
                                switch (graphSelect)
                                {
                                    case GraphSelect.LevelFlightAoA:
                                        grapher.AddGraph(VelCurveGenerator.GetGraphableByName("Level AoA"));
                                        break;
                                    case GraphSelect.MaxLiftAoA:
                                        grapher.AddGraph(VelCurveGenerator.GetGraphableByName("Max Lift AoA"));
                                        break;
                                    case GraphSelect.ThrustAvailable:
                                        grapher.AddGraph(VelCurveGenerator.GetGraphableByName("Thrust Available"));
                                        break;
                                    case GraphSelect.LiftDragRatio:
                                        grapher.AddGraph(VelCurveGenerator.GetGraphableByName("Lift/Drag Ratio"));
                                        break;
                                    case GraphSelect.DragForce:
                                        grapher.AddGraph(VelCurveGenerator.GetGraphableByName("Drag"));
                                        break;
                                    case GraphSelect.LiftSlope:
                                        grapher.AddGraph(VelCurveGenerator.GetGraphableByName("Lift Slope"));
                                        break;
                                }
                                graphDirty = false;
                                break;
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("graphMode");
                }

                if (selectedCrossHairVect.x >= 0 && selectedCrossHairVect.y >= 0)
                {
                    selectedCrossHairVect = CrossHairsFromConditions(Altitude, Speed, AoA);
                    SetConditionsFromGraph(selectedCrossHairVect);
                    conditionDetails = GetConditionDetails(CurrentGraphMode, this.Altitude, this.Speed, CurrentGraphMode == GraphMode.AoACurves ? this.AoA : float.NaN, false);
                }
                else
                    conditionDetails = "";

                if (GraphGenerator.Status == CalculationManager.RunStatus.Completed)
                {
                    grapher.Draw();
                    DrawGraph();
                }
            }
            else
            {
                DrawGraph();
            }
        }

        Texture2D maskTex = new Texture2D(graphWidth, graphHeight, TextureFormat.ARGB32, false);

        public float GetGraphValue(int x, int y = -1)
        {
            return grapher.GetValueAt(x, y, 0);
        }
        public string GetConditionDetails(GraphMode mode, float altitude, float speed = float.NaN, float aoa = float.NaN)
        {
            return GetConditionDetails(mode, altitude, speed, aoa, false);
        }
        private string GetConditionDetails(GraphMode mode, float altitude, float speed, float aoa, bool setAoA)
        {
            switch (mode)
            {
                case GraphMode.FlightEnvelope:
                    EnvelopeSurf.EnvelopePoint conditionPtFE = new EnvelopeSurf.EnvelopePoint(this.vessel, this.body, Altitude, Speed, this.rootSolver, 0);
                    if (setAoA)
                        this.AoA = conditionPtFE.AoA_level;

                    return String.Format("Altitude:\t{0:N0}m\n" + "Speed:\t{1:N0}m/s\n" + "Mach:\t{9:N2}\n" + "Level Flight AoA:\t{2:N2}°\n" +
                        "Excess Thrust:\t{3:N0}kN\n" + "Excess Acceleration:\t{4:N2}g\n" + "Max Lift Force:\t{5:N0}kN\n" +
                        "Max Lift AoA:\t{6:N2}°\n" + "Lift/Drag Ratio:\t{8:N2}\n" + "Available Thrust:\t{7:N0}kN",
                        conditionPtFE.altitude, conditionPtFE.speed, conditionPtFE.AoA_level * 180 / Mathf.PI,
                        conditionPtFE.Thrust_excess, conditionPtFE.Accel_excess, conditionPtFE.Lift_max,
                        conditionPtFE.AoA_max * 180 / Mathf.PI, conditionPtFE.Thrust_available, conditionPtFE.LDRatio,
                        conditionPtFE.mach);

                case GraphMode.AoACurves:
                    AoACurve.AoAPoint conditionPtAoA = new AoACurve.AoAPoint(this.vessel, this.body, this.Altitude, this.Speed, this.AoA);

                    return String.Format("Altitude:\t{0:N0}m\n" + "Speed:\t{1:N0}m/s\n" + "Mach:\t{6:N2}\n" + "AoA:\t{2:N2}°\n" +
                        "Lift:\t{3:N0}kN\n" + "Drag:\t{4:N0}kN\n" + "Lift/Drag Ratio:\t{5:N2}",
                        conditionPtAoA.altitude, conditionPtAoA.speed, conditionPtAoA.AoA * 180 / Mathf.PI,
                        conditionPtAoA.Lift, conditionPtAoA.Drag, conditionPtAoA.LDRatio, conditionPtAoA.mach);

                case GraphMode.VelocityCurves:
                    VelCurve.VelPoint conditionPtVel = new VelCurve.VelPoint(this.vessel, this.body, this.Altitude, Speed, this.rootSolver);
                    if (setAoA)
                        this.AoA = conditionPtVel.AoA_level;

                    return String.Format("Altitude:\t{0:N0}m\n" + "Speed:\t{1:N0}m/s\n" + "Mach:\t{7:N2}\n" + "Level Flight AoA:\t{2:N2}°\n" +
                        "Excess Thrust:\t{3:N0}kN\n" +
                        "Max Lift AoA:\t{4:N2}°\n" + "Lift/Drag Ratio:\t{6:N0}\n" + "Available Thrust:\t{5:N0}kN",
                        conditionPtVel.altitude, conditionPtVel.speed, conditionPtVel.AoA_level * 180 / Mathf.PI,
                        conditionPtVel.Thrust_excess,
                        conditionPtVel.AoA_max * 180 / Mathf.PI, conditionPtVel.Thrust_available, conditionPtVel.LDRatio,
                        conditionPtVel.mach);

                default:
                    return "";
            }
        }

        private const int graphWidth = 500;
        private const int graphHeight = 400;
        private const int axisWidth = 10;

        internal readonly Vector2 PlotPosition = new Vector2(25, 155);

        GUIStyle hAxisMarks = new GUIStyle(HighLogic.Skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
        GUIStyle vAxisMarks = new GUIStyle(HighLogic.Skin.label) { fontSize = 12, alignment = TextAnchor.MiddleRight };
        Rect graphRect = new Rect(0, 0, graphWidth, graphHeight);

        private void DrawGraph()
        {
            GUILayout.Box("", GUIStyle.none, GUILayout.Width(graphWidth + axisWidth), GUILayout.Height(5));
            GUILayout.BeginHorizontal();
            GUILayout.Box("", GUIStyle.none, GUILayout.Width(40), GUILayout.Height(graphHeight));
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Box(grapher.vAxisTex, GUIStyle.none, GUILayout.Height(graphHeight), GUILayout.Width(axisWidth));

            GUIContent graph = new GUIContent(grapher.graphTex);
            graphRect = GUILayoutUtility.GetRect(graph, HighLogic.Skin.box, GUILayout.Height(graphHeight), GUILayout.Width(graphWidth));
            GUI.Box(graphRect, graph);
            if (CurrentGraphMode == GraphMode.FlightEnvelope && showEnvelopeMask)
                GUI.Box(graphRect, maskTex, clearBox);
            //GUILayout.Box(graphTex, GUILayout.Height(graphHeight), GUILayout.Width(graphWidth));

            GUILayout.EndHorizontal();
            GUILayout.Box("", GUIStyle.none, GUILayout.Width(graphWidth + axisWidth), GUILayout.Height(5));
            GUILayout.BeginHorizontal();
            GUILayout.Box("", GUIStyle.none, GUILayout.Width(axisWidth + 4), GUILayout.Height(axisWidth));
            GUILayout.Box(grapher.hAxisTex, GUIStyle.none, GUILayout.Width(graphWidth), GUILayout.Height(axisWidth));
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Label("", GUILayout.Width(graphWidth + axisWidth), GUILayout.Height(10));

            for (int i = 0; i <= grapher.verticalAxis.TickCount; i++)
            {

                GUI.Label(new Rect(5, 58 + graphHeight - Mathf.RoundToInt(graphHeight / (float)grapher.verticalAxis.TickCount * i), 40, 15),
                    grapher.verticalAxis.labels[i], vAxisMarks);
            }

            for (int i = 0; i <= grapher.horizontalAxis.TickCount; i++)
            {
                GUI.Label(new Rect(43 + Mathf.RoundToInt(graphWidth / (float)grapher.horizontalAxis.TickCount * i), 80 + graphHeight, 40, 15),
                    grapher.horizontalAxis.labels[i], hAxisMarks);
            }
        }

        private void DrawProgressBar(float percentComplete)
        {
            //GUI.Label(new Rect(PlotPosition.x, PlotPosition.y + graphHeight / 2 - 30, graphWidth + 45, 20), "Calculating... (" + percentComplete * 100 + "%)");
            GUILayout.Label(String.Format("Calculating... ({0:N1}%)", percentComplete * 100), labelCentered, GUILayout.Height(graphHeight), GUILayout.Width(graphWidth));
            Rect rectBar = new Rect(PlotPosition.x, PlotPosition.y + 292 / 2 - 10, 292 + 45, 20);
            //blnReturn = Drawing.DrawBar(styleBack, out rectBar, Width);
            GUI.Button(rectBar, "");

            if ((rectBar.width * percentComplete) > 1)
                DrawBarScaled(rectBar, new GUIStyle(), new GUIStyle(), percentComplete);
        }
        internal static void DrawBarScaled(Rect rectStart, GUIStyle Style, GUIStyle StyleNarrow, float Scale)
        {
            Rect rectTemp = new Rect(rectStart);
            rectTemp.width = (float)Math.Ceiling(rectTemp.width = rectTemp.width * Scale);
            if (rectTemp.width <= 2) Style = StyleNarrow;
            GUI.Label(rectTemp, "", Style);
        }
    }
}
