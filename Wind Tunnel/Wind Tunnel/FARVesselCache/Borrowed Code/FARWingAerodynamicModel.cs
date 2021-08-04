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
using KSPPluginFramework;
using UnityEngine;

namespace KerbalWindTunnel.FARVesselCache
{
    public partial class FARWingAerodynamicModelWrapper
    {
        private void CalculateWingCamberInteractions(double MachNumber, double AoA, out double ACshift, out double ACweight)
        {
            ACshift = 0;
            ACweight = 0;
            ClIncrementFromRear = 0;

            rawAoAmax = CalculateAoAmax(MachNumber);
            rawAoAmax_set(wrappedObject, rawAoAmax);

            liftslope = rawLiftSlope;
            wingInteraction.UpdateOrientationForInteraction(ParallelInPlaneLocal);
            wingInteraction.CalculateEffectsOfUpstreamWing(AoA,
                                                           MachNumber,
                                                           ParallelInPlaneLocal,
                                                           ref ACweight,
                                                           ref ACshift,
                                                           ref ClIncrementFromRear);
            double effectiveUpstreamInfluence = wingInteraction.EffectiveUpstreamInfluence;

            if (effectiveUpstreamInfluence > 0)
            {
                effectiveUpstreamInfluence = wingInteraction.EffectiveUpstreamInfluence;

                AoAmax = wingInteraction.EffectiveUpstreamAoAMax;
                liftslope *= 1 - effectiveUpstreamInfluence;
                liftslope += wingInteraction.EffectiveUpstreamLiftSlope;

                cosSweepAngle *= 1 - effectiveUpstreamInfluence;
                cosSweepAngle += wingInteraction.EffectiveUpstreamCosSweepAngle;
                cosSweepAngle = cosSweepAngle.Clamp(0d, 1d);
            }
            else
            {
                liftslope = rawLiftSlope;
                AoAmax = 0;
            }

            AoAmax += rawAoAmax;
            AoAmax_set(wrappedObject, AoAmax);
        }

        private Vector3 DoCalculateForces(Vector3 velocity, double MachNumber, double AoA, double density, AeroPredictor.Conditions conditions)
        {
            float v_scalar = velocity.magnitude;

            Vector3 forward = part_transform.forward;
            Vector3d velocity_normalized = velocity / v_scalar;

            double q = density * v_scalar * v_scalar * 0.0005; //dynamic pressure, q

            //Projection of velocity vector onto the plane of the wing
            ParallelInPlane = Vector3d.Exclude(forward, velocity).normalized;
            //This just gives the vector to cross with the velocity vector
            perp = Vector3d.Cross(forward, ParallelInPlane).normalized;
            liftDirection = Vector3d.Cross(perp, velocity).normalized;

            ParallelInPlaneLocal = part_transform.InverseTransformDirection(ParallelInPlane);

            // Calculate the adjusted AC position (uses ParallelInPlane)
            AerodynamicCenter = CalculateAerodynamicCenter(MachNumber, AoA, CurWingCentroid);

            //Throw AoA into lifting line theory and adjust for part exposure and compressibility effects

            /*double skinFrictionDrag = HighLogic.LoadedSceneIsFlight
                                          ? FARAeroUtil.SkinFrictionDrag(density,
                                                                         effective_MAC,
                                                                         v_scalar,
                                                                         MachNumber,
                                                                         vessel.externalTemperature,
                                                                         FARAtmosphere.GetAdiabaticIndex(vessel))
                                          : 0.005;
                                          */
            double skinFrictionDrag = FARAeroUtil.SkinFrictionDrag(density, effective_MAC, v_scalar, MachNumber, FARAeroUtil.GetTemperature(conditions), FARAeroUtil.GetAdiabaticIndex(conditions));


            skinFrictionDrag *= 1.1; //account for thickness

            CalculateCoefficients(MachNumber, AoA, skinFrictionDrag, conditions);


            //lift and drag vectors
            Vector3d L, D;
            //lift; submergedDynPreskPa handles lift
            L = liftDirection * (Cl * S) * q;
            //drag is parallel to velocity vector
            D = -velocity_normalized * (Cd * S) * q;

            Vector3d force = L + D;
            if (double.IsNaN(force.sqrMagnitude) || double.IsNaN(AerodynamicCenter.sqrMagnitude))
            {
                force = AerodynamicCenter = Vector3d.zero;
            }
            SyncToObject();
            return force;
        }

