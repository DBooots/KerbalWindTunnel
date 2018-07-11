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

        private VesselCache.SimulatedVessel highlightingVessel = null;
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
        private const string iconPath = "WindTunnel/Textures/Icon";
        private const string iconPath_off = "WindTunnel/Textures/Icon_off";
        private const string iconPath_blizzy = "WindTunnel/Textures/blizzy_Icon";
        private const string iconPath_blizzy_off = "WindTunnel/Textures/blizzy_Icon_off";

        public void UpdateHighlighting(HighlightMode highlightMode, CelestialBody body, float altitude, float speed, float aoa)
        {
            this.highlightMode = highlightMode;
            this.body = body;
            this.altitude = altitude;
            this.speed = speed;
            this.aoa = aoa;

            ClearPartHighlighting();

            ReleaseHighlightingVessel();

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
            part.SetHighlightColor(Graphing.ColorMap.Jet[value]);
            part.SetHighlight(true, false);
        }

        private void ReleaseHighlightingVessel()
        {
            if (highlightingVessel != null)
                highlightingVessel.Release();
            highlightingVessel = null;
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

            highlightingVessel = VesselCache.SimulatedVessel.Borrow(ship, VesselCache.SimCurves.Borrow(body));

            int count = ship.parts.Count;
            highlightingData = new Vector2[count];

            Vector3 inflow = AeroPredictor.InflowVect(aoa);
            maxHighlights = Vector2.zero;
            minHighlights = Vector2.positiveInfinity;

            float pseudoReDragMult = PhysicsGlobals.DragCurvePseudoReynolds.Evaluate(atmDensity * speed);

            for (int i = 0; i < count; i++)
            {
                //Vector3 partForce = highlightingVessel.parts[i].GetAero(inflow, mach, pseudoReDragMult);
                Vector3 partForce = StockAeroUtil.SimAeroForce(body, new ShipConstruct("test", "", new List<Part>() { EditorLogic.fetch.ship.parts[i] }), inflow * speed, altitude);
                partForce = (Quaternion.AngleAxis((aoa * 180 / Mathf.PI), Vector3.left) * partForce);
                maxHighlights.x = Mathf.Max(Mathf.Abs(partForce.z), maxHighlights.x);
                maxHighlights.y = Mathf.Max(Mathf.Abs(partForce.y), maxHighlights.y);
                minHighlights.x = Mathf.Min(Mathf.Abs(partForce.z), minHighlights.x);
                minHighlights.y = Mathf.Min(Mathf.Abs(partForce.y), minHighlights.y);

                highlightingData[i] = new Vector2(Mathf.Abs(partForce.z), Mathf.Abs(partForce.y));
            }
        }

        private void OnEditorShipModified(ShipConstruct ship)
        {
            ReleaseHighlightingVessel();
            UpdateHighlighting(this.highlightMode, this.body, this.altitude, this.speed, this.aoa);
        }

        internal override void Awake()
        {
            base.Awake();

            if (Instance)
                Destroy(Instance);
            Instance = this;

            GameEvents.onEditorShipModified.Add(OnEditorShipModified);
            if (!ActivateBlizzyToolBar())
            {
                //log.debug("Registering GameEvents.");
                appLauncherEventSet = true;
                GameEvents.onGUIApplicationLauncherReady.Add(OnGuiApplicationLauncherReady);
            }
            guiId = GUIUtility.GetControlID(FocusType.Passive);

            window = AddComponent<WindTunnelWindow>();
            window.WindowRect = new Rect(100, 200, 750, 600);
            window.Parent = this;
        }

        internal bool ActivateBlizzyToolBar()
        {
            try
            {
                if (!ToolbarManager.ToolbarAvailable) return false;
                if (HighLogic.LoadedScene != GameScenes.EDITOR && HighLogic.LoadedScene != GameScenes.FLIGHT) return true;
                blizzyToolbarButton = ToolbarManager.Instance.add("ReCoupler", "ReCoupler");
                blizzyToolbarButton.TexturePath = iconPath_blizzy;
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
                GameDatabase.Instance.GetTexture(iconPath, false));
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
                appButton.SetTexture(GameDatabase.Instance.GetTexture(iconPath_off, false));
            if (blizzyToolbarButton != null)
                blizzyToolbarButton.TexturePath = iconPath_blizzy_off;
        }
        public void OnButtonFalse()
        {
            window.Visible = false;

            highlightMode = HighlightMode.Off;
            ClearPartHighlighting();

            if (appButton != null)
                appButton.SetTexture(GameDatabase.Instance.GetTexture(iconPath, false));
            if (blizzyToolbarButton != null)
                blizzyToolbarButton.TexturePath = iconPath_blizzy;
        }

        internal override void OnDestroy()
        {
            base.OnDestroy();

            ClearPartHighlighting();
            
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
