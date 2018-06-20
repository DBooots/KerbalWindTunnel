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
    }
}
