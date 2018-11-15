using System;
using System.Collections.Generic;
using System.Linq;

namespace KerbalWindTunnel.Extensions
{
    public static class Linq2
    {
        public static T[,] Subset<T>(this T[,] vals, int lowerBound0, int upperBound0, int lowerBound1, int upperBound1)
        {
            T[,] result = new T[upperBound0 - lowerBound0 + 1, upperBound1 - lowerBound1 + 1];
            for (int i = lowerBound0; i <= upperBound0; i++)
            {
                for (int j = lowerBound1; j <= upperBound1; j++)
                {
                    result[i - lowerBound0, j - lowerBound1] = vals[i, j];
                }
            }
            return result;
        }
        public static T[] Subset<T>(this T[] vals, int lowerBound, int upperBound)
        {
            T[] result = new T[upperBound - lowerBound + 1];
            for (int i = lowerBound; i <= upperBound; i++)
            {
                result[i - lowerBound] = vals[i];
            }
            return result;
        }

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
            if (bound0 < 0 || bound1 < 0)
                return 0;
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
            if (bound0 < 0 || bound1 < 0)
                return 0;
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
        public static int First<T>(this T[,] vals, int dimension, int index, Predicate<T> predicate)
        {
            int limit = vals.GetUpperBound(dimension);
            if (limit < 0)
                return 0;
            if (dimension == 0)
            {
                for (int i = 0; i <= limit; i++)
                    if (predicate(vals[i, index]))
                        return i;
            }
            else if (dimension == 1)
            {
                for (int i = 0; i <= limit; i++)
                    if (predicate(vals[index, i]))
                        return i;
            }
            else
                throw new ArgumentOutOfRangeException("dimension");
            return -1;
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
                if (lengthX < 0)
                    return 0;
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
                if (lengthY < 0)
                    return 0;
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
        public static IEnumerator<T> GetTaxicabNeighbors<T>(this T[,] vals, int startX, int startY, int maxRange = -1)
        {
            int width = vals.GetUpperBound(0);
            int height = vals.GetUpperBound(1);
            if (startX < 0 || startX > width || startY < 0 || startY > height)
                yield break;

            yield return vals[startX, startY];
            if (maxRange < 0) maxRange = width + height;
            for (int r = 1; r <= maxRange; r++)
            {
                for (int r2 = 0; r2 < r; r++)
                {
                    if (startY + r <= height && startX + r2 <= width) yield return vals[startX + r2, startY + r];
                    if (startX + r <= width && startY - r2 >= 0) yield return vals[startX + r, startY - r2];
                    if (startY - r >= 0 && startX - r2 >= 0) yield return vals[startX - r2, startY - r];
                    if (startX - r >= 0 && startY + r2 <= height) yield return vals[startX - r, startY + r2];
                }
            }
        }
        public static IEnumerator<T> GetTaxicabNeighbors<T>(this T[,] vals, int startX, int startY, int maxRange = -1,
            params Quadrant[] quadrants)
        {
            bool[] quads = new bool[4];
            for (int i = 0; i < quadrants.Length; i++)
                if ((int)quadrants[i] - 1 >= 0)
                    quads[(int)quadrants[i] - 1] = true;

            return GetTaxicabNeighbors(vals, startX, startY, maxRange, quads);
        }
        public static IEnumerator<T> GetTaxicabNeighbors<T>(this T[,] vals, int startX, int startY, int maxRange,
            bool[] quads)
        {
            int width = vals.GetUpperBound(0);
            int height = vals.GetUpperBound(1);
            if (startX < 0 || startX > width || startY < 0 || startY > height)
                yield break;

            yield return vals[startX, startY];
            if (maxRange < 0) maxRange = width + height;
            for (int r = 1; r <= maxRange; r++)
            {
                for (int r2 = 0; r2 < r; r++)
                {
                    if (quads[0] && startY + r <= height && startX + r2 <= width) yield return vals[startX + r2, startY + r];
                    if (quads[3] && startX + r <= width && startY - r2 >= 0) yield return vals[startX + r, startY - r2];
                    if (quads[2] && startY - r >= 0 && startX - r2 >= 0) yield return vals[startX - r2, startY - r];
                    if (quads[1] && startX - r >= 0 && startY + r2 <= height) yield return vals[startX - r, startY + r2];
                }
            }
        }

        public enum Quadrant : int
        {
            I = 1,
            II = 2,
            III = 3,
            IV = 4,
            Default = 0
        }
    }
}
