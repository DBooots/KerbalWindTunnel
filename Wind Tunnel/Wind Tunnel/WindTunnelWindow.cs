using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSPPluginFramework;
using KerbalWindTunnel.RootSolvers;

namespace KerbalWindTunnel
{
    [WindowInitials(Caption = "Kerbal Wind Tunnel",
        Visible = false,
        DragEnabled = true,
        TooltipsEnabled = true,
        WindowMoveEventsEnabled = true)]
    public partial class WindTunnelWindow : MonoBehaviourWindowPlus
    {
        public static WindTunnelWindow Instance;

        public WindTunnel Parent { get; internal set; }

        public RootSolverSettings solverSettings = new RootSolverSettings(
            RootSolver.LeftBound(-15 * Mathf.PI / 180),
            RootSolver.RightBound(35 * Mathf.PI / 180),
            RootSolver.LeftGuessBound(-5 * Mathf.PI / 180),
            RootSolver.RightGuessBound(5 * Mathf.PI / 180),
            RootSolver.ShiftWithGuess(true),
            RootSolver.Tolerance(0.0001f));
        public RootSolver rootSolver = new RootSolvers.Brent();
        private AeroPredictor vessel = null;
        private CelestialBody body = Planetarium.fetch.CurrentMainBody;

        private GraphMode graphMode = GraphMode.FlightEnvelope;
        public enum GraphMode
        {
            FlightEnvelope = 0,
            AoACurves = 1,
            VelocityCurves = 2
        }
        public enum GraphSelect
        {
            LevelFlightAoA = 0,
            MaxLiftAoA = 1,
            ThrustAvailable = 2,
            ExcessThrust = 3,
            ExcessAcceleration = 4,
            MaxLiftForce = 5,
            LiftForce = 0,
            DragForce = 1,
            LiftDragRatio = 2
        }
        private GraphSelect graphSelect = GraphSelect.ExcessThrust;
        private GraphSelect[] savedGraphSelect = new GraphSelect[] { GraphSelect.ExcessThrust, GraphSelect.LiftForce, GraphSelect.LevelFlightAoA };
        private readonly string[] graphModes = new string[] { "Flight Envelope", "AoA Curves", "Velocity Curves" };
        private readonly string[][] graphSelections = new string[][] {
            new string[] { "Level Flight AoA", "Max Lift AoA", "Thrust Available", "Excess Thrust", "Excess Acceleration", "Max Lift Force" },
            new string[] { "Lift Force", "Drag Force", "Lift-Drag Ratio" },
            new string[] { "Level Flight AoA", "Max Lift AoA", "Thrust Available" }
        };
        private readonly string[] highliftModeStrings = new string[] { "Off", "Drag", "Lift" };
        private readonly string[][] graphUnits = new string[][] {
            new string[] { "{0:N2}°", "{0:N2}°", "{0:N0}kN", "{0:N0}kN", "{0:N2}g", "{0:N0}kN" },
            new string[] { "{0:N0}kN", "{0:N0}kN", "{0:N2}" },
            new string[] { "{0:N2}°", "{0:N2}°", "{0:N0}kN" } };

        private bool graphDirty = true;
        private bool graphRequested = false;
        private string altitudeStr = "0";
        private string speedStr = "0";
        private string aoaStr = "0";
        private float altitude = 0;
        private float speed = 100; //TODO:
        private float aoa = 0;
        private bool mach = false;

        private List<cbItem> lstPlanets = new List<cbItem>();
        private CelestialBody cbStar;
        private int planetIndex = 0;

        private GUIStyle exitButton = new GUIStyle(HighLogic.Skin.button);

        internal override void Start()
        {
            base.Start();
            hAxisMarks.normal.textColor = hAxisMarks.focused.textColor = hAxisMarks.hover.textColor = hAxisMarks.active.textColor = Color.white;
            vAxisMarks.normal.textColor = vAxisMarks.focused.textColor = vAxisMarks.hover.textColor = vAxisMarks.active.textColor = Color.white;
            exitButton.normal.textColor = exitButton.focused.textColor = exitButton.hover.textColor = exitButton.active.textColor = Color.red;

            Texture2D crossHair = new Texture2D(1, 1);
            crossHair.SetPixel(0, 0, new Color32(255, 25, 255, 192));
            crossHair.Apply();

            stylePlotCrossHair.normal.background = crossHair;
        }

        internal override void DrawWindow(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(graphWidth + 55 + axisWidth));

