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
        private DataGenerators.EnvelopeSurf EnvelopeSurfGenerator = new DataGenerators.EnvelopeSurf();
        private DataGenerators.AoACurve AoACurveGenerator = new DataGenerators.AoACurve();
        private DataGenerators.VelCurve VelCurveGenerator = new DataGenerators.VelCurve();
        public DataGenerators.DataSetGenerator GraphGenerator
        {
            get
            {
                switch (CurrentGraphMode)
                {
                    case GraphMode.FlightEnvelope: return this.EnvelopeSurfGenerator;
                    case GraphMode.AoACurves: return this.AoACurveGenerator;
                    case GraphMode.VelocityCurves: return this.VelCurveGenerator;
                    default: return null;
                }
            }
        }

        public WindTunnel Parent { get; internal set; }

        public static readonly float gAccel = (float)(Planetarium.fetch.Home.gravParameter / (Planetarium.fetch.Home.Radius * Planetarium.fetch.Home.Radius));

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

        private GraphMode _graphMode = GraphMode.FlightEnvelope;
        public GraphMode CurrentGraphMode
        {
            get { return _graphMode; }
            set
            {
                if (value != CurrentGraphMode)
                {
                    // Cancel any running computations.
                    Cancel();

                    // Save certain settings:
                    savedGraphSelect[(int)CurrentGraphMode] = CurrentGraphSelect;

                    // Actually change mode
                    _graphMode = value;

                    // Load new settings:
                    _graphSelect = savedGraphSelect[(int)CurrentGraphMode];
                    GetConditionDetails(CurrentGraphMode, this.Altitude, this.Speed, this.AoA, false);

                    // Reset the strings:
                    Speed = Speed;
                    Altitude = Altitude;
                    AoA = AoA;

                    // Request a new graph;
                    graphDirty = true;
                    graphRequested = false;
                }
            }
        }
        public enum GraphMode
        {
            FlightEnvelope = 0,
            AoACurves = 1,
            VelocityCurves = 2
        }

        public readonly GraphSelect[][] selectFromIndex = new GraphSelect[][]{
            new GraphSelect[]{ GraphSelect.ExcessThrust, GraphSelect.LevelFlightAoA, GraphSelect.LiftDragRatio, GraphSelect.ThrustAvailable, GraphSelect.LiftSlope, GraphSelect.ExcessAcceleration, GraphSelect.FuelBurn, GraphSelect.MaxLiftAoA, GraphSelect.MaxLiftForce },
            new GraphSelect[]{ GraphSelect.LiftForce, GraphSelect.DragForce, GraphSelect.LiftDragRatio, GraphSelect.LiftSlope, GraphSelect.PitchInput },
            new GraphSelect[]{ GraphSelect.LevelFlightAoA, GraphSelect.LiftDragRatio, GraphSelect.ThrustAvailable, GraphSelect.DragForce, GraphSelect.LiftSlope, GraphSelect.MaxLiftAoA, GraphSelect.MaxLiftForce } };

        public readonly int[][] indexFromSelect = new int[][]
        {
            new int[]{ 0, 1, 2, 3, 4, 5, 6, 7, 8, 0, 0, 0 },
            new int[]{ 0, 0, 2, 0, 3, 0, 0, 0, 0, 0, 1, 4 },
            new int[]{ 0, 0, 1, 2, 4, 0, 0, 5, 6, 0, 3, 0 } };

        public enum GraphSelect
        {
            ExcessThrust = 0,
            LevelFlightAoA = 1,
            LiftDragRatio = 2,
            ThrustAvailable = 3,
            LiftSlope = 4,
            ExcessAcceleration = 5,
            FuelBurn = 6,
            MaxLiftAoA = 7,
            MaxLiftForce = 8,

            LiftForce = 9,
            DragForce = 10,
            PitchInput = 11
            //LiftDragRatio = 2,
            //LiftSlope = 4

            //LevelFlightAoA = 1,
            //LiftDragRatio = 2,
            //ThrustAvailable = 3,
            //DragForce = 10,
            //LiftSlope = 4,
            //MaxLiftAoA = 7,
            //MaxLiftForce = 8,
        }
        private GraphSelect _graphSelect = GraphSelect.ExcessThrust;
        public GraphSelect CurrentGraphSelect
        {
            get { return _graphSelect; }
            set
            {
                if (value != _graphSelect)
                {
                    _graphSelect = value;
                    graphDirty = true;
                    graphRequested = false;
                }
            }
        }

        private GraphSelect[] savedGraphSelect = new GraphSelect[] { GraphSelect.ExcessThrust, GraphSelect.LiftForce, GraphSelect.LevelFlightAoA };
        private readonly string[] graphModes = new string[] { "Flight Envelope", "AoA Curves", "Velocity Curves" };
        private readonly string[][] graphSelections = new string[][] {
            new string[] { "Excess Thrust", "Level Flight AoA", "Lift/Drag Ratio", "Thrust Available", "Lift Slope", "Excess Acceleration" },
            new string[] { "Lift Force", "Drag Force", "Lift/Drag Ratio", "Lift Slope", "Pitch Input" },
            new string[] { "Level Flight AoA", "Lift/Drag Ratio", "Thrust Available", "Drag Force" }
        };
        private readonly string[] highliftModeStrings = new string[] { "Off", "Drag", "Lift" };
        private readonly string[] graphUnits = new string[] { "{0:N0}kN", "{0:N2}°", "{0:N2}", "{0:N0}kN", "{0:N2}m^2/°", "{0:N2}g", "{0:N0}kg/s", "{0:N2}°", "{0:N0}kN", "{0:N0}kN", "{0:N0}kN", "{0:F3}" };

        private bool graphDirty = true;
        private bool graphRequested = false;
        private string altitudeStr = "0";
        private string speedStr = "0";
        private string aoaStr = "0";
        private float _altitude = 0;
        public float Altitude
        {
            get { return _altitude; }
            private set
            {
                _altitude = value;
                altitudeStr = value.ToString("F0");
            }
        }
        private float _speed = 100; //TODO:
        public float Speed
        {
            get { return _speed; }
            private set
            {
                _speed = value;
                if (Mach)
                    speedStr = (value / (float)body.GetSpeedOfSound(body.GetPressure(Altitude), Extensions.KSPClassExtensions.GetDensity(body, Altitude))).ToString("F3");
                else
                    speedStr = value.ToString("F2");
            }
        }
        private float _aoa = 0;
        public float AoA
        {
            get { return _aoa; }
            private set
            {
                _aoa = value;
                aoaStr = (value * 180 / Mathf.PI).ToString("F3");
            }
        }
        private bool _mach = false;
        public bool Mach
        {
            get { return _mach; }
            private set
            {
                if (value != _mach)
                {
                    float speedOfSound;

                    lock (body)
                        speedOfSound = (float)body.GetSpeedOfSound(body.GetPressure(Altitude), Extensions.KSPClassExtensions.GetDensity(body, Altitude));

                    if (value)
                        speedStr = (Speed / speedOfSound).ToString("F3");
                    else
                        speedStr = Speed.ToString("F2");

                    _mach = value;
                }
            }
        }

        private Graphing.Graph grapher = new Graphing.Graph(graphWidth, graphHeight, axisWidth);

        internal const float AoAdelta = 0.1f / 180 * Mathf.PI;

        private List<cbItem> lstPlanets = new List<cbItem>();
        private CelestialBody cbStar;
        private int planetIndex = 0;

        private GUIStyle exitButton = new GUIStyle(HighLogic.Skin.button);
        private GUIStyle clearBox = new GUIStyle(HighLogic.Skin.box);
        private GUIStyle labelCentered = new GUIStyle(HighLogic.Skin.label) { alignment = TextAnchor.MiddleCenter };

        Texture2D crossHair = new Texture2D(1, 1);
        Texture2D selectedCrossHair = new Texture2D(1, 1);
        Texture2D clearTex = new Texture2D(1, 1);

        Vector2 selectedCrossHairVect = new Vector2(-1, -1);

        internal override void Start()
        {
            base.Start();
            hAxisMarks.normal.textColor = hAxisMarks.focused.textColor = hAxisMarks.hover.textColor = hAxisMarks.active.textColor = Color.white;
            vAxisMarks.normal.textColor = vAxisMarks.focused.textColor = vAxisMarks.hover.textColor = vAxisMarks.active.textColor = Color.white;
            exitButton.normal.textColor = exitButton.focused.textColor = exitButton.hover.textColor = exitButton.active.textColor = Color.red;
            
            crossHair.SetPixel(0, 0, new Color32(255, 25, 255, 192));
            crossHair.Apply();
            stylePlotCrossHair.normal.background = crossHair;

            selectedCrossHair.SetPixel(0, 0, new Color32(255, 255, 255, 192));
            selectedCrossHair.Apply();
            styleSelectedCrossHair.normal.background = selectedCrossHair;

            clearTex.SetPixel(0, 0, Color.clear);
            clearTex.Apply();
            clearBox.normal.background = clearTex;
        }

        internal override void DrawWindow(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(graphWidth + 55 + axisWidth));

            CurrentGraphMode = (GraphMode)GUILayout.SelectionGrid((int)CurrentGraphMode, graphModes, 3);

            DrawGraph(CurrentGraphMode, CurrentGraphSelect);
            /*if (GUILayout.Button("Test!"))
            {
                Debug.Log("Testing!");

                float atmPressure, atmDensity, mach;
                bool oxygenAvailable;
                lock (body)
                {
                    atmPressure = (float)body.GetPressure(Altitude);
                    atmDensity = (float)Extensions.KSPClassExtensions.GetDensity(body, Altitude);
                    mach = (float)(Speed / body.GetSpeedOfSound(atmPressure, atmDensity));
                    oxygenAvailable = body.atmosphereContainsOxygen;
                }

                //Debug.Log("Aero Force (stock): " + stockVessel.GetAeroForce(body, speed, altitude, 2.847f * Mathf.PI / 180, mach));
                //Debug.Log("Lift Force (stock): " + stockVessel.GetLiftForce(body, speed, altitude, 2.847f * Mathf.PI / 180));

                VesselCache.SimulatedVessel testVessel = VesselCache.SimulatedVessel.Borrow(EditorLogic.fetch.ship, VesselCache.SimCurves.Borrow(body));

                float weight = (float)(testVessel.Mass * body.gravParameter / ((body.Radius + Altitude) * (body.Radius + Altitude))); // TODO: Minus centrifugal force...
                Vector3 thrustForce = testVessel.GetThrustForce(mach, atmDensity, atmPressure, oxygenAvailable);

                DataGenerators.EnvelopeSurf.EnvelopePoint pt = new DataGenerators.EnvelopeSurf.EnvelopePoint(VesselCache.SimulatedVessel.Borrow(EditorLogic.fetch.ship, VesselCache.SimCurves.Borrow(body)), body, Altitude, Speed, this.rootSolver, 0);
                Debug.Log("AoA Level:        " + pt.AoA_level * 180 / Mathf.PI);
                Debug.Log("Thrust Available: " + pt.Thrust_available);
                Debug.Log("Excess Thrust:    " + pt.Thrust_excess);
                Debug.Log("Excess Accel:     " + pt.Accel_excess);
                Debug.Log("Speed:            " + pt.speed);
                Debug.Log("Altitude:         " + pt.altitude);
                Debug.Log("Force:            " + pt.force);
                Debug.Log("LiftForce:        " + pt.liftforce);
                Debug.Log("");
                AeroPredictor.Conditions conditions = new AeroPredictor.Conditions(body, Speed, Altitude);
                Debug.Log("Aero Force (sim'd): " + AeroPredictor.ToFlightFrame(testVessel.GetAeroForce(conditions, pt.AoA_level, 0, out Vector3 torque), pt.AoA_level));
                Debug.Log("Lift Force (sim'd): " + AeroPredictor.ToFlightFrame(testVessel.GetLiftForce(conditions, pt.AoA_level, 0, out Vector3 lTorque), pt.AoA_level));
                Debug.Log("Aero torque: " + torque);
                Debug.Log("Lift torque: " + lTorque);
                Debug.Log("Aero torque1:  " + testVessel.GetAeroTorque(conditions, pt.AoA_level, 1));
                Debug.Log("Aero torque-1: " + testVessel.GetAeroTorque(conditions, pt.AoA_level, -1));
                Debug.Log("");
            }//*/

            CurrentGraphSelect = selectFromIndex[(int)CurrentGraphMode][GUILayout.SelectionGrid(indexFromSelect[(int)CurrentGraphMode][(int)CurrentGraphSelect], graphSelections[(int)CurrentGraphMode], 3)];

            if (CurrentGraphMode == GraphMode.AoACurves || CurrentGraphMode == GraphMode.VelocityCurves)
            {
                GUILayout.BeginHorizontal(GUILayout.Height(25), GUILayout.ExpandHeight(false));
                GUILayout.Label("Altitude: ", GUILayout.Width(62));
                altitudeStr = GUILayout.TextField(altitudeStr, GUILayout.Width(105), GUILayout.Height(22));

                if (CurrentGraphMode == GraphMode.AoACurves)
                {
                    Rect toggleRect = GUILayoutUtility.GetRect(new GUIContent(""), HighLogic.Skin.label, GUILayout.Width(20));
                    Mach = GUI.Toggle(toggleRect, Mach, "    ", new GUIStyle(HighLogic.Skin.toggle) { padding = new RectOffset(-6, -6, -6, -6) });
                    //Mach = GUILayout.Toggle(Mach, "", GUILayout.Width(30), GUILayout.Height(20));
                    if (!Mach)
                    {
                        GUILayout.Label("Speed (m/s): ", GUILayout.Width(101));
                        speedStr = GUILayout.TextField(speedStr, GUILayout.Width(132), GUILayout.Height(22));
                    }
                    else
                    {
                        GUILayout.Label("Speed (Mach): ", GUILayout.Width(101));
                        speedStr = GUILayout.TextField(speedStr, GUILayout.Width(132), GUILayout.Height(22));
                    }
                }
                else
                {
                    GUILayout.Label("", GUILayout.Width(20));
                    GUILayout.Label("", GUILayout.Width(101));
                    GUILayout.Label("", GUILayout.Width(132));
                }

                if (GUILayout.Button("Apply"))
                {
                    if (float.TryParse(altitudeStr, out float altitude) && (CurrentGraphMode != GraphMode.AoACurves | float.TryParse(speedStr, out float speed)))
                    {
                        if(this.Altitude != altitude || this.Speed != speed)
                        {
                            this.Altitude = altitude;
                            if (CurrentGraphMode == GraphMode.AoACurves)
                            {

                                if (Mach)
                                    lock (body)
                                        _speed *= (float)body.GetSpeedOfSound(body.GetPressure(Altitude), Extensions.KSPClassExtensions.GetDensity(body, Altitude));
                                this.Speed = speed;
                            }

                            Cancel();

                            graphDirty = true;
                            graphRequested = false;
                            Parent.UpdateHighlighting(Parent.highlightMode, this.body, this.Altitude, this.Speed, this.AoA);
                        }
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
                EnvelopeSurfGenerator.Clear();
                AoACurveGenerator.Clear();
                VelCurveGenerator.Clear();
                this.conditionDetails = "";

                if (vessel is VesselCache.SimulatedVessel releasable)
                    releasable.Release();
                this.vessel = VesselCache.SimulatedVessel.Borrow(EditorLogic.fetch.ship, VesselCache.SimCurves.Borrow(body));
                //this.vessel = new StockAero();
                Parent.UpdateHighlighting(Parent.highlightMode, this.body, this.Altitude, this.Speed, this.AoA);
                selectedCrossHairVect = new Vector2(-1, -1);
                maskConditions = DataGenerators.EnvelopeSurf.Conditions.Blank;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(2);

            if (GUILayout.Button("Update Vessel", GUILayout.Height(25)))
            {
                graphDirty = true;
                graphRequested = false;
                EnvelopeSurfGenerator.Clear();
                AoACurveGenerator.Clear();
                VelCurveGenerator.Clear();
                this.conditionDetails = "";

                if (vessel is VesselCache.SimulatedVessel releasable)
                    releasable.Release();
                this.vessel = VesselCache.SimulatedVessel.Borrow(EditorLogic.fetch.ship, VesselCache.SimCurves.Borrow(body));
                //this.vessel = new StockAero();

                selectedCrossHairVect = new Vector2(-1, -1);
                maskConditions = DataGenerators.EnvelopeSurf.Conditions.Blank;
            }

            // Display selected point details.
            GUILayout.Label(this.conditionDetails);
            if (CurrentGraphMode == GraphMode.AoACurves && AoACurveGenerator.Status == CalculationManager.RunStatus.Completed)
            {
                DataGenerators.AoACurve.AoAPoint zeroPoint = new DataGenerators.AoACurve.AoAPoint(vessel, body, Altitude, Speed, 0);
                GUILayout.Label(String.Format("CL_Alpha_0:\t{0:F3}m^2/°", zeroPoint.dLift / zeroPoint.dynamicPressure));
            }
            
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Part Highlighting: ");
            WindTunnel.HighlightMode newhighlightMode = (WindTunnel.HighlightMode)GUILayout.SelectionGrid((int)WindTunnel.Instance.highlightMode, highliftModeStrings, 3);

            if(newhighlightMode != WindTunnel.Instance.highlightMode)
            {
                Parent.UpdateHighlighting(newhighlightMode, this.body, this.Altitude, this.Speed, this.AoA);
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
                        if (tempAoA != AoA)
                        {
                            AoA = tempAoA;
                            Parent.UpdateHighlighting(Parent.highlightMode, this.body, this.Altitude, this.Speed, this.AoA);
                        }
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            if (GUI.Button(new Rect(this.WindowRect.width - 18, 2, 16, 16), "X", exitButton))
                WindTunnel.Instance.CloseWindow();
            
            Vector2 vectMouse = Event.current.mousePosition;

            if (selectedCrossHairVect.x >= 0 && selectedCrossHairVect.y >= 0)
            {
                if(graphRect.x + selectedCrossHairVect.x != vectMouse.x)
                GUI.Box(new Rect(graphRect.x + selectedCrossHairVect.x, graphRect.y, 1, graphRect.height), "", styleSelectedCrossHair);
                if (CurrentGraphMode == GraphMode.FlightEnvelope)
                    if (graphRect.y + selectedCrossHairVect.y != vectMouse.y)
                        GUI.Box(new Rect(graphRect.x, graphRect.y + selectedCrossHairVect.y, graphRect.width, 1), "", styleSelectedCrossHair);
            }

            if (graphRect.Contains(vectMouse) && Status == CalculationManager.RunStatus.Completed)
            {
                GUI.Box(new Rect(vectMouse.x, graphRect.y, 1, graphRect.height), "", stylePlotCrossHair);
                if (CurrentGraphMode == GraphMode.FlightEnvelope)
                    GUI.Box(new Rect(graphRect.x, vectMouse.y, graphRect.width, 1), "", stylePlotCrossHair);

                float showValue = GetGraphValue((int)(vectMouse.x - graphRect.x), CurrentGraphMode == GraphMode.FlightEnvelope ? (int)(graphHeight - (vectMouse.y - graphRect.y)) : -1);
                GUI.Label(new Rect(vectMouse.x + 5, vectMouse.y - 20, 80, 15), String.Format(graphUnits[(int)CurrentGraphSelect], showValue), SkinsLibrary.CurrentTooltip);

                if(Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    //conditionDetails = GetConditionDetails((vectMouse.x - graphRect.x) / graphWidth, CurrentGraphMode == GraphMode.FlightEnvelope ? (graphHeight - (vectMouse.y - graphRect.y)) / graphHeight : float.NaN);
                    selectedCrossHairVect = vectMouse - graphRect.position;
                    SetConditionsFromGraph(selectedCrossHairVect);
                    conditionDetails = GetConditionDetails(CurrentGraphMode, this.Altitude, this.Speed, this.AoA, true);
                }
            }
            if(CurrentGraphMode == GraphMode.FlightEnvelope && cAxisRect.Contains(vectMouse) && Status == CalculationManager.RunStatus.Completed)
            {
                GUI.Box(new Rect(vectMouse.x, cAxisRect.y, 1, cAxisRect.height), "", stylePlotCrossHair);
                float showValue = (vectMouse.x - cAxisRect.x) / (cAxisRect.width - 1) * (grapher.colorAxis.Max - grapher.colorAxis.Min) + grapher.colorAxis.Min;
                GUI.Label(new Rect(vectMouse.x + 5, vectMouse.y - 20, 80, 15), String.Format("{0}", showValue), SkinsLibrary.CurrentTooltip);
            }
        }

        private void SetConditionsFromGraph(Vector2 crossHairs)
        {
            switch (CurrentGraphMode)
            {
                case GraphMode.FlightEnvelope:
                    this.Altitude = ((graphHeight - crossHairs.y) / graphHeight) * (grapher.YMax - grapher.YMin) + grapher.YMin;
                    this.Speed = (crossHairs.x / graphWidth) * (grapher.XMax - grapher.XMin) + grapher.XMin;
                    break;
                case GraphMode.AoACurves:
                    this.AoA = ((crossHairs.x / graphWidth) * (grapher.XMax - grapher.XMin) + grapher.XMin) * Mathf.PI / 180;
                    break;
                case GraphMode.VelocityCurves:
                    this.Speed = (crossHairs.x / graphWidth) * (grapher.XMax - grapher.XMin) + grapher.XMin;
                    break;
            }
        }

        private Vector2 CrossHairsFromConditions(float altitude, float speed, float aoa)
        {
            switch (CurrentGraphMode)
            {
                case GraphMode.FlightEnvelope:
                    return new Vector2((speed - grapher.XMin) / (grapher.XMax - grapher.XMin) * graphWidth,
                        altitude / (grapher.YMax - grapher.YMin) * graphHeight);
                case GraphMode.AoACurves:
                    return new Vector2(((aoa * 180 / Mathf.PI) - grapher.XMin) / (grapher.XMax - grapher.XMin) * graphWidth, 0);
                case GraphMode.VelocityCurves:
                    return new Vector2((speed - grapher.XMin) / (grapher.XMax - grapher.XMin) * graphWidth, 0);
                default:
                    return new Vector2(-1, -1);
            }
        }

        public void Cancel()
        {
            GraphGenerator.Cancel();
        }

        private string conditionDetails = "";
        private GUIStyle styleSelectedCrossHair = new GUIStyle();
        private GUIStyle stylePlotCrossHair = new GUIStyle();

        public CalculationManager.RunStatus Status
        {
            get { return GraphGenerator.Status; }
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
            grapher.Dispose();
            Destroy(maskTex);
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
