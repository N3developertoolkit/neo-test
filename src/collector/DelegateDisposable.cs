using System;
using System.Collections.Generic;

namespace Neo.Collector
{
    class DelegateDisposable : IDisposable
    {
        readonly Action disposeDelegate;
        bool disposed = false;

        public DelegateDisposable(Action disposeDelegate)
        {
            this.disposeDelegate = disposeDelegate;
        }

        public void Dispose()
        {
            if (!disposed)
            {
                disposeDelegate();
                disposed = true;
            }
        }
    }
}