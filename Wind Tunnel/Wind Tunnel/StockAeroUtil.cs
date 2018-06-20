/*
From Trajectories
Copyright 2014, Youen Toupin
This file is part of Trajectories, under MIT license.
StockAeroUtil by atomicfury

The MIT License (MIT)

Copyright (c) 2014 Youen Toupin, aka neuoy
©Copyright (c) 2017 S.Gray, aka PiezPiedPy

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

Except as contained in this notice, the name of the copyright holders shall not
be used in advertising or otherwise to promote the sale, use or other dealings
in this Software without prior written authorization from the copyright holders.

Any files and code contained within those files ("those files") authored
by S.Gray (aka PiezPiedPy) are not subject to the MIT License terms.
See the details within those files and/or the details below for their
respective License and Copyright terms:

  1. Any and all rights, assumed or otherwise, to the code contained within those
  files and/or any compiles using code generated from the code within those files,
  are removed for any and all persons, companies and/or corporations unless prior
  written authorization from the copyright holders is given.

  2. Any and all rights, assumed or otherwise, to any commercial or personal gain
  from the use and/or resale of those files and/or any compiles using code generated
  from the code within those files, but not including personally created user content,
  are removed unless prior written authorization from the copyright holders is given. 
  
  3. Permission is hereby granted, for personal use only, free of charge, not for
  resale, to any person wishing to use the code contained within those files and/or
  any compiles using code generated from the code within those files, including without
  limitation the rights to use, copy, modify, merge, publish, distribute, create user
  content and/ or sub-license, subject to the following conditions:

  4. The above copyright notice and this permission notice shall be included and remain
  intact for all copies and/or other works using the code contained within those files.

  5. Except as contained in this notice, the name of the copyright holders shall not be
  used in advertising or otherwise to promote the sale, use, or other dealings without
  prior written authorization from the copyright holders.

  6. THE CODE WITHIN THOSE FILES AND/OR ANY COMPILES USING CODE GENERATED FROM THE CODE
  WITHIN THOSE FILES IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
  INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
  PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
  FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
  OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE CODE WITHIN THOSE FILES AND/OR
  ANY COMPILES USING CODE GENERATED FROM THE CODE WITHIN THOSE FILES, OR THE USE OR OTHER
  DEALINGS IN THE CODE WITHIN THOSE FILES AND/OR ANY COMPILES USING CODE GENERATED FROM THE
  CODE WITHIN THOSE FILES.
*/

//#define PRECOMPUTE_CACHE
using System;
using UnityEngine;

namespace KerbalWindTunnel
{
    // this class provides several methods to access stock aero information
    public static class StockAeroUtil
    {
        /// <summary>
        /// This function should return exactly the same value as Vessel.atmDensity, but is more generic because you don't need an actual vessel updated by KSP to get a value at the desired location.
        /// Computations are performed for the current body position, which means it's theoritically wrong if you want to know the temperature in the future, but since body rotation is not used (position is given in sun frame), you should get accurate results up to a few weeks.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="body"></param>
        /// <returns></returns>
        public static double GetTemperature(Vector3d position, CelestialBody body)
        {
            if (!body.atmosphere)
                return PhysicsGlobals.SpaceTemperature;

            double altitude = (position - body.position).magnitude - body.Radius;
            if (altitude > body.atmosphereDepth)
                return PhysicsGlobals.SpaceTemperature;

            Vector3 up = (position - body.position).normalized;
            float polarAngle = Mathf.Acos(Vector3.Dot(body.bodyTransform.up, up));
            if (polarAngle > Mathf.PI / 2.0f)
            {
                polarAngle = Mathf.PI - polarAngle;
            }
            float time = (Mathf.PI / 2.0f - polarAngle) * 57.29578f;

            Vector3 sunVector = (FlightGlobals.Bodies[0].position - position).normalized;
            float sunAxialDot = Vector3.Dot(sunVector, body.bodyTransform.up);
            float bodyPolarAngle = Mathf.Acos(Vector3.Dot(body.bodyTransform.up, up));
            float sunPolarAngle = Mathf.Acos(sunAxialDot);
            float sunBodyMaxDot = (1.0f + Mathf.Cos(sunPolarAngle - bodyPolarAngle)) * 0.5f;
            float sunBodyMinDot = (1.0f + Mathf.Cos(sunPolarAngle + bodyPolarAngle)) * 0.5f;
            float sunDotCorrected = (1.0f + Vector3.Dot(sunVector, Quaternion.AngleAxis(45f * Mathf.Sign((float)body.rotationPeriod), body.bodyTransform.up) * up)) * 0.5f;
            float sunDotNormalized = (sunDotCorrected - sunBodyMinDot) / (sunBodyMaxDot - sunBodyMinDot);
            double atmosphereTemperatureOffset = (double)body.latitudeTemperatureBiasCurve.Evaluate(time) + (double)body.latitudeTemperatureSunMultCurve.Evaluate(time) * sunDotNormalized + (double)body.axialTemperatureSunMultCurve.Evaluate(sunAxialDot);
            double temperature = body.GetTemperature(altitude) + (double)body.atmosphereTemperatureSunMultCurve.Evaluate((float)altitude) * atmosphereTemperatureOffset;

            return temperature;
        }

