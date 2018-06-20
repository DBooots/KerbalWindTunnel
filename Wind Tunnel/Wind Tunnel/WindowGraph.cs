using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KerbalWindTunnel.Graphing;
using KerbalWindTunnel.Extensions;
using UnityEngine;

namespace KerbalWindTunnel
{
    public partial class WindTunnelWindow
    {

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
                            EnvelopeSurf.Calculate(vessel, body, 0, 2000, 10, 0, 25000, 100);
                            graphRequested = true;
                        }
                        switch (EnvelopeSurf.Status)
                        {
                            case CalculationManager.RunStatus.PreStart:
                            case CalculationManager.RunStatus.Cancelled:
                            case CalculationManager.RunStatus.Running:
                                DrawProgressBar(EnvelopeSurf.PercentComplete);
                                break;
                            case CalculationManager.RunStatus.Completed:
                                float bottom = EnvelopeSurf.currentConditions.lowerBoundAltitude;
                                float top = EnvelopeSurf.currentConditions.upperBoundAltitude;
                                float left = EnvelopeSurf.currentConditions.lowerBoundSpeed;
                                float right = EnvelopeSurf.currentConditions.upperBoundSpeed;
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
                                switch (graphSelect)
                                {
                                    case GraphSelect.ExcessThrust:
                                        CreateSurfGraph(left, right, bottom, top, EnvelopeSurf.envelopePoints.SelectToArray(pt => pt.Thrust_excess), true); // Excess Thrust
                                        break;
                                    case GraphSelect.ExcessAcceleration:
                                        CreateSurfGraph(left, right, bottom, top, EnvelopeSurf.envelopePoints.SelectToArray(pt => pt.Accel_excess), true); // Excess Acceleration
                                        break;
                                    case GraphSelect.ThrustAvailable:
                                        CreateSurfGraph(left, right, bottom, top, EnvelopeSurf.envelopePoints.SelectToArray(pt => pt.Thrust_available), true); // Thrust Available
                                        break;
                                    case GraphSelect.LevelFlightAoA:
                                        CreateSurfGraph(left, right, bottom, top, EnvelopeSurf.envelopePoints.SelectToArray(pt => pt.AoA_level * 180 / Mathf.PI)); // Level Flight AoA
                                        break;
                                    case GraphSelect.MaxLiftAoA:
                                        CreateSurfGraph(left, right, bottom, top, EnvelopeSurf.envelopePoints.SelectToArray(pt => pt.AoA_max * 180 / Mathf.PI)); // Max Lift AoA
                                        break;
                                    case GraphSelect.MaxLiftForce:
                                        CreateSurfGraph(left, right, bottom, top, EnvelopeSurf.envelopePoints.SelectToArray(pt => pt.Lift_max)); // Max Lift Force
                                        break;
                                }
                                graphDirty = false;
                                break;
                        }
                        break;
                    case GraphMode.AoACurves:
                        if (!graphRequested)
                        {
                            AoACurve.Calculate(vessel, body, altitude, speed, -20f * Mathf.PI / 180, 20f * Mathf.PI / 180, 0.5f * Mathf.PI / 180);
                            graphRequested = true;
                        }
                        switch (AoACurve.Status)
                        {
                            case CalculationManager.RunStatus.PreStart:
                            case CalculationManager.RunStatus.Cancelled:
                            case CalculationManager.RunStatus.Running:
                                DrawProgressBar(EnvelopeSurf.PercentComplete);
                                break;
                            case CalculationManager.RunStatus.Completed:
                                float left = AoACurve.currentConditions.lowerBound * 180 / Mathf.PI;
                                float right = AoACurve.currentConditions.upperBound * 180 / Mathf.PI;
                                switch (graphSelect)
                                {
                                    case GraphSelect.LiftForce:
                                        CreateLineGraph(left, right, AoACurve.AoAPoints.Select(pt => pt.Lift).ToArray()); // Lift Force
                                        break;
                                    case GraphSelect.DragForce:
                                        CreateLineGraph(left, right, AoACurve.AoAPoints.Select(pt => pt.Drag).ToArray()); // Drag Force
                                        break;
                                    case GraphSelect.LiftDragRatio:
                                        CreateLineGraph(left, right, AoACurve.AoAPoints.Select(pt => pt.LDRatio).ToArray()); // Lift-Drag Ratio
                                        break;
                                }
                                graphDirty = false;
                                break;
                        }
                        break;
                    case GraphMode.VelocityCurves:
                        if (!graphRequested)
                        {
                            VelCurve.Calculate(vessel, body, altitude, 0, 2000, 10);
                            graphRequested = true;
                        }
                        switch (VelCurve.Status)
                        {
                            case CalculationManager.RunStatus.PreStart:
                            case CalculationManager.RunStatus.Cancelled:
                            case CalculationManager.RunStatus.Running:
                                DrawProgressBar(EnvelopeSurf.PercentComplete);
                                break;
                            case CalculationManager.RunStatus.Completed:
                                float left = VelCurve.currentConditions.lowerBound;
                                float right = VelCurve.currentConditions.upperBound;
                                switch (graphSelect)
                                {
                                    case GraphSelect.LevelFlightAoA:
                                        CreateLineGraph(left, right, VelCurve.VelPoints.Select(pt => float.IsNaN(pt.AoA_level) ? float.PositiveInfinity : pt.AoA_level * 180 / Mathf.PI).ToArray()); // Level Flight AoA
                                        break;
                                    case GraphSelect.MaxLiftAoA:
                                        CreateLineGraph(left, right, VelCurve.VelPoints.Select(pt => float.IsNaN(pt.AoA_max) ? float.PositiveInfinity : pt.AoA_max * 180 / Mathf.PI).ToArray()); // Max Lift AoA
                                        break;
                                    case GraphSelect.ThrustAvailable:
                                        CreateLineGraph(left, right, VelCurve.VelPoints.Select(pt => pt.Thrust_available).ToArray()); // Thrust Available
                                        break;
                                }
                                graphDirty = false;
                                break;
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("graphMode");
                }
            }
            else
            {
                DrawGraph();
            }
        }

        private static int ValueToPixel(int size, float value, float maxValue, float minValue = 0)
        {
            if (float.IsNaN(value))
                return 0;
            if (value >= maxValue || float.IsPositiveInfinity(value))
                return size - 1;
            if (value <= minValue || float.IsNegativeInfinity(value))
                return 0;
            float range = maxValue - minValue;
            return Mathf.FloorToInt((value - minValue) / range * size);
        }
        private static float PixelToValue(int x, float[] values)
        {
            return PixelToValue(x, values.GetUpperBound(0) + 1, values);
        }
        private static float PixelToValue(int x, int length, float[] values)
        {
            float fraction = (float)x / (graphWidth - 1) * (length - 1);
            int lIndx = Mathf.FloorToInt(fraction);
            int rIndx = Mathf.CeilToInt(fraction);
            if (lIndx == rIndx)
                return values[lIndx];
            return (values[rIndx] - values[lIndx]) * (fraction % 1) + values[lIndx];    //Mathf.Lerp(values[lIndx], values[rIndx], fraction % 1);
        }
        private static float PixelsToValue(int x, int y, float[,] values)
        {
            int xNum = values.GetUpperBound(0) + 1;
            int yNum = values.GetUpperBound(1) + 1;
            return PixelsToValue(x, y, xNum, yNum, values);
        }
        private static float PixelsToValue(int x, int y, int lengthX, int lengthY, float[,] values)
        {
            float fractionX = (float)x / (graphWidth - 1) * (lengthX - 1);
            int lIndx = Mathf.FloorToInt(fractionX);
            int rIndx = Mathf.CeilToInt(fractionX);

            float fractionY = (float)y / (graphHeight - 1) * (lengthY - 1);
            int lIndy = Mathf.FloorToInt(fractionY);
            int rIndy = Mathf.CeilToInt(fractionY);

            if (lIndx == rIndx && lIndy == rIndy)
                return values[lIndx, lIndy];

            float vX1, vX2;
            if (lIndx == rIndx)
            {
                vX1 = values[lIndx, lIndy];
                vX2 = values[lIndx, rIndy];
            }
            else
            {
                vX1 = (values[rIndx, lIndy] - values[lIndx, lIndy]) * (fractionX % 1) + values[lIndx, lIndy];
                vX2 = (values[rIndx, rIndy] - values[lIndx, rIndy]) * (fractionX % 1) + values[lIndx, rIndy];
            }
            return (vX2 - vX1) * (fractionY % 1) + vX1;
        }

        private float[,] surfValues;
        private void CreateSurfGraph(float xLeft, float xRight, float yBottom, float yTop, float[,] values)
        {
            CreateSurfGraph(xLeft, xRight, yBottom, yTop, values, false);
        }
        private void CreateSurfGraph(float xLeft, float xRight, float yBottom, float yTop, float[,] values, float maxValue, float minValue = float.NaN)
        {
            CreateSurfGraph(xLeft, xRight, yBottom, yTop, values, false, maxValue, minValue);
        }
        private void CreateSurfGraph(float xLeft, float xRight, float yBottom, float yTop, float[,] values, bool blankNegatives = false, float maxValue = float.NaN, float minValue = float.NaN)
        {
            surfValues = values;
            int xNum = values.GetUpperBound(0) + 1;
            int yNum = values.GetUpperBound(1) + 1;
            float topRange;
            if (float.IsNaN(maxValue))
                topRange = values.Max(true);
            else
                topRange = maxValue;

            for(int x = 0; x < graphWidth; x++)
            {
                for(int yg = 0; yg < graphHeight; yg++)
                {
                    int y = graphHeight - 1 - yg;
                    float pixelValue = PixelsToValue(x, y, xNum, yNum, values);
                    if (float.IsNaN(pixelValue) || float.IsInfinity(pixelValue) || (blankNegatives && pixelValue <= 0))
                        graphTex.SetPixel(x, y, Color.black);
                    else
                        graphTex.SetPixel(x, y, ColorMapJetDark(pixelValue / topRange));
                }
            }

/*#if DEBUG
            byte[] PNG = graphTex.EncodeToPNG();
            System.IO.File.WriteAllBytes(string.Format("{0}/DeltaVWorkingPlot.png", Resources.PathPlugin), PNG);
#endif*/
            graphTex.Apply();

            this.graphSettings = new GraphSettings(xLeft, xRight, yBottom, yTop);

            CreateHorzAxis(this.graphSettings);
            CreateVertAxis(this.graphSettings);

            DrawGraph();
        }

        static Color[] blank = null;
        private float[] lineValues;
        private void CreateLineGraph(float xLeft, float xRight, float[] values)
        {
            lineValues = values;
            int xNum = values.Length;
            if (blank == null)
            {
                blank = graphTex.GetPixels();
                for (int i = blank.Length - 1; i >= 0; i--)
                    blank[i] = Color.black;
            }

            graphTex.SetPixels(blank);

            float[] vs = values.Where(v => !float.IsInfinity(v)).ToArray();
            if (vs.Length == 0)
                vs = new float[] { float.NaN };
            float max = vs.Max();
            float min = vs.Min();
            float majUnit = GetMajorUnit(max, min, false);
            float maxH = max % majUnit == 0 ? max : Mathf.CeilToInt(Mathf.Max(max, 0) / majUnit * 1.05f) * majUnit;
            float minH = min % majUnit == 0 ? min : Mathf.FloorToInt(Mathf.Min(min, 0) / majUnit * 1.05f) * majUnit;

            float step = (xRight - xLeft) / (xNum - 1);
            for (int i = 0; i < xNum - 1; i++)
            {
                int x1 = ValueToPixel(graphWidth, step * i + xLeft, xRight, xLeft);
                int x2 = ValueToPixel(graphWidth, step * (i + 1) + xLeft, xRight, xLeft);
                int y1 = ValueToPixel(graphHeight, values[i], maxH, minH);
                int y2 = ValueToPixel(graphHeight, values[i + 1], maxH, minH);
                DrawingHelper.DrawLine(ref graphTex, x1, y1, x2, y2, Color.green);
            }

            graphTex.Apply();

            this.graphSettings = new GraphSettings(xLeft, xRight, min, max);

            CreateHorzAxis(this.graphSettings);
            CreateVertAxis(this.graphSettings);

            DrawGraph();
        }

        public float GetGraphValue(int x, int y = -1)
        {
            if (y == -1)
                return PixelToValue(x, lineValues);
            else
                return PixelsToValue(x, y, surfValues);
        }
        private string GetConditionDetails(float x, float y = float.NaN)
        {
            switch (graphMode)
            {
                case GraphMode.FlightEnvelope:
                    this.altitude = y * (graphSettings.yTop - graphSettings.yBottom) + graphSettings.yBottom;
                    this.altitudeStr = String.Format("{0:N0}", this.altitude);
                    this.speed = x * (graphSettings.xRight - graphSettings.xLeft) + graphSettings.xLeft;
                    this.speedStr = String.Format("{0:N0}", this.speed);
                    EnvelopeSurf.EnvelopePoint conditionPtFE = new EnvelopeSurf.EnvelopePoint(this.vessel, this.body, altitude, speed, this.rootSolver, 0);
                    this.aoa = conditionPtFE.AoA_level;
                    this.aoaStr = String.Format("{0:N2}", this.aoa);

                    return String.Format("Altitude:\t{0:N0}m\n" + "Speed:\t{1:N0}m/s\n" + "Level Flight AoA:\t{2:N2}°\n" +
                        "Excess Thrust:\t{3:N0}kN\n" + "Excess Acceleration:\t{4:N2}g\n" + "Max Lift Force:\t{5:N0}kN\n" +
                        "Max Lift AoA:\t{6:N2}°\n" + "Lift/Drag Ratio:\t{8:N2}\n" + "Available Thrust:\t{7:N0}kN",
                        conditionPtFE.altitude, conditionPtFE.speed, conditionPtFE.AoA_level * 180 / Mathf.PI,
                        conditionPtFE.Thrust_excess, conditionPtFE.Accel_excess, conditionPtFE.Lift_max,
                        conditionPtFE.AoA_max * 180 / Mathf.PI, conditionPtFE.Thrust_available, conditionPtFE.LDRatio);

                case GraphMode.AoACurves:
                    this.aoa = (x * (graphSettings.xRight - graphSettings.xLeft) + graphSettings.xLeft) / 180 * Mathf.PI;
                    this.aoaStr = String.Format("{0}", this.aoa);
                    AoACurve.AoAPoint conditionPtAoA = new AoACurve.AoAPoint(this.vessel, this.body, this.altitude, this.speed, this.aoa);

                    return String.Format("Altitude:\t{0:N0}m\n" + "Speed:\t{1:N0}m/s\n" + "AoA:\t{2:N2}°\n" +
                        "Lift:\t{3:N0}kN\n" + "Drag:\t{4:N0}kN\n" + "Lift/Drag Ratio:\t{5:N2}",
                        conditionPtAoA.altitude, conditionPtAoA.speed, conditionPtAoA.AoA * 180 / Mathf.PI,
                        conditionPtAoA.Lift, conditionPtAoA.Drag, conditionPtAoA.LDRatio);

                case GraphMode.VelocityCurves:
                    this.speed = x * (graphSettings.xRight - graphSettings.xLeft) + graphSettings.xLeft;
                    this.speedStr = String.Format("{0:N0}", this.speed);
                    VelCurve.VelPoint conditionPtVel = new VelCurve.VelPoint(this.vessel, this.body, this.altitude, speed, this.rootSolver);
                    this.aoa = conditionPtVel.AoA_level;
                    this.aoaStr = String.Format("{0:N0}", this.aoa);

                    return String.Format("Altitude:\t{0:N0}m\n" + "Speed:\t{1:N0}m/s\n" + "Level Flight AoA:\t{2:N2}°\n" +
                        "Excess Thrust:\t{3:N0}kN\n" +
                        "Max Lift AoA:\t{4:N2}°\n" + "Lift/Drag Ratio:\t{6:N0}\n" + "Available Thrust:\t{5:N0}kN",
                        conditionPtVel.altitude, conditionPtVel.speed, conditionPtVel.AoA_level * 180 / Mathf.PI,
                        conditionPtVel.Thrust_excess,
                        conditionPtVel.AoA_max * 180 / Mathf.PI, conditionPtVel.Thrust_available, conditionPtVel.LDRatio);

                default:
                    return "";
            }
        }

        private GraphSettings graphSettings;

        private const int graphWidth = 500;
        private const int graphHeight = 400;
        private const int axisWidth = 10;

        internal readonly Vector2 PlotPosition = new Vector2(25, 155);

        Texture2D graphTex = new Texture2D(graphWidth, graphHeight, TextureFormat.ARGB32, false);
        Texture2D axisTexVert = new Texture2D(axisWidth, graphHeight, TextureFormat.ARGB32, false);
        Texture2D axisTexHorz = new Texture2D(graphWidth, axisWidth, TextureFormat.ARGB32, false);
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
            GUILayout.Box(axisTexVert, GUIStyle.none, GUILayout.Height(graphHeight), GUILayout.Width(axisWidth));

            GUIContent graph = new GUIContent(graphTex);
            graphRect = GUILayoutUtility.GetRect(graph, HighLogic.Skin.box, GUILayout.Height(graphHeight), GUILayout.Width(graphWidth));
            GUI.Box(graphRect, graph);
            //GUILayout.Box(graphTex, GUILayout.Height(graphHeight), GUILayout.Width(graphWidth));

            GUILayout.EndHorizontal();
            GUILayout.Box("", GUIStyle.none, GUILayout.Width(graphWidth + axisWidth), GUILayout.Height(5));
            GUILayout.BeginHorizontal();
            GUILayout.Box("", GUIStyle.none, GUILayout.Width(axisWidth + 4), GUILayout.Height(axisWidth));
            GUILayout.Box(axisTexHorz, GUIStyle.none, GUILayout.Width(graphWidth), GUILayout.Height(axisWidth));
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Label("", GUILayout.Width(graphWidth + axisWidth), GUILayout.Height(10));

            for (int i = 0; i <= graphSettings.yMarks; i++)
            {
                GUI.Label(new Rect(5, 58 + graphHeight - ValueToPixel(graphHeight, i, graphSettings.yMarks), 40, 15),
                    String.Format("{0}", graphSettings.yMajUnit * i + graphSettings.yBottom), vAxisMarks);
            }

            for (int i = 0; i <= graphSettings.xMarks; i++)
            {
                GUI.Label(new Rect(43 + ValueToPixel(graphWidth, i, graphSettings.xMarks), 80 + graphHeight, 40, 15),
                    String.Format("{0}", graphSettings.xMajUnit * i + graphSettings.xLeft), hAxisMarks);
            }
        }

        private void DrawProgressBar(float percentComplete)
        {
            //GUI.Label(new Rect(PlotPosition.x, PlotPosition.y + graphHeight / 2 - 30, graphWidth + 45, 20), "Calculating... (" + percentComplete * 100 + "%)");
            GUILayout.Label(String.Format("Calculating... ({0:N1}%)", percentComplete * 100), GUILayout.Height(graphHeight));
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

        private void CreateVertAxis(GraphSettings graphSettings)
        {
            Color[] pixels = axisTexVert.GetPixels();
            int nextLine = 0;
            int index = 0;
            
            for(int yg = 0; yg < graphHeight; yg++)
            {
                Color rowColor;
                if (yg == nextLine)
                {
                    rowColor = Color.white;
                    index++;
                    nextLine = ValueToPixel(graphHeight, index, graphSettings.yMarks, 0);
                }
                else
                    rowColor = new Color(0, 0, 0, 0);
                for (int x = 0; x < axisWidth - 1; x++)
                {
                    int gindex = x + axisWidth * yg;
                    pixels[gindex] = rowColor;
                }
                pixels[axisWidth - 1 + axisWidth * yg] = Color.white;
            }

            axisTexVert.SetPixels(pixels);
            axisTexVert.Apply();
        }
        private void CreateHorzAxis(GraphSettings graphSettings)
        {
            Color[] pixels = axisTexHorz.GetPixels();
            int nextLine = 0;
            int index = 0;

            for (int x = 0; x < graphWidth; x++)
            {
                Color rowColor;
                if (x == nextLine)
                {
                    rowColor = Color.white;
                    index++;
                    nextLine = ValueToPixel(graphWidth, index, graphSettings.xMarks, 0);
                }
                else
                    rowColor = new Color(0, 0, 0, 0);
                for (int yg = 0; yg < axisWidth - 1; yg++)
                {
                    int gindex = x + graphWidth * yg;
                    pixels[gindex] = rowColor;
                }
                pixels[x + graphWidth * (axisWidth - 1)] = Color.white;
            }

            axisTexHorz.SetPixels(pixels);
            axisTexHorz.Apply();
        }

        public static float GetMajorUnit(float range)
        {
            const float c = 18f / 11;
            if (range < 0)
                range = -range;
            float oom = Mathf.Pow(10, Mathf.Floor(Mathf.Log10(range)));
            float normVal = range / oom;
            if (normVal > 5 * c)
                return 2 * oom;
            else if (normVal > 2.5f * c)
                return oom;
            else if (normVal > c)
                return 0.5f * oom;
            else
                return 0.2f * oom;
        }
        public static float GetMajorUnit(float max, float min, bool forX)
        {
            if (Mathf.Sign(max) != Mathf.Sign(min))
                return GetMajorUnit(max);
            float c;
            if (forX)
                c = 12f / 7;
            else
                c = 40f / 21;
            float range = Mathf.Max(max, -min);
            if (range < 0)
                range = -range;
            float oom = Mathf.Pow(10, Mathf.Floor(Mathf.Log10(range)));
            float normVal = range / oom;
            if (normVal > 5 * c)
                return 2 * oom;
            else if (normVal > 2.5f * c)
                return oom;
            else if (normVal > c)
                return 0.5f * oom;
            else
                return 0.2f * oom;
        }

        public static Color ColorMapJet(float value)
        {
            if (float.IsNaN(value))
                return Color.black;

            const float fractional = 1f / 3f;
            const float mins = 128f / 255f;

            if (value < fractional)
            {
                value = (value / fractional * (128 - 255) + 255) / 255;
                return new Color(mins, 1, value, 1);
            }
            if (value < 2 * fractional)
            {
                value = ((value - fractional) / fractional * (255 - 128) + 128) / 255;
                return new Color(value, 1, mins, 1);
            }
            value = ((value - 2 * fractional) / fractional * (128 - 255) + 255) / 255;
            return new Color(1, value, mins, 1);
        }
        public static Color ColorMapJetDark(float value)
        {
            if (float.IsNaN(value))
                return Color.black;

            const float fractional = 0.25f;
            const float mins = 128f / 255f;

            if (value < fractional)
            {
                value = (value / fractional * (255 - 128) + 128) / 255;
                return new Color(mins, value, 1, 1);
            }
            if (value < 2 * fractional)
            {
                value = ((value - fractional) / fractional * (128 - 255) + 255) / 255;
                return new Color(mins, 1, value, 1);
            }
            if (value < 3 * fractional)
            {
                value = ((value - 2 * fractional) / fractional * (255 - 128) + 128) / 255;
                return new Color(value, 1, mins, 1);
            }
            value = ((value - 3 * fractional) / fractional * (128 - 255) + 255) / 255;
            return new Color(1, value, mins, 1);
        }

        private struct GraphSettings
        {
            public readonly float xLeft, xRight, yBottom, yTop;
            public readonly float xMajUnit, yMajUnit;
            public readonly int xMarks, yMarks;

            public GraphSettings(float xLeft, float xRight, float yBottom, float yTop)
            {
                xMajUnit = GetMajorUnit(xRight, xLeft, true);
                yMajUnit = GetMajorUnit(yTop, yBottom, false);
                if (xLeft % xMajUnit == 0)
                    this.xLeft = xLeft;
                else
                    this.xLeft = Mathf.Floor(Mathf.Min(xLeft, 0) / xMajUnit * 1.05f) * xMajUnit;
                if (xRight % xMajUnit == 0)
                    this.xRight = xRight;
                else
                    this.xRight = Mathf.Ceil(Mathf.Max(xRight, 0) / xMajUnit * 1.05f) * xMajUnit;
                if (yBottom % yMajUnit == 0)
                    this.yBottom = yBottom;
                else
                    this.yBottom = Mathf.Floor(Mathf.Min(yBottom, 0) / yMajUnit * 1.05f) * yMajUnit;
                if (yTop % yMajUnit == 0)
                    this.yTop = yTop;
                else
                    this.yTop = Mathf.Ceil(Mathf.Max(yTop, 0) / yMajUnit * 1.05f) * yMajUnit;

                xMarks = Mathf.RoundToInt((this.xRight - this.xLeft) / this.xMajUnit);
                //xMarks = Mathf.CeilToInt(Mathf.Max(xRight, 0) / xMajUnit * 1.05f) - Mathf.FloorToInt(Mathf.Min(xLeft, 0) / xMajUnit * 1.05f);
                yMarks = Mathf.RoundToInt((this.yTop - this.yBottom) / this.yMajUnit);
                //yMarks = Mathf.CeilToInt(Mathf.Max(yTop, 0) / yMajUnit * 1.05f) - Mathf.FloorToInt(Mathf.Min(yBottom, 0) / yMajUnit * 1.05f);
            }
        }
    }
}
