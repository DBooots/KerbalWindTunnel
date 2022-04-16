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

using UnityEngine;

namespace KerbalWindTunnel.FARVesselCache
{
    public partial class FARVesselCache
    {
        public void GetClCdCmSteady(
            Conditions conditions,
            Vector3 inflow,
            float pitchInput,
            Vector3 torquePoint,
            out Vector3 aeroforce,
            out Vector3 torque
        )
        {
            SetAerodynamicModelVels(inflow);

            aeroforce = torque = Vector3.zero;
            double ReynoldsNumber = FARAeroUtil.CalculateReynoldsNumber(conditions.atmDensity,
                                                                 bodyLength,
                                                                 conditions.speed,
                                                                 conditions.mach,
                                                                 FARAeroUtil.GetTemperature(conditions),
                                                                 FARAeroUtil.GetAdiabaticIndex(conditions));
            float skinFrictionDragCoefficient = (float)FARAeroUtil.SkinFrictionDrag(ReynoldsNumber, conditions.mach);

            float pseudoKnudsenNumber = (float)(conditions.mach / (ReynoldsNumber + conditions.mach));

            Vector3d velocity = inflow.normalized;

            foreach (FARWingAerodynamicModelWrapper wingAerodynamicModel in _wingAerodynamicModel)
            {
                if (wingAerodynamicModel == null)
                    continue;

                //w.ComputeForceEditor(velocity, input.MachNumber, 2);

                wingAerodynamicModel.EditorClClear(true);

                /*if (FARHook.FARControllableSurfaceType.IsAssignableFrom(wingAerodynamicModel.WrappedObject.GetType()))
                    FARMethodAssist.FARControllableSurface_SetControlStateEditor(wingAerodynamicModel.WrappedObject,
                        torquePoint,
                        velocity,
                        pitchInput,     // TODO: Confirm that I am using the same pitch input convention.
                        0,
                        0,
                        0, //Flaps
                        false);

                else*/
                if (wingAerodynamicModel.isShielded)
                    continue;

                Vector3 force = wingAerodynamicModel.ComputeForceEditor(velocity, conditions.mach, conditions.atmDensity, conditions) * 1000;
                aeroforce += force;

                Vector3 relPos = wingAerodynamicModel.AerodynamicCenter - torquePoint;

                torque += -Vector3d.Cross(relPos, force);
            }

            var center = FARMethodAssist.NewFARCenterQuery();
            // aeroSection.simContext.center.ClearAll() would allow for perpetual reuse of an object.
            foreach (object aeroSection in _currentAeroSections)
                FARMethodAssist.FARAeroSection_PredictionCalculateAeroForces(aeroSection,
                    conditions.atmDensity,
                    conditions.mach,
                    (float)(ReynoldsNumber / bodyLength),
                    pseudoKnudsenNumber,
                    skinFrictionDragCoefficient,
                    velocity,
                    center);

            aeroforce += FARMethodAssist.FARCenterQuery_force(center) * 1000;
            torque += -FARMethodAssist.FARCenterQuery_TorqueAt(center, torquePoint) * 1000;
        }
    }
}
