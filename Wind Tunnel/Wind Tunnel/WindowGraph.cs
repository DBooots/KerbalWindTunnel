using System;
using KerbalWindTunnel.Graphing;
using KerbalWindTunnel.DataGenerators;
using KerbalWindTunnel.Threading;
using UnityEngine;

namespace KerbalWindTunnel
{
    public partial class WindTunnelWindow
    {
        private float altitudeStep = 200;
        private float maxAltitude = 25000;
        private float speedStep = 20;
        private float maxSpeed = 2000;
        public const int graphWidth = 500;
        public const int graphHeight = 400;
        public const int axisWidth = 10;

        internal readonly Vector2 PlotPosition = new Vector2(25, 155);

        private GUIStyle hAxisMarks = new GUIStyle(HighLogic.Skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
        private GUIStyle vAxisMarks = new GUIStyle(HighLogic.Skin.label) { fontSize = 12, alignment = TextAnchor.MiddleRight };
        private Rect graphRect = new Rect(0, 0, graphWidth, graphHeight);
        private Rect cAxisRect = new Rect(0, 0, graphWidth, axisWidth);

        private void DrawGraph(GraphMode graphMode, GraphSelect graphSelect)
        {
            if (graphDirty)
            {
                if (this.vessel == null)
                    this.vessel = VesselCache.SimulatedVessel.Borrow(EditorLogic.fetch.ship, VesselCache.SimCurves.Borrow(body));
                    //this.vessel = new StockAero();

                if (!graphRequested)
                {
                    switch (graphMode)
                    {
                        case GraphMode.FlightEnvelope:
                            EnvelopeSurfGenerator.Calculate(vessel, body, 0, maxSpeed, speedStep, 0, maxAltitude, altitudeStep);
                            break;
                        case GraphMode.AoACurves:
                            AoACurveGenerator.Calculate(vessel, body, Altitude, Speed, -20f * Mathf.Deg2Rad, 20f * Mathf.Deg2Rad, 0.5f * Mathf.Deg2Rad);
                            break;
                        case GraphMode.VelocityCurves:
                            VelCurveGenerator.Calculate(vessel, body, Altitude, 0, maxSpeed, speedStep);
                            break;
                    }
                    graphRequested = true;
                }
                switch (GraphGenerator.Status)
                {
                    case CalculationManager.RunStatus.PreStart:
                    case CalculationManager.RunStatus.Cancelled:
                    case CalculationManager.RunStatus.Running:
                        DrawProgressBar(GraphGenerator.PercentComplete);
                        break;
                    case CalculationManager.RunStatus.Completed:
                        grapher.SetCollection(GraphGenerator.Graphables);
                        grapher.SetVisibilityExcept(false, graphSelect.ToFormattedString());
                        if (graphMode == GraphMode.FlightEnvelope)
                        {
                            if (WindTunnelSettings.ShowEnvelopeMask && (WindTunnelSettings.ShowEnvelopeMaskAlways || (CurrentGraphSelect != GraphSelect.ExcessThrust && CurrentGraphSelect != GraphSelect.ExcessAcceleration)))
                                grapher["Envelope Mask"].Visible = true;
                            grapher["Fuel-Optimal Path"].Visible = true;
                            grapher["Time-Optimal Path"].Visible = true;
                        }
                        DrawGraph();
                        graphDirty = false;
                        break;
                }

                if (selectedCrossHairVect.x >= 0 && selectedCrossHairVect.y >= 0)
                {
                    selectedCrossHairVect = CrossHairsFromConditions(Altitude, Speed, AoA);
                    SetConditionsFromGraph(selectedCrossHairVect);
                    conditionDetails = GetConditionDetails(CurrentGraphMode, this.Altitude, this.Speed, CurrentGraphMode == GraphMode.AoACurves ? this.AoA : float.NaN, false);
                }
                else
                    conditionDetails = "";
            }
            else
            {
                DrawGraph();
            }
        }

        public float GetGraphValue(int x, int y = -1)
        {
            return grapher.ValueAtPixel(x, y, 0);
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
                    EnvelopeSurf.EnvelopePoint conditionPtFE = new EnvelopeSurf.EnvelopePoint(this.vessel, this.body, altitude, speed, 0);
                    if (setAoA)
                        this.AoA = conditionPtFE.AoA_level;
                    return conditionPtFE.ToString();

                case GraphMode.AoACurves:
                    AoACurve.AoAPoint conditionPtAoA = new AoACurve.AoAPoint(this.vessel, this.body, altitude, speed, aoa);
                    return conditionPtAoA.ToString();

                case GraphMode.VelocityCurves:
                    VelCurve.VelPoint conditionPtVel = new VelCurve.VelPoint(this.vessel, this.body, altitude, speed);
                    if (setAoA)
                        this.AoA = conditionPtVel.AoA_level;
                    return conditionPtVel.ToString();

                default:
                    return "";
            }
        }
        
