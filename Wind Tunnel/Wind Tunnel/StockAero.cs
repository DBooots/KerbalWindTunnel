using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KerbalWindTunnel
{
    public class StockAero : AeroPredictor
    {
        public override float Mass
        {
            get
            {
                return EditorLogic.fetch.ship.GetTotalMass();
            }
        }

        public override Vector3 GetAeroForce(Conditions conditions, float AoA, float pitchInput = 0)
        {
            return StockAeroUtil.SimAeroForce(conditions.body, EditorLogic.fetch.ship, InflowVect(AoA) * conditions.speed, conditions.altitude);
        }
        public override Vector3 GetLiftForce(Conditions conditions, float AoA, float pitchInput = 0)
        {
            return StockAeroUtil.SimLiftForce(conditions.body, EditorLogic.fetch.ship, InflowVect(AoA) * conditions.speed, conditions.altitude);
        }

        public override Vector3 GetThrustForce(float mach, float atmDensity, float atmPressure, bool oxygenPresent)
        {
            Vector3 thrust = Vector3.zero;
            int stage = 0;
            for (int i = EditorLogic.fetch.ship.Parts.Count - 1; i >= 0; i--)
            {
                Part part = EditorLogic.fetch.ship.Parts[i];
                //if (part.inStageIndex != 0)
                //  continue;

                List<ModuleEngines> engines = part.FindModulesImplementing<ModuleEngines>();
                if (engines.Count == 0)
                    continue;

                //Debug.Log("Engine #" + i + " is in stage " + part.inverseStage);
                if (part.inverseStage < stage)
                    continue;
                if (part.inverseStage > stage)
                {
                    thrust = Vector3.zero;
                    stage = part.inverseStage;
                    //Debug.Log("Found a new, higher stage.");
                }

                ModuleEngines primaryEngine;
                if (part.FindModulesImplementing<MultiModeEngine>().FirstOrDefault() != null)
                    primaryEngine = engines.Find(x => x.engineID == part.FindModulesImplementing<MultiModeEngine>().FirstOrDefault().primaryEngineID);
                else
                    primaryEngine = engines.FirstOrDefault();

                if (primaryEngine == null)
                {
                    Debug.Log("No primary engine??");
                    continue;
                }

                if (primaryEngine.propellants.Any(p => p.name == "IntakeAir") && !oxygenPresent)
                {
                    //Debug.Log("Skipping as needs oxygen");
                    continue;
                }

                float thrustLevel = 1;
                if (primaryEngine.atmChangeFlow)
                {
                    if (primaryEngine.useAtmCurve)
                        thrustLevel = primaryEngine.atmCurve.Evaluate(atmDensity * (40f / 49f));
                    else
                        thrustLevel = atmDensity * (40f / 49f);
                }
                thrustLevel *= primaryEngine.useVelCurve ? primaryEngine.velCurve.Evaluate(mach) : 1;

                if (thrustLevel > primaryEngine.flowMultCap)
                    thrustLevel = primaryEngine.flowMultCap + (thrustLevel - primaryEngine.flowMultCap) / (primaryEngine.flowMultCapSharpness + thrustLevel / primaryEngine.flowMultCap - 1);
                thrustLevel = Mathf.Max(thrustLevel, primaryEngine.CLAMP);

                float isp = primaryEngine.atmosphereCurve.Evaluate(atmPressure);
                if (primaryEngine.useThrottleIspCurve)
                    isp *= Mathf.Lerp(1f, primaryEngine.throttleIspCurve.Evaluate(1), primaryEngine.throttleIspCurveAtmStrength.Evaluate(atmPressure));

                Vector3d engineThrust = Vector3d.zero;
                for (int j = primaryEngine.thrustTransforms.Count - 1; j >= 0; j--)
                {
                    engineThrust -= primaryEngine.thrustTransforms[j].forward * primaryEngine.thrustTransformMultipliers[j];
                }

                engineThrust *= thrustLevel * isp * primaryEngine.g * primaryEngine.multIsp * primaryEngine.maxFuelFlow * primaryEngine.multFlow * (primaryEngine.thrustPercentage / 100f);
                //Debug.Log("Thrust: " + engineThrust);
                //Debug.Log("Adding engine " + primaryEngine.part.name);
                thrust += engineThrust;
            }

            return thrust;
        }

        public override float GetFuelBurnRate(float mach, float atmDensity, float atmPressure, bool oxygenPresent)
        {
            throw new NotImplementedException();
        }

        public override Vector3 GetAeroTorque(Conditions conditions, float AoA, float pitchInput = 0, bool dryTorque = false)
        {
            throw new NotImplementedException();
        }

        public override float GetPitchInput(Conditions conditions, float AoA, bool dryTorque = false, float guess = float.NaN)
        {
            return 0;
        }
    }
}
