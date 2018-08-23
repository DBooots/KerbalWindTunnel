using System;
using KerbalWindTunnel.Extensions;

namespace KerbalWindTunnel.Graphing
{
    public class SurfGraph : Graphable3
    {
        public override ColorMap Color { get; set; } = ColorMap.Jet_Dark;
        
        private float[,] _values;
        public float[,] Values
        {
            get { return _values; }
            set
            {
                _values = value;
                OnValuesChanged(null);
            }
        }

        public SurfGraph(float[,] values, float xLeft, float xRight, float yBottom, float yTop, bool scaleZToAxis = false)
        {
            this._values = values;
            this.XMin = xLeft;
            this.XMax = xRight;
            this.YMin = yBottom;
            this.YMax = yTop;
            this.ZMin = values.Min(true);
            this.ZMax = values.Max(true);
            this.ColorFunc = (x, y, z) => (z - ZMin) / (ZMax - ZMin);
            if (scaleZToAxis)
            {
                float axisMax = Axis.GetMax(ZMin, ZMax);
                float axisMin = Axis.GetMin(ZMin, ZMax);
                //this.ZAxisScaler = (ZMax - ZMin) / (Axis.GetMax(ZMin, ZMax) - Axis.GetMin(ZMin, ZMax));
                this.ColorFunc = (x, y, z) => (z - axisMin) / (axisMax - axisMin);
            }
        }
        
        public override void Draw(ref UnityEngine.Texture2D texture, float xLeft, float xRight, float yBottom, float yTop)
        {
            int width = texture.width - 1;
            int height = texture.height - 1;
            
            float graphStepX = (xRight - xLeft) / width;
            float graphStepY = (yTop - yBottom) / height;

            for (int x = 0; x <= width; x++)
            {
                for (int y = 0; y <= height; y++)
                {
                    float xF = x * graphStepX + xLeft;
                    float yF = y * graphStepY + yBottom;
                    if (xF < XMin || xF > XMax || yF < YMin || yF > YMax)
                        continue;
                    texture.SetPixel(x, y, this.Color[ColorFunc(xF, yF, ValueAt(xF, yF) * ZAxisScaler)]);
                }
            }

            texture.Apply();
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

        public void DrawMask(ref UnityEngine.Texture2D texture, float xLeft, float xRight, float yBottom, float yTop, Func<float, bool> maskCriteria, UnityEngine.Color maskColor, bool lineOnly = true, int lineWidth = 1)
        {
            int width = texture.width - 1;
            int height = texture.height - 1;

            float graphStepX = (xRight - xLeft) / width;
            float graphStepY = (yTop - yBottom) / height;

            for (int x = 0; x <= width; x++)
            {
                for (int y = 0; y <= height; y++)
                {
                    float xF = x * graphStepX + xLeft;
                    float yF = y * graphStepY + yBottom;

                    if (lineOnly)
                    {
                        float pixelValue = ValueAt(xF, yF);
                        bool mask = false;

                        if (!maskCriteria(pixelValue))
                        {
                            for (int w = 1; w <= lineWidth; w++)
                            {
                                if ((x >= w && maskCriteria(ValueAt((x - w) * graphStepX + xLeft, yF))) ||
                                    (x < width - w && maskCriteria(ValueAt((x + w) * graphStepX + xLeft, yF))) ||
                                    (y >= w && maskCriteria(ValueAt(xF, (y - w) * graphStepY + yBottom))) ||
                                    (y < height - w && maskCriteria(ValueAt(xF, (y + w) * graphStepY + yBottom))))
                                {
                                    mask = true;
                                    break;
                                }
                            }
                        }
                        if (mask)
                            texture.SetPixel(x, y, maskColor);
                        else
                            texture.SetPixel(x, y, UnityEngine.Color.clear);
                    }
                    else
                    {
                        if (!maskCriteria(ValueAt(xF, yF)) || xF < XMin || xF > XMax || yF < YMin || yF > YMax)
                            texture.SetPixel(x, y, maskColor);
                        else
                            texture.SetPixel(x, y, UnityEngine.Color.clear);
                    }
                }
            }

            texture.Apply();
        }

        public void SetValues(float[,] values, float xLeft, float xRight, float yBottom, float yTop, bool scaleZToAxis = false)
        {
            this._values = values;
            this.XMin = xLeft;
            this.XMax = xRight;
            this.YMin = yBottom;
            this.YMax = yTop;
            this.ZMin = values.Min(true);
            this.ZMax = values.Max(true);
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
