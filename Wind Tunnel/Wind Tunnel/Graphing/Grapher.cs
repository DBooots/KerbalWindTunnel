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

        private AxesSettingWindow axesWindow;
        
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

        public float CMin { get; set; } = float.NaN;
        public float CMax { get; set; } = float.NaN;

        public virtual float Width { get; set; }
        public virtual float Height { get; set; }

        public Axis horizontalAxis = new Axis(0, 0, true);
        public Axis verticalAxis = new Axis(0, 0, false);
        public Axis colorAxis = new Axis(0, 0);

        protected internal float selfXmin, selfXmax, selfYmin, selfYmax, selfZmin, selfZmax;
        protected internal float setXmin, setXmax, setYmin, setYmax, setZmin, setZmax;
        protected internal bool[] useSelfAxes = new bool[] { true, true, true };

        protected bool axesDirty = true;
        protected bool graphDirty = true;

        public Grapher(int width, int height, int axisWidth)
        {
            this.graphTex = new UnityEngine.Texture2D(width, height, UnityEngine.TextureFormat.ARGB32, false);
            this.hAxisTex = new UnityEngine.Texture2D(width, axisWidth, UnityEngine.TextureFormat.ARGB32, false);
            this.vAxisTex = new UnityEngine.Texture2D(axisWidth, height, UnityEngine.TextureFormat.ARGB32, false);
            this.cAxisTex = new UnityEngine.Texture2D(width, axisWidth, UnityEngine.TextureFormat.ARGB32, false);

            setXmin = setXmax = setYmin = setYmax = setZmin = setZmax = float.NaN;

            axesWindow = new AxesSettingWindow(this);
        }
        public Grapher(int width, int height, int axisWidth, IEnumerable<IGraphable> graphs) : this(width, height, axisWidth)
        {
            AddRange(graphs);
        }

        public PopupDialog SpawnAxesWindow() => axesWindow.SpawnPopupDialog();
        
        public void SetAxesLimits(int index, float min, float max, bool delayRecalculate = false)
        {
            if (index > 2 || float.IsNaN(min) || float.IsNaN(max))
                return;

            useSelfAxes[index] = false;
            switch (index)
            {
                case 0:
                    setXmin = min;
                    setXmax = max;
                    break;
                case 1:
                    setYmin = min;
                    setYmax = max;
                    break;
                case 2:
                    setZmin = min;
                    setZmax = max;
                    break;
            }
            if (!delayRecalculate)
                RecalculateLimits();
        }
        public void ReleaseAxesLimits(int index, bool delayRecalculate = false)
        {
            useSelfAxes[index] = true;
            if (!delayRecalculate)
                RecalculateLimits();
        }
        public void ResetStoredLimits(int index = -1)
        {
            switch (index)
            {
                case 0:
                    setXmin = setXmax = float.NaN;
                    break;
                case 1:
                    setYmin = setYmax = float.NaN;
                    break;
                case 2:
                    setZmin = setZmax = float.NaN;
                    break;
                case -1:
                    ResetStoredLimits(0);
                    ResetStoredLimits(1);
                    ResetStoredLimits(2);
                    break;
            }
        }

        public override bool RecalculateLimits()
        {
            float[] oldLimits = new float[] { XMin, XMax, YMin, YMax, ZMin, ZMax, CMin, CMax };

            bool baseResult = base.RecalculateLimits();

            CMin = CMax = float.NaN;
            for (int i = 0; i < graphs.Count; i++)
            {
                if (!graphs[i].Visible) continue;
                if (graphs[i] is SurfGraph surfGraph)
                {
                    if (float.IsNaN(CMin) || CMin > surfGraph.CMin) CMin = surfGraph.CMin;
                    if (float.IsNaN(CMax) || CMax < surfGraph.CMax) CMax = surfGraph.CMax;
                }
            }
            // If color is somehow not set...
            if (float.IsNaN(CMin)) CMin = ZMin;
            if (float.IsNaN(CMax)) CMax = ZMax;
            // Check that the ColorMap will accept our limits, otherwise attempt to find new ones.
            if (!float.IsNaN(CMin) && !dominantColorMap.Filter(CMin))
            {
                if (float.IsNegativeInfinity(CMin) && dominantColorMap.Filter(float.MinValue)) CMin = float.MinValue;
                else if (CMin < 0 && dominantColorMap.Filter(0)) CMin = 0;
            }
            if (!float.IsNaN(CMax) && !dominantColorMap.Filter(CMax))
            {
                if (float.IsPositiveInfinity(CMax) && dominantColorMap.Filter(float.MaxValue)) CMax = float.MaxValue;
                else if (CMax > 0 && dominantColorMap.Filter(0)) CMax = 0;
            }

            bool setsNaN = float.IsNaN(setXmin) || float.IsNaN(setXmax) || float.IsNaN(setYmin) || float.IsNaN(setYmax) || float.IsNaN(setZmin) || float.IsNaN(setZmax);
            if (((useSelfAxes[0] || useSelfAxes[1] || useSelfAxes[2]) && baseResult) || setsNaN)
            {
                horizontalAxis = new Axis(XMin, XMax, true);
                verticalAxis = new Axis(YMin, YMax, false);
                colorAxis = new Axis(CMin, CMax);

                XMin = selfXmin = horizontalAxis.Min;
                XMax = selfXmax = horizontalAxis.Max;
                YMin = selfYmin = verticalAxis.Min;
                YMax = selfYmax = verticalAxis.Max;
                ZMin = CMin = selfZmin = colorAxis.Min;
                ZMax = CMax = selfZmax = colorAxis.Max;

                if (setsNaN)
                {
                    setXmin = selfXmin; setXmax = selfXmax;
                    setYmin = selfYmin; setYmax = selfYmax;
                    setZmin = selfZmin; setZmax = selfZmax;
                }
            }
            if (!useSelfAxes[0])
            {
                XMin = setXmin; XMax = setXmax;
                horizontalAxis = new Axis(XMin, XMax, true, true);
            }
            if (!useSelfAxes[1])
            {
                YMin = setYmin; YMax = setYmax;
                verticalAxis = new Axis(YMin, YMax, false, true);
            }
            if (!useSelfAxes[2])
            {
                ZMin = CMin = setZmin; ZMax = CMax = setZmax;
                colorAxis = new Axis(CMin, CMax, forceBounds: true);
            }
            
            if (axesDirty || !(oldLimits[0] == XMin && oldLimits[1] == XMax && oldLimits[2] == YMin && oldLimits[3] == YMax && oldLimits[4] == ZMin && oldLimits[5] == ZMax && oldLimits[6] == CMin && oldLimits[7] == CMax))
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
            
            this.Draw(ref this.graphTex, XMin, XMax, YMin, YMax, CMin, CMax);

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
            ResetStoredLimits();
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

        public override void Clear()
        {
            base.Clear();
            ResetStoredLimits();
        }
        public override bool Remove(IGraphable graph)
        {
            bool result = base.Remove(graph);
            if (result && this.Count == 0)
                ResetStoredLimits();
            return result;
        }
        public override void RemoveAt(int index)
        {
            base.RemoveAt(index);
            if (this.Count == 0)
                ResetStoredLimits();
        }
    }
}
