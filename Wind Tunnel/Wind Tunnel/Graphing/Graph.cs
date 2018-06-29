using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KerbalWindTunnel.Graphing
{
    public class Graph : IDisposable
    {
        public UnityEngine.Texture2D graphTex;
        public UnityEngine.Texture2D hAxisTex;
        public UnityEngine.Texture2D vAxisTex;
        public UnityEngine.Texture2D cAxisTex;

        public virtual float XMin { get; private set; } = float.NaN;
        public virtual float XMax { get; private set; } = float.NaN;
        public virtual float YMin { get; private set; } = float.NaN;
        public virtual float YMax { get; private set; } = float.NaN;
        public virtual float ZMin { get; private set; } = float.NaN;
        public virtual float ZMax { get; private set; } = float.NaN;
        public virtual float Width { get; set; }
        public virtual float Height { get; set; }

        public Axis horizontalAxis = new Axis(0, 0, true);
        public Axis verticalAxis = new Axis(0, 0, false);
        public Axis colorAxis = new Axis(0, 0);

        private List<IGraphable> graphs = new List<IGraphable>();
        private bool graphDirty = true;

        public Graph(int width, int height, int axisWidth)
        {
            this.graphTex = new UnityEngine.Texture2D(width, height, UnityEngine.TextureFormat.ARGB32, false);
            this.hAxisTex = new UnityEngine.Texture2D(width, axisWidth, UnityEngine.TextureFormat.ARGB32, false);
            this.vAxisTex = new UnityEngine.Texture2D(axisWidth, height, UnityEngine.TextureFormat.ARGB32, false);
            this.cAxisTex = new UnityEngine.Texture2D(width, height, UnityEngine.TextureFormat.ARGB32, false);
        }
        public Graph(int width, int height, int axisHeight, IEnumerable<IGraphable> graphs) : this(width, height, axisHeight)
        {
            foreach (IGraphable g in graphs)
                AddGraph(g);
        }
        
        private void SetGraphDirty(object sender, EventArgs e) { graphDirty = true; }

        public void Clear()
        {
            for (int i = graphs.Count - 1; i >= 0; i--)
                RemoveGraphAt(i);
            graphDirty = true;
        }

        public void AddGraph(IGraphable newGraph)
        {
            graphs.Add(newGraph);
            newGraph.ValuesChanged += SetGraphDirty;
            graphDirty = true;
        }

        public void InsertGraph(int index, IGraphable newGraph)
        {
            graphs.Insert(index, newGraph);
            newGraph.ValuesChanged += SetGraphDirty;
            graphDirty = true;
        }
        
        public void RemoveGraph(IGraphable graph)
        {
            graphs.Remove(graph);
            graphDirty = true;
            graph.ValuesChanged -= SetGraphDirty;
        }
        public void RemoveGraphAt(int index)
        {
            IGraphable graphable = graphs[index];
            graphs.RemoveAt(index);
            graphDirty = true;
            graphable.ValuesChanged -= SetGraphDirty;
        }

        public bool RecalculateLimits()
        {
            float[] oldLimits = new float[] { XMin, XMax, YMin, YMax, ZMin, ZMax };
            XMin = XMax = YMin = YMax = ZMin = ZMax = float.NaN;

            for (int i = 0; i < graphs.Count; i++)
            {
                if (graphs[i] is SurfGraph surf)
                {
                    if (surf.ZMin < this.ZMin || float.IsNaN(this.ZMin)) this.ZMin = surf.ZMin;
                    if (surf.ZMax > this.ZMax || float.IsNaN(this.ZMax)) this.ZMax = surf.ZMax;
                }
                if (graphs[i].XMin < this.XMin || float.IsNaN(this.XMin)) this.XMin = graphs[i].XMin;
                if (graphs[i].XMax > this.XMax || float.IsNaN(this.XMax)) this.XMax = graphs[i].XMax;
                if (graphs[i].YMin < this.YMin || float.IsNaN(this.YMin)) this.YMin = graphs[i].YMin;
                if (graphs[i].YMax > this.YMax || float.IsNaN(this.YMax)) this.YMax = graphs[i].YMax;
            }

            horizontalAxis = new Axis(XMin, XMax, true);
            verticalAxis = new Axis(YMin, YMax, false);
            colorAxis = new Axis(ZMin, ZMax);
            XMin = horizontalAxis.Min;
            XMax = horizontalAxis.Max;
            YMin = verticalAxis.Min;
            YMax = verticalAxis.Max;
            ZMin = colorAxis.Min;
            ZMax = colorAxis.Max;

            if (!(oldLimits[0] == XMin && oldLimits[1] == XMax && oldLimits[2] == YMin && oldLimits[3] == YMax && oldLimits[4] == ZMin && oldLimits[5] == ZMax))
            {
                graphDirty = true;
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

        public void Draw()
        {
            if (!graphDirty)
                return;

            if (RecalculateLimits())
            {
                ClearTexture(ref hAxisTex);
                ClearTexture(ref vAxisTex);
                ClearTexture(ref cAxisTex);
                horizontalAxis.DrawAxis(ref hAxisTex, UnityEngine.Color.white);
                verticalAxis.DrawAxis(ref vAxisTex, UnityEngine.Color.white);
                colorAxis.DrawAxis(ref cAxisTex, UnityEngine.Color.white);
            }
            ClearTexture(ref graphTex);

            for (int i = 0; i < graphs.Count; i++)
            {
                graphs[i].Draw(ref this.graphTex, XMin, XMax, YMin, YMax);
            }

            graphDirty = false;
        }

        public float GetValueAt(int x, int y, int index = 0)
        {
            if (graphs.Count - 1 < index)
                return float.NaN;

            float xVal = x / (float)(graphTex.width - 1) * (XMax - XMin) + XMin;
            float yVal = y / (float)(graphTex.height - 1) * (YMax - YMin) + YMin;

            return graphs[index].ValueAt(xVal, yVal);
        }
        public string GetFormattedValueAt(int x, int y)
        {
            if (graphs.Count == 0)
                return "";

            float xVal = x / (float)(graphTex.width - 1) * (XMax - XMin) + XMin;
            float yVal = y / (float)(graphTex.height - 1) * (YMax - YMin) + YMin;

            string returnValue = String.Format("{2}{0:" + graphs[0].StringFormat + "}{1}", graphs[0].ValueAt(xVal, yVal), graphs[0].Unit, graphs[0].Name != "" ? graphs[0].Name + ": " : "");
            for (int i = 1; i < graphs.Count; i++)
            {
                returnValue += String.Format("\n{2}{0:" + graphs[i].StringFormat + "}{1}", graphs[i].ValueAt(xVal, yVal), graphs[i].Unit, graphs[0].Name != "" ? graphs[0].Name + ": " : "");
            }
            return returnValue;
        }

        public static explicit operator UnityEngine.Texture2D(Graph graph)
        {
            return graph.graphTex;
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
        string Name { get; set; }
        float XMin { get; }
        float XMax { get; }
        float YMin { get; }
        float YMax { get; }
        bool Transpose { get; }
        string Unit { get; }
        string StringFormat { get; }
        ColorMap Color { get; set; }
        Graph.CoordsToColorFunc ColorFunc { get; set; }
        void Draw(ref UnityEngine.Texture2D texture, float xLeft, float xRight, float yBottom, float yTop);
        float ValueAt(float x, float y);
        event EventHandler ValuesChanged;
    }

}
