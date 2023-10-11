using System.Collections.Generic;
using System.Threading;

namespace RemoteProcedureCalls.Network
{
    public class BlockingQueue<T>
    {
        private Queue<T> queue = new Queue<T>();
        private object lockObject = new object();

        public void Unlock()
        {
            lock (lockObject) Monitor.PulseAll(lockObject);
        }

        public void Enqueue(T item)
        {
            lock (lockObject)
            {
                queue.Enqueue(item);
                Monitor.Pulse(lockObject);
            }
        }

        public bool TryDequeue(out T data)
        {
            lock (lockObject)
            {
                if (queue.Count == 0)
                {
                    Monitor.Wait(lockObject);
                }
                return queue.TryDequeue(out data);
            }
        }
    }
}