        private void CalculateCoefficients(double MachNumber, double AoA, double skinFrictionCoefficient, AeroPredictor.Conditions conditions)
        {
            minStall = 0;

            rawLiftSlope = CalculateSubsonicLiftSlope(MachNumber); // / AoA;     //Prandtl lifting Line


            CalculateWingCamberInteractions(MachNumber, AoA, out double ACshift, out double ACweight);
            DetermineStall(AoA);

            double beta = Math.Sqrt(MachNumber * MachNumber - 1);
            if (double.IsNaN(beta) || beta < 0.66332495807107996982298654733414)
                beta = 0.66332495807107996982298654733414;

            double TanSweep = Math.Sqrt((1 - cosSweepAngle * cosSweepAngle).Clamp(0, 1)) / cosSweepAngle;
            double beta_TanSweep = beta / TanSweep;


            double Cd0 = CdCompressibilityZeroLiftIncrement(MachNumber, cosSweepAngle, TanSweep, beta_TanSweep, beta) +
                         2 * skinFrictionCoefficient;
            double CdMax = CdMaxFlatPlate(MachNumber, beta);
            e = FARAeroUtil.CalculateOswaldsEfficiencyNitaScholz(effective_AR, cosSweepAngle, Cd0, TaperRatio);
            piARe = effective_AR * e * Math.PI;

            double CosAoA = Math.Cos(AoA);

            if (MachNumber <= 0.8)
            {
                double Cn = liftslope;
                FinalLiftSlope = liftslope;
                double sinAoA = Math.Sqrt((1 - CosAoA * CosAoA).Clamp(0, 1));
                Cl = Cn * CosAoA * Math.Sign(AoA);

                Cl += ClIncrementFromRear;
                Cl *= sinAoA;

                if (Math.Abs(Cl) > Math.Abs(ACweight))
                    ACshift *= Math.Abs(ACweight / Cl).Clamp(0, 1);
                Cd = Cl * Cl / piARe; //Drag due to 3D effects on wing and base constant
                Cd += Cd0;
            }
            /*
             * Supersonic nonlinear lift / drag code
             *
             */
            else if (MachNumber > 1.4)
            {
                double coefMult = 2 / (FARAeroUtil.GetAdiabaticIndex(conditions) * MachNumber * MachNumber);

                double supersonicLENormalForceFactor = CalculateSupersonicLEFactor(beta, TanSweep, beta_TanSweep);

                double normalForce = GetSupersonicPressureDifference(MachNumber, AoA, conditions);
                FinalLiftSlope = coefMult * normalForce * supersonicLENormalForceFactor;

                Cl = FinalLiftSlope * CosAoA * Math.Sign(AoA);
                Cd = beta * Cl * Cl / piARe;

                Cd += Cd0;
            }
            /*
             * Transonic nonlinear lift / drag code
             * This uses a blend of subsonic and supersonic aerodynamics to try and smooth the gap between the two regimes
             */
            else
            {
                //This determines the weight of supersonic flow; subsonic uses 1-this
                double supScale = 2 * MachNumber;
                supScale -= 6.6;
                supScale *= MachNumber;
                supScale += 6.72;
                supScale *= MachNumber;
                supScale += -2.176;
                supScale *= -4.6296296296296296296296296296296;

                double Cn = liftslope;
                double sinAoA = Math.Sqrt((1 - CosAoA * CosAoA).Clamp(0, 1));
                Cl = Cn * CosAoA * sinAoA * Math.Sign(AoA);

                if (MachNumber <= 1)
                {
                    Cl += ClIncrementFromRear * sinAoA;
                    if (Math.Abs(Cl) > Math.Abs(ACweight))
                        ACshift *= Math.Abs(ACweight / Cl).Clamp(0, 1);
                }

                FinalLiftSlope = Cn * (1 - supScale);
                Cl *= 1 - supScale;

                double M = MachNumber.Clamp(1.2, double.PositiveInfinity);

                double coefMult = 2 / (FARAeroUtil.GetAdiabaticIndex(conditions) * M * M);  // !!!!

                double supersonicLENormalForceFactor = CalculateSupersonicLEFactor(beta, TanSweep, beta_TanSweep);

                double normalForce = GetSupersonicPressureDifference(M, AoA, conditions);

                double supersonicLiftSlope = coefMult * normalForce * supersonicLENormalForceFactor * supScale;
                FinalLiftSlope += supersonicLiftSlope;


                Cl += CosAoA * Math.Sign(AoA) * supersonicLiftSlope;

                double effectiveBeta = beta * supScale + (1 - supScale);

                Cd = effectiveBeta * Cl * Cl / piARe;

                Cd += Cd0;
            }

            //AC shift due to flaps
            Vector3d ACShiftVec;
            if (!double.IsNaN(ACshift) && MachNumber <= 1)
                ACShiftVec = ACshift * ParallelInPlane;
            else
                ACShiftVec = Vector3d.zero;

            //Stalling effects
            stall = stall.Clamp(minStall, 1);

            //AC shift due to stall
            if (stall > 0)
                ACShiftVec -= 0.75 / criticalCl * MAC_actual * Math.Abs(Cl) * stall * ParallelInPlane * CosAoA;

            Cl -= Cl * stall * 0.769;
            Cd += Cd * stall * 3;
            Cd = Math.Max(Cd, CdMax * (1 - CosAoA * CosAoA));

            AerodynamicCenter += ACShiftVec;

            Cl *= wingInteraction.ClInterferenceFactor;

            FinalLiftSlope *= wingInteraction.ClInterferenceFactor;

            ClIncrementFromRear = 0;
        }

