using System;
using System.Linq;

namespace KerbalWindTunnel.Graphing
{
    public class MetaLineGraph : LineGraph, ILineGraph
    {
        protected float[][] metaData;
        public float[][] MetaData { get => metaData; set => SetMetaData(value); }
        public string[] MetaFields { get; set; } = new string[0];
        public IFormatProvider[] FormatProviders { get; set; } = new IFormatProvider[0];
        protected int metaCount = 0;
        public int MetaFieldCount { get => metaCount; }

        public MetaLineGraph(float[] values, float xLeft, float xRight) : base(values, xLeft, xRight)
        {
            metaData = new float[0][];
            MetaFields = new string[0];
        }
        public MetaLineGraph(UnityEngine.Vector2[] values) : base(values)
        {
            metaData = new float[0][];
            MetaFields = new string[0];
        }

        public MetaLineGraph(float[] values, float xLeft, float xRight, string[] metaFields, float[][] metaData)
            : base(values, xLeft, xRight)
        {
            metaCount = metaData.Length;
            this.metaData = metaData;
            this.MetaFields = new string[metaCount];
            metaFields.CopyTo(this.MetaFields, 0);
        }
        public MetaLineGraph(UnityEngine.Vector2[] values, string[] metaFields, float[][] metaData)
            : base(values)
        {
            metaCount = metaData.Length;
            this.metaData = metaData;
            this.MetaFields = new string[metaCount];
            metaFields.CopyTo(this.MetaFields, 0);
        }

        public MetaLineGraph(float[] values, float xLeft, float xRight, string[] metaFields, float[][] metaData, IFormatProvider[] formatProviders)
            : this(values, xLeft, xRight)
        {
            this.FormatProviders = new IFormatProvider[metaCount];
            formatProviders.CopyTo(this.FormatProviders, 0);
        }
        public MetaLineGraph(UnityEngine.Vector2[] values, string[] metaFields, float[][] metaData, IFormatProvider[] formatProviders)
            : this(values, metaFields, metaData)
        {
            this.FormatProviders = new IFormatProvider[metaCount];
            formatProviders.CopyTo(this.FormatProviders, 0);
        }
        
        public void SetMetaData(float[][] metaData) => SetMetaData(metaData, Values.Length);
        private void SetMetaData(float[][] metaData, int length)
        {
            for (int i = 0; i < metaData.Length; i++)
            {
                if (metaData[i].Length != length)
                    throw new ArgumentOutOfRangeException("metaData");
            }
            AdjustArrayLengths(metaData.Length);
            this.metaData = metaData;
        }
        public void SetValues(float[] values, float xLeft, float xRight, float[][] metaData)
        {
            SetMetaData(metaData, values.Length);
            SetValues(values, xLeft, xRight);
        }
        public void SetValues(UnityEngine.Vector2[] values, float[][] metaData)
        {
            SetMetaData(metaData, values.Length);
            SetValues(values);
        }

        private void AdjustArrayLengths(int length)
        {
            if (length > metaCount)
            {
                metaCount = length;
                string[] fields = new string[metaCount];
                MetaFields.CopyTo(fields, 0);
                MetaFields = fields;
                IFormatProvider[] formats = new IFormatProvider[metaCount];
                FormatProviders.CopyTo(formats, 0);
                FormatProviders = formats;
            }
            else
                metaCount = length;
        }

