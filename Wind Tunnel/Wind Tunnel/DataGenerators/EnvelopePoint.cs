using System;
using UnityEngine;

namespace KerbalWindTunnel.DataGenerators
{
    public struct EnvelopePoint
    {
        public readonly float AoA_level;
        public readonly float Thrust_excess;
        public readonly float Accel_excess;
        public readonly float Lift_max;
        public readonly float AoA_max;
        public readonly float Thrust_available;
        public readonly float altitude;
        public readonly float speed;
        public readonly float LDRatio;
        public readonly Vector3 force;
        public readonly Vector3 aeroforce;
        public readonly float mach;
        public readonly float dynamicPressure;
        public readonly float dLift;
        public readonly float drag;
        public readonly float pitchInput;
        public readonly float fuelBurnRate;
        //public readonly float stabilityRange;
        //public readonly float stabilityScore;
        //public readonly float stabilityDerivative;
        public readonly bool completed;

        public EnvelopePoint(AeroPredictor vessel, CelestialBody body, float altitude, float speed, float AoA_guess = float.NaN, float maxA_guess = float.NaN, float pitchI_guess = float.NaN)
        {
#if ENABLE_PROFILER
            UnityEngine.Profiling.Profiler.BeginSample("EnvelopePoint..ctor");
#endif
            this.altitude = altitude;
            this.speed = speed;
            AeroPredictor.Conditions conditions = new AeroPredictor.Conditions(body, speed, altitude);
            float gravParameter, radius;
            gravParameter = (float)body.gravParameter;
            radius = (float)body.Radius;
            this.mach = conditions.mach;
            this.dynamicPressure = 0.0005f * conditions.atmDensity * speed * speed;
            float weight = (vessel.Mass * gravParameter / ((radius + altitude) * (radius + altitude))) - (vessel.Mass * speed * speed / (radius + altitude));
            //AoA_max = vessel.GetMaxAoA(conditions, out Lift_max, maxA_guess);
            AoA_level = vessel.GetAoA(conditions, weight, guess: AoA_guess, pitchInputGuess: 0, lockPitchInput: true);
            Vector3 thrustForce = vessel.GetThrustForce(conditions, AoA_level);
            fuelBurnRate = vessel.GetFuelBurnRate(conditions, AoA_level);
            if (float.IsNaN(maxA_guess))
            {
                AoA_max = vessel.GetMaxAoA(conditions, out Lift_max, maxA_guess);
                //Lift_max = AeroPredictor.GetLiftForceMagnitude(vessel.GetAeroForce(conditions, AoA_max, 1) + thrustForce, AoA_max);
            }
            else
            {
                AoA_max = maxA_guess;
                Lift_max = AeroPredictor.GetLiftForceMagnitude(vessel.GetLiftForce(conditions, AoA_max, 1) + (vessel.ThrustIsConstantWithAoA ? AeroPredictor.ToVesselFrame(thrustForce, AoA_max) : vessel.GetThrustForce(conditions, AoA_max)), AoA_max);
            }

            if (AoA_level < AoA_max)
                pitchInput = vessel.GetPitchInput(conditions, AoA_level, guess: pitchI_guess);
            else
                pitchInput = 1;

            if (speed < 5 && Math.Abs(altitude) < 10)
                AoA_level = 0;

            Thrust_available = AeroPredictor.GetUsefulThrustMagnitude(thrustForce);

            //vessel.GetAeroCombined(conditions, AoA_level, pitchInput, out force, out Vector3 torque);
            force = vessel.GetAeroForce(conditions, AoA_level, pitchInput);
            aeroforce = AeroPredictor.ToFlightFrame(force, AoA_level); //vessel.GetLiftForce(body, speed, altitude, AoA_level, mach, atmDensity);
            drag = -aeroforce.z;
            float lift = aeroforce.y;
            Thrust_excess = -drag - AeroPredictor.GetDragForceMagnitude(thrustForce, AoA_level);
            if (weight > Lift_max)// AoA_level >= AoA_max)
            {
                Thrust_excess = Lift_max - weight;
                AoA_level = AoA_max;
            }
            Accel_excess = (Thrust_excess / vessel.Mass / WindTunnelWindow.gAccel);
            LDRatio = Math.Abs(lift / drag);
            dLift = (vessel.GetLiftForceMagnitude(conditions, AoA_level + WindTunnelWindow.AoAdelta, pitchInput) - lift)
                / (WindTunnelWindow.AoAdelta * Mathf.Rad2Deg);
            //stabilityDerivative = (vessel.GetAeroTorque(conditions, AoA_level + WindTunnelWindow.AoAdelta, pitchInput).x - torque.x)
            //    / (WindTunnelWindow.AoAdelta * Mathf.Rad2Deg);
            //GetStabilityValues(vessel, conditions, AoA_level, out stabilityRange, out stabilityScore);

            completed = true;

#if ENABLE_PROFILER
            UnityEngine.Profiling.Profiler.EndSample();
#endif
        }

