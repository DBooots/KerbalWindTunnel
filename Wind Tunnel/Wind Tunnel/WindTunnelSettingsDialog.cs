using System.Collections.Generic;
using UnityEngine;

namespace KerbalWindTunnel
{
    public static class WindTunnelSettings
    {
        public static bool useCoefficients = false;
        public static bool defaultToMach = false;
        public static bool startMinimized = false;
        public static void InitializeSettings()
        {

        }
    }
    public partial class WindTunnelWindow
    {
        private PopupDialog settingsDialog;
        private PopupDialog SpawnDialog()
        {
            List<DialogGUIBase> dialog = new List<DialogGUIBase>
            {
                new DialogGUIToggle(WindTunnelSettings.useCoefficients, "Lift, Drag as coefficients",
                    delegate (bool b) {
                        WindTunnelWindow.Instance.graphDirty = true;
                        WindTunnelWindow.Instance.graphRequested = false;
                        WindTunnelSettings.useCoefficients = !WindTunnelSettings.useCoefficients; }),
                new DialogGUIToggle(WindTunnelSettings.defaultToMach, "Default to speed as Mach", delegate (bool b) { WindTunnelSettings.defaultToMach = !WindTunnelSettings.defaultToMach; }),
                new DialogGUIToggle(WindTunnelSettings.startMinimized, "Start minimized", delegate (bool b) { WindTunnelSettings.startMinimized = !WindTunnelSettings.startMinimized; }),
                new DialogGUIButton("Accept", delegate { WindTunnelWindow.Instance.Visible = true; settingsDialog.Dismiss(); })
            };

            return PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new MultiOptionDialog("KWTSettings", "", "Kerbal Wind Tunnel Settings", UISkinManager.defaultSkin, dialog.ToArray()),
                false, UISkinManager.defaultSkin);
        }
    }
}