        private static double GetSupersonicPressureDifference(double M, double AoA, AeroPredictor.Conditions conditions)
        {
            double maxSinBeta = FARAeroUtil.CalculateSinMaxShockAngle(M, FARAeroUtil.GetAdiabaticIndex(conditions));
            double minSinBeta = 1 / M;

            //In radians, Corresponds to ~2.8 degrees or approximately what you would get from a ~4.8% thick diamond airfoil
            const double halfAngle = 0.05;

            double AbsAoA = Math.Abs(AoA);

            //Region 1 is the upper surface ahead of the max thickness
            double angle1 = halfAngle - AbsAoA;
            double M1;
            //pressure ratio wrt to freestream pressure
            double p1 = angle1 >= 0
                            ? ShockWaveCalculation(angle1, M, out M1, maxSinBeta, minSinBeta, conditions)
                            : PMExpansionCalculation(Math.Abs(angle1), M, out M1, conditions);

            //Region 2 is the upper surface behind the max thickness
            double p2 = PMExpansionCalculation(2 * halfAngle, M1, conditions) * p1;

            //Region 3 is the lower surface ahead of the max thickness
            double angle3 = halfAngle + AbsAoA;
            //pressure ratio wrt to freestream pressure
            double p3 = ShockWaveCalculation(angle3, M, out double M3, maxSinBeta, minSinBeta, conditions);

            //Region 4 is the lower surface behind the max thickness
            double p4 = PMExpansionCalculation(2 * halfAngle, M3, conditions) * p3;

            double pRatio = (p3 + p4 - (p1 + p2)) * 0.5;

            return pRatio;
        }

        private static double ShockWaveCalculation(
            double angle,
            double inM,
            out double outM,
            double maxSinBeta,
            double minSinBeta,
            AeroPredictor.Conditions conditions
        )
        {
            double sinBeta =
                FARAeroUtil.CalculateSinWeakObliqueShockAngle(inM, FARAeroUtil.GetAdiabaticIndex(conditions), angle);
            if (double.IsNaN(sinBeta))
                sinBeta = maxSinBeta;

            sinBeta.Clamp(minSinBeta, maxSinBeta);

            double normalInM = sinBeta * inM;
            normalInM = normalInM.Clamp(1, double.PositiveInfinity);

            double tanM = inM * Math.Sqrt((1 - sinBeta * sinBeta).Clamp(0, 1));

            double normalOutM = FARAeroUtil.MachBehindShockCalc(normalInM, conditions);

            outM = Math.Sqrt(normalOutM * normalOutM + tanM * tanM);

            double pRatio = FARAeroUtil.PressureBehindShockCalc(normalInM, conditions);

            return pRatio;
        }

        private static double PMExpansionCalculation(double angle, double inM, out double outM, AeroPredictor.Conditions conditions)
        {
            inM = inM.Clamp(1, double.PositiveInfinity);
            double nu1;
            nu1 = FARAeroUtil.PrandtlMeyerMach.Evaluate((float)inM, conditions);
            double theta = angle * Mathf.Rad2Deg;
            double nu2 = nu1 + theta;
            double maxPrandtlMeyerTurnAngle = FARAeroUtil.maxPrandtlMeyerTurnAngle(conditions);
            if (nu2 >= maxPrandtlMeyerTurnAngle)
                nu2 = maxPrandtlMeyerTurnAngle;
            outM = FARAeroUtil.PrandtlMeyerAngle.Evaluate((float)nu2, conditions);

            return FARAeroUtil.StagnationPressureCalc(inM, conditions) / FARAeroUtil.StagnationPressureCalc(outM, conditions);
        }

        public void EditorClClear(bool reset_stall)
        {
            Cl = 0;
            Cd = 0;
            if (reset_stall)
            {
                stall = 0;
                stall_set(wrappedObject, stall);
            }

            Cl_set(wrappedObject, Cl);
            Cd_set(wrappedObject, Cd);
        }
    }
}
