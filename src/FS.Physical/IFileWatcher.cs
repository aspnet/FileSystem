// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Extensions.FileProviders
{
    internal interface IFileWatcher : IDisposable
    {
        IChangeToken CreateFileChangeToken(string filter);
    }
}
