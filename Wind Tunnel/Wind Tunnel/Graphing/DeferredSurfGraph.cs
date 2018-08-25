using KerbalWindTunnel.Extensions;
using System;
using System.Linq;

namespace KerbalWindTunnel.Graphing
{
    public class DeferredSurfGraph<T> : SurfGraphBase
    {
        protected DeferredArray<T, float> _values;

        public override float[,] Values
        {
            get => _values.ToArray();
            set => throw new NotImplementedException();
        }

        internal override float this[int i, int j] => _values[i,j];
        internal override int GetUpperBound(int dimension) => _values.GetUpperBound(dimension);

        public DeferredSurfGraph(T[,] origin, Func<T, float> selector, float xLeft, float xRight, float yBottom, float yTop, bool scaleZToAxis = false) : base(xLeft, xRight, yBottom, yTop)
        {
            this._values = new DeferredArray<T, float>(origin, selector);
            this.ZMin = _values.Min(true);
            this.ZMax = _values.Max(true);
            this.ColorFunc = (x, y, z) => (z - ZMin) / (ZMax - ZMin);
            if (scaleZToAxis)
            {
                float axisMax = Axis.GetMax(ZMin, ZMax);
                float axisMin = Axis.GetMin(ZMin, ZMax);
                //this.ZAxisScaler = (ZMax - ZMin) / (Axis.GetMax(ZMin, ZMax) - Axis.GetMin(ZMin, ZMax));
                this.ColorFunc = (x, y, z) => (z - axisMin) / (axisMax - axisMin);
            }
        }

        public override float ValueAt(float x, float y)
        {
            if (Transpose)
            {
                float temp = x;
                x = y;
                y = temp;
            }

            int xI1, xI2;
            float fX;
            if (x <= XMin)
            {
                xI1 = xI2 = 0;
                fX = 0;
            }
            else
            {
                int lengthX = _values.GetUpperBound(0);
                if (x >= XMax)
                {
                    xI1 = xI2 = lengthX;
                    fX = 1;
                }
                else
                {
                    float stepX = (XMax - XMin) / lengthX;
                    xI1 = (int)Math.Floor((x - XMin) / stepX);
                    fX = (x - XMin) / stepX % 1;
                    xI2 = xI1 + 1;
                    if (fX == 0)
                        xI2 = xI1;
                    else
                        xI2 = xI1 + 1;
                }
            }

            if (y <= YMin)
            {
                if (xI1 == xI2) return _values[xI1, 0];
                return _values[xI1, 0] * (1 - fX) + _values[xI2, 0] * fX;
            }
            else
            {
                int lengthY = _values.GetUpperBound(1);
                if (y >= YMax)
                {
                    if (xI1 == xI2) return _values[xI1, 0];
                    return _values[xI1, lengthY] * (1 - fX) + _values[xI2, lengthY] * fX;
                }
                else
                {
                    float stepY = (YMax - YMin) / lengthY;
                    int yI1 = (int)Math.Floor((y - YMin) / stepY);
                    float fY = (y - YMin) / stepY % 1;
                    int yI2;
                    if (fY == 0)
                        yI2 = yI1;
                    else
                        yI2 = yI1 + 1;

                    if (xI1 == xI2 && yI1 == yI2)
                        return _values[xI1, yI1];
                    else if (xI1 == xI2)
                        return _values[xI1, yI1] * (1 - fY) + _values[xI1, yI2] * fY;
                    else if (yI1 == yI2)
                        return _values[xI1, yI1] * (1 - fX) + _values[xI2, yI1] * fX;

                    return _values[xI1, yI1] * (1 - fX) * (1 - fY) +
                        _values[xI2, yI1] * fX * (1 - fY) +
                        _values[xI1, yI2] * (1 - fX) * fY +
                        _values[xI2, yI2] * fX * fY;
                }
            }
        }

        public void SetValues(T[,] origin, Func<T, float> selector, float xLeft, float xRight, float yBottom, float yTop, bool scaleZToAxis = false)
        {
            _values = new DeferredArray<T, float>(origin, selector);
            this.XMin = xLeft;
            this.XMax = xRight;
            this.YMin = yBottom;
            this.YMax = yTop;
            this.ZMin = _values.Min(true);
            this.ZMax = _values.Max(true);
            this.ColorFunc = (x, y, z) => (z - ZMin) / (ZMax - ZMin);
            if (scaleZToAxis)
            {
                float axisMax = Axis.GetMax(ZMin, ZMax);
                float axisMin = Axis.GetMin(ZMin, ZMax);
                //this.ZAxisScaler = (ZMax - ZMin) / (Axis.GetMax(ZMin, ZMax) - Axis.GetMin(ZMin, ZMax));
                this.ColorFunc = (x, y, z) => (z - axisMin) / (axisMax - axisMin);
            }
            OnValuesChanged(null);
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

            int height = _values.GetUpperBound(1);
            int width = _values.GetUpperBound(0);
            float xStep = (XMax - XMin) / width;
            float yStep = (YMax - YMin) / height;

            string strCsv;
            if (Name != "")
                strCsv = String.Format("{0} [{1}]", Name, ZUnit != "" ? ZUnit : "-");
            else
                strCsv = String.Format("{0}", ZUnit != "" ? ZUnit : "-");

            for (int x = 0; x <= width; x++)
                strCsv += String.Format(",{0}", xStep * x);

            try
            {
                System.IO.File.AppendAllText(fullFilePath, strCsv + "\r\n");
            }
            catch (Exception) { }

            for (int y = height; y >= 0; y--)
            {
                strCsv = string.Format("{0}", y * yStep);
                for (int x = 0; x <= width; x++)
                    strCsv += string.Format(",{0:" + StringFormat.Replace("N", "F") + "}", _values[x, y]);

                try
                {
                    System.IO.File.AppendAllText(fullFilePath, strCsv + "\r\n");
                }
                catch (Exception) { }
            }
        }
    }
}