        /// <summary>
        /// Gets the air density (rho) for the specified altitude on the specified body.
        /// This is an approximation, because actual calculations, taking sun exposure into account to compute air temperature, require to know the actual point on the body where the density is to be computed (knowing the altitude is not enough).
        /// However, the difference is small for high altitudes, so it makes very little difference for trajectory prediction.
        /// </summary>
        /// <param name="altitude">Altitude above sea level (in meters)</param>
        /// <param name="body"></param>
        /// <returns></returns>
        public static double GetDensity(double altitude, CelestialBody body)
        {
            if (!body.atmosphere)
                return 0;

            if (altitude > body.atmosphereDepth)
                return 0;

            double pressure = body.GetPressure(altitude);

            // get an average day/night temperature at the equator
            double sunDot = 0.5;
            float sunAxialDot = 0;
            double atmosphereTemperatureOffset = (double)body.latitudeTemperatureBiasCurve.Evaluate(0)
                + (double)body.latitudeTemperatureSunMultCurve.Evaluate(0) * sunDot
                + (double)body.axialTemperatureSunMultCurve.Evaluate(sunAxialDot);
            double temperature = // body.GetFullTemperature(altitude, atmosphereTemperatureOffset);
                body.GetTemperature(altitude)
                + (double)body.atmosphereTemperatureSunMultCurve.Evaluate((float)altitude) * atmosphereTemperatureOffset;


            return body.GetDensity(pressure, temperature);
        }

        public static double GetDensity(Vector3d position, CelestialBody body)
        {
            if (!body.atmosphere)
                return 0;

            double altitude = (position - body.position).magnitude - body.Radius;
            if (altitude > body.atmosphereDepth)
                return 0;

            double pressure = body.GetPressure(altitude);
            double temperature = // body.GetFullTemperature(position);
                GetTemperature(position, body);

            return body.GetDensity(pressure, temperature);
        }

        //*******************************************************
        public static Vector3 SimAeroForce(CelestialBody body, IShipconstruct vessel, Vector3 v_wrld_vel, Vector3 position)
        {
            double latitude = body.GetLatitude(position) / 180.0 * Math.PI;
            double altitude = (position - body.position).magnitude - body.Radius;

            return SimAeroForce(body, vessel, v_wrld_vel, altitude, latitude);
        }

