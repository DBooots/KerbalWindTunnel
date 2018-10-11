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
            calculationManager.Cancel();
            calculationManager = new CalculationManager();
            valuesSet = false;
        }

        public virtual void Clear()
        {
            calculationManager.Cancel();
            calculationManager = new CalculationManager();
        }

        public virtual void Dispose()
        {
            calculationManager.Cancel();
        }
    }
}
