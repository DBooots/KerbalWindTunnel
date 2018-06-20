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
    public class Brent : RootSolver
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
                /*string msg = string.Format("Tolerance must be positive. Recieved {0}.", tolerance);
                throw new ArgumentOutOfRangeException(msg);*/
                tolerance = Math.Abs(tolerance);
            }

            errorEstimate = float.MaxValue;

            // Standardize the problem.  To solve g(x) = target,
            // solve g(x) = 0 where g(x) = f(x) - target.
            float g(float x) => f(x) - target;

            // Implementation and notation based on Chapter 4 in
            // "Algorithms for Minimization without Derivatives"
            // by Richard Brent.

            float c, d, e, fa, fb, fc, tol, m, p, q, r, s;

            // set up aliases to match Brent's notation
            float a = left; float b = right; float t = tolerance;
            iterationsUsed = 0;

            fa = g(a);
            fb = g(b);

            if (fa * fb > 0.0)
            {
                /*string str = "Invalid starting bracket. Function must be above target on one end and below target on other end.";
                string msg = string.Format("{0} Target: {1}. f(left) = {2}. f(right) = {3}", str, target, fa + target, fb + target);
                throw new ArgumentException(msg);*/
                return float.NaN;
            }

            label_int:
            c = a; fc = fa; d = e = b - a;
            label_ext:
            if (Math.Abs(fc) < Math.Abs(fb))
            {
                a = b; b = c; c = a;
                fa = fb; fb = fc; fc = fa;
            }

            iterationsUsed++;

            tol = 2.0f * t * Math.Abs(b) + t;
            errorEstimate = m = 0.5f * (c - b);
            if (Math.Abs(m) > tol && fb != 0.0) // exact comparison with 0 is OK here
            {
                // See if bisection is forced
                if (Math.Abs(e) < tol || Math.Abs(fa) <= Math.Abs(fb))
                {
                    d = e = m;
                }
                else
                {
                    s = fb / fa;
                    if (a == c)
                    {
                        // linear interpolation
                        p = 2.0f * m * s; q = 1.0f - s;
                    }
                    else
                    {
                        // Inverse quadratic interpolation
                        q = fa / fc; r = fb / fc;
                        p = s * (2.0f * m * q * (q - r) - (b - a) * (r - 1.0f));
                        q = (q - 1.0f) * (r - 1.0f) * (s - 1.0f);
                    }
                    if (p > 0.0)
                        q = -q;
                    else
                        p = -p;
                    s = e; e = d;
                    if (2.0 * p < 3.0 * m * q - Math.Abs(tol * q) && p < Math.Abs(0.5 * s * q))
                        d = p / q;
                    else
                        d = e = m;
                }
                a = b; fa = fb;
                if (Math.Abs(d) > tol)
                    b += d;
                else if (m > 0.0)
                    b += tol;
                else
                    b -= tol;
                if (iterationsUsed == maxIterations)
                    return b;

                fb = g(b);
                if ((fb > 0.0 && fc > 0.0) || (fb <= 0.0 && fc <= 0.0))
                    goto label_int;
                else
                    goto label_ext;
            }
            else
                return b;
        }
    }
}
