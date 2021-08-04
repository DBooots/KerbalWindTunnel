using System;
using UnityEngine;
using KSPPluginFramework;
using KerbalWindTunnel.Extensions.Reflection;

namespace KerbalWindTunnel.FARVesselCache
{
    public partial class FARWingAerodynamicModelWrapper
    {
        //private static object wingInteractionFloatCurveLock = new object();

        // Read before set
        public int nonSideAttach;           // readonly
        private double effective_MAC;
        private double cosSweepAngle;
        private Vector3 sweepPerpLocal;     // readonly
        private Vector3 sweepPerp2Local;    // readonly
        public double b_2_actual;           // readonly
        public double MAC_actual;           // readonly
        protected static double criticalCl;  // constant
        private double liftslope;
        protected double effective_AR;
        public double TaperRatio;           // readonly
        private double effective_b_2;
        private Vector3d CurWingCentroid;   // readonly
        public double S;                    // readonly
        public bool isShielded;             // readonly

        public double Effective_MAC { get => effective_MAC; }
        public double Effective_b_2 { get => effective_b_2; }
        public double RawLiftSlope { get => rawLiftSlope; }
        public double Stall { get => stall; }
        public double CosSweepAngle { get => cosSweepAngle; }
        public double ZeroLiftCdIncrement { get => zeroLiftCdIncrement_get(wrappedObject); }

        // Set before read
        private Vector3d ParallelInPlane;
        private Vector3d perp;
        private Vector3d liftDirection;
        private Vector3d ParallelInPlaneLocal;
        public Vector3d AerodynamicCenter { get; protected set; }
        public double Cl;
        public double Cd;
        private double minStall;
        private double rawLiftSlope;
        private double e;
        private double piARe;
        private double FinalLiftSlope;
        protected double ClIncrementFromRear;
        protected double transformed_AR;
        protected double stall;
        public double rawAoAmax;
        private double AoAmax;

        internal FARWingInteractionWrapper wingInteraction;
        public ThreadSafeTransform part_transform;

        private readonly object wrappedObject;
        public object WrappedObject { get => wrappedObject; }

        public Vector3d Vel { get; internal set; }

        #region Field Delegates
        private static Func<object, int> nonSideAttach_get;
        private static Func<object, double> effective_MAC_get;
        private static Action<object, double> effective_MAC_set;
        private static Func<object, double> cosSweepAngle_get;
        private static Action<object, double> cosSweepAngle_set;
        private static Func<object, Vector3d> sweepPerpLocal_get;
        private static Func<object, Vector3d> sweepPerp2Local_get;
        private static Func<object, double> b_2_actual_get;
        private static Func<object, double> MAC_actual_get;
        private static Func<object, double> liftslope_get;
        private static Action<object, double> liftslope_set;
        private static Func<object, double> effective_AR_get;
        private static Action<object, double> effective_AR_set;
        private static Func<object, double> TaperRatio_get;
        private static Func<object, double> effective_b_2_get;
        private static Action<object, double> effective_b_2_set;
        private static Func<object, Vector3d> CurWingCentroid_get;
        private static Func<object, double> S_get;
        private static Func<object, bool> isShielded_get;
        private static Func<object, double> zeroLiftCdIncrement_get;

        private static Func<object, Vector3d> ParallelInPlane_get;
        private static Action<object, Vector3d> ParallelInPlane_set;
        private static Func<object, Vector3d> perp_get;
        private static Action<object, Vector3d> perp_set;
        private static Func<object, Vector3d> liftDirection_get;
        private static Action<object, Vector3d> liftDirection_set;
        private static Func<object, Vector3d> ParallelInPlaneLocal_get;
        private static Action<object, Vector3d> ParallelInPlaneLocal_set;
        private static Func<object, Vector3d> AerodynamicCenter_get;
        private static Action<object, Vector3d> AerodynamicCenter_set;
        private static Func<object, double> Cl_get;
        private static Action<object, double> Cl_set;
        private static Func<object, double> Cd_get;
        private static Action<object, double> Cd_set;
        private static Func<object, double> minStall_get;
        private static Action<object, double> minStall_set;
        private static Func<object, double> rawLiftSlope_get;
        private static Action<object, double> rawLiftSlope_set;
        private static Func<object, double> e_get;
        private static Action<object, double> e_set;
        private static Func<object, double> piARe_get;
        private static Action<object, double> piARe_set;
        private static Func<object, double> FinalLiftSlope_get;
        private static Action<object, double> FinalLiftSlope_set;
        private static Func<object, double> ClIncrementFromRear_get;
        private static Action<object, double> ClIncrementFromRear_set;
        private static Func<object, double> transformed_AR_get;
        private static Action<object, double> transformed_AR_set;
        private static Func<object, double> stall_get;
        private static Action<object, double> stall_set;
        private static Action<object, double> rawAoAmax_set;
        private static Action<object, double> AoAmax_set;

