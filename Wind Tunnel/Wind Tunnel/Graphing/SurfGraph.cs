using System;
using KerbalWindTunnel.Extensions;

namespace KerbalWindTunnel.Graphing
{
    public class SurfGraph : IGraphable
    {
        public string Name { get; set; } = "";
        public float XMin { get; private set; }
        public float XMax { get; private set; }
        public float YMin { get; private set; }
        public float YMax { get; private set; }
        public float ZMin { get; private set; }
        public float ZMax { get; private set; }
        public ColorMap Color { get; set; } = ColorMap.Jet_Dark;
        public Graph.CoordsToColorFunc ColorFunc { get; set; }
        public bool Transpose { get; set; } = false;
        public string Unit { get; set; }
        public string StringFormat { get; set; } = "G";
        private float[,] _values;
        public float[,] Values
        {
            get { return _values; }
            set
            {
                _values = value;
                ValuesChanged.Invoke(this, null);
            }
        }
        public event EventHandler ValuesChanged;

        public SurfGraph(float[,] values, float xLeft, float xRight, float yBottom, float yTop)
        {
            this._values = values;
            this.XMin = xLeft;
            this.XMax = xRight;
            this.YMin = yBottom;
            this.YMax = yTop;
            this.ZMin = values.Min(true);
            this.ZMax = values.Max(true);
            this.ColorFunc = (x, y, z) => (z - ZMin) / (ZMax - ZMin);
        }
        
        public void Draw(ref UnityEngine.Texture2D texture, float xLeft, float xRight, float yBottom, float yTop)
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
                    texture.SetPixel(x, y, this.Color[ColorFunc(xF, yF, ValueAt(xF, yF))]);
                }
            }

            texture.Apply();
        }

        public float ValueAt(float x, float y)
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

        public void SetValues(float[,] values, float xLeft, float xRight, float yBottom, float yTop)
        {
            this.XMin = xLeft;
            this.XMax = xRight;
            this.YMin = yBottom;
            this.YMax = yTop;
            this.ZMin = values.Min(true);
            this.ZMax = values.Max(true);
            this.Values = values;
        }
    }
}
