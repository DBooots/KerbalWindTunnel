using System.Collections.Generic;
using UnityEngine;

namespace KerbalWindTunnel
{
    public class WindTunnelSettings
    {
        public static bool UseCoefficients
        {
            get { return Instance.useCoefficients; }
            set
            {
                Instance.useCoefficients = value;
                settingsChanged = true;
            }
        }
        [Persistent]
        public bool useCoefficients;

        public static bool DefaultToMach
        {
            get { return Instance.defaultToMach; }
            set
            {
                Instance.defaultToMach = value;
                settingsChanged = true;
            }
        }
        [Persistent]
        public bool defaultToMach;

        public static bool StartMinimized
        {
            get { return Instance.startMinimized; }
            set
            {
                Instance.startMinimized = value;
                settingsChanged = true;
            }
        }
        [Persistent]
        public bool startMinimized;

        public static bool UseSingleColorHighlighting
        {
            get { return Instance.useSingleColorHighlighting; }
            set
            {
                Instance.useSingleColorHighlighting = value;
                settingsChanged = true;
            }
        }
        [Persistent]
        public bool useSingleColorHighlighting;

        public static bool ShowEnvelopeMask
        {
            get { return Instance.showEnvelopeMask; }
            set
            {
                Instance.showEnvelopeMask = value;
                settingsChanged = true;
            }
        }
        [Persistent]
        public bool showEnvelopeMask = true;

        public static bool ShowEnvelopeMaskAlways
        {
            get { return Instance.showEnvelopeMaskAlways; }
            set
            {
                Instance.showEnvelopeMaskAlways = value;
                settingsChanged = true;
            }
        }
        [Persistent]
        public bool showEnvelopeMaskAlways = false;

        public static bool UseBlizzy
        {
            get { return Instance.useBlizzy; }
            set
            {
                Instance.useBlizzy = value;
                settingsChanged = true;
            }
        }
        [Persistent]
        public bool useBlizzy = false;

        public static bool AutoFitAxes
        {
            get { return Instance.autoFitAxes; }
            set
            {
                Instance.autoFitAxes = value;
                settingsChanged = true;
            }
        }
        [Persistent]
        public bool autoFitAxes = true;

        private static bool settingsChanged = false;
        private static bool settingsLoaded = false;

        internal static WindTunnelSettings Instance = new WindTunnelSettings();

        public static void InitializeSettings()
        {
            if (settingsLoaded)
                return;

            Instance.LoadSettingsFromFile();

            settingsLoaded = true;
        }
        private void LoadSettingsFromFile()
        {
            ConfigNode[] settingsNode = GameDatabase.Instance.GetConfigNodes("KerbalWindTunnelSettings");
            if (settingsNode.Length < 1)
            {
                Debug.Log("Kerbal Wind Tunnel Settings file note found.");
                // To trigger creating a settings file.
                settingsChanged = true;
                return;
            }
            ConfigNode.LoadObjectFromConfig(this, settingsNode[0]);
        }

        public static void SaveSettings()
        {
            Instance.SaveSettingsToFile();
        }
        private void SaveSettingsToFile()
        {
            if (!settingsChanged)
                return;

            ConfigNode data = ConfigNode.CreateConfigFromObject(this, 0, new ConfigNode("KerbalWindTunnelSettings"));

            ConfigNode save = new ConfigNode();
            save.AddNode(data);
            save.Save("GameData/WindTunnel/KerbalWindTunnelSettings.cfg");
        }
    }

    public partial class WindTunnelWindow
    {
        private PopupDialog settingsDialog;
        private PopupDialog SpawnDialog()
        {
            List<DialogGUIBase> dialog = new List<DialogGUIBase>
            {
                new DialogGUIToggle(WindTunnelSettings.UseCoefficients, "Lift, Drag as coefficients",
                    delegate (bool b) {
                        Instance.graphDirty = true;
                        Instance.graphRequested = false;
                        WindTunnelSettings.UseCoefficients = !WindTunnelSettings.UseCoefficients; }),
                new DialogGUIToggle(WindTunnelSettings.DefaultToMach, "Default to speed as Mach", delegate (bool b) { WindTunnelSettings.DefaultToMach = !WindTunnelSettings.DefaultToMach; }),
                new DialogGUIToggle(WindTunnelSettings.StartMinimized, "Start minimized", delegate (bool b) { WindTunnelSettings.StartMinimized = !WindTunnelSettings.StartMinimized; }),
                new DialogGUIToggle(WindTunnelSettings.UseSingleColorHighlighting, "Use simple part highlighting", delegate (bool b) {WindTunnelSettings.UseSingleColorHighlighting = !WindTunnelSettings.UseSingleColorHighlighting; }),
                new DialogGUIToggle(WindTunnelSettings.ShowEnvelopeMask, "Show flight envelope outline on graphs", delegate (bool b) {WindTunnelSettings.ShowEnvelopeMask = !WindTunnelSettings.ShowEnvelopeMask; }),
                new DialogGUIToggle(WindTunnelSettings.ShowEnvelopeMaskAlways && WindTunnelSettings.ShowEnvelopeMask, "Show flight envelope outline even on flight envelope", delegate (bool b) {if(WindTunnelSettings.ShowEnvelopeMask) WindTunnelSettings.ShowEnvelopeMaskAlways = !WindTunnelSettings.ShowEnvelopeMaskAlways; }),
                new DialogGUIToggle(WindTunnelSettings.AutoFitAxes, string.Format("Auto-fit axes to graph data\n(Uncheck to plot full range)"), delegate (bool b) {WindTunnelSettings.AutoFitAxes = !WindTunnelSettings.AutoFitAxes; grapher.AutoFitAxes = WindTunnelSettings.AutoFitAxes; })
            };

            if (ToolbarManager.ToolbarAvailable)
                dialog.Add(new DialogGUIToggle(WindTunnelSettings.UseBlizzy, "Use Blizzy's Toolbar", delegate (bool b) { WindTunnelSettings.UseBlizzy = !WindTunnelSettings.UseBlizzy; }));

            dialog.Add(new DialogGUIButton("Accept", delegate { WindTunnelWindow.Instance.Visible = true; settingsDialog.Dismiss(); }));

            return PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new MultiOptionDialog("KWTSettings", "", "Kerbal Wind Tunnel Settings", UISkinManager.defaultSkin, dialog.ToArray()),
                false, UISkinManager.defaultSkin);
        }
    }
}
