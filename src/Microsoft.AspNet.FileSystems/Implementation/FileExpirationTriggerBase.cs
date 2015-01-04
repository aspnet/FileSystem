// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Framework.Expiration.Interfaces;

namespace Microsoft.AspNet.FileSystems
{
    public abstract class FileExpirationTriggerBase : IExpirationTrigger
    {
        public bool ActiveExpirationCallbacks
        {
            get { return true; }
        }

        protected CancellationTokenSource TokenSource { get; } = new CancellationTokenSource();

        public virtual bool IsExpired
        {
            get { return TokenSource.Token.IsCancellationRequested; }
        }

        public IDisposable RegisterExpirationCallback(Action<object> callback, object state)
        {
            return TokenSource.Token.Register(callback, state);
        }

        /// <remarks>
        /// The <see cref="Task"/> returned by this method is not waited on by the calling code. It's only purpose
        /// is to guarantee the correct sequence of events for unit tests.
        /// </remarks>
        public Task Changed()
        {
            return Task.Run(() =>
            {
                try
                {
                    TokenSource.Cancel();
                }
                catch
                {
                }
            });
        }
    }
}