        //*******************************************************
        public static Vector3 SimLiftForce(CelestialBody body, IShipconstruct vessel, Vector3 v_wrld_vel, double altitude, double latitude = 0.0)
        {
            //Profiler.Start("SimLiftForce");
            double pressure = body.GetPressure(altitude);
            // Lift and drag for force accumulation.
            Vector3d total_lift = Vector3d.zero;

            // dynamic pressure for standard drag equation
            double rho = GetDensity(altitude, body);
            double dyn_pressure = 0.0005 * rho * v_wrld_vel.sqrMagnitude;

            if (rho <= 0)
            {
                return Vector3.zero;
            }

            double soundSpeed = body.GetSpeedOfSound(pressure, rho);
            double mach = v_wrld_vel.magnitude / soundSpeed;
            if (mach > 25.0) { mach = 25.0; }

            // Loop through all parts, accumulating drag and lift.
            for (int i = vessel.Parts.Count - 1; i >= 0; i--)
            {
                // need checks on shielded components
                Part part = vessel.Parts[i];

                if (part.ShieldedFromAirstream || part.Rigidbody == null)
                {
                    continue;
                }

                // Get Drag
                Vector3 sim_dragVectorDir = v_wrld_vel.normalized;
                Vector3 sim_dragVectorDirLocal = -(part.transform.InverseTransformDirection(sim_dragVectorDir));

                Vector3 liftForce = new Vector3(0, 0, 0);

                //Profiler.Start("SimLiftForce#Body");
                switch (part.dragModel)
                {
                    case Part.DragModel.DEFAULT:
                    case Part.DragModel.CUBE:
                        DragCubeList cubes = part.DragCubes;

                        DragCubeList.CubeData p_drag_data = new DragCubeList.CubeData();

                        if (!cubes.None) // since 1.0.5, some parts don't have drag cubes (for example fuel lines and struts)
                        {
                            try
                            {
                                cubes.SetDragWeights();
                                cubes.SetPartOcclusion();
                                cubes.AddSurfaceDragDirection(-sim_dragVectorDirLocal, (float)mach, ref p_drag_data);
                            }
                            catch (Exception)
                            {
                                cubes.SetDrag(sim_dragVectorDirLocal, (float)mach);
                                cubes.ForceUpdate(true, true);
                                cubes.AddSurfaceDragDirection(-sim_dragVectorDirLocal, (float)mach, ref p_drag_data);
                                //Debug.Log(String.Format("Trajectories: Caught NRE on Drag Initialization.  Should be fixed now.  {0}", e));
                            }

                            float pseudoreynolds = (float)(rho * Mathf.Abs(v_wrld_vel.magnitude));
                            float pseudoredragmult = PhysicsGlobals.DragCurvePseudoReynolds.Evaluate(pseudoreynolds);

                            liftForce = p_drag_data.liftForce;
                        }
                        break;
                }

                // If it isn't a wing or lifter, get body lift.
                if (!part.hasLiftModule)
                {
                    float simbodyLiftScalar = part.bodyLiftMultiplier * PhysicsGlobals.BodyLiftMultiplier * (float)dyn_pressure;
                    simbodyLiftScalar *= PhysicsGlobals.GetLiftingSurfaceCurve("BodyLift").liftMachCurve.Evaluate((float)mach);
                    Vector3 bodyLift = part.transform.rotation * (simbodyLiftScalar * liftForce);
                    bodyLift = Vector3.ProjectOnPlane(bodyLift, sim_dragVectorDir);
                    // Only accumulate forces for non-LiftModules
                    total_lift += bodyLift;
                }
                //Profiler.Stop("SimLiftForce#Body");

                // Find ModuleLifingSurface for wings and liftforce.
                // Should catch control surface as it is a subclass
                //Profiler.Start("SimLiftForce#LiftModule");
                for (int j = part.Modules.Count - 1; j >= 0; j--)
                {
                    if (part.Modules[j] is ModuleLiftingSurface)
                    {
                        float mcs_mod = 1.0f;
                        double liftQ = dyn_pressure * 1000;
                        ModuleLiftingSurface wing = (ModuleLiftingSurface)part.Modules[j];
                        Vector3 nVel = Vector3.zero;
                        Vector3 liftVector = Vector3.zero;
                        float liftdot;
                        float absdot;
                        wing.SetupCoefficients(v_wrld_vel, out nVel, out liftVector, out liftdot, out absdot);

                        double prevMach = part.machNumber;
                        part.machNumber = mach;
                        Vector3 local_lift = mcs_mod * wing.GetLiftVector(liftVector, liftdot, absdot, liftQ, (float)mach);
                        part.machNumber = prevMach;

                        total_lift += local_lift;
                    }
                }
                //Profiler.Stop("SimLiftForce#LiftModule");
            }
            // RETURN STUFF
            //Profiler.Stop("SimLiftForce");
            return total_lift;
        }

