// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNet.FileSystems
{
    internal class FileChangeTrigger : FileExpirationTriggerBase
    {
        private static readonly TimeSpan FileCheckInterval = TimeSpan.FromSeconds(3);
        private readonly IFileSystem _fileSystem;
        private DateTime _lastRefreshTime;
        private DateTimeOffset _lastModified;

        public FileChangeTrigger(IFileSystem fileSystem, string filePath)
        {
            _fileSystem = fileSystem;
            FilePath = filePath;
            UpdateFileInfo();
        }

        public string FilePath { get; }

        public override bool IsExpired
        {
            get { return base.IsExpired || IsFileChangedInFileSystem(); }
        }

        // for unit testing.
        protected virtual DateTime UtcNow
        {
            get { return DateTime.UtcNow; }
        }

        private bool IsFileChangedInFileSystem()
        {
            if (UtcNow - _lastRefreshTime < FileCheckInterval)
            {
                // Don't hit the file system if the cache is still valid.
                return false;
            }

            var previousLastModified = _lastModified;
            UpdateFileInfo();

            return previousLastModified != _lastModified;
        }

        private void UpdateFileInfo()
        {
            var fileInfo = _fileSystem.GetFileInfo(FilePath);
            _lastModified = fileInfo.LastModified;

            _lastRefreshTime = UtcNow;
        }
    }
}