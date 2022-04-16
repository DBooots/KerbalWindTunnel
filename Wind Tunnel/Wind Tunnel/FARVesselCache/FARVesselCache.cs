using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Smooth.Pools;
using KerbalWindTunnel.Extensions.Reflection;

namespace KerbalWindTunnel.FARVesselCache
{
    public partial class FARVesselCache : AeroPredictor, VesselCache.IReleasable
    {
        public List<VesselCache.SimulatedEngine> engines = new List<VesselCache.SimulatedEngine>();

        public override float Mass => totalMass;
        public override float Area => area;
        public override bool ThreadSafe => true;
        private static readonly Pool<FARVesselCache> pool = new Pool<FARVesselCache>(Create, Reset);

        public static bool accountForControls = false;

        public float totalMass = 0;
        public float dryMass = 0;
        public float area = 0;
        public float MAC = 0;
        public float b_2 = 0;
        public float maxCrossSectionFromBody = 0;
        public float bodyLength = 0;

        private FARVesselCache parent = null;

        public static int PoolSize
        {
            get { return pool.Size; }
        }

        public override bool ThrustIsConstantWithAoA => true;

        private static Func<object> getFAREditorGUIInstance;
        private static Func<object, object> getFARSimInstance;

        private List<FARWingAerodynamicModelWrapper> _wingAerodynamicModel;
        private List<object> _currentAeroSections;

        internal static bool SetMethods(Type FARType, Type editorGUIType)
        {
            getFAREditorGUIInstance = editorGUIType.StaticMethod<Func<object>>(editorGUIType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public).GetGetMethod());
            getFARSimInstance = editorGUIType.FieldGet(editorGUIType.GetField("_instantSim", BindingFlags.Instance | BindingFlags.NonPublic));

            return true;
        }

        private static FARVesselCache Create()
        {
            return new FARVesselCache();
        }

        public void Release()
        {
            pool.Release(this);
        }

        private static void Reset(FARVesselCache obj)
        {
            VesselCache.SimulatedEngine.Release(obj.engines);
            obj.engines.Clear();
        }

        public static FARVesselCache Borrow(IShipconstruct v, CelestialBody body)
        {
            FARVesselCache vessel;
            lock (pool)
                vessel = pool.Borrow();
            vessel.Init(v, body);
            return vessel;
        }
        public static FARVesselCache BorrowClone(FARVesselCache predictor)
        {
            FARVesselCache clone;
            lock (pool)
                clone = pool.Borrow();
            clone.InitClone(predictor);
            return clone;
        }

        private static object GetFARSimulationInstance()
        {
            return getFARSimInstance(getFAREditorGUIInstance());
        }