        //*******************************************************
        public static Vector3 SimDragForce(CelestialBody body, IShipconstruct vessel, Vector3 v_wrld_vel, double altitude, double latitude = 0.0)
        {
            double pressure = body.GetPressure(altitude);
            // Lift and drag for force accumulation.
            Vector3d total_drag = Vector3d.zero;

            // dynamic pressure for standard drag equation
            double rho = GetDensity(altitude, body);
            double dyn_pressure = 0.0005 * rho * v_wrld_vel.sqrMagnitude;

            if (rho <= 0)
            {
                return Vector3.zero;
            }

            double soundSpeed = body.GetSpeedOfSound(pressure, rho);
            double mach = v_wrld_vel.magnitude / soundSpeed;
            if (mach > 25.0) { mach = 25.0; }

            // Loop through all parts, accumulating drag and lift.
            for (int i = vessel.Parts.Count - 1; i >= 0; i--)
            {
                // need checks on shielded components
                Part part = vessel.Parts[i];

                if (part.ShieldedFromAirstream || part.Rigidbody == null)
                {
                    continue;
                }

                // Get Drag
                Vector3 sim_dragVectorDir = v_wrld_vel.normalized;
                Vector3 sim_dragVectorDirLocal = -(part.transform.InverseTransformDirection(sim_dragVectorDir));

                Vector3d dragForce;

                switch (part.dragModel)
                {
                    case Part.DragModel.DEFAULT:
                    case Part.DragModel.CUBE:
                        DragCubeList cubes = part.DragCubes;

                        DragCubeList.CubeData p_drag_data = new DragCubeList.CubeData();

                        float drag;
                        if (cubes.None) // since 1.0.5, some parts don't have drag cubes (for example fuel lines and struts)
                        {
                            drag = part.maximum_drag;
                        }
                        else
                        {
                            try
                            {
                                cubes.SetDragWeights();
                                cubes.SetPartOcclusion();
                                cubes.AddSurfaceDragDirection(-sim_dragVectorDirLocal, (float)mach, ref p_drag_data);
                            }
                            catch (Exception)
                            {
                                cubes.SetDrag(sim_dragVectorDirLocal, (float)mach);
                                cubes.ForceUpdate(true, true);
                                cubes.AddSurfaceDragDirection(-sim_dragVectorDirLocal, (float)mach, ref p_drag_data);
                                //Debug.Log(String.Format("Trajectories: Caught NRE on Drag Initialization.  Should be fixed now.  {0}", e));
                            }

                            float pseudoreynolds = (float)(rho * Mathf.Abs(v_wrld_vel.magnitude));
                            float pseudoredragmult = PhysicsGlobals.DragCurvePseudoReynolds.Evaluate(pseudoreynolds);
                            drag = p_drag_data.areaDrag * PhysicsGlobals.DragCubeMultiplier * pseudoredragmult;
                        }

                        double sim_dragScalar = dyn_pressure * (double)drag * PhysicsGlobals.DragMultiplier;
                        dragForce = -(Vector3d)sim_dragVectorDir * sim_dragScalar;

                        break;

                    case Part.DragModel.SPHERICAL:
                        dragForce = -(Vector3d)sim_dragVectorDir * (double)part.maximum_drag;
                        break;

                    case Part.DragModel.CYLINDRICAL:
                        dragForce = -(Vector3d)sim_dragVectorDir * (double)Mathf.Lerp(part.minimum_drag, part.maximum_drag, Mathf.Abs(Vector3.Dot(part.partTransform.TransformDirection(part.dragReferenceVector), sim_dragVectorDir)));
                        break;

                    case Part.DragModel.CONIC:
                        dragForce = -(Vector3d)sim_dragVectorDir * (double)Mathf.Lerp(part.minimum_drag, part.maximum_drag, Vector3.Angle(part.partTransform.TransformDirection(part.dragReferenceVector), sim_dragVectorDir) / 180f);
                        break;

                    default:
                        // no drag to apply
                        dragForce = new Vector3d();
                        break;
                }

                total_drag += dragForce;


                // Find ModuleLifingSurface for wings and liftforce.
                // Should catch control surface as it is a subclass
                for (int j = part.Modules.Count - 1; j >= 0; j--)
                {
                    if (part.Modules[j] is ModuleLiftingSurface)
                    {
                        float mcs_mod = 1.0f;
                        double liftQ = dyn_pressure * 1000;
                        ModuleLiftingSurface wing = (ModuleLiftingSurface)part.Modules[j];
                        Vector3 nVel = Vector3.zero;
                        Vector3 liftVector = Vector3.zero;
                        float liftdot;
                        float absdot;
                        wing.SetupCoefficients(v_wrld_vel, out nVel, out liftVector, out liftdot, out absdot);

                        double prevMach = part.machNumber;
                        part.machNumber = mach;
                        Vector3 local_drag = mcs_mod * wing.GetDragVector(nVel, absdot, liftQ);
                        part.machNumber = prevMach;

                        total_drag += local_drag;
                    }
                }

            }
            // RETURN STUFF
            return total_drag;
        }