        private static void GetStabilityValues(AeroPredictor vessel, AeroPredictor.Conditions conditions, float AoA_centre, out float stabilityRange, out float stabilityScore)
        {
            const int step = 5;
            const int range = 90;
            const int alphaSteps = range / step;
            float[] torques = new float[2 * alphaSteps + 1];
            float[] aoas = new float[2 * alphaSteps + 1];
            int start, end;
            for (int i = 0; i <= 2 * alphaSteps; i++)
            {
                aoas[i] = (i - alphaSteps) * step * Mathf.Deg2Rad;
                torques[i] = vessel.GetAeroTorque(conditions, aoas[i], 0).x;
            }
            int eq = 0 + alphaSteps;
            int dir = (int)Math.Sign(torques[eq]);
            if (dir == 0)
            {
                start = eq - 1;
                end = eq + 1;
            }
            else
            {
                while (eq > 0 && eq < 2 * alphaSteps)
                {
                    eq += dir;
                    if (Math.Sign(torques[eq]) != dir)
                        break;
                }
                if (eq == 0 || eq == 2 * alphaSteps)
                {
                    stabilityRange = 0;
                    stabilityScore = 0;
                    return;
                }
                if (dir < 0)
                {
                    start = eq;
                    end = eq + 1;
                }
                else
                {
                    start = eq - 1;
                    end = eq;
                }
            }
            while (torques[start] > 0 && start > 0)
                start -= 1;
            while (torques[end] < 0 && end < 2 * alphaSteps - 1)
                end += 1;
            float min = (Mathf.InverseLerp(torques[start], torques[start + 1], 0) + start) * step;
            float max = (-Mathf.InverseLerp(torques[end], torques[end - 1], 0) + end) * step;
            stabilityRange = max - min;
            stabilityScore = 0;
            for (int i = start; i < end; i++)
            {
                stabilityScore += (torques[i] + torques[i + 1]) / 2 * step;
            }
        }

        public override string ToString()
        {
            return String.Format("Altitude:\t{0:N0}m\n" + "Speed:\t{1:N0}m/s\n" + "Mach:\t{9:N2}\n" + "Level Flight AoA:\t{2:N2}°\n" +
                    "Excess Thrust:\t{3:N0}kN\n" + "Excess Acceleration:\t{4:N2}g\n" + "Max Lift Force:\t{5:N0}kN\n" +
                    "Max Lift AoA:\t{6:N2}°\n" + "Lift/Drag Ratio:\t{8:N2}\n" + "Available Thrust:\t{7:N0}kN",
                    altitude, speed, AoA_level * Mathf.Rad2Deg,
                    Thrust_excess, Accel_excess, Lift_max,
                    AoA_max * Mathf.Rad2Deg, Thrust_available, LDRatio,
                    mach);
        }
    }
}
