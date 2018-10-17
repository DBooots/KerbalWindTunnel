using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KerbalWindTunnel.Extensions
{
    public static class GUILayoutHelper
    {
        public static bool[] ToggleGrid(string[] strings, bool[] values, int xCount)
        {
            bool[] valuesOut = new bool[values.Length];
            GUILayout.BeginVertical();
            int marginWidthL = HighLogic.Skin.toggle.margin.left;
            int marginWidthR = HighLogic.Skin.label.margin.right;

            int elementWidth = 0;
            Rect position = new Rect(0,0,0,0);
            for (int i = 0; i < values.Length; i++)
            {
                if (i % xCount == 0)
                {
                    position = GUILayoutUtility.GetRect(new GUIContent(""), GUIStyle.none, GUILayout.ExpandWidth(true));
                    GUILayout.Space(2);
                    float width = position.width;
                    position.x += marginWidthL;
                    int availableWidth = (int)width;// - marginWidthL - marginWidthR;
                    elementWidth = availableWidth / xCount;
                }
                Rect toggle = position;
                toggle.x += elementWidth * (i % xCount);//marginWidthL + 
                toggle.width = elementWidth - marginWidthR;
                valuesOut[i] = GUI.Toggle(toggle, values[i], strings[i]);
            }
            GUILayout.EndVertical();
            return valuesOut;
        }
    }
}
