using System;
using System.Collections.Generic;
using System.Linq;
using KerbalWindTunnel.Graphing;
using KerbalWindTunnel.Threading;

namespace KerbalWindTunnel.DataGenerators
{
    public abstract class DataSetGenerator : IGraphableProvider, ICalculationManager, IDisposable
    {
        protected Dictionary<string, IGraphable> graphs = new Dictionary<string, IGraphable>(StringComparer.InvariantCultureIgnoreCase);
        public virtual List<IGraphable> Graphables { get { return graphs.Values.ToList(); } }
        protected CalculationManager calculationManager = new CalculationManager();
        protected bool valuesSet = false;

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

        public virtual IGraphable GetGraphableByName(string graphName)
        {
            if (graphs.TryGetValue(graphName, out IGraphable graph))
                return graph;
            return null;
        }

        public virtual GraphableCollection GetGraphableCollection()
        {
            return new GraphableCollection(this.graphs.Values);
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
