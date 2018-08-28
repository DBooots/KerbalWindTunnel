using System;
using System.Collections.Generic;
using System.Threading;
using System.Collections;

namespace KerbalWindTunnel.Threading
{
    public static class ThreadPool
    {
        private static List<Thread> pool = new List<Thread>();
        private static ConcurrentQueue<object> queue = new ConcurrentQueue<object>();
        public static int ThreadCount
        {
            get
            {
                lock (pool)
                    return pool.Count;
            }
            set
            {
                lock (pool)
                    lock (threadCountKey)
                    {
                        threadCount = value;
                        if (threadCount > pool.Count)
                        {
                            int n = threadCount - pool.Count;
                            int baseCount = pool.Count;
                            for (int i = 0; i < n; i++)
                            {
                                Thread newThread = new Thread(new ParameterizedThreadStart(ThreadTask)) { IsBackground = true };
                                pool.Add(newThread);
                                newThread.Start(baseCount + i);
                            }
                        }
                    }
            }
        }

        private static int threadCount;
        private static object threadCountKey = new object();
        private static ManualResetEvent itemAdded = new ManualResetEvent(false);
        private static bool dispose = false;
        private static bool started = false;

        public static void Start(int threads = 0)
        {
            if (threads <= 0)
                threads = Math.Max(UnityEngine.SystemInfo.processorCount + threads, 1);
            if (started)
            {
                threadCount = threads;
                return;
            }
            threadCount = threads;
            started = true;
            UnityEngine.Debug.LogFormat("Custom thread pool started: {0}", threadCount);

            lock (pool)
            {
                pool.Capacity = threadCount;
                for (int i = 0; i < threadCount; i++)
                {
                    pool.Add(new Thread(new ParameterizedThreadStart(ThreadTask)) { IsBackground = true });
                }
                for (int i = 0; i < threadCount; i++)
                {
                    pool[i].Start(i);
                }
            }
        }

        public static void QueueUserWorkItem(WaitCallback callback)
        {
            QueueUserWorkItem(callback, null);
        }
        public static void QueueUserWorkItem(WaitCallback callback, object state, bool verbose = false)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(callback, state);
        }

        private static void ThreadTask(object idObj)
        {
            int id = (int)idObj;
            while (!dispose)
            {
                //if (ThreadCount > threadCount)
                    //break;
                queue.WaitForItem();
                while (queue.TryDequeue(out object task))
                {
                    try
                    {
                        CallbackStatePair callbackStatePair = (CallbackStatePair)task;
                        callbackStatePair.callback(callbackStatePair.state);
                    }
                    catch (Exception ex)
                    { UnityEngine.Debug.Log(ex); }
                }
            }
            lock (pool)
                pool.RemoveAt(id);
        }

        public static void Dispose(bool abort = false)
        {
            dispose = true;
            queue.ForceRelease();
            if (abort)
                foreach (Thread thread in pool)
                    thread.Abort();
        }

        private struct CallbackStatePair
        {
            public readonly WaitCallback callback;
            public readonly object state;
            public CallbackStatePair(WaitCallback callback, object state)
            {
                this.callback = callback;
                this.state = state;
            }
        }
    }

    public class ConcurrentQueue<T> : IEnumerable<T>
    {
        private readonly Queue<T> queue = new Queue<T>();
        private readonly object countKey = new object();
        private int count = 0;
        internal ManualResetEvent waitForItem = new ManualResetEvent(false);

        public ConcurrentQueue() { }
        public ConcurrentQueue(IEnumerable<T> collection)
        {
            foreach (T item in collection)
                Enqueue(item);
        }

        public int Count
        {
            get
            {
                lock (countKey)
                    return count;
            }
        }

        public bool IsEmpty
        {
            get
            {
                lock (countKey)
                    return count == 0;
            }
        }

        public void CopyTo(T[] array, int index)
        {
            lock (queue)
                queue.CopyTo(array, index);
        }

        public void Enqueue(T item)
        {
            lock (queue)
                lock (countKey)
                {
                    queue.Enqueue(item);
                    count++;
                    // The queue must have been previously empty, so release waiters.
                    if (count == 1)
                        waitForItem.Set();
                }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new ConcurrentQueueEnumerator<T>(this);
        }

        public T[] ToArray()
        {
            lock (queue)
                return queue.ToArray();
        }

        public bool TryDequeue(out T item)
        {
            lock (queue)
            {
                lock (countKey)
                {
                    if (count > 0)
                    {
                        item = queue.Dequeue();
                        count--;
                        if (count == 0)
                            waitForItem.Reset();
                        return true;
                    }
                }
                item = default(T);
                return false;
            }
        }

        public bool TryPeek(out T item)
        {
            lock (queue)
            {
                lock (countKey)
                    if (count > 0)
                    {
                        item = queue.Peek();
                        return true;
                    }
                item = default(T);
                return false;
            }
        }

        public bool WaitForItem()
        {
            return waitForItem.WaitOne();
        }
        public bool WaitForItem(int millisecondsTimeout)
        {
            return waitForItem.WaitOne(millisecondsTimeout);
        }
        public bool WaitForItem(TimeSpan timeout)
        {
            return waitForItem.WaitOne(timeout);
        }

        internal void ForceRelease()
        {
            lock (countKey)
                waitForItem.Reset();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public class ConcurrentQueueEnumerator<T> : IEnumerator<T>
        {
            private readonly ConcurrentQueue<T> queuer;

            public ConcurrentQueueEnumerator(ConcurrentQueue<T> queuer)
            {
                this.queuer = queuer;
            }
            private ConcurrentQueueEnumerator() { }

            public T Current => current;
            private T current;

            object IEnumerator.Current => this.Current;

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                return queuer.TryDequeue(out this.current);
            }

            public void Reset()
            {
            }
        }
    }
}