            GraphMode newGraphMode = (GraphMode)GUILayout.SelectionGrid((int)graphMode, graphModes, 3);
            if (newGraphMode != graphMode)
            {
                switch (graphMode)
                {
                    case GraphMode.FlightEnvelope:
                        Graphing.EnvelopeSurf.Cancel();
                        break;
                    case GraphMode.AoACurves:
                        Graphing.AoACurve.Cancel();
                        break;
                    case GraphMode.VelocityCurves:
                        Graphing.VelCurve.Cancel();
                        break;
                }
                savedGraphSelect[(int)graphMode] = graphSelect;
                graphMode = newGraphMode;
                graphSelect = savedGraphSelect[(int)graphMode];
                speedStr = speed.ToString();
                altitudeStr = altitude.ToString();
                aoaStr = aoa.ToString();
                graphDirty = true;
                graphRequested = false;
            }

            DrawGraph(graphMode, graphSelect);
            /*if (GUILayout.Button("Test!"))
            {
                Debug.Log("Testing!");

                float atmPressure, atmDensity, mach;
                bool oxygenAvailable;
                lock (body)
                {
                    atmPressure = (float)body.GetPressure(altitude);
                    atmDensity = (float)Extensions.KSPClassExtensions.GetDensity(body, altitude);
                    mach = (float)(speed / body.GetSpeedOfSound(atmPressure, atmDensity));
                    oxygenAvailable = body.atmosphereContainsOxygen;
                }

                //Debug.Log("Aero Force (stock): " + stockVessel.GetAeroForce(body, speed, altitude, 2.847f * Mathf.PI / 180, mach));
                //Debug.Log("Lift Force (stock): " + stockVessel.GetLiftForce(body, speed, altitude, 2.847f * Mathf.PI / 180));

                VesselCache.SimulatedVessel testVessel = (VesselCache.SimulatedVessel)vessel;//VesselCache.SimulatedVessel.Borrow(EditorLogic.fetch.ship, VesselCache.SimCurves.Borrow(body));

                float weight = (float)(testVessel.Mass * body.gravParameter / ((body.Radius + altitude) * (body.Radius + altitude))); // TODO: Minus centrifugal force...
                Vector3 thrustForce = testVessel.GetThrustForce(mach, atmDensity, atmPressure, oxygenAvailable);

                Graphing.EnvelopeSurf.EnvelopePoint pt = new Graphing.EnvelopeSurf.EnvelopePoint(VesselCache.SimulatedVessel.Borrow(EditorLogic.fetch.ship, VesselCache.SimCurves.Borrow(body)), body, altitude, speed, this.rootSolver, 0);
                Debug.Log("AoA Level:        " + pt.AoA_level * 180 / Mathf.PI);
                Debug.Log("Thrust Available: " + pt.Thrust_available);
                Debug.Log("Excess Thrust:    " + pt.Thrust_excess);
                Debug.Log("Excess Accel:     " + pt.Accel_excess);
                Debug.Log("Speed:            " + pt.speed);
                Debug.Log("Altitude:         " + pt.altitude);
                Debug.Log("Force:            " + pt.force);
                Debug.Log("LiftForce:        " + pt.liftforce);
                Debug.Log("");
                Debug.Log("Aero Force (sim'd): " + AeroPredictor.ToFlightFrame(testVessel.GetAeroForce(body, speed, altitude, pt.AoA_level, mach), pt.AoA_level));
                Debug.Log("Lift Force (sim'd): " + AeroPredictor.ToFlightFrame(testVessel.GetLiftForce(body, speed, altitude, pt.AoA_level), pt.AoA_level));
                AeroPredictor stockVessel = new StockAero();
                Debug.Log("Aero Force (stock): " + AeroPredictor.ToFlightFrame(stockVessel.GetAeroForce(body, speed, altitude, pt.AoA_level, mach), pt.AoA_level));
                Debug.Log("Lift Force (stock): " + AeroPredictor.ToFlightFrame(stockVessel.GetLiftForce(body, speed, altitude, pt.AoA_level), pt.AoA_level));
                Debug.Log("");
            }//*/

            GraphSelect newgraphSelect = (GraphSelect)GUILayout.SelectionGrid((int)graphSelect, graphSelections[(int)graphMode], 3);
            if (newgraphSelect != graphSelect)
            {
                graphSelect = newgraphSelect;
                graphDirty = true;
                graphRequested = false;
            }

