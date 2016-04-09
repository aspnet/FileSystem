// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Extensions.FileProviders.Physical.Watcher;

namespace Microsoft.Extensions.FileProviders
{
    public class MockIFileSystemWatcher : IFileSystemWatcher
    {
        private string _root;

        public MockIFileSystemWatcher(string root)
        {
            _root = root;
        }

        public bool EnableRaisingEvents { get; set; }

        public event EventHandler<string> OnFileChange;

        public event EventHandler OnError;

        public void CallOnFileChange(string fullFilePath)
        {
            while (fullFilePath != _root)
            {
                OnFileChange(this, fullFilePath);
                fullFilePath = Path.GetDirectoryName(fullFilePath);
            }
        }

        public void CallOnError()
        {
            OnError(this, null);
        }

        public void Dispose()
        {
        }
    }
}
