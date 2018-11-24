using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace KerbalWindTunnel.Graphing
{
    public class GraphableCollection : IGraphable, IList<IGraphable>
    {
        public string Name { get; set; } = "";
        public bool Visible
        {
            get => _visible;
            set
            {
                bool changed = _visible != value;
                _visible = value;
                if (changed)
                    OnValuesChanged(null);
            }
        }
        private bool _visible = true;
        public bool DisplayValue { get; set; } = true;
        public virtual float XMin { get; set; } = float.NaN;
        public virtual float XMax { get; set; } = float.NaN;
        public virtual float YMin { get; set; } = float.NaN;
        public virtual float YMax { get; set; } = float.NaN;
        public Func<float, float> XAxisScale { get; set; } = (v) => v;
        public Func<float, float> YAxisScale { get; set; } = (v) => v;

        protected Dictionary<string, IGraphable> graphDict = new Dictionary<string, IGraphable>(StringComparer.InvariantCultureIgnoreCase);
        protected List<IGraphable> graphs = new List<IGraphable>();

        public event EventHandler ValuesChanged;

        protected bool autoFitAxes = true;
        public virtual bool AutoFitAxes
        {
            get => autoFitAxes;
            set
            {
                autoFitAxes = value;
                for (int i = graphs.Count - 1; i >= 0; i--)
                    if (graphs[i] is GraphableCollection collection)
                        collection.AutoFitAxes = value;
            }
        }

        public string XUnit
        {
            get
            {
                int index = graphs.FindIndex(g => g.Visible);
                return index >= 0 ? graphs[index].XUnit : "";
            }
            set
            {
                for (int i = graphs.Count - 1; i >= 0; i--)
                    graphs[i].XUnit = value;
            }
        }
        public string YUnit
        {

            get
            {
                int index = graphs.FindIndex(g => g.Visible);
                return index >= 0 ? graphs[index].YUnit : "";
            }
            set
            {
                for (int i = graphs.Count - 1; i >= 0; i--)
                    graphs[i].YUnit = value;
            }
        }
        public string XName
        {
            get
            {
                int index = graphs.FindIndex(g => g.Visible);
                return index >= 0 ? graphs[index].XName : "";
            }
            set
            {
                for (int i = graphs.Count - 1; i >= 0; i--)
                    graphs[i].XName = value;
            }
        }
        public string YName
        {
            get
            {
                IGraphable yGraph = graphs.FirstOrDefault(g => g.Visible);
                if (yGraph == null) return "";
                if (yGraph is Graphable graph && graph.yName == null)
                {
                    string nameSubstring = GetNameSubstring();
                    if (nameSubstring != "")
                        return nameSubstring.Trim();
                }
                return yGraph.YName;
            }
            set
            {
                for (int i = graphs.Count - 1; i >= 0; i--)
                    graphs[i].YName = value;
            }
        }

        public IGraphable this[string name]
        {
            get => graphDict[name];
            set
            {
                if (value == null)
                    return;
                int index = graphs.FindIndex(g => g.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
                if (index < 0)
                    return;
                graphs[index].ValuesChanged -= ValuesChangedSubscriber;
                graphs[index] = value;
                graphDict[name] = value;
                graphs[index].ValuesChanged += ValuesChangedSubscriber;
                OnValuesChanged(null);
            }
        }

        public virtual IEnumerable<IGraphable> Graphables
        {
            get => graphs.ToList();
            set
            {
                for (int i = graphs.Count - 1; i >= 0; i--)
                    graphs[i].ValuesChanged -= ValuesChangedSubscriber;
                graphs.Clear();
                graphDict.Clear();
                //for (int i = value.Count - 1; i >= 0; i--)
                //    value[i].ValuesChanged += ValuesChangedSubscriber;
                //graphs = value;
                AddRange(value);
            }
        }

        public int Count { get => graphs.Count; }

        bool ICollection<IGraphable>.IsReadOnly => false;

        public IGraphable this[int index]
        {
            get => graphs[index];
            set
            {
                if (value == null)
                    return;
                graphs[index].ValuesChanged -= ValuesChangedSubscriber;
                graphs[index] = value;
                graphDict[graphDict.ElementAt(index).Key] = value;
                graphs[index].ValuesChanged += ValuesChangedSubscriber;
                OnValuesChanged(null);
            }
        }

        public GraphableCollection() { }
        public GraphableCollection(IGraphable graph)
        {
            Add(graph);
        }
        public GraphableCollection(IEnumerable<IGraphable> graphs)
        {
            AddRange(graphs);
        }

        public virtual void Draw(ref UnityEngine.Texture2D texture, float xLeft, float xRight, float yBottom, float yTop)
        {
            if (!Visible) return;
            for (int i = 0; i < graphs.Count; i++)
            {
                graphs[i].Draw(ref texture, xLeft, xRight, yBottom, yTop);
            }
        }
        public virtual void Draw(ref UnityEngine.Texture2D texture, float xLeft, float xRight, float yBottom, float yTop, float cMin, float cMax)
        {
            if (!Visible) return;
            for (int i = 0; i < graphs.Count; i++)
            {
                if (graphs[i] is SurfGraph surfGraph)
                    surfGraph.Draw(ref texture, xLeft, xRight, yBottom, yTop, cMin, cMax);
                else
                    graphs[i].Draw(ref texture, xLeft, xRight, yBottom, yTop);
            }
        }

        public virtual bool RecalculateLimits()
        {
            float[] oldLimits = new float[] { XMin, XMax, YMin, YMax};
            XMin = XMax = YMin = YMax = float.NaN;

            for (int i = 0; i < graphs.Count; i++)
            {
                if (!graphs[i].Visible) continue;
                float xMin, xMax, yMin, yMax;
                if (!autoFitAxes)
                {
                    xMin = graphs[i].XAxisScale(graphs[i].XMin);
                    xMax = graphs[i].XAxisScale(graphs[i].XMax);
                    yMin = graphs[i].YAxisScale(graphs[i].YMin);
                    yMax = graphs[i].YAxisScale(graphs[i].YMax);
                }
                else
                {
                    if (graphs[i] is LineGraph lineGraph)
                    {
                        GetLimitsAutoLine(lineGraph, out xMin, out xMax, out yMin, out yMax);
                    }
                    else if (graphs[i] is SurfGraph surfGraph)
                    {
                        GetLimitsAutoSurf(surfGraph, out xMin, out xMax, out yMin, out yMax);
                    }
                    else if (graphs[i] is OutlineMask outlineGraph)
                    {
                        xMin = XMin; xMax = XMax; yMin = YMin; yMax = YMax;
                    }
                    else
                    {
                        xMin = graphs[i].XAxisScale(graphs[i].XMin);
                        xMax = graphs[i].XAxisScale(graphs[i].XMax);
                        yMin = graphs[i].YAxisScale(graphs[i].YMin);
                        yMax = graphs[i].YAxisScale(graphs[i].YMax);
                    }
                }
                if (xMin < this.XMin || float.IsNaN(this.XMin)) this.XMin = xMin;
                if (xMax > this.XMax || float.IsNaN(this.XMax)) this.XMax = xMax;
                if (yMin < this.YMin || float.IsNaN(this.YMin)) this.YMin = yMin;
                if (yMax > this.YMax || float.IsNaN(this.YMax)) this.YMax = yMax;
            }
            if (XMin == float.NaN || XMax == float.NaN || YMin == float.NaN || YMax == float.NaN)
                XMin = XMax = YMin = YMax = 0;
            if (!(oldLimits[0] == XMin && oldLimits[1] == XMax && oldLimits[2] == YMin && oldLimits[3] == YMax))
            {
                return true;
            }
            return false;
        }

        protected void GetLimitsAutoSurf(SurfGraph surfGraph, out float xMin, out float xMax, out float yMin, out float yMax)
        {
            float[,] values = surfGraph.Values;
            int width = values.GetUpperBound(0);
            int height = values.GetUpperBound(1);
            bool breakFlag = false;
            int x, y;
            for (x = 0; x <= width; x++)
            {
                for (y = 0; y <= height; y++)
                    if (surfGraph.Color.Filter(values[x, y]))
                    {
                        breakFlag = true;
                        break;
                    }
                if (breakFlag) break;
            }
            xMin = (surfGraph.XMax - surfGraph.XMin) / width * x;

            breakFlag = false;
            for (x = width; x >= 0; x--)
            {
                for (y = 0; y <= height; y++)
                    if (surfGraph.Color.Filter(values[x, y]))
                    {
                        breakFlag = true;
                        break;
                    }
                if (breakFlag) break;
            }
            xMax = (surfGraph.XMax - surfGraph.XMin) / width * x;

            breakFlag = false;
            for (y = 0; y <= height; y++)
            {
                for (x = 0; x <= width; x++)
                    if (surfGraph.Color.Filter(values[x, y]))
                    {
                        breakFlag = true;
                        break;
                    }
                if (breakFlag) break;
            }
            yMin = (surfGraph.YMax - surfGraph.YMin) / height * y;

            breakFlag = false;
            for (y = height; y >= 0; y--)
            {
                for (x = 0; x <= width; x++)
                    if (surfGraph.Color.Filter(values[x, y]))
                    {
                        breakFlag = true;
                        break;
                    }
                if (breakFlag) break;
            }
            yMax = (surfGraph.YMax - surfGraph.YMin) / height * y;

            if (yMin > yMax)
            {
                yMin = surfGraph.YMin;
                yMax = surfGraph.YMax;
            }
            if (xMin > xMax)
            {
                xMin = surfGraph.XMin;
                xMax = surfGraph.XMax;
            }
        }

        /*protected void GetLimitsAutoOutline(OutlineMask outlineMask, out float xMin, out float xMax, out float yMin, out float yMax)
        {

        }*/

        protected void GetLimitsAutoLine(LineGraph lineGraph, out float xMin, out float xMax, out float yMin, out float yMax)
        {
            UnityEngine.Vector2[] values = lineGraph.Values;
            xMin = float.NaN;
            xMax = float.NaN;
            yMin = float.NaN;
            yMax = float.NaN;

            for (int i = values.Length - 1; i >= 0; i--)
            {
                if (lineGraph.Color.Filter(values[i].y) && !float.IsNaN(values[i].x) && !float.IsInfinity(values[i].x))
                {
                    if (float.IsNaN(xMin) || values[i].x < xMin) xMin = values[i].x;
                    if (float.IsNaN(xMax) || values[i].x > xMax) xMax = values[i].x;
                    if (float.IsNaN(yMin) || values[i].y < yMin) yMin = values[i].y;
                    if (float.IsNaN(yMax) || values[i].y > yMax) yMax = values[i].y;
                }
            }
        }

        public float ValueAt(float x, float y)
        {
            return ValueAt(x, y, Math.Max(graphs.FindIndex(g => g.Visible), 0));
        }

        public float ValueAt(float x, float y, int index = 0)
        {
            if (graphs.Count - 1 < index)
                return float.NaN;

            if (graphs[index] is ILineGraph lineGraph)
                return lineGraph.ValueAt(x, y, XMax - XMin, YMax - YMin);
            return graphs[index].ValueAt(x, y);
        }

        public string GetFormattedValueAt(float x, float y, bool withName = false) { return GetFormattedValueAt(x, y, -1, withName); }
        public string GetFormattedValueAt(float x, float y, int index = -1, bool withName = false)
        {
            if (graphs.Count == 0)
                return "";

            if (index >= 0)
            {
                if (graphs[index] is ILineGraph lineGraph)
                    return lineGraph.GetFormattedValueAt(x, y, XMax - XMin, YMax - YMin, withName);
                return graphs[index].GetFormattedValueAt(x, y, withName);
            }

            if (graphs.Count > 1)
                withName = true;

            string returnValue = "";
            for (int i = 0; i < graphs.Count; i++)
            {
                if (!graphs[i].Visible || !graphs[i].DisplayValue)
                    continue;
                string graphValue;
                if (graphs[i] is ILineGraph lineGraph)
                    graphValue = lineGraph.GetFormattedValueAt(x, y, XMax - XMin, YMax - YMin, withName);
                else
                    graphValue = graphs[i].GetFormattedValueAt(x, y, withName);
                if (graphValue != "" && returnValue != "")
                    returnValue += String.Format("\n{0}", graphValue);
                else
                    returnValue += String.Format("{0}", graphValue);
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
            List<IGraphable> visibleGraphs = graphs.Where(g => g.Visible).ToList();
            if (visibleGraphs.Count < 2)
                return "";
            int maxL = visibleGraphs[0].Name.Length;
            int commonL = 0;
            while (commonL < maxL && visibleGraphs[1].Name.StartsWith(visibleGraphs[0].Name.Substring(0, commonL + 1)))
                commonL++;
            string nameSubstring = visibleGraphs[0].Name.Substring(0, commonL);
            if (nameSubstring.EndsWith("("))
                nameSubstring = nameSubstring.Substring(0, nameSubstring.Length - 1);
            
            for(int i = 2; i < visibleGraphs.Count; i++)
            {
                if (!visibleGraphs[i].Name.StartsWith(nameSubstring))
                    return "";
            }
            return nameSubstring;
        }

        protected virtual void OnValuesChanged(EventArgs eventArgs)
        {
            RecalculateLimits();
            ValuesChanged?.Invoke(this, eventArgs);
        }

        public void SetVisibility(bool visible)
        {
            for (int i = graphs.Count - 1; i >= 0; i--)
                graphs[i].Visible = visible;
        }

        public void SetVisibilityExcept(bool visible, string exception)
        {
            this.SetVisibility(visible);
            this[exception].Visible = !visible;
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
                graphs[i].ValuesChanged -= ValuesChangedSubscriber;
            graphs.Clear();
            graphDict.Clear();
            OnValuesChanged(null);
        }

        public virtual int IndexOf(IGraphable graphable)
        {
            return graphs.IndexOf(graphable);
        }

        public virtual void Add(IGraphable newGraph)
        {
            if (newGraph == null)
                return;
            graphs.Add(newGraph);
            graphDict.Add(newGraph.Name, newGraph);
            newGraph.ValuesChanged += ValuesChangedSubscriber;
            OnValuesChanged(null);
        }

        public virtual void AddRange(IEnumerable<IGraphable> newGraphs)
        {
            IEnumerator<IGraphable> enumerator = newGraphs.GetEnumerator();
            while(enumerator.MoveNext())
            {
                if (enumerator.Current == null)
                    continue;
                graphs.Add(enumerator.Current);
                graphDict.Add(enumerator.Current.Name, enumerator.Current);
                enumerator.Current.ValuesChanged += ValuesChangedSubscriber;
            }
            OnValuesChanged(null);
        }

        public virtual void Insert(int index, IGraphable newGraph)
        {
            if (newGraph == null)
                return;
            graphs.Insert(index, newGraph);
            graphDict.Add(newGraph.Name, newGraph);
            newGraph.ValuesChanged += ValuesChangedSubscriber;
            OnValuesChanged(null);
        }

        public virtual bool Remove(IGraphable graph)
        {
            bool val = graphs.Remove(graph);
            if (val)
            {
                graphDict.Remove(graphDict.First(p => p.Value == graph).Key);
                graph.ValuesChanged -= ValuesChangedSubscriber;
                OnValuesChanged(null);
            }
            return val;
        }
        public virtual void RemoveAt(int index)
        {
            IGraphable graphable = graphs[index];
            graphs.RemoveAt(index);
            graphDict.Remove(graphDict.ElementAt(index).Key);
            graphable.ValuesChanged -= ValuesChangedSubscriber;
            OnValuesChanged(null);
        }

        protected virtual void ValuesChangedSubscriber(object sender, EventArgs e)
        { OnValuesChanged(null); }

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

        public void WriteToFile(string filename, string sheetName = "")
        {
            if (!System.IO.Directory.Exists(WindTunnel.graphPath))
                System.IO.Directory.CreateDirectory(WindTunnel.graphPath);

            if (graphs.Count > 1 && graphs.All(g => g is LineGraph || !g.Visible))
            {
                LineGraph[] lineGraphs = graphs.Select(g => g.Visible).Cast<LineGraph>().ToArray();
                UnityEngine.Vector2[] basis = lineGraphs[0].Values;
                int count = basis.Length;

                bool combinable = true;
                for (int i = lineGraphs.Length - 1; i >= 1; i--)
                {
                    UnityEngine.Vector2[] points = lineGraphs[i].Values;
                    for (int j = count - 1; i >= 0; i--)
                    {
                        if (points[j].x != basis[j].x)
                        {
                            combinable = false;
                            break;
                        }
                    }
                    if (!combinable)
                        break;
                }

                if (combinable)
                {
                    WriteLineGraphsToCombinedFile(filename, lineGraphs, sheetName);
                    return;
                }
            }

            for(int i = 0; i < graphs.Count; i++)
            {
                if (graphs.Count > 1 && graphs[i].Visible)
                    graphs[i].WriteToFile(filename, (sheetName != "" ? sheetName + "_" : "") + graphs[i].Name.Replace("/", "-").Replace("\\", "-"));
            }
        }

        protected void WriteLineGraphsToCombinedFile(string filename, LineGraph[] lineGraphs, string sheetName = "")
        {
            if (sheetName == "")
                sheetName = this.Name.Replace("/", "-").Replace("\\", "-");

            string fullFilePath = string.Format("{0}/{1}{2}.csv", WindTunnel.graphPath, filename, sheetName != "" ? "_" + sheetName : "");

            try
            {
                if (System.IO.File.Exists(fullFilePath))
                    System.IO.File.Delete(fullFilePath);
            }
            catch (Exception ex) { UnityEngine.Debug.LogFormat("Unable to delete file:{0}", ex.Message); }

            int count = lineGraphs.Length;
            string strCsv = "";
            if (lineGraphs[0].XName != "")
                strCsv += string.Format("{0} [{1}]", lineGraphs[0].XName, lineGraphs[0].XUnit != "" ? lineGraphs[0].XUnit : "-");
            else
                strCsv += string.Format("{0}", lineGraphs[0].XUnit != "" ? lineGraphs[0].XUnit : "-");

            for (int i = 0; i < count; i++)
            {
                if (lineGraphs[i].Name != "")
                    strCsv += string.Format(",{0} [{1}]", lineGraphs[i].Name, lineGraphs[i].YUnit != "" ? lineGraphs[i].YUnit : "-");
                else
                    strCsv += string.Format(",{0}", lineGraphs[i].YUnit != "" ? lineGraphs[i].YUnit : "-");
                if (lineGraphs[i] is MetaLineGraph metaLineGraph)
                {
                    for (int m = 0; m <= metaLineGraph.MetaFieldCount; m++)
                        strCsv += "," + (m > metaLineGraph.MetaFields.Length || String.IsNullOrEmpty(metaLineGraph.MetaFields[m]) ? "" : metaLineGraph.MetaFields[m]);
                }
            }

            try
            {
                System.IO.File.AppendAllText(fullFilePath, strCsv + "\r\n");
            }
            catch (Exception ex) { UnityEngine.Debug.Log(ex.Message); }

            IEnumerator<float> xEnumerator = lineGraphs[0].Values.Select(v => v.x).GetEnumerator();
            int j = -1;
            while (xEnumerator.MoveNext())
            {
                j++;
                strCsv = string.Format("{0}", xEnumerator.Current);
                for (int i = 0; i < count; i++)
                {
                    strCsv += string.Format(",{0:" + lineGraphs[i].StringFormat.Replace("N", "F") + "}", lineGraphs[i].Values[j].y);
                    if (lineGraphs[i] is MetaLineGraph metaLineGraph)
                    {
                        for (int m = 0; m <= metaLineGraph.MetaFieldCount; m++)
                        {
                            if (metaLineGraph.MetaStringFormats.Length >= m)
                                strCsv += "," + metaLineGraph.MetaData[m][j].ToString(metaLineGraph.MetaStringFormats[m].Replace("N", "F"));
                            else
                                strCsv += "," + metaLineGraph.MetaData[m][j].ToString();
                        }
                    }
                }
                try
                {
                    System.IO.File.AppendAllText(fullFilePath, strCsv + "\r\n");
                }
                catch (Exception) { }
            }
        }
    }

    public class GraphableCollection3 : GraphableCollection, IGraphable3
    {
        public virtual float ZMin { get; set; } = float.NaN;
        public virtual float ZMax { get; set; } = float.NaN;
        public float ZAxisScaler { get; set; } = 1;

        public string ZUnit
        {
            get
            {
                IGraphable3 graphable3 = (IGraphable3)graphs.FirstOrDefault(g => g is IGraphable3 && g.Visible);
                return graphable3 != null ? graphable3.ZUnit : "";
            }
            set
            {
                for (int i = graphs.Count - 1; i >= 0; i--)
                    if (graphs[i] is IGraphable3)
                        ((IGraphable3)graphs[i]).ZUnit = value;
            }
        }
        public string ZName
        {
            get
            {
                IGraphable3 graphable3 = (IGraphable3)graphs.FirstOrDefault(g => g is IGraphable3 && g.Visible);
                return graphable3 != null ? graphable3.ZName : "";
            }
            set
            {
                for (int i = graphs.Count - 1; i >= 0; i--)
                    if (graphs[i] is IGraphable3)
                        ((IGraphable3)graphs[i]).ZName = value;
            }
        }

        public ColorMap dominantColorMap = ColorMap.Jet_Dark;
        public int dominantColorMapIndex = -1;

        public GraphableCollection3() : base() { }
        public GraphableCollection3(IGraphable graph) : base(graph) { }
        public GraphableCollection3(IEnumerable<IGraphable> graphs) : base(graphs) { }

        public override bool RecalculateLimits()
        {
            float[] oldLimits = new float[] { XMin, XMax, YMin, YMax, ZMin, ZMax };
            XMin = XMax = YMin = YMax = ZMin = ZMax = float.NaN;
            dominantColorMap = null;

            for (int i = 0; i < graphs.Count; i++)
            {
                if (!graphs[i].Visible) continue;
                if (graphs[i] is Graphable3 surf)
                {
                    float zMin = surf.ZMin;
                    float zMax = surf.ZMax;
                    if (zMin < this.ZMin || float.IsNaN(this.ZMin)) this.ZMin = zMin;
                    if (zMax > this.ZMax || float.IsNaN(this.ZMax)) this.ZMax = zMax;
                    if (dominantColorMap == null)
                    {
                        dominantColorMap = surf.Color;
                        dominantColorMapIndex = i;
                    }
                }

                float xMin, xMax, yMin, yMax;
                if (!autoFitAxes)
                {
                    xMin = graphs[i].XAxisScale(graphs[i].XMin);
                    xMax = graphs[i].XAxisScale(graphs[i].XMax);
                    yMin = graphs[i].YAxisScale(graphs[i].YMin);
                    yMax = graphs[i].YAxisScale(graphs[i].YMax);
                }
                else
                {
                    if (graphs[i] is LineGraph lineGraph)
                    {
                        GetLimitsAutoLine(lineGraph, out xMin, out xMax, out yMin, out yMax);
                    }
                    else if (graphs[i] is SurfGraph surfGraph)
                    {
                        GetLimitsAutoSurf(surfGraph, out xMin, out xMax, out yMin, out yMax);
                    }
                    else if (graphs[i] is OutlineMask outlineGraph)
                    {
                        xMin = XMin; xMax = XMax; yMin = YMin; yMax = YMax;
                    }
                    else
                    {
                        xMin = graphs[i].XAxisScale(graphs[i].XMin);
                        xMax = graphs[i].XAxisScale(graphs[i].XMax);
                        yMin = graphs[i].YAxisScale(graphs[i].YMin);
                        yMax = graphs[i].YAxisScale(graphs[i].YMax);
                    }
                }

                if (xMin < this.XMin || float.IsNaN(this.XMin)) this.XMin = xMin;
                if (xMax > this.XMax || float.IsNaN(this.XMax)) this.XMax = xMax;
                if (yMin < this.YMin || float.IsNaN(this.YMin)) this.YMin = yMin;
                if (yMax > this.YMax || float.IsNaN(this.YMax)) this.YMax = yMax;
            }

            if (dominantColorMap == null)
                dominantColorMap = ColorMap.Jet_Dark;

            if (XMin == float.NaN || XMax == float.NaN || YMin == float.NaN || YMax == float.NaN || ZMin == float.NaN || ZMax == float.NaN)
                XMin = XMax = YMin = YMax = ZMin = ZMax = 0;
            if (!(oldLimits[0] == XMin && oldLimits[1] == XMax && oldLimits[2] == YMin && oldLimits[3] == YMax && oldLimits[4] == ZMin && oldLimits[5] == ZMax))
            {
                return true;
            }
            return false;
        }
    }
}
