using System.Collections.Generic;
using System.Threading;

namespace XenConsoleComm.Tests.Helpers
{
    class ThreadSafeDict<TKey, TValue> : Dictionary<TKey, TValue>
    {
        private Mutex _mutex = new Mutex();

        public new TValue this[TKey key]
        {
            get
            {
                lock (_mutex)
                {
                    return base[key];
                }
            }

            set
            {
                lock (_mutex)
                {
                    base[key] = value;
                }
            }
        }
    }
}
