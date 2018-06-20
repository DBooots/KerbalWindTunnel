using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;

namespace KerbalWindTunnel
{
    public class CalculationManager
    {
        private volatile int linked = 0;
        private volatile int completed = 0;
        private readonly object linkLock = new object();
        private readonly object completeLock = new object();
        private readonly object statusLock = new object();
        private volatile bool _cancelled = false;
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

        public void Cancel()
        {
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
            return true;
        }

        public State GetStateToken()
        {
            return State.CreateToken(this);
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