            if (graphMode == GraphMode.AoACurves || graphMode == GraphMode.VelocityCurves)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Altitude: ");
                altitudeStr = GUILayout.TextField(altitudeStr);

                if (graphMode == GraphMode.AoACurves)
                {
                    bool newMach = GUILayout.Toggle(mach,"");//, "Mach");
                    if (newMach != mach)
                    {
                        lock (body)
                        {
                            if (mach)
                                speedStr = (speed / (float)body.GetSpeedOfSound(body.GetPressure(altitude), Extensions.KSPClassExtensions.GetDensity(body, altitude))).ToString();
                            else
                                speedStr = (speed * (float)body.GetSpeedOfSound(body.GetPressure(altitude), Extensions.KSPClassExtensions.GetDensity(body, altitude))).ToString();
                        }
                        mach = newMach;
                    }
                    if (!mach)
                    {
                        GUILayout.Label("Speed (m/s): ");
                        speedStr = GUILayout.TextField(speedStr);
                    }
                    else
                    {
                        GUILayout.Label("Speed (Mach): ");
                        speedStr = GUILayout.TextField(speedStr);
                    }
                }
                if (GUILayout.Button("Apply"))
                {
                    if (float.TryParse(altitudeStr, out altitude) && (graphMode != GraphMode.AoACurves || float.TryParse(speedStr, out speed)))
                    {
                        switch (graphMode)
                        {
                            case GraphMode.FlightEnvelope:
                                Graphing.EnvelopeSurf.Cancel();
                                break;
                            case GraphMode.AoACurves:
                                Graphing.AoACurve.Cancel();
                                break;
                            case GraphMode.VelocityCurves:
                                Graphing.VelCurve.Cancel();
                                break;
                        }

                        if (mach)
                            lock (body)
                                speed *= (float)body.GetSpeedOfSound(body.GetPressure(altitude), Extensions.KSPClassExtensions.GetDensity(body, altitude));

                        graphDirty = true;
                        graphRequested = false;
                        Parent.UpdateHighlighting(Parent.highlightMode, this.body, this.altitude, this.speed, this.aoa);
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUILayout.Width(200));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<"))
                planetIndex -= 1;
            GUILayout.Label(body.bodyName);
            if (GUILayout.Button(">"))
                planetIndex += 1;

            if (planetIndex < 0)
                planetIndex += lstPlanets.Count;
            if (planetIndex >= lstPlanets.Count)
                planetIndex -= lstPlanets.Count;

            CelestialBody newBody = lstPlanets[planetIndex].CB;

            if (newBody != body)
            {
                body = newBody;
                graphDirty = true;
                graphRequested = false;
                Graphing.EnvelopeSurf.Clear();
                Graphing.AoACurve.Clear();
                Graphing.VelCurve.Clear();
                this.conditionDetails = "";

                if (vessel is VesselCache.SimulatedVessel releasable)
                    releasable.Release();
                this.vessel = VesselCache.SimulatedVessel.Borrow(EditorLogic.fetch.ship, VesselCache.SimCurves.Borrow(body));
                //this.vessel = new StockAero();
                Parent.UpdateHighlighting(Parent.highlightMode, this.body, this.altitude, this.speed, this.aoa);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(2);

            if (GUILayout.Button("Update Vessel", GUILayout.Height(25)))
            {
                graphDirty = true;
                graphRequested = false;
                Graphing.EnvelopeSurf.Clear();
                Graphing.AoACurve.Clear();
                Graphing.VelCurve.Clear();
                this.conditionDetails = "";

                if (vessel is VesselCache.SimulatedVessel releasable)
                    releasable.Release();
                this.vessel = VesselCache.SimulatedVessel.Borrow(EditorLogic.fetch.ship, VesselCache.SimCurves.Borrow(body));
                //this.vessel = new StockAero();
            }

            // Display selected point details.
            GUILayout.Label(this.conditionDetails);
            

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Part Highlighting: ");
            WindTunnel.HighlightMode newhighlightMode = (WindTunnel.HighlightMode)GUILayout.SelectionGrid((int)WindTunnel.Instance.highlightMode, highliftModeStrings, 3);

            if(newhighlightMode != WindTunnel.Instance.highlightMode)
            {
                Parent.UpdateHighlighting(newhighlightMode, this.body, this.altitude, this.speed, this.aoa);
            }

            GUILayout.EndHorizontal();
            if (newhighlightMode != WindTunnel.HighlightMode.Off)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("AoA (deg): ");
                aoaStr = GUILayout.TextField(aoaStr);
                if (GUILayout.Button("Apply"))
                {
                    if (float.TryParse(aoaStr, out float tempAoA))
                    {
                        tempAoA *= Mathf.PI / 180;
                        if (tempAoA != aoa)
                        {
                            aoa = tempAoA;
                            Parent.UpdateHighlighting(Parent.highlightMode, this.body, this.altitude, this.speed, this.aoa);
                        }
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            if (GUI.Button(new Rect(this.WindowRect.width - 18, 2, 16, 16), "X", exitButton))
                WindTunnel.Instance.CloseWindow();

            Vector2 vectMouse = Event.current.mousePosition;
            if (graphRect.Contains(vectMouse) && Status == CalculationManager.RunStatus.Completed)
            {
                GUI.Box(new Rect(vectMouse.x, graphRect.y, 1, graphRect.height), "", stylePlotCrossHair);
                if (graphMode == GraphMode.FlightEnvelope)
                    GUI.Box(new Rect(graphRect.x, vectMouse.y, graphRect.width, 1), "", stylePlotCrossHair);

                float showValue = GetGraphValue((int)(vectMouse.x - graphRect.x), graphMode == GraphMode.FlightEnvelope ? (int)(graphHeight - (vectMouse.y - graphRect.y)) : -1);
                GUI.Label(new Rect(vectMouse.x + 5, vectMouse.y - 20, 80, 15), String.Format(graphUnits[(int)graphMode][(int)graphSelect], showValue), SkinsLibrary.CurrentTooltip);

                if(Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    conditionDetails = GetConditionDetails((vectMouse.x - graphRect.x) / graphWidth, graphMode == GraphMode.FlightEnvelope ? (graphHeight - (vectMouse.y - graphRect.y)) / graphHeight : float.NaN);
                }
            }
        }

        private string conditionDetails = "";
        private GUIStyle stylePlotCrossHair = new GUIStyle();

        public CalculationManager.RunStatus Status
        {
            get
            {
                switch (graphMode)
                {
                    case GraphMode.AoACurves:
                        return Graphing.AoACurve.Status;
                    case GraphMode.VelocityCurves:
                        return Graphing.VelCurve.Status;
                    case GraphMode.FlightEnvelope:
                        return Graphing.EnvelopeSurf.Status;
                }
                return CalculationManager.RunStatus.PreStart;
            }
        }

        internal override void Awake()
        {
            base.Awake();

            if (Instance)
                Destroy(Instance);
            Instance = this;

            // Fetch Celestial Bodies per TransferWindowPlanner method:
            foreach (CelestialBody body in FlightGlobals.Bodies)
                lstPlanets.Add(new cbItem(body));

            cbStar = FlightGlobals.Bodies.FirstOrDefault(x => x.referenceBody == x.referenceBody);
            BodyParseChildren(cbStar);
            // Filter to only include planets with Atmospheres
            lstPlanets = lstPlanets.FindAll(x => x.CB.atmosphere == true);
            planetIndex = lstPlanets.FindIndex(x => x.CB == FlightGlobals.GetHomeBody());
            body = lstPlanets[planetIndex].CB;
        }

        internal override void OnDestroy()
        {
            base.OnDestroy();
            Destroy(graphTex);
        }

        private void BodyParseChildren(CelestialBody cbRoot, int Depth = 0)
        {
            foreach (cbItem item in lstPlanets.Where(x => x.Parent == cbRoot).OrderBy(x => x.SemiMajorRadius))
            {
                item.Depth = Depth;
                if (item.CB != cbStar)
                {
                    lstPlanets.Add(item);
                    if (lstPlanets.Where(x => x.Parent == item.CB).Count() > 1)
                    {
                        BodyParseChildren(item.CB, Depth + 1);
                    }
                }
            }
        }

#pragma warning disable IDE1006 // Naming Styles
        internal class cbItem
#pragma warning restore IDE1006 // Naming Styles
        {
            internal cbItem(CelestialBody CB)
            {
                this.CB = CB;
                if (CB.referenceBody != CB)
                    this.SemiMajorRadius = CB.orbit.semiMajorAxis;
            }

            internal CelestialBody CB { get; private set; }
            internal int Depth = 0;
            internal string Name { get { return CB.bodyName; } }
            internal string NameFormatted { get { return new string(' ', Depth * 4) + Name; } }
            internal double SemiMajorRadius { get; private set; }
            internal CelestialBody Parent { get { return CB.referenceBody; } }
        }
    }
}
