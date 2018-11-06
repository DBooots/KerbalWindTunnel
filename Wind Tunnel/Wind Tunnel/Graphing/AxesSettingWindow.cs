using System;
using System.Collections.Generic;
using UnityEngine;

namespace KerbalWindTunnel.Graphing
{
    public class AxesSettingWindow
    {
        public readonly Grapher grapher;

        private string xMinStr, xMaxStr, yMinStr, yMaxStr, zMinStr, zMaxStr;
        private float setXmin, setXmax, setYmin, setYmax, setZmin, setZmax;
        private bool[] useSelfAxesToggle = new bool[] { true, true, true };

        public AxesSettingWindow(Grapher grapher)
        {
            this.grapher = grapher;
        }

        public PopupDialog SpawnPopupDialog()
        {
            setXmin = grapher.setXmin;
            setXmax = grapher.setXmax;
            setYmin = grapher.setYmin;
            setYmax = grapher.setYmax;
            setZmin = grapher.setZmin;
            setZmax = grapher.setZmax;
            xMinStr = grapher.setXmin.ToString();
            xMaxStr = grapher.setXmax.ToString();
            yMinStr = grapher.setYmin.ToString();
            yMaxStr = grapher.setYmax.ToString();
            zMinStr = grapher.setZmin.ToString();
            zMaxStr = grapher.setZmax.ToString();
            useSelfAxesToggle[0] = grapher.useSelfAxes[0];
            useSelfAxesToggle[1] = grapher.useSelfAxes[1];
            useSelfAxesToggle[2] = grapher.useSelfAxes[2];

            List<DialogGUIBase> dialog = new List<DialogGUIBase>
            {
                //new DialogGUIToggleButton(() => useSelfAxesToggle[0] && useSelfAxesToggle[1] && useSelfAxesToggle[2], "Auto Axes", (value) => useSelfAxesToggle[0] = useSelfAxesToggle[1] = useSelfAxesToggle[2] = value, h: 20),
                //new DialogGUISpace(5),
                new DialogGUILabel("X-Axis"),
                new DialogGUIToggle(() => useSelfAxesToggle[0], "Auto", (value) => useSelfAxesToggle[0] = value, h: 20),
                new DialogGUIHorizontalLayout(new DialogGUILabel(String.Format("Min:\t")), new DialogGUITextInput(xMinStr, false, 10, (str) => xMinStr = str, 25) { OptionInteractableCondition = () => !useSelfAxesToggle[0] }),
                new DialogGUIHorizontalLayout(new DialogGUILabel(String.Format("Max:\t")), new DialogGUITextInput(xMaxStr, false, 10, (str) => xMaxStr = str, 25) { OptionInteractableCondition = () => !useSelfAxesToggle[0] }),
                new DialogGUISpace(5),
                new DialogGUILabel("Y-Axis"),
                new DialogGUIToggle(() => useSelfAxesToggle[1], "Auto", (value) => useSelfAxesToggle[1] = value, h: 20),
                new DialogGUIHorizontalLayout(new DialogGUILabel(String.Format("Min:\t")), new DialogGUITextInput(yMinStr, false, 10, (str) => yMinStr = str, 25) { OptionInteractableCondition = () => !useSelfAxesToggle[1] }),
                new DialogGUIHorizontalLayout(new DialogGUILabel(String.Format("Max:\t")), new DialogGUITextInput(yMaxStr, false, 10, (str) => yMaxStr = str, 25) { OptionInteractableCondition = () => !useSelfAxesToggle[1] }),
                new DialogGUISpace(5),
                new DialogGUILabel("Z-Axis"),
                new DialogGUIToggle(() => useSelfAxesToggle[2], "Auto", (value) => useSelfAxesToggle[2] = value, h: 20),
                new DialogGUIHorizontalLayout(new DialogGUILabel(String.Format("Min:\t")), new DialogGUITextInput(zMinStr, false, 10, (str) => zMinStr = str, 25) { OptionInteractableCondition = () => !useSelfAxesToggle[2] }),
                new DialogGUIHorizontalLayout(new DialogGUILabel(String.Format("Max:\t")), new DialogGUITextInput(zMaxStr, false, 10, (str) => zMaxStr = str, 25) { OptionInteractableCondition = () => !useSelfAxesToggle[2] }),
                new DialogGUISpace(5),
                new DialogGUIButton("Apply", () =>
                {
                    float[] temp = new float[6];
                    if(float.TryParse(xMinStr, out temp[0]) && float.TryParse(xMaxStr, out temp[1]) &&
                        float.TryParse(yMinStr, out temp[2]) && float.TryParse(yMaxStr, out temp[3]) &&
                        float.TryParse(zMinStr, out temp[4]) && float.TryParse(zMaxStr, out temp[5]))
                    {
                        setXmin = temp[0]; setXmax = temp[1];
                        setYmin = temp[2]; setYmax = temp[3];
                        setZmin = temp[4]; setZmax = temp[5];
                    }

                    if (!useSelfAxesToggle[0])
                        grapher.SetAxesLimits(0, setXmin, setXmax, true);
                    else
                        grapher.ReleaseAxesLimits(0, true);
                    if (!useSelfAxesToggle[1])
                        grapher.SetAxesLimits(1, setYmin, setYmax, true);
                    else
                        grapher.ReleaseAxesLimits(1, true);
                    if (!useSelfAxesToggle[2])
                        grapher.SetAxesLimits(2, setZmin, setZmax, true);
                    else
                        grapher.ReleaseAxesLimits(2, true);

                    grapher.RecalculateLimits();
                }, false),
                new DialogGUIButton("Close", () => { }, true)
            };

            return PopupDialog.SpawnPopupDialog(new Vector2(0, 1), new Vector2(0, 1),
                new MultiOptionDialog("KWTAxesSettings", "", "Axes Settings", UISkinManager.defaultSkin, 150,
                    dialog.ToArray()),
                false, UISkinManager.defaultSkin);
        }
    }
}
