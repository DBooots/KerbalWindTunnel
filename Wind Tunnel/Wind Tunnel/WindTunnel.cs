using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.UI.Screens;
using KSPPluginFramework;

namespace KerbalWindTunnel
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public partial class WindTunnel : MonoBehaviourExtended
    {
        public static WindTunnel Instance;

        public enum HighlightMode
        {
            Off = 0,
            Drag = 1,
            Lift = 2
        }

        public HighlightMode highlightMode = HighlightMode.Off;
        
        private Vector2[] highlightingData;
        private Vector2 maxHighlights;
        private Vector2 minHighlights;
        private List<Part> highlightedParts = new List<Part>(100);
        CelestialBody body = null;
        float altitude = 0;
        float speed = 0;
        float aoa = 0;

        WindTunnelWindow window;

        private static ApplicationLauncherButton appButton = null;
        internal static IButton blizzyToolbarButton = null;
        private int guiId;
        private bool appLauncherEventSet = false;
        public const string texPath = "GameData/WindTunnel/Textures/";
        public const string graphPath = "GameData/WindTunnel/PluginData";
        private const string iconPath = "KWT_Icon_on.png";
        private const string iconPath_off = "KWT_Icon.png";
        private const string iconPath_blizzy = "KWT_Icon_blizzy_on.png";
        private const string iconPath_blizzy_off = "KWT_Icon_blizzy.png";
        internal const string iconPath_settings = "KWT_settings.png";
        internal const string iconPath_save = "KWT_saveIcon.png";

        private Texture2D icon_on = new Texture2D(38, 38, TextureFormat.ARGB32, false);
        private Texture2D icon = new Texture2D(38, 38, TextureFormat.ARGB32, false);
        private Texture2D icon_blizzy_on = new Texture2D(24, 24, TextureFormat.ARGB32, false);
        private Texture2D icon_blizzy = new Texture2D(24, 24, TextureFormat.ARGB32, false);
        
        Graphing.ColorMap dragMap = new Graphing.ColorMap(v => new Color(1, 0, 0, v));
        Graphing.ColorMap liftMap = new Graphing.ColorMap(v => new Color(0, 1, 0, v));

        public void UpdateHighlighting(HighlightMode highlightMode, CelestialBody body, float altitude, float speed, float aoa)
        {
            this.highlightMode = highlightMode;
            this.body = body;
            this.altitude = altitude;
            this.speed = speed;
            this.aoa = aoa;

            ClearPartHighlighting();

            if (highlightMode == HighlightMode.Off)
                return;

            GenerateHighlightingData(EditorLogic.fetch.ship, body, altitude, speed, aoa);

            int count = highlightingData.Length;
            float min, max;
            switch (highlightMode)
            {
                case HighlightMode.Drag:
                    min = minHighlights.x;
                    max = maxHighlights.x;
                    for (int i = 0; i < count; i++)
                    {
                        float value = (highlightingData[i].x - min) / (max - min);
                        HighlightPart(EditorLogic.fetch.ship.parts[i], value);
                    }
                    break;
                case HighlightMode.Lift:
                    min = minHighlights.y;
                    max = maxHighlights.y;
                    for (int i = 0; i < count; i++)
                    {
                        float value = (highlightingData[i].y - min) / (max - min);
                        HighlightPart(EditorLogic.fetch.ship.parts[i], value);
                    }
                    break;
            }
        }

        private void HighlightPart(Part part, float value)
        {
            highlightedParts.Add(part);

            part.SetHighlightType(Part.HighlightType.AlwaysOn);
            if (WindTunnelSettings.UseSingleColorHighlighting)
                switch (highlightMode)
                {
                    case HighlightMode.Lift:
                        part.SetHighlightColor(liftMap[value]);
                        break;
                    case HighlightMode.Drag:
                    default:
                        part.SetHighlightColor(dragMap[value]);
                        break;
                }
            else
                part.SetHighlightColor(Graphing.ColorMap.Jet[value]);
            part.SetHighlight(true, false);
        }

        private void ClearPartHighlighting()
        {
            for (int i = highlightedParts.Count - 1; i >= 0; i--)
            {
                highlightedParts[i].SetHighlightDefault();
            }
            highlightedParts.Clear();
        }

        private void GenerateHighlightingData(ShipConstruct ship, CelestialBody body, float altitude, float speed, float aoa)
        {
            float mach, atmDensity;
            lock (body)
            {
                atmDensity = (float)Extensions.KSPClassExtensions.GetDensity(body, altitude);
                mach = speed / (float)body.GetSpeedOfSound(body.GetPressure(altitude), atmDensity);
            }

            int count = ship.parts.Count;
            highlightingData = new Vector2[count];

            Vector3 inflow = AeroPredictor.InflowVect(aoa);
            maxHighlights = Vector2.zero;
            minHighlights = Vector2.positiveInfinity;

            float pseudoReDragMult = PhysicsGlobals.DragCurvePseudoReynolds.Evaluate(atmDensity * speed);

            for (int i = 0; i < count; i++)
            {
                VesselCache.SimulatedPart simPart = VesselCache.SimulatedPart.Borrow(ship.parts[i], null);
                Vector3 partForce = simPart.GetAero(inflow, mach, pseudoReDragMult);

                ModuleLiftingSurface liftingSurface = ship.parts[i].FindModuleImplementing<ModuleLiftingSurface>();
                if (liftingSurface != null)
                {
                    VesselCache.SimulatedLiftingSurface simLiftSurf = VesselCache.SimulatedLiftingSurface.Borrow(liftingSurface, simPart);
                    partForce += simLiftSurf.GetForce(inflow, mach);
                    simLiftSurf.Release();
                }
                simPart.Release();
                //Vector3 partForce = highlightingVessel.parts[i].GetAero(inflow, mach, pseudoReDragMult);
                //Vector3 partForce = StockAeroUtil.SimAeroForce(body, new ShipConstruct("test", "", new List<Part>() { EditorLogic.fetch.ship.parts[i] }), inflow * speed, altitude);
                partForce = AeroPredictor.ToFlightFrame(partForce, aoa);  // (Quaternion.AngleAxis((aoa * 180 / Mathf.PI), Vector3.left) * partForce);
                maxHighlights.x = Mathf.Max(Mathf.Abs(partForce.z), maxHighlights.x);
                maxHighlights.y = Mathf.Max(Mathf.Abs(partForce.y), maxHighlights.y);
                minHighlights.x = Mathf.Min(Mathf.Abs(partForce.z), minHighlights.x);
                minHighlights.y = Mathf.Min(Mathf.Abs(partForce.y), minHighlights.y);

                highlightingData[i] = new Vector2(Mathf.Abs(partForce.z), Mathf.Abs(partForce.y));
            }
        }

        private void OnEditorShipModified(ShipConstruct ship)
        {
            UpdateHighlighting(this.highlightMode, this.body, this.altitude, this.speed, this.aoa);
        }

        internal override void Awake()
        {
            base.Awake();

            WindTunnelSettings.InitializeSettings();

            Threading.ThreadPool.Start(-2);

            if (Instance)
                Destroy(Instance);
            Instance = this;

            icon.LoadImage(System.IO.File.ReadAllBytes(texPath + iconPath_off));
            icon_on.LoadImage(System.IO.File.ReadAllBytes(texPath + iconPath));
            icon_blizzy.LoadImage(System.IO.File.ReadAllBytes(texPath + iconPath_blizzy_off));
            icon_blizzy_on.LoadImage(System.IO.File.ReadAllBytes(texPath + iconPath_blizzy));

            GameEvents.onEditorShipModified.Add(OnEditorShipModified);
            if (!ActivateBlizzyToolBar())
            {
                //log.debug("Registering GameEvents.");
                appLauncherEventSet = true;
                GameEvents.onGUIApplicationLauncherReady.Add(OnGuiApplicationLauncherReady);
            }
            guiId = GUIUtility.GetControlID(FocusType.Passive);

            window = AddComponent<WindTunnelWindow>();
            window.WindowRect = new Rect(150, 50, 100, 100); //750, 600
            window.Parent = this;
            window.Mach = WindTunnelSettings.DefaultToMach;
            window.Minimized = WindTunnelSettings.StartMinimized;
        }

        internal bool ActivateBlizzyToolBar()
        {
            try
            {
                if (!WindTunnelSettings.UseBlizzy) return false;
                if (!ToolbarManager.ToolbarAvailable) return false;
                if (HighLogic.LoadedScene != GameScenes.EDITOR && HighLogic.LoadedScene != GameScenes.FLIGHT) return true;
                blizzyToolbarButton = ToolbarManager.Instance.add("ReCoupler", "ReCoupler");
                blizzyToolbarButton.TexturePath = iconPath_blizzy_off;
                blizzyToolbarButton.ToolTip = "ReCoupler";
                blizzyToolbarButton.Visible = true;
                blizzyToolbarButton.OnClick += (e) =>
                {
                    ButtonToggle();
                };
                return true;
            }
            catch
            {
                // Blizzy Toolbar instantiation error.  ignore.
                return false;
            }
        }

        private void OnGuiApplicationLauncherReady()
        {
            appButton = ApplicationLauncher.Instance.AddModApplication(
                OnButtonTrue,
                OnButtonFalse,
                null,
                null,
                null,
                null,
                ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.SPH | ApplicationLauncher.AppScenes.FLIGHT,
                icon);
        }

        public void CloseWindow()
        {
            if (appButton != null)
                appButton.SetFalse();
            else
                OnButtonFalse();
        }

        public void ButtonToggle()
        {
            if (!window.Visible)
                OnButtonTrue();
            else
                OnButtonFalse();
        }

        public void OnButtonTrue()
        {
            window.Visible = true;
            if (appButton != null)
                appButton.SetTexture(icon_on);
            if (blizzyToolbarButton != null)
                blizzyToolbarButton.TexturePath = iconPath_blizzy;
        }
        public void OnButtonFalse()
        {
            window.Visible = false;

            highlightMode = HighlightMode.Off;
            ClearPartHighlighting();

            if (appButton != null)
                appButton.SetTexture(icon);
            if (blizzyToolbarButton != null)
                blizzyToolbarButton.TexturePath = iconPath_blizzy_off;
        }

        internal override void OnDestroy()
        {
            base.OnDestroy();

            Destroy(icon);
            Destroy(icon_on);
            Destroy(icon_blizzy);
            Destroy(icon_blizzy_on);
            
            ClearPartHighlighting();

            WindTunnelSettings.SaveSettings();
            
            //log.debug("Unregistering GameEvents.");
            GameEvents.onEditorShipModified.Remove(OnEditorShipModified);
            if (appLauncherEventSet)
                GameEvents.onGUIApplicationLauncherReady.Remove(OnGuiApplicationLauncherReady);
            if (appButton != null)
                ApplicationLauncher.Instance.RemoveModApplication(appButton);
            if (blizzyToolbarButton != null)
                blizzyToolbarButton.Destroy();
        }
    }
}
