using System;

namespace KerbalWindTunnel.Graphing
{
    public interface IGraphable
    {
        string Name { get; }
        bool Visible { get; set; }
        bool DisplayValue { get; set; }
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
        public bool Visible
        {
            get => _visible;
            set
            {
                bool changed = _visible != value;
                _visible = value;
                if (changed) OnValuesChanged(null);
            }
        }
        private bool _visible = true;
        public bool DisplayValue { get; set; } = true;
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
