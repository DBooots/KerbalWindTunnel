using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KerbalWindTunnel.Extensions;
using KerbalWindTunnel.Extensions.Reflection;

namespace KerbalWindTunnel.FARVesselCache
{
    public partial class FARWingInteractionWrapper
    {
        public object WrappedObject { get; private set; }

        public double EffectiveUpstreamMAC;
        public double EffectiveUpstreamb_2;
        public double EffectiveUpstreamLiftSlope;// { get; private set; }
        public double EffectiveUpstreamArea;
        public double EffectiveUpstreamStall;   // must be pushed
        public double EffectiveUpstreamCosSweepAngle;// { get; private set; }
        public double EffectiveUpstreamAoAMax;// { get; private set; }
        public double EffectiveUpstreamAoA;
        public double EffectiveUpstreamCd0; // must be pushed
        public double EffectiveUpstreamInfluence;// { get; private set; } // must be pushed

        private List<FARWingAerodynamicModelWrapper> nearbyWingModulesForwardList = new List<FARWingAerodynamicModelWrapper>();
        private List<FARWingAerodynamicModelWrapper> nearbyWingModulesBackwardList = new List<FARWingAerodynamicModelWrapper>();
        private List<FARWingAerodynamicModelWrapper> nearbyWingModulesLeftwardList = new List<FARWingAerodynamicModelWrapper>();
        private List<FARWingAerodynamicModelWrapper> nearbyWingModulesRightwardList = new List<FARWingAerodynamicModelWrapper>();

        private List<double> nearbyWingModulesForwardInfluence;
        private List<double> nearbyWingModulesBackwardInfluence;
        private List<double> nearbyWingModulesLeftwardInfluence;
        private List<double> nearbyWingModulesRightwardInfluence;


        private short srfAttachFlipped; // readonly
        private FloatCurve wingCamberFactor; // readonly
        private FloatCurve wingCamberMoment; // readonly

        internal FARWingAerodynamicModelWrapper parentWingModule;

        private static Func<object, double> ClInterferenceFactor_get;
        private static Func<FloatCurve> wingCamberFactor_get;
        private static Func<FloatCurve> wingCamberMoment_get;
        private static Action<object, double> effectiveUpstreamStall_set;
        private static Action<object, double> effectiveUpstreamCd0_set;
        private static Action<object, double> effectiveUpstreamInfluence_set;

        private static Func<object, List<double>> nearbyWingModulesForwardInfluence_get;
        private static Func<object, List<double>> nearbyWingModulesBackwardInfluence_get;
        private static Func<object, List<double>> nearbyWingModulesLeftwardInfluence_get;
        private static Func<object, List<double>> nearbyWingModulesRightwardInfluence_get;
        private static Action<object, List<double>> nearbyWingModulesForwardInfluence_set;
        private static Action<object, List<double>> nearbyWingModulesBackwardInfluence_set;
        private static Action<object, List<double>> nearbyWingModulesLeftwardInfluence_set;
        private static Action<object, List<double>> nearbyWingModulesRightwardInfluence_set;

        private static Func<object, IList> nearbyWingModulesForwardList_get;
        private static Func<object, IList> nearbyWingModulesBackwardList_get;
        private static Func<object, IList> nearbyWingModulesLeftwardList_get;
        private static Func<object, IList> nearbyWingModulesRightwardList_get;
        private static Action<object, IList> nearbyWingModulesForwardList_set;
        private static Action<object, IList> nearbyWingModulesBackwardList_set;
        private static Action<object, IList> nearbyWingModulesLeftwardList_set;
        private static Action<object, IList> nearbyWingModulesRightwardList_set;

        private static Func<object, short> srfAttachFlipped_get;

        private static Action<object, Vector3d> updateOrientationForInteraction_method;

        private static Func<IList, IList> cloneModulesList_method;

        private bool ListsSet = false;

        private FARWingInteractionWrapper() { }

        public FARWingInteractionWrapper(object wrappedObject)
        {
            if (!FARHook.FARWingInteractionType.IsAssignableFrom(wrappedObject.GetType()))
                throw new ArgumentException();
            this.WrappedObject = wrappedObject;
        }

