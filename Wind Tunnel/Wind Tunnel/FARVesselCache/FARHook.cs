using System;
using System.Linq;
using System.Reflection;

namespace KerbalWindTunnel.FARVesselCache
{
    public static class FARHook
    {
        private static bool initiated = false;
        private static bool installed = false;
        private static Assembly FARassembly;
        public static Type simulationType;
        public static Type FARWingInteractionType;
        public static Type FARWingAerodynamicModelType;
        public static Type FARControllableSurfaceType;
        public static Type FARAeroSectionType;

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
            try
            {
                const string assemblyName = "FerramAerospaceResearch";
                FARassembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName.Contains(assemblyName) && !a.FullName.Contains(assemblyName + ".Base"));
                if (FARassembly == null)
                {
                    UnityEngine.Debug.Log("KerbalWindTunnel: Using Stock Aero.");
                    installed = false;
                    initiated = true;
                    return;
                }
                UnityEngine.Debug.Log("KerbalWindTunnel: Using FAR Aero.");
                installed = true;

                simulationType = FARassembly.GetTypes().FirstOrDefault(t => t.Name.Contains("InstantConditionSim"));
                Type editorGUIType = FARassembly.GetTypes().FirstOrDefault(t => t.Name.Contains("EditorGUI"));
                Type FARAeroUtilType = FARassembly.GetTypes().FirstOrDefault(t => t.Name.Contains("FARAeroUtil"));
                Type FARAtmosphereType = FARassembly.GetTypes().FirstOrDefault(t => t.Name.Contains("FARAtmosphere"));
                FARWingInteractionType = FARassembly.GetTypes().FirstOrDefault(t => t.Name.Contains("FARWingInteraction"));
                FARWingAerodynamicModelType = FARassembly.GetTypes().FirstOrDefault(t => t.Name.Contains("FARWingAerodynamicModel"));
                FARAeroSectionType = FARassembly.GetTypes().FirstOrDefault(t => t.Name.Contains("FARAeroSection"));
                FARControllableSurfaceType = FARassembly.GetTypes().FirstOrDefault(t => t.Name.Contains("FARControllableSurface"));

                if (simulationType == null || FARAeroUtilType == null || FARAtmosphereType == null || editorGUIType == null || FARWingInteractionType == null || FARWingAerodynamicModelType == null || FARAeroSectionType == null)
                {
                    UnityEngine.Debug.LogError("KerbalWindTunnel: FAR API type not found!");
                    if (simulationType == null) UnityEngine.Debug.LogError("InstantConditionSim not found.");
                    if (editorGUIType == null) UnityEngine.Debug.LogError("EditorGUI not found.");
                    if (FARAeroUtilType == null) UnityEngine.Debug.LogError("FARAeroUtil not found.");
                    if (FARAtmosphereType == null) UnityEngine.Debug.LogError("FARAtmosphere not found.");
                    if (FARWingAerodynamicModelType == null) UnityEngine.Debug.LogError("FARWingAerodynamicModel not found.");
                    if (FARWingInteractionType == null) UnityEngine.Debug.LogError("FARWingInteraction not found.");
                    if (FARAeroSectionType == null) UnityEngine.Debug.LogError("FARAeroSection not found.");
                    installed = false;
                    initiated = true;
                    return;
                }

                if (!FARVesselCache.SetMethods(simulationType, editorGUIType))
                    installed = false;
                if (!FARMethodAssist.Initialize(FARassembly))
                    installed = false;
                if (!FARAeroUtil.InitializeMethods(FARAeroUtilType, FARAtmosphereType))
                    installed = false;
                if (!FARWingAerodynamicModelWrapper.InitializeMethods(FARWingAerodynamicModelType))
                    installed = false;
                if (!FARWingInteractionWrapper.InitializeMethods(FARWingInteractionType))
                    installed = false;
                if (!FARCloneAssist.InitializeMethods(FARAeroSectionType))
                    installed = false;
                if (installed == false)
                    UnityEngine.Debug.LogError("KerbalWindTunnel: Some FAR initialization failed.");

                initiated = true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("KerbalWindTunnel: Exception when initiating FAR Aero. Using Stock Aero.");
                installed = false;
                initiated = true;
                UnityEngine.Debug.LogException(ex);
                return;
            }
        }
    }
}
