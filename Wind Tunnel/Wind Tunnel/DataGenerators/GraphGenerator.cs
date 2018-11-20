using System;
using System.Collections.Generic;
using System.Linq;
using KerbalWindTunnel.Graphing;
using KerbalWindTunnel.Threading;

namespace KerbalWindTunnel.DataGenerators
{
    public abstract class DataSetGenerator : ICalculationManager, IDisposable
    {
        public GraphableCollection Graphables { get => graphables; }
        protected GraphableCollection graphables = new GraphableCollection();
        protected CalculationManager calculationManager = new CalculationManager();
        protected bool valuesSet = false;

        public IGraphable this[string name] { get => graphables[name]; set => graphables[name] = value; }
        public static explicit operator GraphableCollection (DataSetGenerator me) => me.Graphables;

        public virtual CalculationManager.RunStatus Status
        {
            get
            {
                CalculationManager.RunStatus status = calculationManager.Status;
                if (status == CalculationManager.RunStatus.Completed && !valuesSet)
                    return CalculationManager.RunStatus.Running;
                if (status == CalculationManager.RunStatus.PreStart && valuesSet)
                    return CalculationManager.RunStatus.Completed;
                return status;
            }
        }

        public virtual float PercentComplete
        {
            get { return calculationManager.PercentComplete; }
        }

        public virtual void Cancel()
        {
            if (calculationManager.Status != CalculationManager.RunStatus.PreStart)
            {
                calculationManager.Cancel();
                calculationManager.Dispose();
                calculationManager = new CalculationManager();
            }
            valuesSet = false;
        }

        public virtual void Clear()
        {
            calculationManager.Cancel();
            calculationManager.Dispose();
            calculationManager = new CalculationManager();
        }

        public virtual void Dispose()
        {
            calculationManager.Cancel();
            calculationManager.Dispose();
        }

        public abstract void OnAxesChanged(AeroPredictor vessel, float xMin, float xMax, float yMin, float yMax, float zMin, float zMax);
    }
}
