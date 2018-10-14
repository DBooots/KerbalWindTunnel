using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KerbalWindTunnel.Graphing
{
    public class Legend
    {
        public static readonly Color[] Bright = new Color[]
            { Color.green, Color.yellow, Color.red, Color.cyan, new Color(148, 0, 211), new Color(255, 140, 0), Color.blue };
        private Grapher grapher;
        private Color[] colors = Bright;

        public Legend(Grapher grapher)
        {
            this.grapher = grapher;
        }

        public void SetColors()
        {
            int colorIndex = 0;
            for (int i = 0; i < grapher.Count; i++)
            {
                if (grapher[i] is ILineGraph lineGraph)
                {
                    lineGraph.Color = this.colors[colorIndex % colors.Length];
                    colorIndex++;
                }
            }
        }
    }
}
