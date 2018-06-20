using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AscentProfilePlanner
{
    class FloatCurve2
    {
        public Keyframe2[,] keys { get { return (Keyframe2[,])_keys.Clone(); } }
        private Keyframe2[,] _keys;
        public float[] xTimes { get; private set; }
        public float[] yTimes { get; private set; }

        private double[,][] coeffCache;

        public Keyframe2 this[int indexX, int indexY]
        {
            get
            {
                return _keys[indexX, indexY];
            }
            set
            {
                _keys[indexY, indexY] = value;
            }
        }
        public Keyframe2 this[float timeX, float timeY]
        {
            get
            {
                int indexX = xTimes.IndexOf(timeX);
                int indexY = yTimes.IndexOf(timeY);

                if (indexX == -1) throw new ArgumentException("Given value did not match the allowable entries.", "timeX");
                if (indexY == -1) throw new ArgumentException("Given value did not match the allowable entries.", "timeY");

                return this[indexX, indexY];
            }
            set
            {
                int indexX = xTimes.IndexOf(timeX);
                int indexY = yTimes.IndexOf(timeY);

                if (indexX == -1) throw new ArgumentException("Given value did not match the allowable entries.", "timeX");
                if (indexY == -1) throw new ArgumentException("Given value did not match the allowable entries.", "timeY");

                this[indexX, indexY] = value;
            }
        }

        public int[] size { get { return new int[] { _keys.GetUpperBound(0), _keys.GetUpperBound(1) }; } }
        public int length { get { return _keys.Length; } }

        public int GetUpperBound(int dimension) { return _keys.GetUpperBound(dimension); }

        /*public FloatCurve2(int xNum, int yNum)
        {
            _keys = new Keyframe[xNum, yNum];
        }*/
        public FloatCurve2(float[] xTimes, float[] yTimes)
        {
            _keys = new Keyframe2[xTimes.Length, yTimes.Length];
            this.xTimes = xTimes;
            this.yTimes = yTimes;
            Array.Sort(this.xTimes);
            Array.Sort(this.yTimes);
            this.coeffCache = new double[xTimes.Length - 1, yTimes.Length - 1][];
        }
        /*public FloatCurve2(float[] xTimes, float[] yTimes, Keyframe[,] values) : this(xTimes, yTimes)
        {
            _keys = (Keyframe[,])values.Clone();
        }*/

        public int[] AddKey(float timeX, float timeY, float value)
        {
            return AddKey(timeX, timeY, value, 0, 0, 0);
        }
        public int[] AddKey(float timeX, float timeY, float value, float ddx, float ddy)
        {
            return AddKey(timeX, timeY, value, ddx, ddy, 0);
        }
        public int[] AddKey(float timeX, float timeY, float value, float ddx, float ddy, float dddxdy)
        {
            int indexX = xTimes.IndexOf(timeX);
            int indexY = yTimes.IndexOf(timeY);
            if (indexX >= 0 && indexY >= 0)
                _keys[indexX, indexY] = new Keyframe2(timeX, timeY, value, ddx, ddy, dddxdy);
            else
            {
                if (indexX == -1) throw new ArgumentException("Given value did not match the allowable entries.", "timeX");
                if (indexY == -1) throw new ArgumentException("Given value did not match the allowable entries.", "timeY");
            }
            return new int[2] { indexX, indexY };
        }

        public float Evaluate(float timeX, float timeY)
        {
            int xSquare;// = Array.FindIndex(xTimes, x => timeX < x) - 1;
            int ySquare;// = Array.FindIndex(yTimes, y => timeY < y) - 1;
            if (timeX < xTimes[0])
                xSquare = 0;
            else if (timeX > xTimes[xTimes.Length - 1])
                xSquare = xTimes.Length - 2;
            else
                xSquare = Array.FindIndex(xTimes, x => timeX < x) - 1;

            if (timeY < yTimes[0])
                ySquare = 0;
            else if (timeY > yTimes[yTimes.Length - 1])
                ySquare = yTimes.Length - 2;
            else
                ySquare = Array.FindIndex(yTimes, y => timeY < y) - 1;

            float dx = (xTimes[xSquare + 1] - xTimes[xSquare]);
            float dy = (yTimes[ySquare + 1] - yTimes[ySquare]);
            float xN = Mathf.Clamp01((timeX - xTimes[xSquare]) / dx);
            float yN = Mathf.Clamp01((timeY - yTimes[ySquare]) / dy);

            if (coeffCache[xSquare, ySquare].Length <= 0)
            {

                float[] knowns = new float[16] {
                    _keys[xSquare,ySquare].value,
                    _keys[xSquare + 1,ySquare].value,
                    _keys[xSquare,ySquare + 1].value,
                    _keys[xSquare + 1,ySquare + 1].value,
                    _keys[xSquare,ySquare].dDx * dx,
                    _keys[xSquare + 1,ySquare].dDx * dx,
                    _keys[xSquare,ySquare + 1].dDx * dx,
                    _keys[xSquare + 1,ySquare + 1].dDx * dx,
                    _keys[xSquare,ySquare].dDy * dy,
                    _keys[xSquare + 1,ySquare].dDy * dy,
                    _keys[xSquare,ySquare + 1].dDy * dy,
                    _keys[xSquare + 1,ySquare + 1].dDy * dy,
                    _keys[xSquare,ySquare].ddDxDy * dx * dy,
                    _keys[xSquare + 1,ySquare].ddDxDy * dx * dy,
                    _keys[xSquare,ySquare + 1].ddDxDy * dx * dy,
                    _keys[xSquare + 1,ySquare + 1].ddDxDy * dx * dy
                };

                coeffCache[xSquare, ySquare] = new double[16] {
                1 * knowns[0],
                1 * knowns[4],
                -3 * knowns[0] + 3 * knowns[1] - 2 * knowns[4] - 1 * knowns[5],
                2 * knowns[0] - 2 * knowns[1] + 1 * knowns[4] + 1 * knowns[5],
                1 * knowns[8],
                1 * knowns[12],
                -3 * knowns[8] + 3 * knowns[9] - 2 * knowns[12] - 1 * knowns[13],
                2 * knowns[8] - 2 * knowns[9] + 1 * knowns[12] + 1 * knowns[13],
                -3 * knowns[0] + 3 * knowns[2] - 2 * knowns[8] - 1 * knowns[10],
                -3 * knowns[4] + 3 * knowns[6] - 2 * knowns[12] - 1 * knowns[14],
                9 * knowns[0] - 9 * knowns[1] - 9 * knowns[2] + 9 * knowns[3] + 6 * knowns[4] + 3 * knowns[5] - 6 * knowns[6] - 3 * knowns[7] + 6 * knowns[8] - 6 * knowns[9] + 3 * knowns[10] - 3 * knowns[11] + 4 * knowns[12] + 2 * knowns[13] + 2 * knowns[14] + 1 * knowns[15],
                -6 * knowns[0] + 6 * knowns[1] + 6 * knowns[2] - 6 * knowns[3] - 3 * knowns[4] - 3 * knowns[5] + 3 * knowns[6] + 3 * knowns[7] - 4 * knowns[8] + 4 * knowns[9] - 2 * knowns[10] + 2 * knowns[11] - 2 * knowns[12] - 2 * knowns[13] - 1 * knowns[14] - 1 * knowns[15],
                2 * knowns[0] - 2 * knowns[2] + 1 * knowns[8] + 1 * knowns[10],
                2 * knowns[4] - 2 * knowns[6] + 1 * knowns[12] + 1 * knowns[14],
                -6 * knowns[0] + 6 * knowns[1] + 6 * knowns[2] - 6 * knowns[3] - 4 * knowns[4] - 2 * knowns[5] + 4 * knowns[6] + 2 * knowns[7] - 3 * knowns[8] + 3 * knowns[9] - 3 * knowns[10] + 3 * knowns[11] - 2 * knowns[12] - 1 * knowns[13] - 2 * knowns[14] - 1 * knowns[15],
                4 * knowns[0] - 4 * knowns[1] - 4 * knowns[2] + 4 * knowns[3] + 2 * knowns[4] + 2 * knowns[5] - 2 * knowns[6] - 2 * knowns[7] + 2 * knowns[8] - 2 * knowns[9] + 2 * knowns[10] - 2 * knowns[11] + 1 * knowns[12] + 1 * knowns[13] + 1 * knowns[14] + 1 * knowns[15]
                };
            }

            return (float)Solve(coeffCache[xSquare, ySquare], xN, yN);
        }

        private double Solve(double[] coeffs, float x, float y)
        {
            float x2 = x * x;
            float x3 = x2 * x;
            float y2 = y * y;
            float y3 = y2 * y;

            return (coeffs[0] + coeffs[1] * x + coeffs[2] * x2 + coeffs[3] * x3) +
                (coeffs[4] + coeffs[5] * x + coeffs[6] * x2 + coeffs[7] * x3) * y +
                (coeffs[8] + coeffs[9] * x + coeffs[10] * x2 + coeffs[11] * x3) * y2 +
                (coeffs[12] + coeffs[13] * x + coeffs[14] * x2 + coeffs[15] * x3) * y3;
        }

        public struct Keyframe2
        {
            public float timeX { get; set; }
            public float timeY { get; set; }
            public float value { get; set; }
            public float dDx { get; set; }
            public float dDy { get; set; }
            public float ddDxDy { get; set; }

            public int tangentMode { get { return 0; } set { } }

            public Keyframe2(float timex, float timey, float value)
            {
                this.timeX = timex;
                this.timeY = timey;
                this.value = value;
                this.dDx = 0.0f;
                this.dDy = 0.0f;
                this.ddDxDy = 0.0f;
            }

            public Keyframe2(float timex, float timey, float value, float ddx, float ddy)
            {
                this.timeX = timex;
                this.timeY = timey;
                this.value = value;
                this.dDx = ddx;
                this.dDy = ddy;
                this.ddDxDy = 0.0f;
            }

            public Keyframe2(float timex, float timey, float value, float ddx, float ddy, float dddxdy)
            {
                this.timeX = timex;
                this.timeY = timey;
                this.value = value;
                this.dDx = ddx;
                this.dDy = ddy;
                this.ddDxDy = dddxdy;
            }

            public static Keyframe2 operator + (Keyframe2 key, float value)
            {
                key.value += value;
                return key;
            }
            public static Keyframe2 operator + (Keyframe2 key1, Keyframe2 key2)
            {
                if (key1.timeX != key2.timeX || key1.timeY != key2.timeY)
                    throw new ArgumentException("The given keys did not match coordinates.");
                return new Keyframe2(key1.timeX, key2.timeX, key1.value + key2.value, key1.dDx + key2.dDx, key1.dDy + key2.dDy, key1.ddDxDy + key2.ddDxDy);
            }
            public static implicit operator float(Keyframe2 key)
            {
                return key.value;
            }
        }
    }
}
