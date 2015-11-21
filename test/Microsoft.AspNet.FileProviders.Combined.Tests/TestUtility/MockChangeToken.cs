using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.AspNet.FileProviders.Combined.Tests.TestUtility
{
    internal class MockChangeToken : IChangeToken
    {
        private readonly List<Action<object>> _callsback = new List<Action<object>>();

        public bool ActiveChangeCallbacks
        {
            get
            {
                return true;
            }
        }

        public bool HasChanged
        {
            get; set;
        }

        public int NbOfCallback
        {
            get
            {
                return _callsback.Count;
            }
        }

        public IDisposable RegisterChangeCallback(Action<object> callback, object state)
        {
            _callsback.Add(callback);
            return new MockDisposable();
        }

        internal void RaiseCallback(object item)
        {
            foreach(var callback in _callsback)
            {
                callback(item);
            }
        }

        class MockDisposable : IDisposable
        {
            public bool Disposed { get; set; } = false;
            public void Dispose()
            {
                Disposed = true;
            }
        }
    }
}
