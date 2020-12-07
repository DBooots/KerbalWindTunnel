using System;
using System.Linq;
using System.Reflection;
using KerbalWindTunnel.Extensions.Reflection;

namespace KerbalWindTunnel.FARVesselCache
{
    public static class FARHook
    {
        private static bool initiated = false;
        private static bool installed = false;
        private static Assembly FARassembly;
        public static Type simulationType;
        public static Type simulationOutputType;
        public static Action<CelestialBody> UpdateCurrentBody;

        public static bool FARInstalled
        {
            get
            {
                if (!initiated)
                    Initiate();
                return installed;
            }
        }

        public static void Initiate()
        {
            if (initiated) return;

            const string assemblyName = "FerramAerospaceResearch";
            FARassembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName.Contains(assemblyName));
            if (FARassembly == null)
            {
                UnityEngine.Debug.Log("Kerbal Wind Tunnel - FAR not installed");
                installed = false;
                initiated = true;
                return;
            }
            else
                UnityEngine.Debug.Log("Kerbal Wind Tunnel - FAR is installed.");
            installed = true;

            simulationType = FARassembly.GetTypes().FirstOrDefault(t => t.Name.Contains("InstantConditionSimulation"));
            simulationOutputType = FARassembly.GetTypes().FirstOrDefault(t => t.Name.Contains("InstantConditionSimOutput"));
            if (simulationType == null || simulationOutputType == null)
            {
                UnityEngine.Debug.LogError("Kerbal Wind Tunnel - FAR API type not found!");
                if (simulationType == null)
                    UnityEngine.Debug.Log("Could not find 'InstandConditionSimulation' Type.");
                if (simulationOutputType == null)
                    UnityEngine.Debug.Log("Could not find 'InstantConditionSimOutput' Type.");
                installed = false;
                initiated = true;
                return;
            }

            try
            {
                UpdateCurrentBody = (Action<CelestialBody>)Delegate.CreateDelegate(typeof(Action<CelestialBody>), FARassembly.GetTypes().FirstOrDefault(t => t.Name.Contains("FARAPI")).GetNestedType("Simulation").GetMethod("UpdateCurrentBody"));

                InstantConditionSimulationWrapper._constructor = simulationType.Constructor();
                InstantConditionSimulationWrapper._iterationOutput = simulationType.FieldGet<object>("iterationOutput");
                InstantConditionSimulationWrapper._ready = simulationType.PropertyGet<bool>("Ready");
                InstantConditionSimulationWrapper._calculateAccelerationDueToGravity = simulationType.InstanceMethod<Func<object, CelestialBody, double, double>>("CalculateAccelerationDueToGravity");
                //InstantConditionSimulationWrapper._computeNonDimensionalForcesO = simulationType.InstanceMethod<Func<object, object, bool, bool, object>>("ComputeNonDimensionalForces");
                InstantConditionSimulationWrapper._computeNonDimensionalForcesS = simulationType.InstanceMethod<Func<object, double, double, double, double, double, double, double, double, bool, bool, object>>("ComputeNonDimensionalForces");
                InstantConditionSimulationWrapper._computeNonDimensionalForcesE = simulationType.InstanceMethod<Func<object, double, double, double, double, double, double, double, double, int, bool, bool, bool, object>>("ComputeNonDimensionalForces");
                //InstantConditionSimulationWrapper._setState = simulationType.InstanceMethod<Action<object, double, double, Vector3d, double, int, bool>>("SetState");
                InstantConditionSimulationWrapper._functionIterateForAlpha = simulationType.InstanceMethod<Func<object, double, double>>("FunctionIterateForAlpha");
                //InstantConditionSimulationWrapper._computeRequiredAoA = simulationType.InstanceMethod<Func<object, double, double, Vector3d, double, int, bool, double>>("ComputeRequiredAoA");
                InstantConditionSimulationWrapper._update = simulationType.InstanceMethod<Action<object>>("Update");

                InstantConditionSimOutputWrapper._getCl = simulationOutputType.FieldGet<double>("Cl");
                InstantConditionSimOutputWrapper._getCd = simulationOutputType.FieldGet<double>("Cd");
                InstantConditionSimOutputWrapper._getCm = simulationOutputType.FieldGet<double>("Cm");
                InstantConditionSimOutputWrapper._getCy = simulationOutputType.FieldGet<double>("Cy");
                InstantConditionSimOutputWrapper._getCn = simulationOutputType.FieldGet<double>("Cn");
                InstantConditionSimOutputWrapper._getC_roll = simulationOutputType.FieldGet<double>("C_roll");
                InstantConditionSimOutputWrapper._getArea = simulationOutputType.FieldGet<double>("area");
                InstantConditionSimOutputWrapper._getMAC = simulationOutputType.FieldGet<double>("MAC");

            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("Kerbal Wind Tunnel - Encountered exception on loading FAR hooks:");
                UnityEngine.Debug.LogException(ex);
                installed = false;
            }

            initiated = true;
        }
    }
}
