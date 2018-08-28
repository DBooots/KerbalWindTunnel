using System;
using System.Collections.Generic;
using System.Linq;

namespace KerbalWindTunnel.Graphing
{
    public class Grapher : GraphableCollection3, IDisposable, IGraphableProvider
    {
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
        
        public delegate float CoordsToColorFunc(float x, float y, float z);
    }

    public interface IGraphable
    {
        string Name { get; }
        float XMin { get; }
        float XMax { get; }
        float YMin { get; }
        float YMax { get; }
        string XUnit { get; set; }
        string YUnit { get; set; }
        string XName { get; set; }
        string YName { get; set; }
        Func<float, float> XAxisScale { get; set; }
        Func<float, float> YAxisScale { get; set; }
        void Draw(ref UnityEngine.Texture2D texture, float xLeft, float xRight, float yBottom, float yTop);
        float ValueAt(float x, float y);
        string GetFormattedValueAt(float x, float y, bool withName = false);
        event EventHandler ValuesChanged;
        void WriteToFile(string filename, string sheetName = "");
    }
    public interface IGraphable3 : IGraphable
    {
        float ZMin { get; }
        float ZMax { get; }
        string ZUnit { get; set; }
        string ZName { get; set; }
        Func<float, float> ZAxisScale { get; set; }
    }

    public abstract class Graphable : IGraphable
    {
        public string Name { get; set; } = "";
        public virtual float XMin { get; protected set; }
        public virtual float XMax { get; protected set; }
        public virtual float YMin { get; protected set; }
        public virtual float YMax { get; protected set; }
        public bool Transpose { get; set; } = false;
        public string XName { get; set; } = "";
        protected internal string yName = null;
        public virtual string YName { get => yName ?? Name; set => yName = value; }
        public string XUnit { get; set; } = "";
        public string YUnit { get; set; } = "";
        public string StringFormat { get; set; } = "G";
        public virtual ColorMap Color { get; set; } = new ColorMap(UnityEngine.Color.white);
        public virtual Grapher.CoordsToColorFunc ColorFunc { get; set; } = (x, y, z) => 0;
        public virtual Func<float, float> XAxisScale { get; set; } = (v) => v;
        public virtual Func<float, float> YAxisScale { get; set; } = (v) => v;

        public abstract void Draw(ref UnityEngine.Texture2D texture, float xLeft, float xRight, float yBottom, float yTop);
        public abstract float ValueAt(float x, float y);
        public event EventHandler ValuesChanged;

        public abstract void WriteToFile(string filename, string sheetName = "");

        public virtual void OnValuesChanged(EventArgs eventArgs)
        {
            ValuesChanged?.Invoke(this, eventArgs);
        }

        public virtual string GetFormattedValueAt(float x, float y, bool withName = false)
        {
            return String.Format("{2}{0:" + StringFormat + "}{1}", ValueAt(x, y), YUnit, withName && Name != "" ? Name + ": " : "");
        }
    }
    public abstract class Graphable3 : Graphable, IGraphable3
    {
        public float ZMin { get; protected set; }
        public float ZMax { get; protected set; }
        public string ZUnit { get; set; }
        public override string YName { get => yName ?? ""; set => yName = value; }
        protected string zName = null;
        public string ZName { get { return zName ?? Name; } set { zName = value; } }
        public float ZAxisScaler { get; set; } = 1;
        public virtual Func<float, float> ZAxisScale { get; set; } = (v) => v;

        public override string GetFormattedValueAt(float x, float y, bool withName = false)
        {
            return String.Format("{2}{0:" + StringFormat + "}{1}", ValueAt(x, y), ZUnit, withName && Name != "" ? Name + ": " : "");
        }
    }

}
