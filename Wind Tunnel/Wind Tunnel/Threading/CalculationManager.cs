using System;
using System.Threading;

namespace KerbalWindTunnel.Threading
{
    public interface ICalculationManager
    {
        CalculationManager.RunStatus Status { get; }
        float PercentComplete { get; }
        void Cancel();
    }

    public class CalculationManager : ICalculationManager, IDisposable
    {
        private volatile bool readyToDispose = false;
        private bool disposed = false;
        private volatile int waiting = 0;
        private volatile int linked = 0;
        private volatile int completed = 0;
        private readonly object waitingLock = new object();
        private readonly object linkLock = new object();
        private readonly object completeLock = new object();
        private readonly object statusLock = new object();
        private volatile bool _cancelled = false;
        private ManualResetEvent completionEvent = new ManualResetEvent(false);
        public Callback OnCancelCallback = delegate { };
        public bool Cancelled
        {
            get
            {
                return _cancelled;
            }
            set
            {
                if (value)
                    this.Status = RunStatus.Cancelled;
            }
        }

        public enum RunStatus
        {
            PreStart = 0,
            Running = 1,
            Completed = 2,
            Cancelled = 3
        }

        private volatile RunStatus _status = RunStatus.PreStart;
        public virtual RunStatus Status
        {
            get
            {
                if (this.Completed && _status != RunStatus.Completed)
                    lock (statusLock)
                        _status = RunStatus.Completed;
                return _status;
            }
            set
            {
                if (value > _status)
                {
                    if (value == RunStatus.Cancelled)
                        this.Cancel();
                    lock (statusLock)
                        _status = value;
                }
            }
        }

        public float PercentComplete
        {
            get
            {
                if (linked == 0)
                    return 1;
                return (float)completed / linked;
            }
        }

        public bool Completed
        {
            get
            {
                if (linked == 0)
                    return false;
                return completed == linked;
            }
            set
            {
                if(value)
                {
                    lock (completeLock)
                    {
                        completed = linked;
                    }
                }
            }
        }

        public bool WaitForCompletion() => WaitForCompletion(-1);
        public bool WaitForCompletion(TimeSpan timeout) => WaitForCompletion((int)Math.Round(timeout.TotalMilliseconds));
        public bool WaitForCompletion(int millisecondsTimeout)
        {
            lock (waitingLock)
                waiting += 1;
            bool result = completionEvent.WaitOne(millisecondsTimeout);
            lock (waitingLock)
            {
                waiting -= 1;
                if (readyToDispose && waiting == 0)
                    this.Dispose();
            }
            return result;
        }

        private CalculationManager LinkTo()
        {
            if (_status == RunStatus.PreStart || _status == RunStatus.Completed)
                lock (statusLock)
                    this._status = RunStatus.Running;
            lock (linkLock)
            {
                linked += 1;
            }
            return this;
        }

        /// <summary>
        /// Public implementation of the <see cref="IDisposable.ReadyToDispose"/> method of the <see cref="IDisposable"/> interface.
        /// </summary>
        public void Cancel()
        {
            if (!Cancelled)
                completionEvent.Set();
            this._cancelled = true;
            lock (statusLock)
                this._status = RunStatus.Cancelled;
            this.OnCancelCallback();
        }

        protected internal bool MarkCompleted()
        {
            lock (completeLock)
            {
                this.completed += 1;
            }
            if (Completed)
                completionEvent.Set();
            return true;
        }

        public State GetStateToken()
        {
            return State.CreateToken(this);
        }
        
        public void Dispose()
        {
            if (disposed)
                return;
            readyToDispose = true;
            lock (waitingLock)
                if (waiting == 0)
                    completionEvent.Close();
            disposed = true;
        }

        public class State
        {
            public readonly CalculationManager manager;
            public object Result
            {
                get
                {
                    return _result;
                }
                protected set
                {
                    _result = value;
                }
            }
            private volatile object _result = null;
            public bool Cancelled { get { return manager.Cancelled; } }
            public bool Completed { get { return this.markedComplete; } }
            private bool markedComplete = false;

            private State() { }
            private State(CalculationManager manager)
            {
                this.manager = manager;
                manager.LinkTo();
            }
            internal static State CreateToken(CalculationManager manager)
            {
                return new State(manager);
            }

            public void StoreResult(object result)
            {
                this.Result = result;
                this.MarkComplete();
            }
            public void MarkComplete()
            {
                if (!this.markedComplete)
                    this.markedComplete = manager.MarkCompleted();
            }
        }
    }
}
