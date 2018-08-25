using System;
namespace KerbalWindTunnel.Extensions
{
    public class DeferredArray<TOrigin, TOutput> where TOutput : IComparable<TOutput>
    {
        private readonly TOrigin[,] origin;
        public Func<TOrigin, TOutput> selector;

        public DeferredArray(TOrigin[,] array, Func<TOrigin, TOutput> selector)
        {
            this.origin = array;
            this.selector = selector;
        }
        private DeferredArray() { }

        public int Length
        {
            get => origin.Length;
        }

        public TOutput this[int i, int j]
        {
            get { return selector(origin[i, j]); }
        }

        public TOutput[,] ToArray()
        {
            return origin.SelectToArray(selector);
        }

        public TOutput Max()
        {
            TOutput max = selector(origin[0, 0]);
            foreach (TOrigin o in origin)
            {
                TOutput v = selector(o);
                if (v.CompareTo(max) == 1)
                    max = v;
            }
            return max;
        }

        public int GetUpperBound(int dimension) => origin.GetUpperBound(dimension);
        public int GetLowerBound(int dimension) => origin.GetLowerBound(dimension);
    }

    public static class DeferredArrayExtensions
    {
        public static float Max<T>(this DeferredArray<T, float> vals, bool excludeInfinity = false)
        {
            int bound0 = vals.GetUpperBound(0);
            int bound1 = vals.GetUpperBound(1);
            float result = float.MinValue;
            for (int i = 0; i < bound0; i++)
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
        public static float Min<T>(this DeferredArray<T, float> vals, bool excludeInfinity = false)
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
