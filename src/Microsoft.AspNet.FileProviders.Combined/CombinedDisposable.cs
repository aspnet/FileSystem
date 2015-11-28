// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.AspNet.FileProviders
{
    internal class CombinedDisposable : IDisposable
    {
        private readonly List<IDisposable> _disposables;

        public CombinedDisposable(List<IDisposable> disposables)
        {
            _disposables = disposables;
        }

        public void Dispose()
        {
            for (var i = 0; i < _disposables.Count; i++)
            {
                _disposables[i].Dispose();
            }
        }
    }
}