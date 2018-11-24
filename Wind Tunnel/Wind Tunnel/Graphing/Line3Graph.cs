using System;
using System.Linq;

namespace KerbalWindTunnel.Graphing
{
    public class Line3Graph : Graphable3, ILineGraph
    {
        public int LineWidth { get; set; } = 1;
        protected UnityEngine.Vector3[] _values;
        public UnityEngine.Vector3[] Values
        {
            get { return _values; }
            set
            {
                _values = value;
                if (_values.Length <= 0)
                {
                    YMin = YMax = XMin = XMax = ZMin = ZMax = 0;
                    return;
                }
                float xLeft = float.MaxValue;
                float xRight = float.MinValue;
                float yMin = float.MaxValue;
                float yMax = float.MinValue;
                float zMin = float.MaxValue;
                float zMax = float.MinValue;
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
                    if (!float.IsInfinity(value[i].z) && !float.IsNaN(value[i].z))
                    {
                        zMin = Math.Min(zMin, value[i].z);
                        zMax = Math.Max(zMax, value[i].z);
                    }
                }
                this.XMax = xRight;
                this.XMin = xLeft;
                this.YMax = yMax;
                this.YMin = yMin;
                this.ZMax = zMax;
                this.ZMin = zMin;

                OnValuesChanged(null);
            }
        }

        public Line3Graph(UnityEngine.Vector3[] values)
        {
            this.Values = values;
        }

        public override void Draw(ref UnityEngine.Texture2D texture, float xLeft, float xRight, float yBottom, float yTop)
        {
            if (!Visible) return;
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
            {
                DrawingHelper.DrawLine(ref texture, xPix[i], yPix[i], xPix[i + 1], yPix[i + 1], this.Color[ColorFunc(xPix[i], yPix[i], _values[i].z)], this.Color[ColorFunc(xPix[i + 1], yPix[i + 1], _values[i + 1].z)]);
                for (int w = 2; w <= LineWidth; w++)
                {
                    int l = w % 2 == 0 ? (-w) >> 1 : (w - 1) >> 1;
                    DrawingHelper.DrawLine(ref texture, xPix[i] + l, yPix[i], xPix[i + 1] + l, yPix[i + 1], this.Color[ColorFunc(xPix[i], yPix[i], _values[i].z)], this.Color[ColorFunc(xPix[i + 1], yPix[i + 1], _values[i + 1].z)]);
                    DrawingHelper.DrawLine(ref texture, xPix[i], yPix[i] + l, xPix[i + 1], yPix[i + 1] + l, this.Color[ColorFunc(xPix[i], yPix[i], _values[i].z)], this.Color[ColorFunc(xPix[i + 1], yPix[i + 1], _values[i + 1].z)]);
                }
            }

            texture.Apply();
        }

        public override float ValueAt(float x, float y)
            => ValueAt(x, y, 1, 1);
        public virtual float ValueAt(float x, float y, float width, float height)
        {
            if (_values.Length <= 0)
                return 0;

            if (Transpose) x = y;
            
            UnityEngine.Vector2 point = new UnityEngine.Vector2(x, y);
            UnityEngine.Vector2 closestPoint = new UnityEngine.Vector2(_values[0].x, _values[0].y);
            float currentDistance = float.PositiveInfinity;
            int length = _values.Length;
            for (int i = 0; i < length - 1 - 1; i++)
            {
                UnityEngine.Vector2 pt1 = new UnityEngine.Vector2(_values[i].x, _values[i].y);
                UnityEngine.Vector2 pt2 = new UnityEngine.Vector2(_values[i + 1].x, _values[i + 1].y);
                UnityEngine.Vector2 lineDir = (pt2 - pt1).normalized;
                UnityEngine.Vector2 closestPt = pt1 + UnityEngine.Vector2.Dot(point - pt1, lineDir) * lineDir;
                if (UnityEngine.Vector2.Dot(closestPt - pt1, lineDir) <= 0)
                {
                        closestPt = pt1;
                }
                else if ((closestPt - pt1).sqrMagnitude >= (pt2 - pt1).sqrMagnitude)
                {
                        closestPt = pt2;
                }
                UnityEngine.Vector2 LocalTransform(UnityEngine.Vector2 vector) => new UnityEngine.Vector2(vector.x / width, vector.y / height);
                float distance = (LocalTransform(point) - LocalTransform(closestPoint)).sqrMagnitude;
                if (distance < currentDistance)
                {
                    currentDistance = distance;
                    closestPoint = closestPt;
                }
            }
            return closestPoint.y;
        }

        public void SetValues(UnityEngine.Vector3[] values)
        {
            this.Values = values;
        }

        public override string GetFormattedValueAt(float x, float y, bool withName = false)
            => GetFormattedValueAt(x, y, 1, 1, withName);
        public virtual string GetFormattedValueAt(float x, float y, float width, float height, bool withName = false)
        {
            if (_values.Length <= 0) return "";
            return String.Format("{2}{0:" + StringFormat + "}{1}", ValueAt(x, y, width, height), ZUnit, withName && Name != "" ? Name + ": " : "");
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

            if (ZName != "")
                strCsv += String.Format(",{0} [{1}]", ZName, ZUnit != "" ? ZUnit : "-");
            else
                strCsv += String.Format(",{0}", ZUnit != "" ? ZUnit : "-");

            try
            {
                System.IO.File.AppendAllText(fullFilePath, strCsv + "\r\n");
            }
            catch (Exception ex) { UnityEngine.Debug.Log(ex.Message); }

            for (int i = 0; i < _values.Length; i++)
            {
                strCsv = String.Format("{0}, {1}, {2:" + StringFormat.Replace("N", "F") + "}", _values[i].x, _values[i].y, _values[i].z);

                try
                {
                    System.IO.File.AppendAllText(fullFilePath, strCsv + "\r\n");
                }
                catch (Exception) { }
            }
        }
    }
}
