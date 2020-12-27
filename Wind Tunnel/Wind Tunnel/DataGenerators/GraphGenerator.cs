using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Graphing;
//using KerbalWindTunnel.Threading;

namespace KerbalWindTunnel.DataGenerators
{
    public abstract class DataSetGenerator : IDisposable
    {
        public GraphableCollection Graphables { get => graphables; }
        protected GraphableCollection graphables = new GraphableCollection();
        protected Task task;
        protected CancellationTokenSource cancellationTokenSource;
        protected System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        protected bool valuesSet = false;

        public IGraphable this[string name] { get => graphables[name]; set => graphables[name] = value; }
        public static explicit operator GraphableCollection (DataSetGenerator me) => me.Graphables;

        public virtual TaskStatus Status
        {
            get
            {
                if (valuesSet)
                    return TaskStatus.RanToCompletion;
                if (task == null)
                    return TaskStatus.WaitingToRun;
                TaskStatus status = task.Status;
                if (status == TaskStatus.RanToCompletion)
                    return TaskStatus.Running;
                return status;
            }
        }

        public virtual TaskStatus InternalStatus
        {
            get
            {
                if (task == null)
                    return TaskStatus.WaitingForActivation;
                return task.Status;
            }
        }

        public abstract float PercentComplete { get; }

        public virtual float InternalPercentComplete => PercentComplete;

        /// <summary>
        /// Returns the current progress rate in amount per second.
        /// </summary>
        public virtual float ProgressRate { get => float.NaN; }

        public virtual TimeSpan ElapsedTime
        {
            get
            {
                if (!stopwatch.IsRunning)
                    return new TimeSpan(0);
                return stopwatch.Elapsed;
            }
        }

        public virtual TimeSpan TimeRemaining
        {
            get
            {
                if (Status == TaskStatus.RanToCompletion)
                    return new TimeSpan(0);
                double secondsAtAverageRate = ElapsedTime.TotalSeconds * (1 - 1 / PercentComplete);
                if (!float.IsNaN(ProgressRate))
                    return TimeSpan.FromSeconds((secondsAtAverageRate + (1 - PercentComplete) / ProgressRate) / 2);
                else
                    return TimeSpan.FromSeconds(secondsAtAverageRate);
            }
        }

        public virtual void Cancel()
        {
            valuesSet = false;
            cancellationTokenSource?.Cancel();
            DisposeOfCancellationToken();
        }

        public virtual void Clear()
        {
            Cancel();
        }

        public void Dispose()
        {
            Dispose(true);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
                DisposeOfCancellationToken();
        }

        protected void DisposeOfCancellationToken()
        {
            if (cancellationTokenSource == null)
                return;

            if (!cancellationTokenSource.IsCancellationRequested)
                cancellationTokenSource.Cancel();

            if (task != null && task.Status < TaskStatus.RanToCompletion)
                task.ContinueWith((t) => cancellationTokenSource.Dispose());
            else
                cancellationTokenSource.Dispose();

            cancellationTokenSource = null;
        }

        public virtual void UpdateGraphs() { }

        public abstract void OnAxesChanged(float xMin, float xMax, float yMin, float yMax, float zMin, float zMax);
    }
}