        private static Func<object, object> wingInteraction_get;
        private static Action<object, object> wingInteraction_set;
#endregion

        #region Method Delegates
        //private delegate void calculateWingCamberInteractions_delegate(object obj, double MachNumber, double AoA, out double ACshift, out double ACWeight);
        private delegate double PMExpansionCalculation_delegate(double angle, double inM, out double outM);

        private static Func<object, double, double, Vector3d, Vector3d> calculateAerodynamicCenter_method;
        private static Func<object, double, double> calculateSubsonicLiftSlope_method;
        //private static calculateWingCamberInteractions_delegate calculateWingCamberInteractions_method;
        private static Func<object, double, double, double, double, double, double> CdCompressibilityZeroLiftIncrement_method;
        private static Action<object, double> determineStall_method;
        private static Func<double, double, double> CdMaxFlatPlate_method;
        private static Func<object, double, double, double, double> calculateSupersonicLEFactor_method;
        private static Func<object, double, double> calculateAoAmax_method;
#endregion

        public struct ThreadSafeTransform
        {
            public Quaternion rotation;
            public Vector3 position;
            public Vector3 forward;
            private Quaternion inverseRotation;

            public ThreadSafeTransform(Transform transform)
            {
                rotation = transform.rotation;
                position = transform.position;
                forward = rotation * Vector3.forward;
                inverseRotation = Quaternion.Inverse(rotation);
            }

            public Vector3 InverseTransformDirection(Vector3 direction) => inverseRotation * direction;
        }

        public FARWingAerodynamicModelWrapper(object wrappedObject)
        {
            if (!FARHook.FARWingAerodynamicModelType.IsAssignableFrom(wrappedObject.GetType()))
                throw new ArgumentException();

            this.wrappedObject = wrappedObject;
            part_transform = new ThreadSafeTransform(((PartModule)wrappedObject).part.transform);
            SyncFromObject();
            nonSideAttach = nonSideAttach_get(this.wrappedObject);
            sweepPerpLocal = sweepPerpLocal_get(this.wrappedObject);
            sweepPerp2Local = sweepPerp2Local_get(this.wrappedObject);
            b_2_actual = b_2_actual_get(this.wrappedObject);
            MAC_actual = MAC_actual_get(this.wrappedObject);
            TaperRatio = TaperRatio_get(this.wrappedObject);
            CurWingCentroid = CurWingCentroid_get(this.wrappedObject);
            S = S_get(this.wrappedObject);
            isShielded = isShielded_get(this.wrappedObject);
        }

        public static FARWingAerodynamicModelWrapper WrapAndCloneObject(object wrappedObject)
        {
            if (!FARHook.FARWingAerodynamicModelType.IsAssignableFrom(wrappedObject.GetType()))
                throw new ArgumentException();

            FARWingAerodynamicModelWrapper wrapper = new FARWingAerodynamicModelWrapper(FARCloneAssist.MemberwiseClone_method.Invoke(wrappedObject, null));
            wrapper.wingInteraction = FARWingInteractionWrapper.WrapAndCloneObject(wingInteraction_get(wrappedObject), wrapper);
            wingInteraction_set(wrapper.wrappedObject, wrapper.wingInteraction.WrappedObject);
            return wrapper;
        }

