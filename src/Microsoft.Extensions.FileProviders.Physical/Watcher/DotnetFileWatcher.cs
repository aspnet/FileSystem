using System;
using System.IO;

namespace Microsoft.Extensions.FileProviders.Physical.Watcher
{
    internal class DotnetFileWatcher : IFileSystemWatcher
    {
        private readonly string _watchedDirectory;
        private FileSystemWatcher _fileSystemWatcher;

        public DotnetFileWatcher(string watchedDirectory)
        {
            if (string.IsNullOrEmpty(watchedDirectory))
            {
                throw new ArgumentNullException(nameof(watchedDirectory));
            }

            _watchedDirectory = watchedDirectory;
            CreateFileSystemWatcher();
        }

        private void FSW_Error(object sender, ErrorEventArgs e)
        {
            // Recreate the watcher
            CreateFileSystemWatcher();
        }

        private void FSW_Renamed(object sender, RenamedEventArgs e)
        {
            NotifyChange(e.OldFullPath);
            NotifyChange(e.FullPath);
        }

        private void FSW_Changed(object sender, FileSystemEventArgs e)
        {
            NotifyChange(e.FullPath);
        }

        private void NotifyChange(string fullPath)
        {
            // Only report file changes
            if (File.Exists(fullPath))
            {
                OnFileChange(fullPath);
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

            _fileSystemWatcher = new FileSystemWatcher(_watchedDirectory);
            _fileSystemWatcher.IncludeSubdirectories = true;

            _fileSystemWatcher.Created += FSW_Changed;
            _fileSystemWatcher.Deleted += FSW_Changed;
            _fileSystemWatcher.Changed += FSW_Changed;
            _fileSystemWatcher.Renamed += FSW_Renamed;
            _fileSystemWatcher.Error += FSW_Error;

            _fileSystemWatcher.EnableRaisingEvents = enableEvents;
        }

        public bool EnableRisingEvents
        {
            get { return _fileSystemWatcher.EnableRaisingEvents; }
            set { _fileSystemWatcher.EnableRaisingEvents = value; }
        }

        public event Action<string> OnFileChange;

        public void Dispose()
        {
            _fileSystemWatcher.Dispose();
        }
    }
}
