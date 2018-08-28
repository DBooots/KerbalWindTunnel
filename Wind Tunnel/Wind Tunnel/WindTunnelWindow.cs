using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSPPluginFramework;
using KerbalWindTunnel.Threading;

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

        DropDownList ddlPlanet;

        bool inputLocked = false;
        const string lockID = "KWTLock";

        public static readonly float gAccel = (float)(Planetarium.fetch.Home.gravParameter / (Planetarium.fetch.Home.Radius * Planetarium.fetch.Home.Radius));

        /*public RootSolverSettings solverSettings = new RootSolverSettings(
            RootSolver.LeftBound(-15 * Mathf.PI / 180),
            RootSolver.RightBound(35 * Mathf.PI / 180),
            RootSolver.LeftGuessBound(-5 * Mathf.PI / 180),
            RootSolver.RightGuessBound(5 * Mathf.PI / 180),
            RootSolver.ShiftWithGuess(true),
            RootSolver.Tolerance(0.0001f));*/
        
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
            new GraphSelect[]{ GraphSelect.LiftForce, GraphSelect.DragForce, GraphSelect.LiftDragRatio, GraphSelect.LiftSlope, GraphSelect.PitchInput, GraphSelect.Torque },
            new GraphSelect[]{ GraphSelect.LevelFlightAoA, GraphSelect.LiftDragRatio, GraphSelect.ThrustAvailable, GraphSelect.DragForce, GraphSelect.LiftSlope, GraphSelect.MaxLiftAoA, GraphSelect.MaxLiftForce } };

        public readonly int[][] indexFromSelect = new int[][]
        {
            new int[]{ 0, 1, 2, 3, 4, 5, 6, 7, 8, 0, 0, 0, 0 },
            new int[]{ 0, 0, 2, 0, 3, 0, 0, 0, 0, 0, 1, 4, 5 },
            new int[]{ 0, 0, 1, 2, 4, 0, 0, 5, 6, 0, 3, 0, 0 } };

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
            PitchInput = 11,
            Torque = 12
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
            new string[] { "Lift Force", "Drag Force", "Lift/Drag Ratio", "Lift Slope", "Pitch Input", "Pitching Torque" },
            new string[] { "Level Flight AoA", "Lift/Drag Ratio", "Thrust Available", "Drag Force" }
        };
        private readonly string[] highliftModeStrings = new string[] { "Off", "Drag", "Lift" };

        private bool graphDirty = true;
        private bool graphRequested = false;
        public bool Minimized { get; set; } = false;

        private string altitudeStr = "0";
        private string speedStr = "100.0";
        private string aoaStr = "0.00";
        private float _altitude = 0;
        public float Altitude
        {
            get { return _altitude; }
            set
            {
                _altitude = value;
                altitudeStr = value.ToString("F0");
            }
        }
        private float _speed = 100; //TODO:
        public float Speed
        {
            get { return _speed; }
            set
            {
                _speed = value;
                if (Mach)
                    speedStr = (value / (float)body.GetSpeedOfSound(body.GetPressure(Altitude), Extensions.KSPClassExtensions.GetDensity(body, Altitude))).ToString("F3");
                else
                    speedStr = value.ToString("F1");
            }
        }
        private float _aoa = 0;
        public float AoA
        {
            get { return _aoa; }
            set
            {
                _aoa = value;
                aoaStr = (value * Mathf.Rad2Deg).ToString("F2");
            }
        }
        private bool _mach = false;
        public bool Mach
        {
            get { return _mach; }
            set
            {
                if (value != _mach)
                {
                    float speedOfSound;

                    lock (body)
                        speedOfSound = (float)body.GetSpeedOfSound(body.GetPressure(Altitude), Extensions.KSPClassExtensions.GetDensity(body, Altitude));

                    if (value)
                        speedStr = (Speed / speedOfSound).ToString("F3");
                    else
                        speedStr = Speed.ToString("F1");

                    _mach = value;
                }
            }
        }

        private Graphing.Grapher grapher = new Graphing.Grapher(graphWidth, graphHeight, axisWidth) { AutoFitAxes = WindTunnelSettings.AutoFitAxes };

        internal const float AoAdelta = 0.1f * Mathf.Deg2Rad;

        private List<cbItem> lstPlanets = new List<cbItem>();
        private CelestialBody cbStar;

        private GUIStyle exitButton = new GUIStyle(HighLogic.Skin.button);
        private GUIStyle downButton = new GUIStyle(HighLogic.Skin.button);
        private GUIStyle clearBox = new GUIStyle(HighLogic.Skin.box);
        private GUIStyle labelCentered = new GUIStyle(HighLogic.Skin.label) { alignment = TextAnchor.MiddleCenter };

        Texture2D crossHair = new Texture2D(1, 1);
        Texture2D selectedCrossHair = new Texture2D(1, 1);
        Texture2D clearTex = new Texture2D(1, 1);
        Texture2D settingsTex = new Texture2D(12, 12, TextureFormat.ARGB32, false);// GameDatabase.Instance.GetTexture(WindTunnel.iconPath_settings, false);
        Texture2D saveIconTex = new Texture2D(21, 21, TextureFormat.ARGB32, false);

        Vector2 selectedCrossHairVect = new Vector2(-1, -1);

        internal override void Start()
        {
            base.Start();

            hAxisMarks.normal.textColor = hAxisMarks.focused.textColor = hAxisMarks.hover.textColor = hAxisMarks.active.textColor = Color.white;
            vAxisMarks.normal.textColor = vAxisMarks.focused.textColor = vAxisMarks.hover.textColor = vAxisMarks.active.textColor = Color.white;
            exitButton.normal.textColor = exitButton.focused.textColor = exitButton.hover.textColor = exitButton.active.textColor = Color.red;
            downButton.normal = downButton.active;
            
            crossHair.SetPixel(0, 0, new Color32(255, 25, 255, 192));
            crossHair.Apply();
            stylePlotCrossHair.normal.background = crossHair;

            selectedCrossHair.SetPixel(0, 0, new Color32(255, 255, 255, 192));
            selectedCrossHair.Apply();
            styleSelectedCrossHair.normal.background = selectedCrossHair;

            clearTex.SetPixel(0, 0, Color.clear);
            clearTex.Apply();
            clearBox.normal.background = clearTex;

            settingsTex.LoadImage(System.IO.File.ReadAllBytes(WindTunnel.texPath + WindTunnel.iconPath_settings));
            saveIconTex.LoadImage(System.IO.File.ReadAllBytes(WindTunnel.texPath + WindTunnel.iconPath_save));

            onWindowVisibleChanged += (MonoBehaviourWindow sender, bool visible) => { if (inputLocked && !visible && sender == this) { EditorLogic.fetch.Unlock(lockID); inputLocked = false; } };
        }

        internal override void Update()
        {
            base.Update();

            if (Visible && this.WindowRect.Contains(Event.current.mousePosition))
            {
                if (!inputLocked)
                {
                    EditorLogic.fetch.Lock(false, false, false, lockID);
                    inputLocked = true;
                }
            }
            else
            {
                if (inputLocked)
                {
                    EditorLogic.fetch.Unlock(lockID);
                    inputLocked = false;
                }
            }
        }

        internal override void DrawWindow(int id)
        {
            if (GUI.Button(new Rect(this.WindowRect.width - 27, 2, 25, 25), "X", exitButton))
            {
                WindTunnel.Instance.CloseWindow();
                return;
            }

            if (GUI.Button(new Rect(this.WindowRect.width - 54, 2, 25, 25), "▲", Minimized ? downButton : HighLogic.Skin.button))
            {
                Minimized = !Minimized;
                if (Minimized)
                {
                    this.WindowRect.height = 100;
                    this.WindowRect.width = 100;
                }
            }

            
            if (GUI.Button(new Rect(this.WindowRect.width - 81, 2, 25, 25), settingsTex))
            {
                settingsDialog = SpawnDialog();
                this.Visible = false;
            }

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(graphWidth + 55 + axisWidth));

            if (!Minimized)
            {
                if (GraphGenerator.Status == CalculationManager.RunStatus.Completed)
                {
                    if (GUI.Button(new Rect(12, 80 + graphHeight + 9 + 11 - (CurrentGraphMode != GraphMode.FlightEnvelope ? 28 : 0), 25, 25), saveIconTex))
                    {
                        if (EditorLogic.fetch.ship != null)
                            grapher.WriteToFile(EditorLogic.fetch.ship.shipName);
                    }
                }

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
            }

            if (Minimized || CurrentGraphMode == GraphMode.AoACurves || CurrentGraphMode == GraphMode.VelocityCurves)
            {
                GUILayout.BeginHorizontal(GUILayout.Height(25), GUILayout.ExpandHeight(false));
                GUILayout.Label("Altitude: ", GUILayout.Width(62));
                altitudeStr = GUILayout.TextField(altitudeStr, GUILayout.Width(105));

                if (CurrentGraphMode == GraphMode.AoACurves || Minimized)
                {
                    Rect toggleRect = GUILayoutUtility.GetRect(new GUIContent(""), HighLogic.Skin.label, GUILayout.Width(20));
                    toggleRect.position -= new Vector2(7, 3);
                    Mach = GUI.Toggle(toggleRect, Mach, "    ", new GUIStyle(HighLogic.Skin.toggle) { padding = new RectOffset(6, 2, -6, -6), contentOffset = new Vector2(6, 2) });
                    //Mach = GUILayout.Toggle(Mach, "", GUILayout.Width(30), GUILayout.Height(20));
                    if (!Mach)
                    {
                        GUILayout.Label("Speed (m/s): ", GUILayout.Width(101));
                        speedStr = GUILayout.TextField(speedStr, GUILayout.Width(132));
                    }
                    else
                    {
                        GUILayout.Label("Speed (Mach): ", GUILayout.Width(101));
                        speedStr = GUILayout.TextField(speedStr, GUILayout.Width(132));
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

            if (!Minimized)
            {
                GUILayout.BeginVertical(GUILayout.Width(200));

                ddlPlanet.DrawButton();
                
                GUILayout.Space(2);

                if (GUILayout.Button("Update Vessel", GUILayout.Height(25)))
                {
                    OnVesselChanged();
                    this.vessel = VesselCache.SimulatedVessel.Borrow(EditorLogic.fetch.ship, VesselCache.SimCurves.Borrow(body));
                }

                // Display selected point details.
                GUILayout.Label(this.conditionDetails);
                if (CurrentGraphMode == GraphMode.AoACurves && AoACurveGenerator.Status == CalculationManager.RunStatus.Completed)
                {
                    DataGenerators.AoACurve.AoAPoint zeroPoint = new DataGenerators.AoACurve.AoAPoint(vessel, body, Altitude, Speed, 0);
                    GUILayout.Label(String.Format("CL_Alpha_0:\t{0:F3}m^2/°\nCL_Alpha_avg:\t{1:F3}m^2/°", zeroPoint.dLift / zeroPoint.dynamicPressure, AoACurveGenerator.AverageLiftSlope));
                }

                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Part Highlighting: ");
            WindTunnel.HighlightMode newhighlightMode = (WindTunnel.HighlightMode)GUILayout.SelectionGrid((int)WindTunnel.Instance.highlightMode, highliftModeStrings, 3);

            if(newhighlightMode != Parent.highlightMode)
            {
                Parent.UpdateHighlighting(newhighlightMode, this.body, this.Altitude, this.Speed, this.AoA);
                if (newhighlightMode == WindTunnel.HighlightMode.Off)
                    this.WindowRect.height = 100;
            }

            GUILayout.EndHorizontal();
            if (newhighlightMode != WindTunnel.HighlightMode.Off)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("AoA (deg): ");
                aoaStr = GUILayout.TextField(aoaStr, GUILayout.Width(132));
                if (GUILayout.Button("Apply"))
                {
                    if (float.TryParse(aoaStr, out float tempAoA))
                    {
                        tempAoA *= Mathf.Deg2Rad;
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
            
            Vector2 vectMouse = Event.current.mousePosition;

            if (selectedCrossHairVect.x >= 0 && selectedCrossHairVect.y >= 0 && Status == CalculationManager.RunStatus.Completed)
            {
                if (graphRect.x + selectedCrossHairVect.x != vectMouse.x || !graphRect.Contains(vectMouse))
                    GUI.Box(new Rect(graphRect.x + selectedCrossHairVect.x, graphRect.y, 1, graphRect.height), "", styleSelectedCrossHair);
                if (CurrentGraphMode == GraphMode.FlightEnvelope)
                    if (graphRect.y + selectedCrossHairVect.y != vectMouse.y || !graphRect.Contains(vectMouse))
                        GUI.Box(new Rect(graphRect.x, graphRect.y + selectedCrossHairVect.y, graphRect.width, 1), "", styleSelectedCrossHair);
            }

            if (graphRect.Contains(vectMouse) && Status == CalculationManager.RunStatus.Completed)
            {
                GUI.Box(new Rect(vectMouse.x, graphRect.y, 1, graphRect.height), "", stylePlotCrossHair);
                if (CurrentGraphMode == GraphMode.FlightEnvelope)
                    GUI.Box(new Rect(graphRect.x, vectMouse.y, graphRect.width, 1), "", stylePlotCrossHair);

                float showValue = GetGraphValue((int)(vectMouse.x - graphRect.x), CurrentGraphMode == GraphMode.FlightEnvelope ? (int)(graphHeight - (vectMouse.y - graphRect.y)) : -1);
                //GUI.Label(new Rect(vectMouse.x + 5, vectMouse.y - 20, 80, 15), String.Format(graphUnits[(int)CurrentGraphSelect], showValue), SkinsLibrary.CurrentTooltip);
                GUIContent labelContent = new GUIContent(grapher.GetFormattedValueAtPixel((int)(vectMouse.x - graphRect.x), (int)(graphHeight - (vectMouse.y - graphRect.y))));
                Vector2 labelSize = SkinsLibrary.CurrentTooltip.CalcSize(labelContent);
                if (labelSize.x < 80)
                    labelSize.x = 80;
                if (labelSize.y < 15)
                    labelSize.y = 15;
                GUI.Label(new Rect(vectMouse.x + 5, vectMouse.y - 20, labelSize.x, labelSize.y), labelContent, SkinsLibrary.CurrentTooltip);

                if(Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    //conditionDetails = GetConditionDetails((vectMouse.x - graphRect.x) / graphWidth, CurrentGraphMode == GraphMode.FlightEnvelope ? (graphHeight - (vectMouse.y - graphRect.y)) / graphHeight : float.NaN);
                    selectedCrossHairVect = vectMouse - graphRect.position;
                    SetConditionsFromGraph(selectedCrossHairVect);
                    conditionDetails = GetConditionDetails(CurrentGraphMode, this.Altitude, this.Speed, this.AoA, true);

                    if (Parent.highlightMode != WindTunnel.HighlightMode.Off)
                        Parent.UpdateHighlighting(Parent.highlightMode, this.body, this.Altitude, this.Speed, this.AoA);
                }
            }
            if(CurrentGraphMode == GraphMode.FlightEnvelope && cAxisRect.Contains(vectMouse) && Status == CalculationManager.RunStatus.Completed)
            {
                GUI.Box(new Rect(vectMouse.x, cAxisRect.y, 1, cAxisRect.height), "", stylePlotCrossHair);
                float showValue = (vectMouse.x - cAxisRect.x) / (cAxisRect.width - 1) * (grapher.colorAxis.Max - grapher.colorAxis.Min) + grapher.colorAxis.Min;
                //GUI.Label(new Rect(vectMouse.x + 5, cAxisRect.y - 15, 80, 15), String.Format(graphUnits[(int)CurrentGraphSelect], showValue), SkinsLibrary.CurrentTooltip);
                GUI.Label(new Rect(vectMouse.x + 5, cAxisRect.y - 15, 80, 15),
                    String.Format("{0:" + ((Graphing.Graphable)(grapher[grapher.dominantColorMapIndex])).StringFormat + "}{1}", showValue, ((Graphing.Graphable3)(grapher[grapher.dominantColorMapIndex])).ZUnit),
                    SkinsLibrary.CurrentTooltip);
            }
        }

        private void OnVesselChanged()
        {
            graphDirty = true;
            graphRequested = false;
            EnvelopeSurfGenerator.Clear();
            AoACurveGenerator.Clear();
            VelCurveGenerator.Clear();
            this.conditionDetails = "";

            if (vessel is VesselCache.SimulatedVessel releasable)
                releasable.Release();

            selectedCrossHairVect = new Vector2(-1, -1);
        }

        private void SetConditionsFromGraph(Vector2 crossHairs)
        {
            switch (CurrentGraphMode)
            {
                case GraphMode.FlightEnvelope:
                    this.Altitude = ((graphHeight - 1 - crossHairs.y) / (graphHeight - 1)) * (grapher.YMax - grapher.YMin) + grapher.YMin;
                    this.Speed = (crossHairs.x / (graphWidth - 1)) * (grapher.XMax - grapher.XMin) + grapher.XMin;
                    break;
                case GraphMode.AoACurves:
                    this.AoA = ((crossHairs.x / (graphWidth - 1)) * (grapher.XMax - grapher.XMin) + grapher.XMin) * Mathf.Deg2Rad;
                    break;
                case GraphMode.VelocityCurves:
                    this.Speed = (crossHairs.x / (graphWidth - 1)) * (grapher.XMax - grapher.XMin) + grapher.XMin;
                    break;
            }
        }

        private Vector2 CrossHairsFromConditions(float altitude, float speed, float aoa)
        {
            switch (CurrentGraphMode)
            {
                case GraphMode.FlightEnvelope:
                    return new Vector2((speed - grapher.XMin) / (grapher.XMax - grapher.XMin) * (graphWidth - 1),
                        (1 - altitude / (grapher.YMax - grapher.YMin)) * (graphHeight - 1));
                case GraphMode.AoACurves:
                    return new Vector2(((aoa * Mathf.Rad2Deg) - grapher.XMin) / (grapher.XMax - grapher.XMin) * (graphWidth - 1), 0);
                case GraphMode.VelocityCurves:
                    return new Vector2((speed - grapher.XMin) / (grapher.XMax - grapher.XMin) * (graphWidth - 1), 0);
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
            cbStar = FlightGlobals.Bodies.FirstOrDefault(x => x.referenceBody == x.referenceBody);
            BodyParseChildren(cbStar);
            // Filter to only include planets with Atmospheres
            lstPlanets.RemoveAll(x => x.CB.atmosphere != true);
            
            int planetIndex = lstPlanets.FindIndex(x => x.CB == FlightGlobals.GetHomeBody());
            body = lstPlanets[planetIndex].CB;

            ddlPlanet = new DropDownList(lstPlanets.Select(p => p.NameFormatted), planetIndex, this);
            ddlPlanet.OnSelectionChanged += OnPlanetSelected;
            ddlManager.AddDDL(ddlPlanet);

            GameEvents.onEditorLoad.Add(OnVesselLoaded);
            GameEvents.onEditorNewShipDialogDismiss.Add(OnNewVessel);
            GameEvents.onEditorPodPicked.Add(OnRootChanged);
        }

        internal override void OnGUIOnceOnly()
        {
            GUIStyle glyphStyle = new GUIStyle(HighLogic.Skin.button) { alignment = TextAnchor.MiddleCenter };
            GUIStyle blank = new GUIStyle();
            glyphStyle.active.background = blank.active.background;
            glyphStyle.focused.background = blank.focused.background;
            glyphStyle.hover.background = blank.hover.background;
            glyphStyle.normal.background = blank.normal.background;
            ddlManager.DropDownGlyphs = new GUIContentWithStyle("▼", glyphStyle);
            ddlManager.DropDownSeparators = new GUIContentWithStyle("|", glyphStyle);
        }

        private void OnNewVessel()
        {
            this.Parent.CloseWindow();
            OnVesselChanged();
            vessel = null;
        }

        private void OnVesselLoaded(ShipConstruct vessel, KSP.UI.Screens.CraftBrowserDialog.LoadType loadType)
        {
            OnVesselChanged();
            this.vessel = null;
            //this.vessel = VesselCache.SimulatedVessel.Borrow(vessel, VesselCache.SimCurves.Borrow(body));
        }

        private void OnRootChanged(Part rootPart)
        {
            OnVesselChanged();
            // TODO: Could do fancier stuff like compare angles and then adjust all AoAs appropriately.
            this.vessel = VesselCache.SimulatedVessel.Borrow(EditorLogic.fetch.ship, VesselCache.SimCurves.Borrow(body));
        }

        private void OnPlanetSelected(DropDownList sender, int OldIndex, int NewIndex)
        {
            CelestialBody newBody = lstPlanets[NewIndex].CB;

            if (newBody == body)
                return;

            body = newBody;
            graphDirty = true;
            graphRequested = false;
            this.conditionDetails = "";

            if (vessel is VesselCache.SimulatedVessel releasable)
                releasable.Release();
            this.vessel = VesselCache.SimulatedVessel.Borrow(EditorLogic.fetch.ship, VesselCache.SimCurves.Borrow(body));
            //this.vessel = new StockAero();
            Parent.UpdateHighlighting(Parent.highlightMode, this.body, this.Altitude, this.Speed, this.AoA);
            selectedCrossHairVect = new Vector2(-1, -1);
            
            switch (body.name.ToLower())
            {
                case "laythe":
                default:
                case "kerbin":
                    maxAltitude = 25000;
                    maxSpeed = 2500;
                    altitudeStep = 200;
                    speedStep = 20;
                    break;
                case "eve":
                    maxAltitude = 35000;
                    maxSpeed = 3500;
                    altitudeStep = 200;
                    speedStep = 28;
                    break;
                case "duna":
                    maxAltitude = 10000;
                    maxSpeed = 1000;
                    altitudeStep = 200;
                    speedStep = 20;
                    break;
                /*case "laythe":
                    maxAltitude = 20000;
                    maxSpeed = 2000;
                    break;*/
                case "jool":
                    maxAltitude = 200000;
                    maxSpeed = 7000;
                    altitudeStep = 2000;
                    speedStep = 70;
                    break;
            }
        }

        internal override void OnDestroy()
        {
            Cancel();
            ThreadPool.Dispose();
            GameEvents.onEditorLoad.Remove(OnVesselLoaded);
            GameEvents.onEditorNewShipDialogDismiss.Remove(OnNewVessel);
            GameEvents.onEditorPodPicked.Remove(OnRootChanged);
            base.OnDestroy();
            grapher.Dispose();
            Destroy(crossHair);
            Destroy(selectedCrossHair);
            Destroy(clearTex);
            Destroy(settingsTex);
            if (inputLocked)
                EditorLogic.fetch.Unlock(lockID);
        }

        private void BodyParseChildren(CelestialBody cbRoot, int Depth = 0)
        {
            List<cbItem> bodies = FlightGlobals.Bodies.Select(p => (cbItem)p).ToList();
            foreach (cbItem item in bodies.Where(x => x.Parent == cbRoot).OrderBy(x => x.SemiMajorRadius))
            {
                item.Depth = Depth;
                if (item.CB != cbStar)
                {
                    lstPlanets.Add(item);
                    if (bodies.Where(x => x.Parent == item.CB).Count() > 1)
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

            public static implicit operator cbItem(CelestialBody body)
            {
                return new cbItem(body);
            }
        }
    }
}
