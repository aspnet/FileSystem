// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.AspNet.FileProviders.Combined.Tests.TestUtility
{
    internal class MockChangeToken : IChangeToken
    {
        private readonly List<Tuple<Action<object>, object>> _callsback = new List<Tuple<Action<object>, object>>();

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

        public List<Tuple<Action<object>, object>> Callsback
        {
            get
            {
                return _callsback;
            }
        }

        public IDisposable RegisterChangeCallback(Action<object> callback, object state)
        {
            _callsback.Add(Tuple.Create(callback, state));
            return new MockDisposable();
        }

        internal void RaiseCallback(object item)
        {
            foreach(var callback in _callsback)
            {
                callback.Item1(item);
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
