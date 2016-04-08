// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Extensions.FileProviders.Physical.Watcher
{
    public interface IFileSystemWatcher : IDisposable
    {
        event Action<string> OnFileChange;

        bool EnableRisingEvents { get; set; }
    }
}
