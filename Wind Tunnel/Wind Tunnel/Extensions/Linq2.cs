using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KerbalWindTunnel.Extensions
{
    public static class Linq2
    {
        public static TResult[,] SelectToArray<TInput, TResult>(this TInput[,] vals, Func<TInput, TResult> selector)
        {
            int bound0 = vals.GetUpperBound(0);
            int bound1 = vals.GetUpperBound(1);
            TResult[,] results = new TResult[bound0 + 1, bound1 + 1];

            for (int i = 0; i <= bound0; i++)
            {
                for(int j = 0; j<=bound1; j++)
                {
                    results[i, j] = selector(vals[i, j]);
                }
            }

            return results;
        }
        public static float Max(this float[,] vals, bool excludeInfinity = false)
        {
            int bound0 = vals.GetUpperBound(0);
            int bound1 = vals.GetUpperBound(1);
            float result = float.MinValue;
            for(int i = 0; i < bound0; i++)
            {
                for (int j = 0; j < bound1; j++)
                {
                    if (excludeInfinity && float.IsPositiveInfinity(vals[i, j]))
                        continue;
                    if (vals[i, j] > result && !float.IsNaN(vals[i, j]))
                        result = vals[i, j];
                }
            }
            return result;
        }
        public static float Min(this float[,] vals, bool excludeInfinity = false)
        {
            int bound0 = vals.GetUpperBound(0);
            int bound1 = vals.GetUpperBound(1);
            float result = float.MaxValue;
            for (int i = 0; i < bound0; i++)
            {
                for (int j = 0; j < bound1; j++)
                {
                    if (excludeInfinity && float.IsNegativeInfinity(vals[i, j]))
                        continue;
                    if (vals[i, j] < result && !float.IsNaN(vals[i, j]))
                        result = vals[i, j];
                }
            }
            return result;
        }
        public static float Lerp2(this float[,] vals, float x, float y)
        {
            int xI1, xI2;
            float fX;
            if (x <= 0)
            {
                xI1 = xI2 = 0;
                fX = 0;
            }
            else
            {
                int lengthX = vals.GetUpperBound(0);
                if (x >= 1)
                {
                    xI1 = xI2 = lengthX;
                    fX = 1;
                }
                else
                {
                    float stepX = 1f / lengthX;
                    xI1 = (int)Math.Floor(x / stepX);
                    fX = x / stepX % 1;
                    xI2 = xI1 + 1;
                    if (fX == 0)
                        xI2 = xI1;
                    else
                        xI2 = xI1 + 1;
                }
            }

            if (y <= 0)
            {
                if (xI1 == xI2) return vals[xI1, 0];
                return vals[xI1, 0] * (1 - fX) + vals[xI2, 0] * fX;
            }
            else
            {
                int lengthY = vals.GetUpperBound(1);
                if (y >= 1)
                {
                    if (xI1 == xI2) return vals[xI1, 0];
                    return vals[xI1, lengthY] * (1 - fX) + vals[xI2, lengthY] * fX;
                }
                else
                {
                    float stepY = 1f / lengthY;
                    int yI1 = (int)Math.Floor(y / stepY);
                    float fY = y / stepY % 1;
                    int yI2;
                    if (fY == 0)
                        yI2 = yI1;
                    else
                        yI2 = yI1 + 1;

                    if (xI1 == xI2 && yI1 == yI2)
                        return vals[xI1, yI1];
                    else if (xI1 == xI2)
                        return vals[xI1, yI1] * (1 - fY) + vals[xI1, yI2] * fY;
                    else if (yI1 == yI2)
                        return vals[xI1, yI1] * (1 - fX) + vals[xI2, yI1] * fX;

                    return vals[xI1, yI1] * (1 - fX) * (1 - fY) +
                        vals[xI2, yI1] * fX * (1 - fY) +
                        vals[xI1, yI2] * (1 - fX) * fY +
                        vals[xI2, yI2] * fX * fY;
                }
            }
        }
    }
}
