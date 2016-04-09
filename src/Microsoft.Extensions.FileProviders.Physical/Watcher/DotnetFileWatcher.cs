// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace Microsoft.Extensions.FileProviders.Physical.Watcher
{
    internal class DotnetFileWatcher : IFileSystemWatcher
    {
        private readonly Func<string, FileSystemWatcher> _watcherFactory;
        private readonly string _watchedDirectory;

        private FileSystemWatcher _fileSystemWatcher;

        public DotnetFileWatcher(string watchedDirectory)
            : this(watchedDirectory, DefaultWatcherFactory)
        {
        }

        internal DotnetFileWatcher(string watchedDirectory, Func<string, FileSystemWatcher> fileSystemWatcherFactory)
        {
            if (string.IsNullOrEmpty(watchedDirectory))
            {
                throw new ArgumentNullException(nameof(watchedDirectory));
            }

            _watchedDirectory = watchedDirectory;
            _watcherFactory = fileSystemWatcherFactory;
            CreateFileSystemWatcher();
        }

        public event EventHandler<string> OnFileChange;

        public event EventHandler OnError;

        private static FileSystemWatcher DefaultWatcherFactory(string watchedDirectory)
        {
            if (string.IsNullOrEmpty(watchedDirectory))
            {
                throw new ArgumentNullException(nameof(watchedDirectory));
            }

            return new FileSystemWatcher(watchedDirectory);
        }

        private void FSW_Error(object sender, ErrorEventArgs e)
        {
            // Recreate the watcher
            CreateFileSystemWatcher();

            if (OnError != null)
            {
                OnError(this, null);
            }
        }

        private void FSW_Renamed(object sender, RenamedEventArgs e)
        {
            NotifyChange(e.OldFullPath);
            NotifyChange(e.FullPath);

            if (Directory.Exists(e.FullPath))
            {
                // If the renamed entity is a directory then notify tokens for every sub item.
                foreach (var newLocation in Directory.EnumerateFileSystemEntries(e.FullPath, "*", SearchOption.AllDirectories))
                {
                    // Calculated previous path of this moved item.
                    var oldLocation = Path.Combine(e.OldFullPath, newLocation.Substring(e.FullPath.Length + 1));
                    NotifyChange(oldLocation);
                    NotifyChange(newLocation);
                }
            }
        }

        private void FSW_Changed(object sender, FileSystemEventArgs e)
        {

            NotifyChange(e.FullPath);
        }

        private void NotifyChange(string fullPath)
        {
            if (OnFileChange != null)
            {
                // Only report file changes
                OnFileChange(this, fullPath);
            }
        }

        private void CreateFileSystemWatcher()
        {
            bool enableEvents = false;

            if (_fileSystemWatcher != null)
            {
                enableEvents = _fileSystemWatcher.EnableRaisingEvents;

                _fileSystemWatcher.EnableRaisingEvents = false;

                _fileSystemWatcher.Created -= FSW_Changed;
                _fileSystemWatcher.Deleted -= FSW_Changed;
                _fileSystemWatcher.Changed -= FSW_Changed;
                _fileSystemWatcher.Renamed -= FSW_Renamed;
                _fileSystemWatcher.Error -= FSW_Error;

                _fileSystemWatcher.Dispose();
            }

            _fileSystemWatcher = _watcherFactory(_watchedDirectory);
            _fileSystemWatcher.IncludeSubdirectories = true;

            _fileSystemWatcher.Created += FSW_Changed;
            _fileSystemWatcher.Deleted += FSW_Changed;
            _fileSystemWatcher.Changed += FSW_Changed;
            _fileSystemWatcher.Renamed += FSW_Renamed;
            _fileSystemWatcher.Error += FSW_Error;

            _fileSystemWatcher.EnableRaisingEvents = enableEvents;
        }

        public bool EnableRaisingEvents
        {
            get { return _fileSystemWatcher.EnableRaisingEvents; }
            set { _fileSystemWatcher.EnableRaisingEvents = value; }
        }

        public void Dispose()
        {
            _fileSystemWatcher.Dispose();
        }
    }
}
