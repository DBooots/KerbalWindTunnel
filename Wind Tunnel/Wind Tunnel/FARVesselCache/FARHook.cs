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
                UnityEngine.Debug.Log("FAR not installed");
                installed = false;
                initiated = true;
                return;
            }
            installed = true;

            simulationType = FARassembly.GetTypes().FirstOrDefault(t => t.Name.Contains("InstantConditionSimulation"));
            simulationOutputType = FARassembly.GetTypes().FirstOrDefault(t => t.Name.Contains("InstantConditionSimOutput"));
            if (simulationType == null || simulationOutputType == null)
            {
                UnityEngine.Debug.Log("FAR API type not found!");
                installed = false;
                initiated = true;
                return;
            }

            UpdateCurrentBody = (Action<CelestialBody>)Delegate.CreateDelegate(typeof(Action<CelestialBody>), FARassembly.GetTypes().FirstOrDefault(t => t.Name.Contains("FARAPI")).GetNestedType("Simulation").GetMethod("UpdateCurrentBody"));

            InstantConditionSimulationWrapper._constructor = simulationType.Constructor();
            InstantConditionSimulationWrapper._iterationOutput = simulationType.PropertyGet<object>("iterationOutput");
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

            initiated = true;
        }
    }
}
