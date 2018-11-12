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
        #region Fields and Properties
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
        DropDownList ddlEnvelope;

        bool inputLocked = false;
        const string lockID = "KWTLock";

        public static readonly float gAccel = (float)(Planetarium.fetch.Home.gravParameter / (Planetarium.fetch.Home.Radius * Planetarium.fetch.Home.Radius));
        
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
                    //savedGraphSelect[(int)CurrentGraphMode] = CurrentGraphSelect;
                    // Visibility is now saved by the graphs themselves.

                    // Actually change mode
                    _graphMode = value;

                    // Load new settings:
                    //_graphSelect = savedGraphSelect[(int)CurrentGraphMode];
                    GetConditionDetails(CurrentGraphMode, this.Altitude, this.Speed, this.AoA, false);

                    // Reset the strings:
                    Speed = Speed;
                    Altitude = Altitude;
                    AoA = AoA;

                    // Request a new graph;
                    graphDirty = true;
                    graphRequested = false;

                    // Close axes setting window:
                    if (axesWindow != null)
                        axesWindow.Dismiss();
                    grapher.ReleaseAxesLimits(-1);
                }
            }
        }
        public enum GraphMode
        {
            FlightEnvelope = 0,
            AoACurves = 1,
            VelocityCurves = 2
        }

        /*public readonly GraphSelect[][] selectFromIndex = new GraphSelect[][]{
            new GraphSelect[]{ GraphSelect.ExcessThrust, GraphSelect.LevelFlightAoA, GraphSelect.LiftDragRatio, GraphSelect.ThrustAvailable, GraphSelect.LiftSlope, GraphSelect.ExcessAcceleration, GraphSelect.FuelBurn, GraphSelect.MaxLiftAoA, GraphSelect.MaxLiftForce },
            new GraphSelect[]{ GraphSelect.LiftForce, GraphSelect.DragForce, GraphSelect.LiftDragRatio, GraphSelect.LiftSlope, GraphSelect.PitchInput, GraphSelect.Torque },
            new GraphSelect[]{ GraphSelect.LevelFlightAoA, GraphSelect.LiftDragRatio, GraphSelect.ThrustAvailable, GraphSelect.DragForce, GraphSelect.LiftSlope, GraphSelect.MaxLiftAoA, GraphSelect.MaxLiftForce } };
            */

        /*public readonly int[][] indexFromSelect = new int[][]
        {
            new int[]{ 0, 1, 2, 3, 4, 5, 6, 7, 8, 0, 0, 0, 0 },
            new int[]{ 0, 0, 2, 0, 3, 0, 0, 0, 0, 0, 1, 4, 5 },
            new int[]{ 0, 0, 1, 2, 4, 0, 0, 5, 6, 0, 3, 0, 0 } };*/

        /*public enum GraphSelect
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
        }*/
        //private GraphSelect _graphSelect = GraphSelect.ExcessThrust;
        /*public GraphSelect CurrentGraphSelect
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
        }*/

        bool[][] lineFlags = new bool[3][] { new bool[2], new bool[8], new bool[10] };

        //private GraphSelect[] savedGraphSelect = new GraphSelect[] { GraphSelect.ExcessThrust, GraphSelect.LiftForce, GraphSelect.LevelFlightAoA };
        private readonly string[] graphModes = new string[] { "Flight Envelope", "AoA Curves", "Velocity Curves" };
        private readonly string[][] graphSelections = new string[][] {
            new string[] { "Excess Thrust", "Level Flight AoA", "Lift/Drag Ratio", "Thrust Available", "Max Lift AoA", "Max Lift Force", "Fuel Economy", "Fuel Burn Rate", "Drag Force", "Lift Slope", "Pitch Input", "Excess Acceleration" },
            new string[] { "Lift Force", "Lift/Drag Ratio", "Pitch Input", "Wet", "Drag Force", "Lift Slope", "Pitching Torque", "Dry" },
            new string[] { "Level Flight AoA", "Lift/Drag Ratio", "Thrust Available", "Excess Thrust", "Excess Accleration", "Max Lift AoA", "Lift Slope", "Drag Force", "Max Lift", "Pitch Input" }
        };
        private readonly string[] highlightModeStrings = new string[] { "Off", "Drag", "Lift" };

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
        private float _targetAltitude = 17700;
        private string targetAltitudeStr = "17700";
        public float TargetAltitude
        {
            get { return _targetAltitude; }
            set
            {
                _targetAltitude = value;
                targetAltitudeStr = value.ToString("F0");
            }
        }
        private float _targetSpeed = 1410;
        private string targetSpeedStr = "1410";
        public float TargetSpeed
        {
            get { return _targetSpeed; }
            set
            {
                _targetSpeed = value;
                targetSpeedStr = value.ToString("F1");
            }
        }
        private bool selectingTarget = false;

        private Graphing.Grapher grapher = new Graphing.Grapher(graphWidth, graphHeight, axisWidth) { AutoFitAxes = WindTunnelSettings.AutoFitAxes };

        internal const float AoAdelta = 0.1f * Mathf.Deg2Rad;

        private List<CBItem> lstPlanets = new List<CBItem>();
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

        private string conditionDetails = "";
        private GUIStyle styleSelectedCrossHair = new GUIStyle();
        private GUIStyle stylePlotCrossHair = new GUIStyle();

        public CalculationManager.RunStatus Status { get => GraphGenerator.Status; }
        #endregion Fields and Properties

        #region Window Drawing Methods
        internal override void DrawWindow(int id)
        {
            DrawTopRowButtons();

            GUILayout.BeginVertical();      // Main window - laid out vertically
            if (!Minimized)
            {
                GUILayout.BeginHorizontal();    // Top region

                GUILayout.BeginVertical(GUILayout.Width(graphWidth + axisWidth + 55 + 12));  // Graphing frame
                CurrentGraphMode = (GraphMode)GUILayout.SelectionGrid((int)CurrentGraphMode, graphModes, 3);
                DrawGraph(CurrentGraphMode);
                GUILayout.EndVertical();        // \Graphing frame

                GUILayout.BeginVertical(GUILayout.Width(200));  // Button and info frame
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
                GUILayout.EndVertical();        // \Button and info frame

                GUILayout.EndHorizontal();      // \Top region

                DrawSelectionOptions();

                DrawHighlightingOptions();

                DrawSaveIcon();
            }
            else
            {
                DrawHighlightingOptions();
                DrawConditionOptions();
            }
            GUILayout.EndVertical();

            HandleMouseEvents();
        }

        private void DrawTopRowButtons()
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
        }

        private void DrawSaveIcon()
        {
            if (GraphGenerator.Status == CalculationManager.RunStatus.Completed)
            {
                if (GUI.Button(new Rect(80 + graphWidth, 80 + 5 + graphHeight - 10, 25, 25), saveIconTex))
                {
                    if (EditorLogic.fetch.ship != null)
                        grapher.WriteToFile(EditorLogic.fetch.ship.shipName);
                }
            }
        }

        private void DrawSelectionOptions()
        {
            bool[] newFlags;
            switch (CurrentGraphMode)
            {
                case GraphMode.FlightEnvelope:
                    GUILayout.BeginHorizontal(GUILayout.Height(30));
                    ddlEnvelope.DrawButton(GUILayout.Width(350));
                    GUILayout.Space(5);
                    newFlags = Extensions.GUILayoutHelper.ToggleGrid(new string[] { "Fuel-Optimal Path", "Time-Optimal Path" }, lineFlags[0], 2);
                    if (newFlags[0] != lineFlags[0][0] || newFlags[1] != lineFlags[0][1])
                    {
                        lineFlags[0] = newFlags;
                        EnvelopeSurfGenerator.Graphables["Fuel-Optimal Path"].Visible = lineFlags[0][0];
                        EnvelopeSurfGenerator.Graphables["Time-Optimal Path"].Visible = lineFlags[0][1];
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Space(3);
                    GUILayout.BeginHorizontal(GUILayout.Height(30));
                    GUILayout.Label("Ascent Path Target: Altitude:");
                    targetAltitudeStr = GUILayout.TextField(targetAltitudeStr, GUILayout.Width(119));
                    GUILayout.Label("Speed:");
                    targetSpeedStr = GUILayout.TextField(targetSpeedStr, GUILayout.Width(111));
                    if (GUILayout.Button("Apply") &&
                        float.TryParse(targetAltitudeStr, out float tempA) && float.TryParse(targetSpeedStr, out float tempS))
                    {
                        TargetAltitude = tempA;
                        TargetSpeed = tempS;
                        EnvelopeSurfGenerator.CalculateOptimalLines(vessel, EnvelopeSurfGenerator.currentConditions, TargetSpeed, TargetAltitude, 0, 0);
                    }
                    if (GUILayout.Button("Select on Graph", selectingTarget ? downButton : HighLogic.Skin.button))
                        selectingTarget = !selectingTarget;
                    GUILayout.EndHorizontal();
                    // Dropdown for graphSelect
                    // Toggles for time- and fuel-optimal ascent paths
                    break;
                case GraphMode.AoACurves:
                    newFlags = Extensions.GUILayoutHelper.ToggleGrid(graphSelections[1], lineFlags[1], 4);
                    GUILayout.Space(5);
                    GUILayout.Label("", HighLogic.Skin.horizontalSlider, GUILayout.Height(15));
                    for (int i = newFlags.Length - 1; i >= 0; i--)
                    {
                        if (newFlags[i] != lineFlags[1][i])
                        {
                            lineFlags[1][i] = newFlags[i];
                            switch (i)
                            {
                                case 0:
                                case 4:     // Lift and/or Drag
                                    if (lineFlags[1][i])
                                        lineFlags[1][1] = lineFlags[1][2]= lineFlags[1][5] = lineFlags[1][6] = false;
                                    break;
                                case 1:     //Lift/Drag Ratio
                                    if (lineFlags[1][i])
                                        lineFlags[1][0] = lineFlags[1][2] = lineFlags[1][4] = lineFlags[1][5] = lineFlags[1][6] = false;
                                    break;
                                case 2:     // Pitch Input
                                    if (lineFlags[1][i])
                                        lineFlags[1][0] = lineFlags[1][1] = lineFlags[1][4] = lineFlags[1][5] = lineFlags[1][6] = false;
                                    break;
                                case 3:     // Set pitch and torque to include wet
                                    if (!lineFlags[1][7]) lineFlags[1][7] = true;
                                    break;
                                case 5:     // Lift Slope
                                    if (lineFlags[1][i])
                                        lineFlags[1][0] = lineFlags[1][1] = lineFlags[1][2] = lineFlags[1][4] = lineFlags[1][6] = false;
                                    break;
                                case 6:     // Torque
                                    if (lineFlags[1][i])
                                        lineFlags[1][0] = lineFlags[1][1] = lineFlags[1][2] = lineFlags[1][4] = lineFlags[1][5] = false;
                                    break;
                                case 7:     // Set pitch and torque to include dry
                                    if (!lineFlags[1][3]) lineFlags[1][3] = true;
                                    break;
                                default: throw new ArgumentOutOfRangeException("AoA line graph #");
                            }
                            SetAoAGraphs(lineFlags[1]);
                            break;
                        }
                    }
                    //CurrentGraphSelect = selectFromIndex[(int)CurrentGraphMode][GUILayout.SelectionGrid(indexFromSelect[(int)CurrentGraphMode][(int)CurrentGraphSelect], graphSelections[(int)CurrentGraphMode], 3, GUILayout.Height(64))];
                    DrawConditionOptions();
                    // Toggles for each graph option - graphSelect is not used
                    break;
                case GraphMode.VelocityCurves:
                    newFlags = Extensions.GUILayoutHelper.ToggleGrid(graphSelections[2], lineFlags[2], 5);
                    GUILayout.Space(5);
                    GUILayout.Label("", HighLogic.Skin.horizontalSlider, GUILayout.Height(15));
                    for (int i = newFlags.Length - 1; i >= 0; i--)
                    {
                        if (newFlags[i] != lineFlags[2][i])
                        {
                            lineFlags[2][i] = newFlags[i];
                            switch (i)
                            {
                                case 0:
                                case 5:     // Level flight and/or max lift AoA
                                    if (lineFlags[2][i])
                                        lineFlags[2][1] = lineFlags[2][2] = lineFlags[2][3] = lineFlags[2][4] = lineFlags[2][6] = lineFlags[2][7] = lineFlags[2][8] = lineFlags[2][9] = false;
                                    break;
                                case 1:     // Lift/Drag Ratio
                                    if (lineFlags[2][i])
                                        lineFlags[2][0] = lineFlags[2][2] = lineFlags[2][3] = lineFlags[2][4] = lineFlags[2][5] = lineFlags[2][6] = lineFlags[2][7] = lineFlags[2][8] = lineFlags[2][9] = false;
                                    break;
                                case 2:
                                case 3:
                                case 7:
                                case 8:     // Thrust available and/or Excess Thrust and/or Drag Force and/or Max Lift Force
                                    if (lineFlags[2][i])
                                        lineFlags[2][0] = lineFlags[2][1] = lineFlags[2][4] = lineFlags[2][5] = lineFlags[2][6] = lineFlags[2][9] = false;
                                    break;
                                case 4:     // Excess Acceleration
                                    if (lineFlags[2][i])
                                        lineFlags[2][0] = lineFlags[2][1] = lineFlags[2][2] = lineFlags[2][3] = lineFlags[2][5] = lineFlags[2][6] = lineFlags[2][7] = lineFlags[2][8] = lineFlags[2][9] = false;
                                    break;
                                case 6:     // Lift Slope
                                    if (lineFlags[2][i])
                                        lineFlags[2][0] = lineFlags[2][1] = lineFlags[2][2] = lineFlags[2][3] = lineFlags[2][4] = lineFlags[2][5] = lineFlags[2][7] = lineFlags[2][8] = lineFlags[2][9] = false;
                                    break;
                                case 9:     // Pitch Input
                                    if (lineFlags[2][i])
                                        lineFlags[2][0] = lineFlags[2][1] = lineFlags[2][2] = lineFlags[2][3] = lineFlags[2][4] = lineFlags[2][5] = lineFlags[2][6] = lineFlags[2][7] = lineFlags[2][8] = false;
                                    break;
                                default: throw new ArgumentOutOfRangeException("Velocity line graph #");
                            }
                            SetVelGraphs(lineFlags[2]);
                            break;
                        }
                    }
                    //CurrentGraphSelect = selectFromIndex[(int)CurrentGraphMode][GUILayout.SelectionGrid(indexFromSelect[(int)CurrentGraphMode][(int)CurrentGraphSelect], graphSelections[(int)CurrentGraphMode], 3, GUILayout.Height(64))];
                    DrawConditionOptions();
                    // Toggle for each graph option - graphSelect is not used
                    break;
            }
        }

        private void DrawHighlightingOptions()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Part Highlighting: ");
            WindTunnel.HighlightMode newhighlightMode = (WindTunnel.HighlightMode)GUILayout.SelectionGrid((int)WindTunnel.Instance.highlightMode, highlightModeStrings, 3);

            if (newhighlightMode != Parent.highlightMode)
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
        }

        private void DrawConditionOptions()
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
                    if (this.Altitude != altitude || this.Speed != speed)
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

        private void HandleMouseEvents()
        {
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

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    if (!selectingTarget)
                    {
                        selectedCrossHairVect = vectMouse - graphRect.position;
                        SetConditionsFromGraph(selectedCrossHairVect);
                        conditionDetails = GetConditionDetails(CurrentGraphMode, this.Altitude, this.Speed, this.AoA, true);

                        if (Parent.highlightMode != WindTunnel.HighlightMode.Off)
                            Parent.UpdateHighlighting(Parent.highlightMode, this.body, this.Altitude, this.Speed, this.AoA);
                    }
                    else
                    {
                        selectingTarget = false;
                        Vector2 targetVect = vectMouse - graphRect.position;
                        TargetSpeed = (targetVect.x / (graphWidth - 1)) * (grapher.XMax - grapher.XMin) + grapher.XMin;
                        TargetAltitude = ((graphHeight - 1 - targetVect.y) / (graphHeight - 1)) * (grapher.YMax - grapher.YMin) + grapher.YMin;
                        EnvelopeSurfGenerator.CalculateOptimalLines(vessel, EnvelopeSurfGenerator.currentConditions, TargetSpeed, TargetAltitude, 0, 0);
                    }
                }
            }
            if (CurrentGraphMode == GraphMode.FlightEnvelope && cAxisRect.Contains(vectMouse) && Status == CalculationManager.RunStatus.Completed)
            {
                GUI.Box(new Rect(vectMouse.x, cAxisRect.y, 1, cAxisRect.height), "", stylePlotCrossHair);
                float showValue = (vectMouse.x - cAxisRect.x) / (cAxisRect.width - 1) * (grapher.colorAxis.Max - grapher.colorAxis.Min) + grapher.colorAxis.Min;
                //GUI.Label(new Rect(vectMouse.x + 5, cAxisRect.y - 15, 80, 15), String.Format(graphUnits[(int)CurrentGraphSelect], showValue), SkinsLibrary.CurrentTooltip);
                GUI.Label(new Rect(vectMouse.x + 5, cAxisRect.y - 15, 80, 15),
                    String.Format("{0:" + ((Graphing.Graphable)(grapher[grapher.dominantColorMapIndex])).StringFormat + "}{1}", showValue, ((Graphing.Graphable3)(grapher[grapher.dominantColorMapIndex])).ZUnit),
                    SkinsLibrary.CurrentTooltip);
            }
        }
        #endregion Window Drawing Methods

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

        public void Cancel() => GraphGenerator.Cancel();

        #region Triggered Methods
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

        private void OnEnvelopeGraphChanged(DropDownList sender, int OldIndex, int NewIndex)
        {
            if (OldIndex == NewIndex)
                return;
            SetEnvGraphs(NewIndex, lineFlags[0]);
        }

        public void OnAxesChanged(Graphing.Grapher sender, float xMin, float xMax, float yMin, float yMax, float zMin, float zMax)
        {
            if (sender != grapher)
                return;
            if (!graphRequested || graphDirty)
                return;
            GraphGenerator.OnAxesChanged(vessel, xMin, xMax, yMin, yMax, zMin, zMax);
        }
        #endregion Triggered Methods

        #region MonoBehaviour Methods
        internal override void Start()
        {
            base.Start();

            lineFlags[1][0] = lineFlags[1][3] = lineFlags[1][7] = lineFlags[2][0] = true;
            SetEnvGraphs(0, lineFlags[0]);
            SetAoAGraphs(lineFlags[1]);
            SetVelGraphs(lineFlags[2]);

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

            onWindowVisibleChanged += (MonoBehaviourWindow sender, bool visible) =>
            {
                if (!visible && inputLocked && sender == this)
                {
                    EditorLogic.fetch.Unlock(lockID);
                    inputLocked = false;
                }
                if (!visible && sender == this && axesWindow != null)
                    axesWindow.Dismiss();
            };
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
            if (Visible && axesWindow != null)
                axesWindow.RTrf.anchoredPosition = new Vector2(WindowRect.x + WindowRect.width, -WindowRect.y);
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

            ddlEnvelope = new DropDownList(graphSelections[(int)GraphMode.FlightEnvelope], 0, this);
            ddlEnvelope.OnSelectionChanged += OnEnvelopeGraphChanged;
            //ddlEnvelope.styleButton.fixedWidth = ;
            ddlManager.AddDDL(ddlEnvelope);

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
        #endregion MonoBehaviour Methods

        #region CelestialBody Parsing
        private void BodyParseChildren(CelestialBody cbRoot, int Depth = 0)
        {
            List<CBItem> bodies = FlightGlobals.Bodies.Select(p => (CBItem)p).ToList();
            foreach (CBItem item in bodies.Where(x => x.Parent == cbRoot).OrderBy(x => x.SemiMajorRadius))
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
        
        internal class CBItem
        {
            internal CBItem(CelestialBody CB)
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

            public static implicit operator CBItem(CelestialBody body)
            {
                return new CBItem(body);
            }
        }
        #endregion CelestialBody Parsing
    }

    /*public static class EnumParser
    {
        public static string ToFormattedString(this WindTunnelWindow.GraphSelect graphSelect)
        {
            switch (graphSelect)
            {
                case WindTunnelWindow.GraphSelect.ExcessThrust:
                    return "Excess Thrust";
                case WindTunnelWindow.GraphSelect.LevelFlightAoA:
                    return "Level AoA";
                case WindTunnelWindow.GraphSelect.LiftDragRatio:
                    return "Lift/Drag Ratio";
                case WindTunnelWindow.GraphSelect.ThrustAvailable:
                    return "Thrust Available";
                case WindTunnelWindow.GraphSelect.LiftSlope:
                    return "Lift Slope";
                case WindTunnelWindow.GraphSelect.ExcessAcceleration:
                    return "Excess Acceleration";
                case WindTunnelWindow.GraphSelect.FuelBurn:
                    return "Fuel Burn Rate";
                case WindTunnelWindow.GraphSelect.MaxLiftAoA:
                    return "Max Lift AoA";
                case WindTunnelWindow.GraphSelect.MaxLiftForce:
                    return "Max Lift";
                case WindTunnelWindow.GraphSelect.LiftForce:
                    return "Lift";
                case WindTunnelWindow.GraphSelect.DragForce:
                    return "Drag";
                case WindTunnelWindow.GraphSelect.PitchInput:
                    return "Pitch Input";
                case WindTunnelWindow.GraphSelect.Torque:
                    return "Torque";
                default:
                    throw new ArgumentException();
            }
        }
    }*/
}