        public static FARWingInteractionWrapper WrapAndCloneObject(object wrappedObject, FARWingAerodynamicModelWrapper parent = null, bool cloneInfluenceLists = true)
        {
            if (!FARHook.FARWingInteractionType.IsAssignableFrom(wrappedObject.GetType()))
                throw new ArgumentException();

            FARWingInteractionWrapper wrapper = new FARWingInteractionWrapper();

            if (wrappedObject == null)
                return wrapper;

            wrapper.WrappedObject = FARCloneAssist.MemberwiseClone_method.Invoke(wrappedObject, null); // wrappedObject.MemberwiseClone();

            wrapper.nearbyWingModulesForwardInfluence = nearbyWingModulesForwardInfluence_get(wrapper.WrappedObject);
            wrapper.nearbyWingModulesBackwardInfluence = nearbyWingModulesBackwardInfluence_get(wrapper.WrappedObject);
            wrapper.nearbyWingModulesLeftwardInfluence = nearbyWingModulesLeftwardInfluence_get(wrapper.WrappedObject);
            wrapper.nearbyWingModulesRightwardInfluence = nearbyWingModulesRightwardInfluence_get(wrapper.WrappedObject);

            if (cloneInfluenceLists)
            {
                wrapper.nearbyWingModulesForwardInfluence = wrapper.nearbyWingModulesForwardInfluence.ToList();
                wrapper.nearbyWingModulesBackwardInfluence = wrapper.nearbyWingModulesBackwardInfluence.ToList();
                wrapper.nearbyWingModulesLeftwardInfluence = wrapper.nearbyWingModulesLeftwardInfluence.ToList();
                wrapper.nearbyWingModulesRightwardInfluence = wrapper.nearbyWingModulesRightwardInfluence.ToList();
                nearbyWingModulesForwardInfluence_set(wrapper.WrappedObject, wrapper.nearbyWingModulesForwardInfluence);
                nearbyWingModulesBackwardInfluence_set(wrapper.WrappedObject, wrapper.nearbyWingModulesBackwardInfluence);
                nearbyWingModulesLeftwardInfluence_set(wrapper.WrappedObject, wrapper.nearbyWingModulesLeftwardInfluence);
                nearbyWingModulesRightwardInfluence_set(wrapper.WrappedObject, wrapper.nearbyWingModulesRightwardInfluence);
            }
            nearbyWingModulesForwardList_set(wrapper.WrappedObject, nearbyWingModulesForwardList_get(wrapper.WrappedObject).ToIList());
            nearbyWingModulesBackwardList_set(wrapper.WrappedObject, nearbyWingModulesBackwardList_get(wrapper.WrappedObject).ToIList());
            nearbyWingModulesLeftwardList_set(wrapper.WrappedObject, nearbyWingModulesLeftwardList_get(wrapper.WrappedObject).ToIList());
            nearbyWingModulesRightwardList_set(wrapper.WrappedObject, nearbyWingModulesRightwardList_get(wrapper.WrappedObject).ToIList());

            wrapper.srfAttachFlipped = srfAttachFlipped_get(wrapper.WrappedObject);
            wrapper.wingCamberFactor = wingCamberFactor_get().Clone();
            wrapper.wingCamberMoment = wingCamberMoment_get().Clone();
            wrapper.parentWingModule = parent;

            return wrapper;
        }

        public void SetNearbyWingModulesLists(Dictionary<object, FARWingAerodynamicModelWrapper> correlationDict)
        {
            if (ListsSet)
                return;

            IList currentList;

            currentList = nearbyWingModulesForwardList_get(WrappedObject);
            nearbyWingModulesForwardList.Clear();
            for (int i = currentList.Count - 1; i >= 0; i--)
            {
                nearbyWingModulesForwardList.Add(correlationDict[currentList[i]]);
                currentList[i] = correlationDict[currentList[i]].WrappedObject;
            }
            nearbyWingModulesForwardList.Reverse();

            currentList = nearbyWingModulesBackwardList_get(WrappedObject);
            nearbyWingModulesBackwardList.Clear();
            for (int i = currentList.Count - 1; i >= 0; i--)
            {
                nearbyWingModulesBackwardList.Add(correlationDict[currentList[i]]);
                currentList[i] = correlationDict[currentList[i]].WrappedObject;
            }
            nearbyWingModulesBackwardList.Reverse();

            currentList = nearbyWingModulesLeftwardList_get(WrappedObject);
            nearbyWingModulesLeftwardList.Clear();
            for (int i = currentList.Count - 1; i >= 0; i--)
            {
                nearbyWingModulesLeftwardList.Add(correlationDict[currentList[i]]);
                currentList[i] = correlationDict[currentList[i]].WrappedObject;
            }
            nearbyWingModulesLeftwardList.Reverse();

            currentList = nearbyWingModulesRightwardList_get(WrappedObject);
            nearbyWingModulesRightwardList.Clear();
            for (int i = currentList.Count - 1; i >= 0; i--)
            { 
                nearbyWingModulesRightwardList.Add(correlationDict[currentList[i]]);
                currentList[i] = correlationDict[currentList[i]].WrappedObject;
            }
            nearbyWingModulesRightwardList.Reverse();

            ListsSet = true;
        }

