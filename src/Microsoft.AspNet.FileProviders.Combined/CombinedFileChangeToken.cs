// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Primitives;

namespace Microsoft.AspNet.FileProviders
{
    internal class CombinedFileChangeToken : IChangeToken
    {
        private readonly List<IChangeToken> _changeTokens;

        public CombinedFileChangeToken(List<IChangeToken> changeTokens)
        {
            _changeTokens = changeTokens;
        }

        public IDisposable RegisterChangeCallback(Action<object> callback, object state)
        {
            var disposables = new List<IDisposable>();
            for (int i = 0; i < _changeTokens.Count; i++)
            {
                var disposable = _changeTokens[i].RegisterChangeCallback(callback, state);
                disposables.Add(disposable);
            }
            return new CombinedDisposable(disposables);
        }

        public bool HasChanged
        {
            get { return _changeTokens.Any(token => token.HasChanged); }
        }

        public bool ActiveChangeCallbacks
        {
            get { return _changeTokens.Any(token => token.ActiveChangeCallbacks); }
        }

    }
}