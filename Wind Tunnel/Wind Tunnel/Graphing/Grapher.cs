using System;
using System.Collections.Generic;
using System.Linq;

namespace KerbalWindTunnel.Graphing
{
    public class Grapher : GraphableCollection3, IDisposable
    {
        public delegate float CoordsToColorFunc(float x, float y, float z);

        public UnityEngine.Texture2D graphTex;
        public UnityEngine.Texture2D hAxisTex;
        public UnityEngine.Texture2D vAxisTex;
        public UnityEngine.Texture2D cAxisTex;
        
        public override bool AutoFitAxes
        {
            get => base.AutoFitAxes;
            set
            {
                if (value != AutoFitAxes)
                {
                    base.AutoFitAxes = value;
                    graphDirty = true;
                    axesDirty = true;
                }
            }
        }

        public virtual float Width { get; set; }
        public virtual float Height { get; set; }

        public Axis horizontalAxis = new Axis(0, 0, true);
        public Axis verticalAxis = new Axis(0, 0, false);
        public Axis colorAxis = new Axis(0, 0);

        protected bool axesDirty = true;
        protected bool graphDirty = true;

        public Grapher(int width, int height, int axisWidth)
        {
            this.graphTex = new UnityEngine.Texture2D(width, height, UnityEngine.TextureFormat.ARGB32, false);
            this.hAxisTex = new UnityEngine.Texture2D(width, axisWidth, UnityEngine.TextureFormat.ARGB32, false);
            this.vAxisTex = new UnityEngine.Texture2D(axisWidth, height, UnityEngine.TextureFormat.ARGB32, false);
            this.cAxisTex = new UnityEngine.Texture2D(width, axisWidth, UnityEngine.TextureFormat.ARGB32, false);
        }
        public Grapher(int width, int height, int axisWidth, IEnumerable<IGraphable> graphs) : this(width, height, axisWidth)
        {
            AddRange(graphs);
        }
        
        public override bool RecalculateLimits()
        {
            float[] oldLimits = new float[] { XMin, XMax, YMin, YMax, ZMin, ZMax };

            base.RecalculateLimits();

            horizontalAxis = new Axis(XMin, XMax, true);
            verticalAxis = new Axis(YMin, YMax, false);
            colorAxis = new Axis(ZMin, ZMax);
            XMin = horizontalAxis.Min;
            XMax = horizontalAxis.Max;
            YMin = verticalAxis.Min;
            YMax = verticalAxis.Max;
            ZMin = colorAxis.Min;
            ZMax = colorAxis.Max;

            if (axesDirty || !(oldLimits[0] == XMin && oldLimits[1] == XMax && oldLimits[2] == YMin && oldLimits[3] == YMax && oldLimits[4] == ZMin && oldLimits[5] == ZMax))
            {
                graphDirty = true;
                axesDirty = true;
                return true;
            }
            return false;
        }

        public void ClearTexture(ref UnityEngine.Texture2D texture)
        {
            ClearTexture(ref texture, UnityEngine.Color.clear);
        }
        public void ClearTexture(ref UnityEngine.Texture2D texture, UnityEngine.Color color)
        {
            UnityEngine.Color[] pixels = texture.GetPixels();
            for (int i = pixels.Length - 1; i >= 0; i--)
                pixels[i] = color;
            texture.SetPixels(pixels);
            texture.Apply();
        }

        public void DrawGraphs()
        {
            if (!graphDirty)
                return;

            RecalculateLimits();
            if (axesDirty)
            {
                ClearTexture(ref hAxisTex);
                ClearTexture(ref vAxisTex);
                //ClearTexture(ref cAxisTex);
                horizontalAxis.DrawAxis(ref hAxisTex, UnityEngine.Color.white);
                verticalAxis.DrawAxis(ref vAxisTex, UnityEngine.Color.white);
                DrawColorAxis(ref cAxisTex, dominantColorMap);
                colorAxis.DrawAxis(ref cAxisTex, UnityEngine.Color.white, false);
                axesDirty = false;
            }
            ClearTexture(ref graphTex);

            this.Draw(ref this.graphTex, XMin, XMax, YMin, YMax);

            graphDirty = false;
        }

        public float ValueAtPixel(int x, int y, int index = 0)
        {
            if (graphs.Count - 1 < index)
                return float.NaN;

            float xVal = x / (float)(graphTex.width - 1) * (XMax - XMin) + XMin;
            float yVal = y / (float)(graphTex.height - 1) * (YMax - YMin) + YMin;

            return ValueAt(xVal, yVal, index);
        }

        public string GetFormattedValueAtPixel(int xPix, int yPix, int index = -1)
        {
            if (graphs.Count == 0)
                return "";

            float xVal = xPix / (float)(graphTex.width - 1) * (XMax - XMin) + XMin;
            float yVal = yPix / (float)(graphTex.height - 1) * (YMax - YMin) + YMin;

            return GetFormattedValueAt(xVal, yVal, index, false);
        }

        public void SetCollection(IEnumerable<IGraphable> newCollection)
        {
            this.Graphables = newCollection.ToList();
        }

        public static explicit operator UnityEngine.Texture2D(Grapher graph)
        {
            return graph.graphTex;
        }

        public void DrawColorAxis(ref UnityEngine.Texture2D axisTex, ColorMap colorMap)
        {
            int width = axisTex.width - 1;
            int height = axisTex.height - 1;
            bool horizontal = height <= width;
            int major = horizontal ? width : height;
            int minor = horizontal ? height : width;

            for (int a = 0; a <= major; a++)
            {
                UnityEngine.Color rowColor = colorMap[(float)a / major];
                for (int b = 0; b <= minor; b++)
                {
                    if (horizontal)
                        axisTex.SetPixel(a, b, rowColor);
                    else
                        axisTex.SetPixel(b, a, rowColor);
                }
            }

            axisTex.Apply();
        }
        
        protected override void ValuesChangedSubscriber(object sender, EventArgs e)
        {
            graphDirty = true;
            base.ValuesChangedSubscriber(sender, e);
        }
        protected override void OnValuesChanged(EventArgs eventArgs)
        {
            graphDirty = true;
            base.OnValuesChanged(eventArgs);
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(graphTex);
            UnityEngine.Object.Destroy(hAxisTex);
            UnityEngine.Object.Destroy(vAxisTex);
            UnityEngine.Object.Destroy(cAxisTex);
        }
    }
}