        public void Init(IShipconstruct v, CelestialBody body)
        {
            FARAeroUtil.UpdateCurrentActiveBody(body);
            object simInstance = GetFARSimulationInstance();

            maxCrossSectionFromBody = FARMethodAssist.InstantConditionSim__maxCrossSectionFromBody(simInstance);
            bodyLength = FARMethodAssist.InstantConditionSim__bodyLength(simInstance);

            _wingAerodynamicModel = FARCloneAssist.CloneListFARWingAerodynamicModels(FARMethodAssist.InstantConditionSim__wingAerodynamicModel(simInstance));
            _currentAeroSections = FARCloneAssist.CloneListFARAeroSections(FARMethodAssist.InstantConditionSim__currentAeroSections(simInstance));

            parent = null;

            List<Part> oParts = v.Parts;
            int count = oParts.Count;

            bool lgWarning = false;
            int stage = 0;
            for (int i = 0; i < count; i++)
            {
                if (!lgWarning)
                {
                    ModuleWheels.ModuleWheelDeployment gear = oParts[i].FindModuleImplementing<ModuleWheels.ModuleWheelDeployment>();
                    bool forcedRetract = !oParts[i].ShieldedFromAirstream && gear != null && gear.Position > 0;

                    if (forcedRetract)
                        lgWarning = true;
                }

                totalMass += oParts[i].mass + oParts[i].GetResourceMass();
                dryMass += oParts[i].mass;
                CoM += (oParts[i].mass + oParts[i].GetResourceMass()) * oParts[i].transform.TransformPoint(oParts[i].CoMOffset);
                CoM_dry += (oParts[i].mass) * oParts[i].transform.TransformPoint(oParts[i].CoMOffset);

                if (oParts[i].inverseStage > stage)
                {
                    VesselCache.SimulatedEngine.Release(engines);
                    engines.Clear();
                    stage = oParts[i].inverseStage;
                }
                if (oParts[i].inverseStage >= stage)
                {
                    MultiModeEngine multiMode = oParts[i].FindModuleImplementing<MultiModeEngine>();
                    if (multiMode != null)
                    {
                        engines.Add(VesselCache.SimulatedEngine.Borrow(oParts[i].FindModulesImplementing<ModuleEngines>().Find(engine => engine.engineID == multiMode.mode), this));
                    }
                    else
                    {
                        ModuleEngines engine = oParts[i].FindModulesImplementing<ModuleEngines>().FirstOrDefault();
                        if (engine != null)
                            engines.Add(VesselCache.SimulatedEngine.Borrow(engine, this));
                    }
                }
            }

            double area = 0;
            double MAC = 0;
            double b_2 = 0;
            foreach (FARWingAerodynamicModelWrapper wingAerodynamicModel in _wingAerodynamicModel)
            {
                if (!(wingAerodynamicModel != null && ((PartModule)wingAerodynamicModel.WrappedObject).part != null))
                    continue;
                if (!(FARHook.FARControllableSurfaceType.IsAssignableFrom(wingAerodynamicModel.WrappedObject.GetType()) && !wingAerodynamicModel.isShielded))
                    continue;

                float S = (float)wingAerodynamicModel.S;
                area += S;
                MAC += wingAerodynamicModel.Effective_MAC * S;
                b_2 += wingAerodynamicModel.Effective_b_2 * S;

                if (area == 0 || Math.Abs(area) < 1e-14 * double.Epsilon)
                {
                    area = maxCrossSectionFromBody;
                    b_2 = 1;
                    MAC = bodyLength;
                }
            }
            double recipArea = 1 / area;
            MAC *= recipArea;
            b_2 *= recipArea;
            this.area = (float)area;
            this.MAC = (float)MAC;
            this.b_2 = (float)b_2;
        }

        public void InitClone(FARVesselCache vessel)
        {
            totalMass = vessel.totalMass;
            dryMass = vessel.dryMass;
            CoM = vessel.CoM;
            CoM_dry = vessel.CoM_dry;
            maxCrossSectionFromBody = vessel.maxCrossSectionFromBody;
            bodyLength = vessel.bodyLength;
            area = vessel.area;
            MAC = vessel.MAC;
            b_2 = vessel.b_2;

            engines.Clear();
            foreach (VesselCache.SimulatedEngine engine in vessel.engines)
                engines.Add(VesselCache.SimulatedEngine.BorrowClone(engine, null));

            if (parent == null || !ReferenceEquals(GetRootParent(), vessel.GetRootParent()))
            {
                _wingAerodynamicModel = FARCloneAssist.CloneListFARWingAerodynamicModelsSafe(vessel._wingAerodynamicModel);
                _currentAeroSections = FARCloneAssist.CloneListFARAeroSectionsSafe(vessel._currentAeroSections);
            }
            parent = vessel;
        }

        private FARVesselCache GetRootParent()
        {
            FARVesselCache rootParent = this;
            while (rootParent.parent != null)
                rootParent = rootParent.parent;
            return rootParent;
        }

