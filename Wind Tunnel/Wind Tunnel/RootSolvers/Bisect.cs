using System;

namespace KerbalWindTunnel.RootSolvers
{
    /*
     * This method comes from Three Methods for Root-finding in C# by John D. Cook
     * https://www.codeproject.com/Articles/79541/Three-Methods-for-Root-finding-in-C
     * 
     * They are released under the BSD License
     * Copyright <YEAR> <COPYRIGHT HOLDER>
     *           [2014] [John D. Cook]
     *
     * Redistribution and use in source and binary forms, with or without modification,
     * are permitted provided that the following conditions are met:
     *
     * 1. Redistributions of source code must retain the above copyright notice, this
     *    list of conditions and the following disclaimer.
     *
     * 2. Redistributions in binary form must reproduce the above copyright notice, this
     *    list of conditions and the following disclaimer in the documentation and/or
     *    other materials provided with the distribution.
     *
     *  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY
     *  EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
     *  OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT
     *  SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT,
     *  INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED
     *  TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR
     *  BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
     *  CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY
     *  WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
    */
    public class Bisect : RootSolver
    {
        public const int maxIterations = 50;
        public override float Solve(Func<float, float> function, RootSolverSettings solverSettings)
        {
            int iterationsUsed;
            float errorEstimate, result;

            if (solverSettings.leftBound != solverSettings.leftGuessBound || solverSettings.rightBound != solverSettings.rightGuessBound)
            {
                // Take a try at solving it on the narrower bounds provided. This saves a few iterations.
                result = Solve(function, solverSettings.leftGuessBound, solverSettings.rightGuessBound, solverSettings.tolerance, 0, out iterationsUsed, out errorEstimate);
                // The Solve method below returns NaN if the results at these bounds are on the same side of zero.
                if (!float.IsNaN(result))
                    return result;
            }
            result = Solve(function, solverSettings.leftBound, solverSettings.rightBound, solverSettings.tolerance, 0, out iterationsUsed, out errorEstimate);
            if (!float.IsNaN(result))
                return result;

            // If the result on the wider bounds is still NaN, the function either does not cross zero or crosses and comes back. Let's quickly check the middle:
            float mid = function((solverSettings.leftBound + solverSettings.rightBound) / 2);
            float left = function(solverSettings.leftBound);
            if (Math.Sign(mid) != Math.Sign(left))
                result = Solve(function, solverSettings.leftBound, (solverSettings.leftBound + solverSettings.rightBound) / 2, solverSettings.tolerance, 0, out iterationsUsed, out errorEstimate);
            if (!float.IsNaN(result))
                return result;

            // You're still here? Fine, one more shot:
            float right = function(solverSettings.rightBound);
            if (Math.Sign(mid) != Math.Sign(right))
                result = Solve(function, (solverSettings.leftBound + solverSettings.rightBound) / 2, solverSettings.rightBound, solverSettings.tolerance, 0, out iterationsUsed, out errorEstimate);
            return result;
            // We don't care if it's still NaN at this point. That's the best we'll get.
        }

        public float Solve(Func<float, float> f, float left, float right, float tolerance, float target, out int iterationsUsed, out float errorEstimate)
        {
            if (tolerance <= 0.0)
            {
                string msg = string.Format("Tolerance must be positive. Recieved {0}.", tolerance);
                throw new ArgumentOutOfRangeException(msg);
            }

            iterationsUsed = 0;
            errorEstimate = float.MaxValue;

            // Standardize the problem.  To solve f(x) = target,
            // solve g(x) = 0 where g(x) = f(x) - target.
            float g(float x) => f(x) - target;


            float g_left = g(left);  // evaluation of f at left end of interval
            float g_right = g(right);
            float mid;
            float g_mid;
            if (g_left * g_right >= 0.0)
            {
                string str = "Invalid starting bracket. Function must be above target on one end and below target on other end.";
                string msg = string.Format("{0} Target: {1}. f(left) = {2}. f(right) = {3}", str, target, g_left + target, g_right + target);
                throw new ArgumentException(msg);
            }

            float intervalWidth = right - left;

            for
            (
            iterationsUsed = 0;
            iterationsUsed < maxIterations && intervalWidth > tolerance;
            iterationsUsed++
            )
            {
                intervalWidth *= 0.5f;
                mid = left + intervalWidth;

                if ((g_mid = g(mid)) == 0.0)
                {
                    errorEstimate = 0.0f;
                    return mid;
                }
                if (g_left * g_mid < 0.0)           // g changes sign in (left, mid)
                    g_right = g(right = mid);
                else                            // g changes sign in (mid, right)
                    g_left = g(left = mid);
            }
            errorEstimate = right - left;
            return left;
        }
    }
}
