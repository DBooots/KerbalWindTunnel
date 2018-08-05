using System;
using System.Linq;

namespace KerbalWindTunnel.Graphing
{
    public class LineGraph : Graphable
    {
        public UnityEngine.Vector2[] _values;
        public UnityEngine.Vector2[] Values
        {
            get { return _values; }
            private set
            {
                _values = value;
                float xLeft = float.MaxValue;
                float xRight = float.MinValue;
                float yMin = float.MaxValue;
                float yMax = float.MinValue;
                for (int i = value.Length - 1; i >= 0; i--)
                {
                    if (!float.IsInfinity(value[i].x) && !float.IsNaN(value[i].x))
                    {
                        xLeft = Math.Min(xLeft, value[i].x);
                        xRight = Math.Max(xRight, value[i].x);
                    }
                    if (!float.IsInfinity(value[i].y) && !float.IsNaN(value[i].y))
                    {
                        yMin = Math.Min(yMin, value[i].y);
                        yMax = Math.Max(yMax, value[i].y);
                    }
                }
                this.XMax = xRight;
                this.XMin = xLeft;
                this.YMax = yMax;
                this.YMin = yMin;

                float step = (xRight - xLeft) / (value.Length - 1);
                this.sorted = true;
                this.equalSteps = true;
                for (int i = value.Length - 1; i >= 0; i--)
                {
                    if (equalSteps && _values[i].x != xLeft + step * i)
                        equalSteps = false;
                    if (sorted && i > 0 && _values[i].x < _values[i - 1].x)
                        sorted = false;
                    if (!equalSteps && !sorted)
                        break;
                }

                OnValuesChanged(null);
            }
        }
        private bool sorted = false;
        private bool equalSteps = false;

        public LineGraph(float[] values, float xLeft, float xRight)
        {
            SetValues(values, xLeft, xRight);
        }
        public LineGraph(UnityEngine.Vector2[] values)
        {
            this.Values = values;
        }
        
        public override void Draw(ref UnityEngine.Texture2D texture, float xLeft, float xRight, float yBottom, float yTop)
        {
            float xRange = xRight - xLeft;
            float yRange = yTop - yBottom;
            int width = texture.width;
            int height = texture.height;
            int[] xPix, yPix;
            // TODO: Add robustness for NaNs and Infinities.
            if (!Transpose)
            {
                xPix = _values.Select(vect => UnityEngine.Mathf.RoundToInt((vect.x - xLeft) / xRange * width)).ToArray();
                yPix = _values.Select(vect => UnityEngine.Mathf.RoundToInt((vect.y - yBottom) / yRange * height)).ToArray();
            }
            else
            {
                xPix = _values.Select(vect => UnityEngine.Mathf.RoundToInt((vect.y - yBottom) / yRange * width)).ToArray();
                yPix = _values.Select(vect => UnityEngine.Mathf.RoundToInt((vect.x - xLeft) / xRange * height)).ToArray();
            }

            for (int i = _values.Length - 2; i >= 0; i--)
                Extensions.DrawingHelper.DrawLine(ref texture, xPix[i], yPix[i], xPix[i + 1], yPix[i + 1], this.Color[ColorFunc((xPix[i] + xPix[i + 1]) / 2, (yPix[i] + yPix[i + 1]) / 2, 0)]);

            texture.Apply();
        }

        public override float ValueAt(float x, float y)
        {
            if (Transpose) x = y;

            if(equalSteps && sorted)
            {
                if (x <= XMin) return _values[0].y;

                int length = _values.Length - 1;
                if (x >= XMax) return _values[length].y;

                float step = (XMax - XMin) / length;
                int index = (int)Math.Floor((x - XMin) / step);
                float f = (x - XMin) / step % 1;
                if (f == 0)
                    return _values[index].y;
                return _values[index].y * (1 - f) + _values[index + 1].y * f;
            }
            else
            {
                if (sorted)
                {
                    //if (x <= Values[0].x)
                    //    return Values[0].y;
                    if (x >= _values[_values.Length - 1].x)
                        return _values[_values.Length - 1].y;
                    for (int i = _values.Length - 2; i >= 0; i--)
                    {
                        if (x > _values[i].x)
                        {
                            float f = (x - _values[i].x) / (_values[i + 1].x - _values[i].x);
                            return _values[i].y * (1 - f) + _values[i + 1].y * f;
                        }
                    }
                    return _values[0].y;
                }
                else
                {
                    int minX = 0, maxX = _values.Length - 1, length = _values.Length;
                    for (int i = 0; i < length - 1; i++)
                    {
                        if(x >= _values[i].x && x <= _values[i+1].x)
                        {
                            float f = (x - _values[i].x) / (_values[i + 1].x - _values[i].x);
                            return _values[i].y * (1 - f) + _values[i + 1].y * f;
                        }
                        if (_values[i].x == XMax) maxX = i;
                        if (_values[i].x == XMin) minX = i;
                    }
                    if (x <= XMin) return _values[minX].y;
                    if (x >= XMax) return _values[maxX].y;
                    return _values[0].y;
                }
            }
        }

        public void SetValues(float[] values, float xLeft, float xRight)
        {
            this._values = new UnityEngine.Vector2[values.Length];
            float xStep = (xRight - xLeft) / (values.Length - 1);
            float yMin = float.MaxValue;
            float yMax = float.MinValue;
            for (int i = values.Length - 1; i >= 0; i--)
            {
                this._values[i] = new UnityEngine.Vector2(xLeft + xStep * i, values[i]);
                if (!float.IsInfinity(_values[i].y) && !float.IsNaN(_values[i].y))
                {
                    yMin = Math.Min(yMin, _values[i].y);
                    yMax = Math.Max(yMax, _values[i].y);
                }
            }
            this.XMax = xRight;
            this.XMin = xLeft;
            this.YMax = yMax;
            this.YMin = yMin;
            this.sorted = true;
            this.equalSteps = true;

            OnValuesChanged(null);
        }
        public void SetValues(UnityEngine.Vector2[] values)
        {
            this.Values = values;
        }

        public override void WriteToFile(string filename, string sheetName = "")
        {
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

            if (Name != "")
                strCsv += String.Format(",{0} [{1}]", Name, YUnit != "" ? YUnit : "-");
            else
                strCsv += String.Format(",{0}", YUnit != "" ? YUnit : "-");

            try
            {
                System.IO.File.AppendAllText(fullFilePath, strCsv + "\r\n");
            }
            catch (Exception ex) { UnityEngine.Debug.Log(ex.Message); }

            for (int i = 0; i < _values.Length; i++)
            {
                strCsv = String.Format("{0}, {1:" + StringFormat.Replace("N","F") + "}", _values[i].x, _values[i].y);

                try
                {
                    System.IO.File.AppendAllText(fullFilePath, strCsv + "\r\n");
                }
                catch (Exception) { }
            }
        }
    }
}
