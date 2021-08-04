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
using System.Collections.Generic;
using KSPPluginFramework;
using UnityEngine;

namespace KerbalWindTunnel.FARVesselCache
{
    public partial class FARWingInteractionWrapper
    {
        public void CalculateEffectsOfUpstreamWing(
            double thisWingAoA,
            double thisWingMachNumber,
            Vector3d parallelInPlaneLocal,
            ref double ACweight,
            ref double ACshift,
            ref double ClIncrementFromRear
        )
        {
            double thisWingMAC = parentWingModule.Effective_MAC;
            double thisWingb_2 = parentWingModule.Effective_b_2;

            EffectiveUpstreamMAC = 0;
            EffectiveUpstreamb_2 = 0;
            EffectiveUpstreamArea = 0;

            EffectiveUpstreamLiftSlope = 0;
            EffectiveUpstreamStall = 0;
            EffectiveUpstreamCosSweepAngle = 0;
            EffectiveUpstreamAoAMax = 0;
            EffectiveUpstreamAoA = 0;
            EffectiveUpstreamCd0 = 0;
            EffectiveUpstreamInfluence = 0;

            double wingForwardDir = parallelInPlaneLocal.y;
            double wingRightwardDir = parallelInPlaneLocal.x * srfAttachFlipped;

            if (wingForwardDir > 0)
            {
                wingForwardDir *= wingForwardDir;
                UpdateUpstreamValuesFromWingModules(nearbyWingModulesForwardList,
                                                    nearbyWingModulesForwardInfluence,
                                                    wingForwardDir,
                                                    thisWingAoA);
            }
            else
            {
                wingForwardDir *= wingForwardDir;
                UpdateUpstreamValuesFromWingModules(nearbyWingModulesBackwardList,
                                                    nearbyWingModulesBackwardInfluence,
                                                    wingForwardDir,
                                                    thisWingAoA);
            }

            if (wingRightwardDir > 0)
            {
                wingRightwardDir *= wingRightwardDir;
                UpdateUpstreamValuesFromWingModules(nearbyWingModulesRightwardList,
                                                    nearbyWingModulesRightwardInfluence,
                                                    wingRightwardDir,
                                                    thisWingAoA);
            }
            else
            {
                wingRightwardDir *= wingRightwardDir;
                UpdateUpstreamValuesFromWingModules(nearbyWingModulesLeftwardList,
                                                    nearbyWingModulesLeftwardInfluence,
                                                    wingRightwardDir,
                                                    thisWingAoA);
            }

            double MachCoeff = (1 - thisWingMachNumber * thisWingMachNumber).Clamp(0, 1);

            if (MachCoeff == 0 || Math.Abs(MachCoeff) < 1e-14 * double.Epsilon)
                return;
            double flapRatio = (thisWingMAC / (thisWingMAC + EffectiveUpstreamMAC)).Clamp(0, 1);
            float flt_flapRatio = (float)flapRatio;
            //Flap Effectiveness Factor
            double flapFactor = wingCamberFactor.Evaluate(flt_flapRatio);
            //Change in moment due to change in lift from flap
            double dCm_dCl = wingCamberMoment.Evaluate(flt_flapRatio);

            //This accounts for the wing possibly having a longer span than the flap
            double WingFraction = (thisWingb_2 / EffectiveUpstreamb_2).Clamp(0, 1);
            //This accounts for the flap possibly having a longer span than the wing it's attached to
            double FlapFraction = (EffectiveUpstreamb_2 / thisWingb_2).Clamp(0, 1);

            //Lift created by the flap interaction
            double ClIncrement = flapFactor * EffectiveUpstreamLiftSlope * EffectiveUpstreamAoA;
            //Increase the Cl so that even though we're working with the flap's area, it accounts for the added lift across the entire object
            ClIncrement *= (parentWingModule.S * FlapFraction + EffectiveUpstreamArea * WingFraction) /
                           parentWingModule.S;

            // Total flap Cl for the purpose of applying ACshift, including the bit subtracted below
            ACweight = ClIncrement * MachCoeff;

            //Removing additional angle so that lift of the flap is calculated as lift at wing angle + lift due to flap interaction rather than being greater
            ClIncrement -= FlapFraction * EffectiveUpstreamLiftSlope * EffectiveUpstreamAoA;

            //Change in Cm with change in Cl
            ACshift = (dCm_dCl + 0.75 * (1 - flapRatio)) * (thisWingMAC + EffectiveUpstreamMAC);

            ClIncrementFromRear = ClIncrement * MachCoeff;

            effectiveUpstreamCd0_set(WrappedObject, EffectiveUpstreamCd0);
            effectiveUpstreamInfluence_set(WrappedObject, EffectiveUpstreamInfluence);
            effectiveUpstreamStall_set(WrappedObject, EffectiveUpstreamStall);
        }

        private void UpdateUpstreamValuesFromWingModules(
            List<FARWingAerodynamicModelWrapper> wingModules,
            List<double> associatedInfluences,
            double directionalInfluence,
            double thisWingAoA
        )
        {
            directionalInfluence = Math.Abs(directionalInfluence);
            for (int i = 0; i < wingModules.Count; i++)
            {
                FARWingAerodynamicModelWrapper wingModule = wingModules[i];
                double wingInfluenceFactor = associatedInfluences[i] * directionalInfluence;

                if (wingModule == null)
                {
                    //HandleNullPart(wingModules, associatedInfluences, i);
                    //i--;
                    continue;
                }

                if (wingModule.isShielded)
                    continue;

                double tmp = Vector3.Dot(wingModule.part_transform.forward, parentWingModule.part_transform.forward);

                EffectiveUpstreamMAC += wingModule.Effective_MAC * wingInfluenceFactor;
                EffectiveUpstreamb_2 += wingModule.Effective_b_2 * wingInfluenceFactor;
                EffectiveUpstreamArea += wingModule.S * wingInfluenceFactor;

                EffectiveUpstreamLiftSlope += wingModule.RawLiftSlope * wingInfluenceFactor;
                EffectiveUpstreamStall += wingModule.Stall * wingInfluenceFactor;
                EffectiveUpstreamCosSweepAngle += wingModule.CosSweepAngle * wingInfluenceFactor;
                EffectiveUpstreamAoAMax += wingModule.rawAoAmax * wingInfluenceFactor;
                EffectiveUpstreamCd0 += wingModule.ZeroLiftCdIncrement * wingInfluenceFactor;
                EffectiveUpstreamInfluence += wingInfluenceFactor;

                double wAoA = wingModule.CalculateAoA(wingModule.Vel) * Math.Sign(tmp);
                //First, make sure that the AoA are wrt the same direction; then account for any strange angling of the part that shouldn't be there
                tmp = (thisWingAoA - wAoA) * wingInfluenceFactor;

                EffectiveUpstreamAoA += tmp;
            }
        }
    }
}
