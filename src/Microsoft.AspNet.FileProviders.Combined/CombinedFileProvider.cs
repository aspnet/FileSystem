// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Primitives;

namespace Microsoft.AspNet.FileProviders
{
    /// <summary>
    /// Looks up files using embedded resources in the specified assembly.
    /// This file provider is case sensitive.
    /// </summary>
    public class CombinedFileProvider : IFileProvider
    {
        private readonly IFileProvider[] _fileProviders;

        /// <summary>
        /// Initializes a new instance of the <see cref="CombinedFileProvider" /> class using a list of file provider.
        /// </summary>
        /// <param name="fileProviders"></param>
        public CombinedFileProvider(params IFileProvider[] fileProviders)
        {
            _fileProviders = fileProviders;
        }

        /// <summary>
        /// Locates a file at the given path.
        /// </summary>
        /// <param name="subpath">The path that identifies the file. </param>
        /// <returns>The file information. Caller must check Exists property. This will be the first existing <see cref="IFileInfo"/> returned by the provided <see cref="IFileProvider"/> or a not found <see cref="IFileInfo"/> if no existing files is found.</returns>
        public IFileInfo GetFileInfo(string subpath)
        {
            foreach (var fileProvider in _fileProviders)
            {
                var fileInfo = fileProvider.GetFileInfo(subpath);
                if (fileInfo != null && fileInfo.Exists)
                {
                    return fileInfo;
                }
            }
            return new NotFoundFileInfo(subpath);
        }

        /// <summary>
        /// Enumerate a directory at the given path, if any.
        /// </summary>
        /// <param name="subpath">The path that identifies the directory</param>
        /// <returns>Contents of the directory. Caller must check Exists property.
        /// The content is a merge of the contents of the provided <see cref="IFileProvider"/>.
        /// When there is multiple <see cref="IFileInfo"/> with the same Name property, only the first one is included on the results.</returns>
        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            var directoryContents = new CombinedDirectoryContents(_fileProviders, subpath);
            return directoryContents;
        }

        /// <summary>
        /// Creates a <see cref="IChangeToken"/> for the specified <paramref name="filter"/>.
        /// </summary>
        /// <remarks></remarks>
        /// <param name="pattern">Filter string used to determine what files or folders to monitor. Example: **/*.cs, *.*, subFolder/**/*.cshtml.</param>
        /// <returns>An <see cref="IChangeToken"/> that is notified when a file matching <paramref name="filter"/> is added, modified or deleted.
        /// The change token will be notified when one of the change token returned by the provided <see cref="IFileProvider"/> will be notified.</returns>
        public IChangeToken Watch(string pattern)
        {
            // Watch all file providers
            var activeChangeTokens = new List<IChangeToken>();
            foreach (var fileProvider in _fileProviders)
            {
                var changeToken = fileProvider.Watch(pattern);
                if (changeToken != null && changeToken.ActiveChangeCallbacks)
                {
                    activeChangeTokens.Add(changeToken);
                }
            }

            // There is no change token with active change callbacks
            if (activeChangeTokens.Count == 0)
            {
                return NoopChangeToken.Singleton;
            }
            var combinedFileChangeToken = new CombinedFileChangeToken(activeChangeTokens);
            return combinedFileChangeToken;
        }

        private class CombinedDirectoryContents : IDirectoryContents
        {
            private readonly Lazy<List<IDirectoryContents>> _directoriesContents;
            private readonly Lazy<List<IFileInfo>> _files;
            private readonly Lazy<bool> _exists;

            public CombinedDirectoryContents(IFileProvider[] fileProviders, string subpath)
            {
                _directoriesContents =new Lazy<List<IDirectoryContents>>(() =>
                {
                    var directories =new List<IDirectoryContents>();
                    foreach (var fileProvider in fileProviders)
                    {
                        var directoryContents = fileProvider.GetDirectoryContents(subpath);
                        if (directoryContents != null && directoryContents.Exists)
                        {
                            directories.Add(directoryContents);
                        }
                    }
                    return directories;
                }
                );

                _files = new Lazy<List<IFileInfo>>(() =>
                {
                    var files = new List<IFileInfo>();
                    var names = new HashSet<string>();

                    var directories = _directoriesContents.Value;
                    for (int i = 0; i < directories.Count; i++)
                    {
                        var directoryContents = directories[i];
                        foreach (var file in directoryContents)
                        {
                            if (names.Add(file.Name))
                            {
                                files.Add(file);
                            }
                        }
                    }
                    return files;
                });

                _exists = new Lazy<bool>(() =>
                {
                    var directories = _directoriesContents.Value;
                    var exists = directories.Count > 0;
                    return exists;
                });
            }

            public IEnumerator<IFileInfo> GetEnumerator()
            {
                return _files.Value.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return _files.Value.GetEnumerator();
            }

            // This directory exists because it is created only when there is existing IDrectoryContents to merge.
            public bool Exists { get { return _exists.Value; } }
        }

        private class CombinedFileChangeToken : IChangeToken
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
                return new Disposables(disposables);
            }

            public bool HasChanged
            {
                get { return _changeTokens.Any(_ => _.HasChanged); }
            }

            public bool ActiveChangeCallbacks
            {
                get { return _changeTokens.Any(_ => _.ActiveChangeCallbacks); }
            }

            private class Disposables : IDisposable
            {
                private readonly List<IDisposable> _disposables;

                public Disposables(List<IDisposable> disposables)
                {
                    _disposables = disposables;
                }
                public void Dispose()
                {
                    if (_disposables != null)
                    {
                        for (int i = 0; i < _disposables.Count; i++)
                        {
                            _disposables[i].Dispose();
                        }
                    }
                }
            }
        }
    }
}