        public FARWingAerodynamicModelWrapper(FARWingAerodynamicModelWrapper toClone)
        {
            wrappedObject = FARCloneAssist.MemberwiseClone_method.Invoke(toClone.wrappedObject, null);
            wingInteraction = toClone.wingInteraction.Clone(this);
            wingInteraction_set(wrappedObject, wingInteraction.WrappedObject);

            nonSideAttach = toClone.nonSideAttach;
            effective_MAC = toClone.effective_MAC;
            cosSweepAngle = toClone.cosSweepAngle;
            sweepPerpLocal = toClone.sweepPerpLocal;
            sweepPerp2Local = toClone.sweepPerp2Local;
            b_2_actual = toClone.b_2_actual;
            MAC_actual = toClone.MAC_actual;
            liftslope = toClone.liftslope;
            effective_AR = toClone.effective_AR;
            TaperRatio = toClone.TaperRatio;
            effective_b_2 = toClone.effective_b_2;
            CurWingCentroid = toClone.CurWingCentroid;
            S = toClone.S;
        }

        public FARWingAerodynamicModelWrapper Clone() => new FARWingAerodynamicModelWrapper(this);

        public Vector3 ComputeForceEditor(Vector3 velocityVector, double mach, double density, AeroPredictor.Conditions conditions)
        {
            double aoa = CalculateAoA(velocityVector);
            return DoCalculateForces(velocityVector, mach, aoa, density, conditions);
        }

#region Delegated Methods
        public virtual double CalculateAoA(Vector3d velocity)
        {
            double PerpVelocity = Vector3d.Dot(part_transform.forward, velocity.normalized);
            return Math.Asin(PerpVelocity.Clamp(-1, 1));
        }

        public Vector3d CalculateAerodynamicCenter(double MachNumber, double AoA, Vector3d WC)
        {
            SyncToObject();
            Vector3d result = calculateAerodynamicCenter_method(wrappedObject, MachNumber, AoA, WC);
            //SyncFromObject();
            return result;
        }

        private double CalculateSubsonicLiftSlope(double MachNumber)
        {
            SyncToObject();
            double result = calculateSubsonicLiftSlope_method(wrappedObject, MachNumber);
            SyncFromObject();
            return result;
        }

        protected double CalculateAoAmax(double MachNumber)
        {
            SyncToObject();
            double result = calculateAoAmax_method(wrappedObject, MachNumber);
            SyncFromObject();
            return result;
        }

        private double CdCompressibilityZeroLiftIncrement(double M, double SweepAngle, double TanSweep, double beta_TanSweep, double beta)
        {
            SyncToObject();
            double result = CdCompressibilityZeroLiftIncrement_method(wrappedObject, M, SweepAngle, TanSweep, beta_TanSweep, beta);
            SyncFromObject();
            return result;
        }

        private void DetermineStall(double AoA)
        {
            SyncToObject();
            determineStall_method(wrappedObject, AoA);
            SyncFromObject();
        }

        private double CalculateSupersonicLEFactor(double beta, double TanSweep, double beta_TanSweep)
        {
            SyncToObject();
            double result = calculateSupersonicLEFactor_method(wrappedObject, beta, TanSweep, beta_TanSweep);
            SyncFromObject();
            return result;
        }

        private static double CdMaxFlatPlate(double M, double beta) => CdMaxFlatPlate_method(M, beta);

        private static double PMExpansionCalculation(double angle, double inM, AeroPredictor.Conditions conditions)
            => PMExpansionCalculation(angle, inM, out _, conditions);

#endregion

