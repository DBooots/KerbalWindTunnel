using System;

namespace KerbalWindTunnel.RootSolvers
{
    public abstract class RootSolver
    {
        public float Solve(Func<float, float> function)
        {
            return Solve(function, RootSolverSettings.Default);
        }
        public abstract float Solve(Func<float, float> function, RootSolverSettings solverSettings);
        public float Solve(Func<float, float> function, float x0, RootSolverSettings solverSettings)
        {
            return Solve(function, solverSettings.ApplyOption(BestGuess(x0)));
        }
        public float Solve(Func<float, float> function, params RootSolverOption[] options)
        {
            return Solve(function, new RootSolverSettings(options));
        }
        public float Solve(Func<float, float> function, float x0, params RootSolverOption[] options)
        {
            RootSolverSettings settings = new RootSolverSettings(options).ApplyOption(BestGuess(x0));
            return Solve(function, settings);
        }

        public static RootSolverOption BestGuess(float value) { return new RootSolverOption(RootSolverOption.OptionType.BestGuess, value); }
        public static RootSolverOption LeftBound(float value) { return new RootSolverOption(RootSolverOption.OptionType.LeftBound, value); }
        public static RootSolverOption RightBound(float value) { return new RootSolverOption(RootSolverOption.OptionType.RightBound, value); }
        public static RootSolverOption LeftGuessBound(float value) { return new RootSolverOption(RootSolverOption.OptionType.LeftGuessBound, value); }
        public static RootSolverOption RightGuessBound(float value) { return new RootSolverOption(RootSolverOption.OptionType.RightGuessBound, value); }
        public static RootSolverOption Tolerance(float value) { return new RootSolverOption(RootSolverOption.OptionType.Tolerance, value); }
        public static RootSolverOption ShiftWithGuess(bool value) { return new RootSolverOption(RootSolverOption.OptionType.ShiftWithGuess, value ? 1 : 0); }
    }

    public class RootSolverSettings
    {
        public readonly float leftBound = -50;
        public readonly float rightBound = 50;
        public readonly float leftGuessBound = 5;
        public readonly float rightGuessBound = -5;
        public readonly float bestGuess = 0;
        public readonly bool shiftWithGuess = true;
        public readonly float tolerance = 0.001f;

        public static RootSolverSettings Default
        {
            get { return new RootSolverSettings(); }
        }

        public RootSolverSettings() { }
        public RootSolverSettings(params RootSolverOption[] options) : this(RootSolverSettings.Default, options) { }
        public RootSolverSettings(RootSolverSettings oldSettings, params RootSolverOption[] options)
        {
            this.leftBound = oldSettings.leftBound;
            this.rightBound = oldSettings.rightBound;
            this.leftGuessBound = oldSettings.leftGuessBound;
            this.rightGuessBound = oldSettings.rightGuessBound;
            this.bestGuess = oldSettings.bestGuess;
            this.shiftWithGuess = oldSettings.shiftWithGuess;
            this.tolerance = oldSettings.tolerance;

            int count = options.Length;
            for (int i = 0; i < count; i++)
            {
                switch (options[i].type)
                {
                    case RootSolverOption.OptionType.BestGuess:
                        float oldGuess = this.bestGuess;
                        this.bestGuess = options[i].value;
                        if (shiftWithGuess)
                        {
                            leftBound += bestGuess - oldGuess;
                            rightBound += bestGuess - oldGuess;
                            leftGuessBound += bestGuess - oldGuess;
                            rightGuessBound += bestGuess - oldGuess;
                        }
                        else
                        {
                            leftBound = Math.Min(leftBound, bestGuess);
                            rightBound = Math.Max(rightBound, bestGuess);
                            leftGuessBound = Math.Min(leftGuessBound, bestGuess);
                            rightGuessBound = Math.Max(rightGuessBound, bestGuess);
                        }
                        break;
                    case RootSolverOption.OptionType.LeftBound:
                        this.leftBound = shiftWithGuess ? this.bestGuess + options[i].value : options[i].value;
                        this.bestGuess = Math.Max(bestGuess, leftBound);
                        this.leftGuessBound = Math.Max(leftGuessBound, leftBound);
                        this.rightBound = Math.Max(rightBound, leftBound);
                        this.rightGuessBound = Math.Max(rightGuessBound, leftBound);
                        break;
                    case RootSolverOption.OptionType.RightBound:
                        this.rightBound = shiftWithGuess ? this.bestGuess + options[i].value : options[i].value;
                        this.bestGuess = Math.Min(bestGuess, rightBound);
                        this.leftBound = Math.Min(leftBound, rightBound);
                        this.leftGuessBound = Math.Min(leftGuessBound, rightBound);
                        this.rightGuessBound = Math.Min(rightGuessBound, rightBound);
                        break;
                    case RootSolverOption.OptionType.LeftGuessBound:
                        this.leftGuessBound = shiftWithGuess ? this.bestGuess + options[i].value : options[i].value;
                        this.bestGuess = Math.Max(bestGuess, leftGuessBound);
                        this.rightGuessBound = Math.Max(rightGuessBound, leftGuessBound);
                        break;
                    case RootSolverOption.OptionType.RightGuessBound:
                        this.rightGuessBound = shiftWithGuess ? this.bestGuess + options[i].value : options[i].value;
                        this.bestGuess = Math.Min(bestGuess, rightGuessBound);
                        this.leftGuessBound = Math.Min(leftGuessBound, rightGuessBound);
                        break;
                    case RootSolverOption.OptionType.ShiftWithGuess:
                        this.shiftWithGuess = options[i].value == 1;
                        break;
                    case RootSolverOption.OptionType.Tolerance:
                        tolerance = options[i].value;
                        break;
                }
            }
        }
        public RootSolverSettings ApplyOption(RootSolverOption option)
        {
            return new RootSolverSettings(this, option);
        }
    }

    public class RootSolverOption
    {
        public enum OptionType
        {
            BestGuess,
            LeftBound,
            RightBound,
            LeftGuessBound,
            RightGuessBound,
            ShiftWithGuess,
            Tolerance
        }
        public readonly OptionType type;
        public readonly float value;
        internal RootSolverOption(OptionType type, float value)
        {
            this.type = type;
            this.value = value;
        }
    }
}
