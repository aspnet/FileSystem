using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Microsoft.Extensions.FileProviders.Physical.Watcher
{
    internal class PollingFileWatcher : IFileSystemWatcher
    {
        // The minimum interval to rerun the scan
        private static readonly TimeSpan _minRunInternal = TimeSpan.FromSeconds(.5);

        private readonly string _watchedDirectory;

        private Dictionary<string, FileMeta> _knownFiles = new Dictionary<string, FileMeta>();
        private Dictionary<string, FileMeta> _tempDictionary = new Dictionary<string, FileMeta>();

        private Thread _pollingThread;
        private bool _raiseEvents;

        private bool _disposed;

        public PollingFileWatcher(string watchedDirectory)
        {
            if (string.IsNullOrEmpty(watchedDirectory))
            {
                throw new ArgumentNullException(nameof(watchedDirectory));
            }

            _watchedDirectory = watchedDirectory;

            _pollingThread = new Thread(new ThreadStart(PollingLoop));
            _pollingThread.IsBackground = true;
            _pollingThread.Name = nameof(PollingFileWatcher);
            _pollingThread.Start();
        }

        public event Action<string> OnFileChange;

        public bool EnableRisingEvents
        {
            get
            {
                return _raiseEvents;
            }
            set
            {
                EnsureNotDisposed();

                if (value == true)
                {
                    CreateKnownFilesSnapshot();

                    if (_pollingThread.ThreadState == System.Threading.ThreadState.Unstarted)
                    {
                        // Start the loop the first time events are enabled
                        _pollingThread.Start();
                    }
                }
                _raiseEvents = value;
            }
        }

        private void PollingLoop()
        {
            var stopwatch = Stopwatch.StartNew();
            stopwatch.Start();

            while (!_disposed)
            {
                if (stopwatch.Elapsed < _minRunInternal)
                {
                    // Don't run to often
                    // The min wait time here can be double
                    // the value of the variable (FYI)
                    Thread.Sleep(_minRunInternal);
                }

                stopwatch.Reset();

                if (!_raiseEvents)
                {
                    continue;
                }

                CheckForChangedFiles();
            }

            stopwatch.Stop();
        }

        private void CreateKnownFilesSnapshot()
        {
            _knownFiles.Clear();

            ForeachFileInDirectory(f =>
            {
                _knownFiles.Add(f.FullName, new FileMeta(f.LastWriteTime));
            });
        }

        private void CheckForChangedFiles()
        {
            ForeachFileInDirectory(f =>
            {
                var fullFilePath = f.FullName;

                if (!_knownFiles.ContainsKey(fullFilePath))
                {
                    // New file
                    NotifyChange(fullFilePath);
                }
                else
                {
                    var fileMeta = _knownFiles[fullFilePath];
                    if (fileMeta.LastWriteTime != f.LastWriteTime)
                    {
                        // File changed
                        NotifyChange(fullFilePath);
                    }

                    _knownFiles[fullFilePath] = new FileMeta(fileMeta.LastWriteTime, true);
                }

                _tempDictionary.Add(f.FullName, new FileMeta(f.LastWriteTime));
            });

            foreach (var file in _knownFiles)
            {
                if (!file.Value.FoundAgain)
                {
                    // File deleted
                    NotifyChange(file.Key);
                }
            }

            // Swap the two dictionaries
            var swap = _knownFiles;
            _knownFiles = _tempDictionary;
            _tempDictionary = swap;

            _tempDictionary.Clear();
        }

        private void ForeachFileInDirectory(Action<FileInfo> fileAction)
        {
            var watchedFiles = Directory.EnumerateFiles(_watchedDirectory, "*.*", SearchOption.AllDirectories);
            foreach (var fullFilePath in watchedFiles)
            {
                fileAction(new FileInfo(fullFilePath));
            }
        }

        private void NotifyChange(string fullPath)
        {
            if (!_disposed && _raiseEvents)
            {
                OnFileChange(fullPath);
            }
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PollingFileWatcher));
            }
        }

        public void Dispose()
        {
            EnableRisingEvents = false;
            _disposed = true;
        }

        private struct FileMeta
        {
            public FileMeta(DateTime lastWriteTime, bool foundAgain = false)
            {
                LastWriteTime = lastWriteTime;
                FoundAgain = foundAgain;
            }

            public DateTime LastWriteTime;

            public bool FoundAgain;
        }
    }
}