        private void SetAerodynamicModelVels(Vector3d inflow)
        {
            foreach (FARWingAerodynamicModelWrapper aeroModel in _wingAerodynamicModel)
                aeroModel.Vel = inflow;
        }

        public override void GetAeroCombined(Conditions conditions, float AoA, float pitchInput, out Vector3 forces, out Vector3 torques, bool dryTorque = false)
        {
            GetClCdCmSteady(conditions, InflowVect(AoA), pitchInput, dryTorque ? CoM_dry : CoM, out forces, out torques);
            
            //float Q = 0.0005f * conditions.atmDensity * conditions.speed * conditions.speed;
            //torques *= Q;
            //forces *= Q;
        }

        public override Vector3 GetAeroForce(Conditions conditions, float AoA, float pitchInput = 0)
        {
            GetClCdCmSteady(conditions, InflowVect(AoA), pitchInput, Vector3.zero, out Vector3 forces, out _);

            //float Q = 0.0005f * conditions.atmDensity * conditions.speed * conditions.speed;
            return forces;// * Q;
        }

        public override Vector3 GetAeroTorque(Conditions conditions, float AoA, float pitchInput = 0, bool dryTorque = false)
        {
            GetClCdCmSteady(conditions, InflowVect(AoA), pitchInput, dryTorque ? CoM_dry : CoM, out _, out Vector3 torques);

            //float Q = 0.0005f * conditions.atmDensity * conditions.speed * conditions.speed;
            return torques;// * Q;
        }

        // TODO: Add ITorqueProvider and thrust effect on torque
        public override float GetAoA(Conditions conditions, float offsettingForce, bool useThrust = true, bool dryTorque = false, float guess = float.NaN, float pitchInputGuess = float.NaN, bool lockPitchInput = false, float tolerance = 0.0003F)
        {
            Vector3 thrustForce = useThrust ? this.GetThrustForce(conditions) : Vector3.zero;

            if (!accountForControls)
                return base.GetAoA(conditions, offsettingForce, useThrust, dryTorque, guess, 0, true);
            if (lockPitchInput)
                return base.GetAoA(conditions, offsettingForce, useThrust, dryTorque, guess, pitchInputGuess, lockPitchInput);

            float approxAoA = GetAoA(conditions, offsettingForce, useThrust, dryTorque, guess, pitchInputGuess, true, 1 * Mathf.Deg2Rad);
            return base.GetAoA(conditions, offsettingForce, useThrust, dryTorque, approxAoA, pitchInputGuess, lockPitchInput);
        }
        
        public override float GetFuelBurnRate(Conditions conditions, float AoA)
        {
            float burnRate = 0;
            for (int i = engines.Count - 1; i >= 0; i--)
            {
                burnRate += engines[i].GetFuelBurnRate(conditions.mach, conditions.atmDensity);
            }
            return burnRate;
        }

        public override float GetPitchInput(Conditions conditions, float AoA, bool dryTorque = false, float guess = float.NaN, float tolerance = 0.0003F)
        {
            Accord.Math.Optimization.BrentSearch solver = new Accord.Math.Optimization.BrentSearch((input) => this.GetAeroTorque(conditions, AoA, (float)input, dryTorque).x, -0.3, 0.3, 0.0001);
            if (solver.FindRoot())
                return (float)solver.Solution;
            solver.LowerBound = -1;
            solver.UpperBound = 1;
            if (solver.FindRoot())
                return (float)solver.Solution;
            if (this.GetAeroTorque(conditions, AoA, 0, dryTorque).x > 0)
                return -1;
            else
                return 1;
        }

        public override Vector3 GetThrustForce(Conditions conditions, float AoA)
        {
            Vector3 thrust = Vector3.zero;
            for (int i = engines.Count - 1; i >= 0; i--)
            {
                thrust += engines[i].GetThrust(conditions.mach, conditions.atmDensity, conditions.atmPressure, conditions.oxygenAvailable);
            }
            return thrust;
        }
    }
}