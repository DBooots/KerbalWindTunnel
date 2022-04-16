using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KerbalWindTunnel.Extensions.Reflection;

namespace KerbalWindTunnel.FARVesselCache
{
    public static class FARCloneAssist
    {
        public static readonly System.Reflection.MethodInfo MemberwiseClone_method = typeof(System.Object).GetMethod("MemberwiseClone", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        private static Action<object, object> xForceSkinFriction_set;
        private static Func<object, object> xForceSkinFriction_get;
        private static Action<object, object> xForcePressureAoA0_set;
        private static Func<object, object> xForcePressureAoA0_get;
        private static Action<object, object> xForcePressureAoA180_set;
        private static Func<object, object> xForcePressureAoA180_get;
        private static Func<object, IList> partData_get;

        private static Func<object, object> aeroModule_get;
        private static Action<object, object> aeroModule_set;

        private static Type PartDataType;

        public static List<object> CloneListFARAeroSections(IList ListFARAeroSections)
        {
            if (ListFARAeroSections.Count == 0)
                return new List<object>();

            if (!FARHook.FARAeroSectionType.IsAssignableFrom(ListFARAeroSections[0].GetType()))
                throw new ArgumentException();

            List<object> result = new List<object>();

            foreach (object section in ListFARAeroSections)
            {
                object clonedSection = MemberwiseClone_method.Invoke(section, null);

                IList partData = partData_get(clonedSection);
                for (int i = partData.Count - 1; i >= 0; i--)
                {
                    object newPartData = MemberwiseClone_method.Invoke(partData[i], null);
                    object oldAeroModule = aeroModule_get(partData[i]);
                    object newAeroModule = MemberwiseClone_method.Invoke(aeroModule_get(partData[i]), null);
                    aeroModule_set(newPartData, newAeroModule);
                    partData[i] = newPartData;
                }

                xForceSkinFriction_set(clonedSection, System.ObjectExtensions.ObjectExtensions.Copy(xForceSkinFriction_get(section)));
                xForcePressureAoA0_set(clonedSection, System.ObjectExtensions.ObjectExtensions.Copy(xForcePressureAoA0_get(section)));
                xForcePressureAoA180_set(clonedSection, System.ObjectExtensions.ObjectExtensions.Copy(xForcePressureAoA180_get(section)));
            }

            return result;
        }

        public static List<object> CloneListFARAeroSectionsSafe(List<object> ListFARAeroSections)
            => ListFARAeroSections.Select(section => MemberwiseClone_method.Invoke(section, null)).ToList(); // might be able to just return ListFARAeroSections directly...

        public static List<FARWingAerodynamicModelWrapper> CloneListFARWingAerodynamicModels(IList ListFARWingAerodynamicModels)
        {
            if (ListFARWingAerodynamicModels.Count == 0)
                return new List<FARWingAerodynamicModelWrapper>();

            if (ListFARWingAerodynamicModels[0].GetType() == typeof(FARWingAerodynamicModelWrapper))    // Mixed lists should be impossible
                return CloneListFARWingAerodynamicModelsSafe(ListFARWingAerodynamicModels.Cast<FARWingAerodynamicModelWrapper>());

            if (!FARHook.FARWingAerodynamicModelType.IsAssignableFrom(ListFARWingAerodynamicModels[0].GetType()))
                throw new ArgumentException();

            List<FARWingAerodynamicModelWrapper> result = new List<FARWingAerodynamicModelWrapper>();
            // Key is pre-clone, value is post-clone
            Dictionary<object, FARWingAerodynamicModelWrapper> correlationDict = new Dictionary<object, FARWingAerodynamicModelWrapper>();

            foreach (object wingModel in ListFARWingAerodynamicModels)
            {
                FARWingAerodynamicModelWrapper wrappedClone = FARWingAerodynamicModelWrapper.WrapAndCloneObject(wingModel);
                result.Add(wrappedClone);
                correlationDict.Add(wingModel, wrappedClone);
            }
            foreach (FARWingAerodynamicModelWrapper aeroModelWrapper in result)
                aeroModelWrapper.wingInteraction.SetNearbyWingModulesLists(correlationDict);

            return result;
        }
        public static List<FARWingAerodynamicModelWrapper> CloneListFARWingAerodynamicModelsSafe(IEnumerable<FARWingAerodynamicModelWrapper> wrappedWings)
        {
            List<FARWingAerodynamicModelWrapper> result = new List<FARWingAerodynamicModelWrapper>();
            // Key is pre-clone, value is post-clone
            Dictionary<object, FARWingAerodynamicModelWrapper> correlationDict = new Dictionary<object, FARWingAerodynamicModelWrapper>();
            foreach (FARWingAerodynamicModelWrapper wrapper in wrappedWings)
            {
                FARWingAerodynamicModelWrapper wrappedClone = wrapper.Clone();
                result.Add(wrappedClone);
                correlationDict.Add(wrapper.WrappedObject, wrappedClone);
            }

            foreach (FARWingAerodynamicModelWrapper aeroModelWrapper in result)
                aeroModelWrapper.wingInteraction.SetNearbyWingModulesLists(correlationDict);

            return result;
        }

        public static bool InitializeMethods(Type FARAeroSectionType)
        {
            PartDataType = FARAeroSectionType.GetNestedTypes().FirstOrDefault(t => t.Name.Contains("PartData"));
            aeroModule_get = PartDataType.FieldGet("aeroModule");
            aeroModule_set = PartDataType.FieldSet("aeroModule");

            xForceSkinFriction_set = FARAeroSectionType.FieldSet("xForceSkinFriction");
            xForceSkinFriction_get = FARAeroSectionType.FieldGet("xForceSkinFriction");
            xForcePressureAoA0_set = FARAeroSectionType.FieldSet("xForcePressureAoA0");
            xForcePressureAoA0_get = FARAeroSectionType.FieldGet("xForcePressureAoA0");
            xForcePressureAoA180_set = FARAeroSectionType.FieldSet("xForcePressureAoA180");
            xForcePressureAoA180_get = FARAeroSectionType.FieldGet("xForcePressureAoA180");
            partData_get = FARAeroSectionType.FieldGet<IList>(FARAeroSectionType.GetField("partData", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));

            return true;
        }
    }
}