        private void SyncToObject()
        {
            effective_MAC_set(wrappedObject, effective_MAC);    //effective_MAC;
            cosSweepAngle_set(wrappedObject, cosSweepAngle);    //cosSweepAngle;
            liftslope_set(wrappedObject, liftslope);            //liftslope;
            effective_AR_set(wrappedObject, effective_AR);      //effective_AR;
            effective_b_2_set(wrappedObject, effective_b_2);    //effective_b_2;
            ParallelInPlane_set(wrappedObject, ParallelInPlane);    //ParallelInPlane;
            perp_set(wrappedObject, perp);                      //perp;
            liftDirection_set(wrappedObject, liftDirection);    //liftDirection;
            ParallelInPlaneLocal_set(wrappedObject, ParallelInPlaneLocal);  //ParallelInPlaneLocal;
            AerodynamicCenter_set(wrappedObject, AerodynamicCenter);    //AerodynamicCenter;
            Cl_set(wrappedObject, Cl);                          //Cl;
            Cd_set(wrappedObject, Cd);                          //Cd;
            minStall_set(wrappedObject, minStall);              //minStall;
            rawLiftSlope_set(wrappedObject, rawLiftSlope);      //rawLiftSlope;
            e_set(wrappedObject, e);                            //e;
            piARe_set(wrappedObject, piARe);                    //piARe;
            FinalLiftSlope_set(wrappedObject, FinalLiftSlope);  //FinalLiftSlope;
            ClIncrementFromRear_set(wrappedObject, ClIncrementFromRear);    //ClIncrementFromRear;
            transformed_AR_set(wrappedObject, transformed_AR);  //transformed_AR;
            stall_set(wrappedObject, stall);                    //stall;
        }
        private void SyncFromObject()
        {
            effective_MAC = effective_MAC_get(wrappedObject);
            cosSweepAngle = cosSweepAngle_get(wrappedObject);
            liftslope = liftslope_get(wrappedObject);
            effective_AR = effective_AR_get(wrappedObject);
            effective_b_2 = effective_b_2_get(wrappedObject);
            ParallelInPlane = ParallelInPlane_get(wrappedObject);
            perp = perp_get(wrappedObject);
            liftDirection = liftDirection_get(wrappedObject);
            ParallelInPlaneLocal = ParallelInPlaneLocal_get(wrappedObject);
            AerodynamicCenter = AerodynamicCenter_get(wrappedObject);
            Cl = Cl_get(wrappedObject);
            Cd = Cd_get(wrappedObject);
            minStall = minStall_get(wrappedObject);
            rawLiftSlope = rawLiftSlope_get(wrappedObject);
            e = e_get(wrappedObject);
            piARe = piARe_get(wrappedObject);
            FinalLiftSlope = FinalLiftSlope_get(wrappedObject);
            ClIncrementFromRear = ClIncrementFromRear_get(wrappedObject);
            transformed_AR = transformed_AR_get(wrappedObject);
            stall = stall_get(wrappedObject);
        }

