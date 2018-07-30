using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KerbalWindTunnel.Graphing
{
    public class GraphableCollection : DataGenerators.IGraphableProvider, IGraphable, IList<IGraphable>
    {
        public string Name { get; set; } = "";
        public virtual float XMin { get; protected set; } = float.NaN;
        public virtual float XMax { get; protected set; } = float.NaN;
        public virtual float YMin { get; protected set; } = float.NaN;
        public virtual float YMax { get; protected set; } = float.NaN;
        public Func<float, float> XAxisScale { get; set; } = (v) => v;
        public Func<float, float> YAxisScale { get; set; } = (v) => v;

        protected List<IGraphable> graphs = new List<IGraphable>();

        public event EventHandler ValuesChanged;

        public virtual List<IGraphable> Graphables { get { return graphs.ToList(); } }

        public int Count { get { return graphs.Count; } }

        bool ICollection<IGraphable>.IsReadOnly => false;

        public IGraphable this[int index]
        {
            get
            {
                return graphs[index];
            }
            set
            {
                graphs[index].ValuesChanged -= ValuesChangedSubscriber;
                graphs[index] = value;
                graphs[index].ValuesChanged += ValuesChangedSubscriber;
                OnValuesChanged(null);
            }
        }

        public GraphableCollection() { }
        public GraphableCollection(IEnumerable<IGraphable> graphs)
        {
            foreach (IGraphable g in graphs)
                Add(g);
        }

        public virtual void Draw(ref UnityEngine.Texture2D texture, float xLeft, float xRight, float yBottom, float yTop)
        {
            for (int i = 0; i < graphs.Count; i++)
            {
                graphs[i].Draw(ref texture, XMin, XMax, YMin, YMax);
            }
        }
        public virtual bool RecalculateLimits()
        {
            float[] oldLimits = new float[] { XMin, XMax, YMin, YMax};
            XMin = XMax = YMin = YMax = float.NaN;

            for (int i = 0; i < graphs.Count; i++)
            {
                float xMin = graphs[i].XAxisScale(graphs[i].XMin);
                float xMax = graphs[i].XAxisScale(graphs[i].XMax);
                float yMin = graphs[i].YAxisScale(graphs[i].YMin);
                float yMax = graphs[i].YAxisScale(graphs[i].YMax);
                if (xMin < this.XMin || float.IsNaN(this.XMin)) this.XMin = xMin;
                if (xMax > this.XMax || float.IsNaN(this.XMax)) this.XMax = xMax;
                if (yMin < this.YMin || float.IsNaN(this.YMin)) this.YMin = yMin;
                if (yMax > this.YMax || float.IsNaN(this.YMax)) this.YMax = yMax;
            }
            
            if (!(oldLimits[0] == XMin && oldLimits[1] == XMax && oldLimits[2] == YMin && oldLimits[3] == YMax))
            {
                return true;
            }
            return false;
        }

        public float ValueAt(float x, float y)
        {
            return ValueAt(x, y, 0);
        }

        public float ValueAt(float x, float y, int index = 0)
        {
            if (graphs.Count - 1 < index)
                return float.NaN;
            
            return graphs[index].ValueAt(x, y);
        }

        public string GetFormattedValueAt(float x, float y, bool withName = false) { return GetFormattedValueAt(x, y, -1, withName); }
        public string GetFormattedValueAt(float x, float y, int index = -1, bool withName = false)
        {
            if (graphs.Count == 0)
                return "";

            if (index >= 0)
                return graphs[index].GetFormattedValueAt(x, y, withName);

            if (graphs.Count > 1)
                withName = true;

            string returnValue = graphs[0].GetFormattedValueAt(x, y, withName);
            for (int i = 1; i < graphs.Count; i++)
            {
                returnValue += String.Format("\n{0}", graphs[i].GetFormattedValueAt(x, y, withName));
            }
            if (withName)
            {
                string nameSubstring = GetNameSubstring();
                if (nameSubstring != "")
                    return returnValue.Replace(nameSubstring, "");
            }
            return returnValue;
        }

        private string GetNameSubstring()
        {
            if (graphs.Count < 2)
                return "";
            int maxL = graphs[0].Name.Length;
            int commonL = 0;
            while (commonL < maxL && graphs[1].Name.StartsWith(graphs[0].Name.Substring(0, commonL + 1)))
                commonL++;
            string nameSubstring =  graphs[0].Name.Substring(0, commonL);
            if (nameSubstring.EndsWith("("))
                nameSubstring = nameSubstring.Substring(0, nameSubstring.Length - 1);
            
            for(int i = 2; i < graphs.Count; i++)
            {
                if (!graphs[i].Name.StartsWith(nameSubstring))
                    return "";
            }
            return nameSubstring;
        }

        protected virtual void OnValuesChanged(EventArgs eventArgs)
        {
            RecalculateLimits();
            ValuesChanged?.Invoke(this, eventArgs);
        }

        public virtual IGraphable GetGraphableByName(string name)
        {
            return graphs.Find(g => g.Name.ToLower() == name.ToLower());
        }


        public virtual IGraphable Find(Predicate<IGraphable> predicate)
        {
            return graphs.Find(predicate);
        }
        public virtual List<IGraphable> FindAll(Predicate<IGraphable> predicate)
        {
            return graphs.FindAll(predicate);
        }

        public virtual void Clear()
        {
            for (int i = graphs.Count - 1; i >= 0; i--)
                RemoveAt(i);
            OnValuesChanged(null);
        }

        public virtual int IndexOf(IGraphable graphable)
        {
            return graphs.IndexOf(graphable);
        }

        public virtual void Add(IGraphable newGraph)
        {
            graphs.Add(newGraph);
            newGraph.ValuesChanged += ValuesChangedSubscriber;
            OnValuesChanged(null);
        }

        public virtual void Insert(int index, IGraphable newGraph)
        {
            graphs.Insert(index, newGraph);
            newGraph.ValuesChanged += ValuesChangedSubscriber;
            OnValuesChanged(null);
        }

        public virtual bool Remove(IGraphable graph)
        {
            bool val = graphs.Remove(graph);
            if (val)
            {
                graph.ValuesChanged -= ValuesChangedSubscriber;
                OnValuesChanged(null);
            }
            return val;
        }
        public virtual void RemoveAt(int index)
        {
            IGraphable graphable = graphs[index];
            graphs.RemoveAt(index);
            graphable.ValuesChanged -= ValuesChangedSubscriber;
            OnValuesChanged(null);
        }

        protected virtual void ValuesChangedSubscriber(object sender, EventArgs e) { }

        public bool Contains(IGraphable item)
        {
            return graphs.Contains(item);
        }

        public void CopyTo(IGraphable[] array, int arrayIndex)
        {
            graphs.CopyTo(array, arrayIndex);
        }

        public IEnumerator<IGraphable> GetEnumerator()
        {
            return Graphables.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Graphables.GetEnumerator();
        }
    }

    public class GraphableCollection3 : GraphableCollection, IGraphable3
    {
        public virtual float ZMin { get; protected set; } = float.NaN;
        public virtual float ZMax { get; protected set; } = float.NaN;
        public float ZAxisScaler { get; set; } = 1;
        public Func<float, float> ZAxisScale { get; set; } = (v) => v;

        public ColorMap dominantColorMap = ColorMap.Jet_Dark;
        public int dominantColorMapIndex = -1;

        public GraphableCollection3() : base() { }
        public GraphableCollection3(IEnumerable<IGraphable> graphs) : base(graphs) { }

        public override bool RecalculateLimits()
        {
            float[] oldLimits = new float[] { XMin, XMax, YMin, YMax, ZMin, ZMax };
            XMin = XMax = YMin = YMax = ZMin = ZMax = float.NaN;
            dominantColorMap = null;

            for (int i = 0; i < graphs.Count; i++)
            {
                if (graphs[i] is Graphable3 surf)
                {
                    float zMin = surf.ZAxisScale(surf.ZMin);
                    float zMax = surf.ZAxisScale(surf.ZMax);
                    if (zMin < this.ZMin || float.IsNaN(this.ZMin)) this.ZMin = zMin;
                    if (zMax > this.ZMax || float.IsNaN(this.ZMax)) this.ZMax = zMax;
                    if (dominantColorMap == null)
                    {
                        dominantColorMap = surf.Color;
                        dominantColorMapIndex = i;
                    }
                }
                float xMin = graphs[i].XAxisScale(graphs[i].XMin);
                float xMax = graphs[i].XAxisScale(graphs[i].XMax);
                float yMin = graphs[i].YAxisScale(graphs[i].YMin);
                float yMax = graphs[i].YAxisScale(graphs[i].YMax);
                if (xMin < this.XMin || float.IsNaN(this.XMin)) this.XMin = xMin;
                if (xMax > this.XMax || float.IsNaN(this.XMax)) this.XMax = xMax;
                if (yMin < this.YMin || float.IsNaN(this.YMin)) this.YMin = yMin;
                if (yMax > this.YMax || float.IsNaN(this.YMax)) this.YMax = yMax;
            }

            if (dominantColorMap == null)
                dominantColorMap = ColorMap.Jet_Dark;
            
            if (!(oldLimits[0] == XMin && oldLimits[1] == XMax && oldLimits[2] == YMin && oldLimits[3] == YMax && oldLimits[4] == ZMin && oldLimits[5] == ZMax))
            {
                return true;
            }
            return false;
        }
    }
}
