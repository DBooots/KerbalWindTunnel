/*
Code copied and/or derived from Ferram Aerospace Research https://github.com/dkavolis/Ferram-Aerospace-Research/
=========================
Aerodynamics model for Kerbal Space Program

Copyright 2020, Michael Ferrara, aka Ferram4
   
   This file is derived from part of Ferram Aerospace Research.

   Ferram Aerospace Research is free software: you can redistribute it and/or modify
   it under the terms of the GNU General Public License as published by
   the Free Software Foundation, either version 3 of the License, or
   (at your option) any later version.

   Ferram Aerospace Research is distributed in the hope that it will be useful,
   but WITHOUT ANY WARRANTY; without even the implied warranty of
   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
   GNU General Public License for more details.

   You should have received a copy of the GNU General Public License
   along with Ferram Aerospace Research.  If not, see <http://www.gnu.org/licenses/>.

   Serious thanks:		a.g., for tons of bugfixes and code-refactorings
				stupid_chris, for the RealChuteLite implementation
            			Taverius, for correcting a ton of incorrect values
				Tetryds, for finding lots of bugs and issues and not letting me get away with them, and work on example crafts
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager updates
            			ialdabaoth (who is awesome), who originally created Module Manager
                        	Regex, for adding RPM support
				DaMichel, for some ferramGraph updates and some control surface-related features
            			Duxwing, for copy editing the readme
 */

using System;

namespace KerbalWindTunnel.FARVesselCache
{
    public static partial class FARAeroUtil
    {
        public static double MachBehindShockCalc(double M, AeroPredictor.Conditions conditions)
        {
            double gamma = GetAdiabaticIndex(conditions);

            double ratio = gamma - 1;
            ratio *= M * M;
            ratio += 2;
            ratio /= 2 * gamma * M * M - (gamma - 1);
            ratio = Math.Sqrt(ratio);

            return ratio;
        }

        public static double PressureBehindShockCalc(double M, AeroPredictor.Conditions conditions)
        {
            double gamma = GetAdiabaticIndex(conditions);

            double ratio = M * M;
            ratio *= 2 * gamma;
            ratio -= gamma - 1;
            ratio /= gamma + 1;

            return ratio;
        }

        public static double StagnationPressureCalc(double M, AeroPredictor.Conditions conditions)
        {
            double gamma = GetAdiabaticIndex(conditions);

            double ratio = M * M;
            ratio *= gamma - 1;
            ratio *= 0.5;
            ratio++;

            ratio = Math.Pow(ratio, gamma / (gamma - 1));
            return ratio;
        }

        public static double maxPrandtlMeyerTurnAngle(AeroPredictor.Conditions conditions)
        {
            double gamma = GetAdiabaticIndex(conditions);
            double gamma_ = Math.Sqrt((gamma + 1) / (gamma - 1));
            double maxPrandtlMeyerTurnAngle = gamma_ - 1;
            maxPrandtlMeyerTurnAngle *= 90;
            return maxPrandtlMeyerTurnAngle;
        }

        public static class PrandtlMeyerMach
        {
            public static double Evaluate(float inM, AeroPredictor.Conditions conditions)
            {
                double gamma = GetAdiabaticIndex(conditions);
                double gamma_ = Math.Sqrt((gamma + 1) / (gamma - 1));
                double mach = Math.Sqrt(inM * inM - 1);
                double nu = Math.Atan(mach / gamma_);
                nu *= gamma_;
                nu -= Math.Atan(mach);
                nu *= UnityEngine.Mathf.Rad2Deg;
                return nu;
            }
        }

        public static class PrandtlMeyerAngle
        {
            private static CelestialBody body = null;
            private static System.Collections.Generic.Dictionary<double, FloatCurve> cache = new System.Collections.Generic.Dictionary<double, FloatCurve>();
            public static double Evaluate(double nu, AeroPredictor.Conditions conditions)
            {
                double gamma = GetAdiabaticIndex(conditions);
                FloatCurve curve;
                bool foundInCache;
                lock (cache)
                    foundInCache = cache.TryGetValue(gamma, out curve);
                if (!foundInCache)
                {
                    curve = new FloatCurve();
                    double M = 1;
                    double gamma_ = Math.Sqrt((gamma + 1) / (gamma - 1));
                    while (M < 250)
                    {
                        double mach = Math.Sqrt(M * M - 1);

                        double nu_key = Math.Atan(mach / gamma_);
                        nu_key *= gamma_;
                        nu_key -= Math.Atan(mach);
                        nu_key *= UnityEngine.Mathf.Rad2Deg;

                        double nu_mach = (gamma - 1) / 2;
                        nu_mach *= M * M;
                        nu_mach++;
                        nu_mach *= M;
                        nu_mach = mach / nu_mach;
                        nu_mach *= UnityEngine.Mathf.Rad2Deg;

                        nu_mach = 1 / nu_mach;

                        curve.Add((float)nu_key, (float)M, (float)nu_mach, (float)nu_mach);

                        if (M < 3)
                            M += 0.1;
                        else if (M < 10)
                            M += 0.5;
                        else if (M < 25)
                            M += 2;
                        else
                            M += 25;
                    }
                    lock (cache)
                    {
                        // In case another thread got through that faster.
                        if (!cache.ContainsKey(gamma))
                            cache.Add(gamma, curve);
                    }
                }
                lock (curve)
                    return curve.Evaluate((float)nu);
            }
            internal static void ClearCache(CelestialBody newBody)
            {
                if (newBody == body)
                    return;
                body = newBody;
                lock (cache)
                    cache.Clear();
            }
        }
    }
}