        public static bool InitializeMethods(Type type)
        {
            System.Reflection.BindingFlags privateFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            nonSideAttach_get = type.FieldGet<int>("nonSideAttach");
            effective_MAC_get = type.FieldGet<double>(type.GetField("effective_MAC", privateFlags));
            effective_MAC_set = type.FieldSet<double>(type.GetField("effective_MAC", privateFlags));
            cosSweepAngle_get = type.FieldGet<double>(type.GetField("cosSweepAngle", privateFlags));
            cosSweepAngle_set = type.FieldSet<double>(type.GetField("cosSweepAngle", privateFlags));
            sweepPerpLocal_get = type.FieldGet<Vector3d>(type.GetField("sweepPerpLocal", privateFlags));
            sweepPerp2Local_get = type.FieldGet<Vector3d>(type.GetField("sweepPerp2Local", privateFlags));
            b_2_actual_get = type.FieldGet<double>("b_2_actual");
            MAC_actual_get = type.FieldGet<double>("MAC_actual");
            liftslope_get = type.FieldGet<double>(type.GetField("liftslope", privateFlags));
            liftslope_set = type.FieldSet<double>(type.GetField("liftslope", privateFlags));
            effective_AR_get = type.FieldGet<double>(type.GetField("effective_AR", privateFlags));
            effective_AR_set = type.FieldSet<double>(type.GetField("effective_AR", privateFlags));
            TaperRatio_get = type.FieldGet<double>("TaperRatio");
            effective_b_2_get = type.FieldGet<double>(type.GetField("effective_b_2", privateFlags));
            effective_b_2_set = type.FieldSet<double>(type.GetField("effective_b_2", privateFlags));
            CurWingCentroid_get = type.FieldGet<Vector3d>(type.GetField("CurWingCentroid", privateFlags));
            S_get = type.FieldGet<double>("S");
            isShielded_get = type.FieldGet<bool>("isShielded");
            zeroLiftCdIncrement_get = type.FieldGet<double>(type.GetField("zeroLiftCdIncrement", privateFlags));

            ParallelInPlane_get = type.FieldGet<Vector3d>(type.GetField("ParallelInPlane", privateFlags));
            ParallelInPlane_set = type.FieldSet<Vector3d>(type.GetField("ParallelInPlane", privateFlags));
            perp_get = type.FieldGet<Vector3d>(type.GetField("perp", privateFlags));
            perp_set = type.FieldSet<Vector3d>(type.GetField("perp", privateFlags));
            liftDirection_get = type.FieldGet<Vector3d>(type.GetField("liftDirection", privateFlags));
            liftDirection_set = type.FieldSet<Vector3d>(type.GetField("liftDirection", privateFlags));
            ParallelInPlaneLocal_get = type.FieldGet<Vector3d>(type.GetField("ParallelInPlaneLocal", privateFlags));
            ParallelInPlaneLocal_set = type.FieldSet<Vector3d>(type.GetField("ParallelInPlaneLocal", privateFlags));
            AerodynamicCenter_get = type.FieldGet<Vector3d>("AerodynamicCenter");
            AerodynamicCenter_set = type.FieldSet<Vector3d>("AerodynamicCenter");
            Cl_get = type.FieldGet<double>("Cl");
            Cl_set = type.FieldSet<double>("Cl");
            Cd_get = type.FieldGet<double>("Cd");
            Cd_set = type.FieldSet<double>("Cd");
            minStall_get = type.FieldGet<double>(type.GetField("minStall", privateFlags));
            minStall_set = type.FieldSet<double>(type.GetField("minStall", privateFlags));
            rawLiftSlope_get = type.FieldGet<double>(type.GetField("rawLiftSlope", privateFlags));
            rawLiftSlope_set = type.FieldSet<double>(type.GetField("rawLiftSlope", privateFlags));
            e_get = type.FieldGet<double>("e");
            e_set = type.FieldSet<double>("e");
            piARe_get = type.FieldGet<double>(type.GetField("piARe", privateFlags));
            piARe_set = type.FieldSet<double>(type.GetField("piARe", privateFlags));
            FinalLiftSlope_get = type.PropertyGet<double>("FinalLiftSlope");
            FinalLiftSlope_set = type.InstanceMethod<Action<object, double>>(type.GetProperty("FinalLiftSlope").GetSetMethod(true));
            ClIncrementFromRear_get = type.FieldGet<double>(type.GetField("ClIncrementFromRear", privateFlags));
            ClIncrementFromRear_set = type.FieldSet<double>(type.GetField("ClIncrementFromRear", privateFlags));
            transformed_AR_get = type.FieldGet<double>(type.GetField("transformed_AR", privateFlags));
            transformed_AR_set = type.FieldSet<double>(type.GetField("transformed_AR", privateFlags));
            stall_get = type.FieldGet<double>(type.GetField("stall", privateFlags));
            stall_set = type.FieldSet<double>(type.GetField("stall", privateFlags));
            rawAoAmax_set = type.FieldSet<double>("rawAoAmax");
            AoAmax_set = type.FieldSet<double>(type.GetField("AoAmax", privateFlags));

            wingInteraction_get = type.FieldGet(type.GetField("wingInteraction", privateFlags));
            wingInteraction_set = type.FieldSet(type.GetField("wingInteraction", privateFlags));

            calculateAerodynamicCenter_method = type.InstanceMethod<Func<object, double, double, Vector3d, Vector3d>>(type.GetMethod("CalculateAerodynamicCenter", privateFlags));
            calculateSubsonicLiftSlope_method = type.InstanceMethod<Func<object, double, double>>(type.GetMethod("CalculateSubsonicLiftSlope", privateFlags));
            //calculateWingCamberInteractions_method = type.InstanceMethod<calculateWingCamberInteractions_delegate>(type.GetMethod("CalculateWingCamberInteractions", privateFlags));//, new Type[] { typeof(object), typeof(double), typeof(double), typeof(double), typeof(double) });
            calculateAoAmax_method = type.InstanceMethod<Func<object, double, double>>(type.GetMethod("CalculateAoAmax", privateFlags));
            CdCompressibilityZeroLiftIncrement_method = type.InstanceMethod<Func<object, double, double, double, double, double, double>>(type.GetMethod("CdCompressibilityZeroLiftIncrement", privateFlags));//, new Type[] { typeof(object), typeof(double), typeof(double), typeof(double), typeof(double), typeof(double) });
            determineStall_method = type.InstanceMethod<Action<object, double>>(type.GetMethod("DetermineStall", privateFlags));//, new Type[] { typeof(object), typeof(double) });
            CdMaxFlatPlate_method = type.StaticMethod<Func<double, double, double>>(type.GetMethod("CdMaxFlatPlate", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
            calculateSupersonicLEFactor_method = type.InstanceMethod<Func<object, double, double, double, double>>(type.GetMethod("CalculateSupersonicLEFactor", privateFlags));

            return true;
        }
    }
}