        private void DrawGraph()
        {
            grapher.DrawGraphs();
            GUILayout.Box("", GUIStyle.none, GUILayout.Width(graphWidth + axisWidth), GUILayout.Height(5));
            GUILayout.BeginHorizontal();
            GUILayout.Box("", GUIStyle.none, GUILayout.Width(40), GUILayout.Height(graphHeight));
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Box(grapher.vAxisTex, GUIStyle.none, GUILayout.Height(graphHeight), GUILayout.Width(axisWidth));

            GUIContent graph = new GUIContent(grapher.graphTex);
            graphRect = GUILayoutUtility.GetRect(graph, HighLogic.Skin.box, GUILayout.Height(graphHeight), GUILayout.Width(graphWidth));
            GUI.Box(graphRect, graph);

            GUILayout.EndHorizontal();

            GUILayout.Box("", GUIStyle.none, GUILayout.Width(graphWidth + axisWidth), GUILayout.Height(5));
            GUILayout.BeginHorizontal();
            GUILayout.Box("", GUIStyle.none, GUILayout.Width(axisWidth + 4), GUILayout.Height(axisWidth));
            GUILayout.Box(grapher.hAxisTex, GUIStyle.none, GUILayout.Width(graphWidth), GUILayout.Height(axisWidth));
            GUILayout.EndHorizontal();

            GUI.Label(new Rect(43, 88 + graphHeight, graphWidth, 20), string.Format("{0} [{1}]", grapher.XName, grapher.XUnit != "" ? grapher.XUnit : "-"), hAxisMarks);
            Matrix4x4 guiMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(270, new Vector2(graphHeight, graphHeight));
            GUI.Label(new Rect(graphHeight - 64, 0, graphHeight, 20), string.Format("{0} [{1}]", grapher.YName, grapher.YUnit != "" ? grapher.YUnit : "-"), hAxisMarks);
            GUI.matrix = guiMatrix;

            GUILayout.Label("", GUILayout.Width(graphWidth + axisWidth), GUILayout.Height(18));

            if (CurrentGraphMode == GraphMode.FlightEnvelope)
            {
                GUILayout.BeginHorizontal(GUILayout.Height(28));
                GUILayout.Box("", GUIStyle.none, GUILayout.Width(axisWidth + 4), GUILayout.Height(axisWidth));
                GUIContent cAxis = new GUIContent(grapher.cAxisTex);
                cAxisRect = GUILayoutUtility.GetRect(cAxis, GUIStyle.none, GUILayout.Width(graphWidth), GUILayout.Height(axisWidth));
                GUI.Box(cAxisRect, cAxis);
                GUILayout.EndHorizontal();
                GUI.Label(new Rect(43, 115 + graphHeight + 8, graphWidth, 20), string.Format("{0} [{1}]", grapher.ZName, grapher.ZUnit != "" ? grapher.ZUnit : "-"), hAxisMarks);

                for (int i = 0; i <= grapher.colorAxis.TickCount; i++)
                {
                    GUI.Label(new Rect(43 + Mathf.RoundToInt(graphWidth / (float)grapher.colorAxis.TickCount * i), 80 + graphHeight + 28 + 8, 40, 15),
                        grapher.colorAxis.labels[i], hAxisMarks);
                }
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

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
            GUILayout.Label(String.Format("Calculating... ({0:N1}%)", percentComplete * 100), labelCentered, GUILayout.Height(graphHeight + axisWidth + 20 + (CurrentGraphMode == GraphMode.FlightEnvelope ? 28 : 0)), GUILayout.Width(graphWidth));
            //Rect rectBar = new Rect(PlotPosition.x, PlotPosition.y + 292 / 2 - 10, 292 + 45, 20);
            //blnReturn = Drawing.DrawBar(styleBack, out rectBar, Width);
            //GUI.Button(rectBar, "");

            //if ((rectBar.width * percentComplete) > 1)
                //DrawBarScaled(rectBar, new GUIStyle(), new GUIStyle(), percentComplete);
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