        public FARWingInteractionWrapper Clone(FARWingAerodynamicModelWrapper parent = null)
        {
            return WrapAndCloneObject(WrappedObject, parent, false);
        }

        public double ClInterferenceFactor { get => ClInterferenceFactor_get(WrappedObject); }

        public void UpdateOrientationForInteraction(Vector3d parallelInPlaneLocal) => updateOrientationForInteraction_method(WrappedObject, parallelInPlaneLocal);

        public static bool InitializeMethods(Type type)
        {
            ClInterferenceFactor_get = type.PropertyGet<double>("ClInterferenceFactor");

            wingCamberFactor_get = type.StaticFieldGet<FloatCurve>(type.GetField("wingCamberFactor", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
            wingCamberMoment_get = type.StaticFieldGet<FloatCurve>(type.GetField("wingCamberMoment", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
            effectiveUpstreamStall_set = type.InstanceMethod<Action<object, double>>(type.GetProperty("EffectiveUpstreamStall").GetSetMethod(true));
            effectiveUpstreamCd0_set = type.InstanceMethod<Action<object, double>>(type.GetProperty("EffectiveUpstreamCd0").GetSetMethod(true));
            effectiveUpstreamInfluence_set = type.InstanceMethod<Action<object, double>>(type.GetProperty("EffectiveUpstreamInfluence").GetSetMethod(true));

            System.Reflection.BindingFlags privateFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

            nearbyWingModulesForwardInfluence_get = type.FieldGet<List<double>>(type.GetField("nearbyWingModulesForwardInfluence", privateFlags));
            nearbyWingModulesBackwardInfluence_get = type.FieldGet<List<double>>(type.GetField("nearbyWingModulesBackwardInfluence", privateFlags));
            nearbyWingModulesLeftwardInfluence_get = type.FieldGet<List<double>>(type.GetField("nearbyWingModulesLeftwardInfluence", privateFlags));
            nearbyWingModulesRightwardInfluence_get = type.FieldGet<List<double>>(type.GetField("nearbyWingModulesRightwardInfluence", privateFlags));
            nearbyWingModulesForwardInfluence_set = type.FieldSet<List<double>>(type.GetField("nearbyWingModulesForwardInfluence", privateFlags));
            nearbyWingModulesBackwardInfluence_set = type.FieldSet<List<double>>(type.GetField("nearbyWingModulesBackwardInfluence", privateFlags));
            nearbyWingModulesLeftwardInfluence_set = type.FieldSet<List<double>>(type.GetField("nearbyWingModulesLeftwardInfluence", privateFlags));
            nearbyWingModulesRightwardInfluence_set = type.FieldSet<List<double>>(type.GetField("nearbyWingModulesRightwardInfluence", privateFlags));

            nearbyWingModulesForwardList_get = type.FieldGet<IList>(type.GetField("nearbyWingModulesForwardList", privateFlags));
            nearbyWingModulesBackwardList_get = type.FieldGet<IList>(type.GetField("nearbyWingModulesBackwardList", privateFlags));
            nearbyWingModulesLeftwardList_get = type.FieldGet<IList>(type.GetField("nearbyWingModulesLeftwardList", privateFlags));
            nearbyWingModulesRightwardList_get = type.FieldGet<IList>(type.GetField("nearbyWingModulesRightwardList", privateFlags));
            nearbyWingModulesForwardList_set = type.FieldSet<IList>(type.GetField("nearbyWingModulesForwardList", privateFlags));
            nearbyWingModulesBackwardList_set = type.FieldSet<IList>(type.GetField("nearbyWingModulesBackwardList", privateFlags));
            nearbyWingModulesLeftwardList_set = type.FieldSet<IList>(type.GetField("nearbyWingModulesLeftwardList", privateFlags));
            nearbyWingModulesRightwardList_set = type.FieldSet<IList>(type.GetField("nearbyWingModulesRightwardList", privateFlags));

            //Type listType = type.GetField("nearbyWingModulesForwardList", privateFlags).FieldType;
            cloneModulesList_method = typeof(object).InstanceMethod<Func<IList, IList>>(typeof(object).GetMethod("MemberwiseClone", privateFlags));

            srfAttachFlipped_get = type.FieldGet<short>(type.GetField("srfAttachFlipped", privateFlags));

            updateOrientationForInteraction_method = type.InstanceMethod<Action<object, Vector3d>>("UpdateOrientationForInteraction");

            return true;
        }
    }
}