        //*******************************************************
        public static Vector3 SimAeroForce(CelestialBody body, IShipconstruct vessel, Vector3 v_wrld_vel, double altitude, double latitude = 0.0, bool verbose = false)
        {
            //Profiler.Start("SimAeroForce");
            double pressure = body.GetPressure(altitude);
            // Lift and drag for force accumulation.
            Vector3d total_lift = Vector3d.zero;
            Vector3d total_drag = Vector3d.zero;

            // dynamic pressure for standard drag equation
            double rho = GetDensity(altitude, body);
            double dyn_pressure = 0.0005 * rho * v_wrld_vel.sqrMagnitude;

            if (rho <= 0)
            {
                return Vector3.zero;
            }

            double soundSpeed = body.GetSpeedOfSound(pressure, rho);
            double mach = v_wrld_vel.magnitude / soundSpeed;
            if (mach > 25.0) { mach = 25.0; }

            // Loop through all parts, accumulating drag and lift.
            for (int i = 0; i < vessel.Parts.Count; ++i)
            {
                // need checks on shielded components
                Part p = vessel.Parts[i];
#if DEBUG
                Vector3d part_force = Vector3d.zero;
#endif

                if (p.ShieldedFromAirstream || p.Rigidbody == null)
                {
                    continue;
                }

                // Get Drag
                Vector3 sim_dragVectorDir = v_wrld_vel.normalized;
                Vector3 sim_dragVectorDirLocal = -(p.transform.InverseTransformDirection(sim_dragVectorDir));

                Vector3 liftForce = new Vector3(0, 0, 0);
                Vector3d dragForce;

                float pseudoreynolds = (float)(rho * v_wrld_vel.magnitude);
                float pseudoredragmult = PhysicsGlobals.DragCurvePseudoReynolds.Evaluate(pseudoreynolds);

                //Profiler.Start("SimAeroForce#Body");
                switch (p.dragModel)
                {
                    case Part.DragModel.DEFAULT:
                    case Part.DragModel.CUBE:
                        DragCubeList cubes = p.DragCubes;

                        DragCubeList.CubeData p_drag_data = new DragCubeList.CubeData();

                        float drag;
                        if (cubes.None) // since 1.0.5, some parts don't have drag cubes (for example fuel lines and struts)
                        {
                            drag = p.maximum_drag;
                        }
                        else
                        {
                            try
                            {
                                cubes.SetDragWeights();
                                cubes.SetPartOcclusion();
                                cubes.AddSurfaceDragDirection(-sim_dragVectorDirLocal, (float)mach, ref p_drag_data);
                            }
                            catch (Exception)
                            {
                                cubes.SetDrag(sim_dragVectorDirLocal, (float)mach);
                                cubes.ForceUpdate(true, true);
                                cubes.SetDragWeights();
                                cubes.SetPartOcclusion();
                                cubes.AddSurfaceDragDirection(-sim_dragVectorDirLocal, (float)mach, ref p_drag_data);
                                //Debug.Log(String.Format("Trajectories: Caught NRE on Drag Initialization.  Should be fixed now.  {0}", e));
                            }

#if DEBUG
                            if (verbose && i == 0)
                            {
                                Debug.Log(p.name);
                                Debug.Log(string.Format("{0}, {1}, {2}, {3}, {4}, {5}",
                                    p.DragCubes.AreaOccluded[0], p.DragCubes.AreaOccluded[1], p.DragCubes.AreaOccluded[2], p.DragCubes.AreaOccluded[3], p.DragCubes.AreaOccluded[4], p.DragCubes.AreaOccluded[5]));
                                Debug.Log(string.Format("{0}, {1}, {2}, {3}, {4}, {5}",
                                    p.DragCubes.WeightedDrag[0], p.DragCubes.WeightedDrag[1], p.DragCubes.WeightedDrag[2], p.DragCubes.WeightedDrag[3], p.DragCubes.WeightedDrag[4], p.DragCubes.WeightedDrag[5]));
                                Debug.Log(p_drag_data.area);
                                Debug.Log(p_drag_data.exposedArea);
                            }
#endif

                            drag = p_drag_data.areaDrag * PhysicsGlobals.DragCubeMultiplier;

                            liftForce = p_drag_data.liftForce;
                        }

                        dragForce = -(Vector3d)sim_dragVectorDir * drag;

                        break;

                    case Part.DragModel.SPHERICAL:
                        dragForce = -(Vector3d)sim_dragVectorDir * (double)p.maximum_drag;
                        break;

                    case Part.DragModel.CYLINDRICAL:
                        dragForce = -(Vector3d)sim_dragVectorDir * (double)Mathf.Lerp(p.minimum_drag, p.maximum_drag, Mathf.Abs(Vector3.Dot(p.partTransform.TransformDirection(p.dragReferenceVector), sim_dragVectorDir)));
                        break;

                    case Part.DragModel.CONIC:
                        dragForce = -(Vector3d)sim_dragVectorDir * (double)Mathf.Lerp(p.minimum_drag, p.maximum_drag, Vector3.Angle(p.partTransform.TransformDirection(p.dragReferenceVector), sim_dragVectorDir) / 180f);
                        break;

                    default:
                        // no drag to apply
                        dragForce = new Vector3d();
                        break;
                }
                dragForce *= dyn_pressure * pseudoredragmult * PhysicsGlobals.DragMultiplier;

                total_drag += dragForce;
#if DEBUG
                part_force += dragForce;
#endif

                // If it isn't a wing or lifter, get body lift.
                if (!p.hasLiftModule)
                {

                    float simbodyLiftScalar = p.bodyLiftMultiplier * PhysicsGlobals.BodyLiftMultiplier * (float)dyn_pressure;
                    simbodyLiftScalar *= PhysicsGlobals.GetLiftingSurfaceCurve("BodyLift").liftMachCurve.Evaluate((float)mach);
                    Vector3 bodyLift = p.transform.rotation * (simbodyLiftScalar * liftForce);
                    bodyLift = Vector3.ProjectOnPlane(bodyLift, sim_dragVectorDir);
                    // Only accumulate forces for non-LiftModules
#if DEBUG
                    part_force += bodyLift;
#endif
                    total_lift += bodyLift;
                }
                //Profiler.Stop("SimAeroForce#Body");

                // Find ModuleLifingSurface for wings and liftforce.
                // Should catch control surface as it is a subclass
                //Profiler.Start("SimAeroForce#LiftModule");
                for (int j = 0; j < p.Modules.Count; ++j)
                {
                    var m = p.Modules[j];
                    float mcs_mod;
                    if (m is ModuleLiftingSurface)
                    {
                        mcs_mod = 1.0f;
                        double liftQ = dyn_pressure * 1000;
                        ModuleLiftingSurface wing = (ModuleLiftingSurface)m;
                        Vector3 nVel = Vector3.zero;
                        Vector3 liftVector = Vector3.zero;
                        float liftdot;
                        float absdot;
                        wing.SetupCoefficients(v_wrld_vel, out nVel, out liftVector, out liftdot, out absdot);

                        double prevMach = p.machNumber;
                        p.machNumber = mach;
                        Vector3 local_lift = mcs_mod * wing.GetLiftVector(liftVector, liftdot, absdot, liftQ, (float)mach);
                        Vector3 local_drag = mcs_mod * wing.GetDragVector(nVel, absdot, liftQ);
                        p.machNumber = prevMach;

                        total_lift += local_lift;
                        total_drag += local_drag;
#if DEBUG
                        part_force += local_drag + local_lift;
#endif
                    }
                }
                //Profiler.Stop("SimAeroForce#LiftModule");

#if DEBUG
                if (verbose)
                    Debug.Log(p.name + ": " + part_force);
#endif
            }
            // RETURN STUFF
            Vector3 force = total_lift + total_drag;
            //Profiler.Stop("SimAeroForce");
            return force;
        }
    } //StockAeroUtil
}