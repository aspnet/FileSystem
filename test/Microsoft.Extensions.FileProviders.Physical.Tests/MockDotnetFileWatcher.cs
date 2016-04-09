using System;
using System.IO;
using Microsoft.Extensions.FileProviders.Physical.Watcher;

namespace Microsoft.Extensions.FileProviders
{
    public class MockDotnetFileWatcher : IFileSystemWatcher
    {
        private DotnetFileWatcher _mockedWatcher;
        private MockFileSystemWatcher _fsw;

        public MockDotnetFileWatcher(string directory)
        {
            _fsw = new MockFileSystemWatcher(directory);

            _mockedWatcher = new DotnetFileWatcher(directory, d => _fsw);
            _mockedWatcher.EnableRaisingEvents = true;
            _mockedWatcher.OnError += (s, e) =>
            {
                if (OnError != null)
                {
                    OnError(this, e);
                }
            };
            _mockedWatcher.OnFileChange += (s, e) =>
            {
                if (OnFileChange != null)
                {
                    OnFileChange(this, e);
                }
            };
        }

        public bool EnableRaisingEvents { get; set; }

        public event EventHandler OnError;
        public event EventHandler<string> OnFileChange;


        public void CallOnRename(string oldPath, string newPath)
        {
            _fsw.CallOnRenamed(new RenamedEventArgs(
                WatcherChangeTypes.Renamed,
                Path.GetDirectoryName(oldPath),
                Path.GetFileName(newPath),
                Path.GetFileName(oldPath)));
        }

        public void Dispose()
        {
            _mockedWatcher.Dispose();
        }

        private class MockFileSystemWatcher : FileSystemWatcher
        {
            public MockFileSystemWatcher(string root)
                : base(root)
            {
            }

            public void CallOnChanged(FileSystemEventArgs e)
            {
                OnChanged(e);
            }

            public void CallOnCreated(FileSystemEventArgs e)
            {
                OnCreated(e);
            }

            public void CallOnDeleted(FileSystemEventArgs e)
            {
                OnDeleted(e);
            }

            public void CallOnError(ErrorEventArgs e)
            {
                OnError(e);
            }

            public void CallOnRenamed(RenamedEventArgs e)
            {
                OnRenamed(e);
            }
        }
    }
}