        public float MetaValueAt(float x, float y, int metaIndex)
            => MetaValueAt(x, y, 1, 1, metaIndex);
        public float MetaValueAt(float x, float y, float width, float height, int metaIndex)
        {
            if (metaIndex > metaCount)
                return 0;
            if (metaData[metaIndex].Length <= 0)
                return 0;

            if (Transpose) x = y;

            if (equalSteps && sorted)
            {
                if (x <= XMin) return metaData[metaIndex][0];

                int length = _values.Length - 1;
                if (x >= XMax) return metaData[metaIndex][length];

                float step = (XMax - XMin) / length;
                int index = (int)Math.Floor((x - XMin) / step);
                float f = (x - XMin) / step % 1;
                if (f == 0)
                    return metaData[metaIndex][index];
                return metaData[metaIndex][index] * (1 - f) + metaData[metaIndex][index + 1] * f;
            }
            else
            {
                if (sorted)
                {
                    //if (x <= Values[0].x)
                    //    return Values[0].y;
                    if (x >= _values[_values.Length - 1].x)
                        return metaData[metaIndex][_values.Length - 1];
                    for (int i = _values.Length - 2; i >= 0; i--)
                    {
                        if (x > _values[i].x)
                        {
                            float f = (x - _values[i].x) / (_values[i + 1].x - _values[i].x);
                            return metaData[metaIndex][i] * (1 - f) + metaData[metaIndex][i + 1] * f;
                        }
                    }
                    return metaData[metaIndex][0];
                }
                else
                {
                    int minX = 0, maxX = _values.Length - 1, length = _values.Length;
                    for (int i = 0; i < length - 1; i++)
                    {
                        if (x >= _values[i].x && x <= _values[i + 1].x)
                        {
                            float f = (x - _values[i].x) / (_values[i + 1].x - _values[i].x);
                            return metaData[metaIndex][i] * (1 - f) + metaData[metaIndex][i + 1] * f;
                        }
                        if (_values[i].x == XMax) maxX = i;
                        if (_values[i].x == XMin) minX = i;
                    }
                    if (x <= XMin) return metaData[metaIndex][minX];
                    if (x >= XMax) return metaData[metaIndex][maxX];
                    return metaData[metaIndex][0];
                }
            }
        }

        public override string GetFormattedValueAt(float x, float y, bool withName = false)
            => GetFormattedValueAt(x, y, 1, 1, withName);
        public override string GetFormattedValueAt(float x, float y, float width, float height, bool withName = false)
        {
            if (!Visible || Values.Length <= 0) return "";
            string value = base.GetFormattedValueAt(x, y, width, height, withName);
            for (int i = 0; i < metaCount; i++)
            {
                if (FormatProviders.Length >= i)
                    value += String.Format("\n{1}{0}", MetaValueAt(x, y, i).ToString(FormatProviders[i]), String.IsNullOrEmpty(MetaFields[i]) ? "" : MetaFields[i] + ": ");
                else
                    value += String.Format("\n{1}{0}", MetaValueAt(x, y, i).ToString(), String.IsNullOrEmpty(MetaFields[i]) ? "" : MetaFields[i] + ": ");
            }
            return value;
        }

        public override void WriteToFile(string filename, string sheetName = "")
        {

            if (_values.Length <= 0)
                return;

            if (!System.IO.Directory.Exists(WindTunnel.graphPath))
                System.IO.Directory.CreateDirectory(WindTunnel.graphPath);

            if (sheetName == "")
                sheetName = this.Name.Replace("/", "-").Replace("\\", "-");

            string fullFilePath = string.Format("{0}/{1}{2}.csv", WindTunnel.graphPath, filename, sheetName != "" ? "_" + sheetName : "");

            try
            {
                if (System.IO.File.Exists(fullFilePath))
                    System.IO.File.Delete(fullFilePath);
            }
            catch (Exception ex) { UnityEngine.Debug.LogFormat("Unable to delete file:{0}", ex.Message); }

            string strCsv = "";
            if (XName != "")
                strCsv += string.Format("{0} [{1}]", XName, XUnit != "" ? XUnit : "-");
            else
                strCsv += string.Format("{0}", XUnit != "" ? XUnit : "-");

            if (YName != "")
                strCsv += String.Format(",{0} [{1}]", YName, YUnit != "" ? YUnit : "-");
            else
                strCsv += String.Format(",{0}", YUnit != "" ? YUnit : "-");

            for (int i = 0; i < MetaFields.Length && i < metaCount; i++)
                strCsv += String.IsNullOrEmpty(MetaFields[i]) ? "," : "," + MetaFields[i];

            try
            {
                System.IO.File.AppendAllText(fullFilePath, strCsv + "\r\n");
            }
            catch (Exception ex) { UnityEngine.Debug.Log(ex.Message); }

            for (int i = 0; i < _values.Length; i++)
            {
                strCsv = String.Format("{0}, {1:" + StringFormat.Replace("N", "F") + "}", _values[i].x, _values[i].y);
                for (int j = 0; j < metaCount; j++)
                {
                    if (FormatProviders.Length >= j)
                        strCsv += "," + metaData[j][i].ToString(FormatProviders[j]);
                    else
                        strCsv += "," + metaData[j][i].ToString();
                }

                try
                {
                    System.IO.File.AppendAllText(fullFilePath, strCsv + "\r\n");
                }
                catch (Exception) { }
            }
        }
    }
}
