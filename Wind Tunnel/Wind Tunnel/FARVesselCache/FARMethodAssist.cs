using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using KerbalWindTunnel.Extensions.Reflection;
using UnityEngine;

namespace KerbalWindTunnel.FARVesselCache
{
    public static class FARMethodAssist
    {
        private static bool initialized = false;

        private static Func<object, double> InstantConditionSim__maxCrossSectionFromBody_get;
        private static Func<object, double> InstantConditionSim__bodyLength_get;
        private static Func<object, object> InstantConditionSim__currentAeroSections_get;
        private static Func<object, IList> InstantConditionSim__wingAerodynamicModel_get;

        private static Action<object, Vector3, Vector3, float, float, float, int, bool> FARControllableSurface_SetControlStateEditor_method;

        private static Func<object> FARCenterQuery_ctor;
        private static Func<object, Vector3d> FARCenterQuery_force_get;
        private static Func<object, Vector3d, Vector3d> FARCenterQuery_TorqueAt_method;

        private static Action<object, float, float, float, float, float, Vector3, object> FARAeroSection_PredictionCalculateAeroForces_method;


        public static bool Initialize(Assembly assembly)
        {
            //if (initialized)
                //return true;

            Type InstantConditionSim = assembly.GetTypes().FirstOrDefault(t => t.Name.Contains("InstantConditionSim"));
            InstantConditionSim__maxCrossSectionFromBody_get = InstantConditionSim.FieldGet<double>("_maxCrossSectionFromBody");
            InstantConditionSim__bodyLength_get = InstantConditionSim.FieldGet<double>("_bodyLength");
            //InstantConditionSim__currentAeroSections_get = InstantConditionSim.FieldGet<IList>(InstantConditionSim.GetField("_currentAeroSections", BindingFlags.Instance | BindingFlags.NonPublic));
            InstantConditionSim__currentAeroSections_get = InstantConditionSim.FieldGet(InstantConditionSim.GetField("_currentAeroSections", BindingFlags.Instance | BindingFlags.NonPublic));
            InstantConditionSim__wingAerodynamicModel_get = InstantConditionSim.FieldGet<IList>(InstantConditionSim.GetField("_wingAerodynamicModel", BindingFlags.Instance | BindingFlags.NonPublic));

            Type FARControllableSurface = assembly.GetTypes().FirstOrDefault(t => t.Name.Contains("FARControllableSurface"));
            FARControllableSurface_SetControlStateEditor_method = FARControllableSurface.InstanceMethod<Action<object, Vector3, Vector3, float, float, float, int, bool>>("SetControlStateEditor", new Type[] { typeof(object), typeof(Vector3), typeof(Vector3), typeof(float), typeof(float), typeof(float), typeof(int), typeof(bool) });
            //FARControllableSurface_SetControlStateEditor_method = (o, v1, v2, f1, f12, f3, i, b) => { };

            Type FARCenterQuery = assembly.GetTypes().FirstOrDefault(t => t.Name.Contains("FARCenterQuery"));
            FARCenterQuery_ctor = FARCenterQuery.Constructor();
            FARCenterQuery_force_get = FARCenterQuery.FieldGet<Vector3d>("force");
            FARCenterQuery_TorqueAt_method = FARCenterQuery.InstanceMethod<Func<object, Vector3d, Vector3d>>("TorqueAt");

            Type FARAeroSection = assembly.GetTypes().FirstOrDefault(t => t.Name.Contains("FARAeroSection"));
            FARAeroSection_PredictionCalculateAeroForces_method = FARAeroSection.InstanceMethod<Action<object, float, float, float, float, float, Vector3, object>>(FARAeroSection.GetMethod("PredictionCalculateAeroForces", BindingFlags.Instance | BindingFlags.Public));//, new Type[] { typeof(object), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(Vector3), FARCenterQuery });

            initialized = true;
            return true;
        }

        public static float InstantConditionSim__maxCrossSectionFromBody(object InstantConditionSimObj)
            => (float)InstantConditionSim__maxCrossSectionFromBody_get(InstantConditionSimObj);
        public static float InstantConditionSim__bodyLength(object InstantConditionSimObj)
            => (float)InstantConditionSim__bodyLength_get(InstantConditionSimObj);
        public static IList InstantConditionSim__currentAeroSections(object InstantConditionSimObj)
        //=> InstantConditionSim__currentAeroSections_get(InstantConditionSimObj);
        {
            object value = InstantConditionSim__currentAeroSections_get(InstantConditionSimObj);
            return (IList)value;
        }
        public static IList InstantConditionSim__wingAerodynamicModel(object InstantConditionSimObj)
            => InstantConditionSim__wingAerodynamicModel_get(InstantConditionSimObj);

        public static void FARControllableSurface_SetControlStateEditor(object FARControllableSurfaceObj,
            Vector3 CoM, Vector3 velocityVec, float pitch, float yaw, float roll, int flap, bool braking)
            => FARControllableSurface_SetControlStateEditor_method(FARControllableSurfaceObj, CoM, velocityVec, pitch, yaw, roll, flap, braking);

        public static object NewFARCenterQuery() => FARCenterQuery_ctor();
        public static Vector3 FARCenterQuery_force(object FARCenterQueryObj) => FARCenterQuery_force_get(FARCenterQueryObj);
        public static Vector3 FARCenterQuery_TorqueAt(object FARCenterQueryObj, Vector3 point)
            => FARCenterQuery_TorqueAt_method(FARCenterQueryObj, point);

        public static void FARAeroSection_PredictionCalculateAeroForces(object FARAeroSectionObj, float atmDensity, float machNumber, float reynoldsPerUnitLength, float pseudoKnudsenNumber, float skinFrictionDrag, Vector3 vel, object center)
            => FARAeroSection_PredictionCalculateAeroForces_method(FARAeroSectionObj, atmDensity, machNumber, reynoldsPerUnitLength, pseudoKnudsenNumber, skinFrictionDrag, vel, center);
        
    }
}
