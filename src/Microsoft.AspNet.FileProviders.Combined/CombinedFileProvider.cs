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
        private readonly IEnumerable<IFileProvider> _fileProviders;

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
        /// <returns>Contents of the directory. Caller must check Exists property. The content is a merge of the contents of the provided <see cref="IFileProvider"/>. When there is multiple <see cref="IFileInfo"/> with the same Name property, only the first one is included on the results.</returns>
        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            // gets the content of all directories and merged them
            var existingDirectoryContentsForAllProviders = _fileProviders.Select(
                fileProvider => fileProvider.GetDirectoryContents(subpath))
                .Where(directoryContents => directoryContents != null && directoryContents.Exists)
                .ToList();

            // There is no existing directory contents
            if (existingDirectoryContentsForAllProviders.Count == 0)
            {
                return new NotFoundDirectoryContents();
            }
            var combinedDirectoryContents = new CombinedDirectoryContents(existingDirectoryContentsForAllProviders);
            return combinedDirectoryContents;
        }

        /// <summary>
        /// Creates a <see cref="IChangeToken"/> for the specified <paramref name="filter"/>.
        /// </summary>
        /// <remarks></remarks>
        /// <param name="pattern">Filter string used to determine what files or folders to monitor. Example: **/*.cs, *.*, subFolder/**/*.cshtml.</param>
        /// <returns>An <see cref="IChangeToken"/> that is notified when a file matching <paramref name="filter"/> is added, modified or deleted. The change token will be notified when one of the change token returned by the provided <see cref="IFileProvider"/> will be notified.</returns>
        public IChangeToken Watch(string pattern)
        {
            // Watch all file providers
            var activeChangeTokens = _fileProviders
                .Select(fileProvider => fileProvider.Watch(pattern))
                .Where(changeToken => changeToken != null && changeToken.ActiveChangeCallbacks).ToList();

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
            private readonly Dictionary<string, IFileInfo> _files = new Dictionary<string, IFileInfo>();

            public CombinedDirectoryContents(IEnumerable<IEnumerable<IFileInfo>> listOfFiles)
            {
                foreach (var files in listOfFiles)
                {
                    Exists = true;
                    foreach (var file in files)
                    {
                        if (!_files.ContainsKey(file.Name))
                        {
                            _files.Add(file.Name, file);
                        }
                    }
                }
            }

            public IEnumerator<IFileInfo> GetEnumerator()
            {
                return _files.Values.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return _files.Values.GetEnumerator();
            }

            public bool Exists { get; }
        }

        private class CombinedFileChangeToken : IChangeToken
        {
            private readonly IEnumerable<IChangeToken> _changeTokens;

            public CombinedFileChangeToken(IEnumerable<IChangeToken> changeTokens)
            {
                _changeTokens = changeTokens ?? new List<IChangeToken>();
            }

            public IDisposable RegisterChangeCallback(Action<object> callback, object state)
            {
                return new Disposables(_changeTokens.Where(changeToken => changeToken.ActiveChangeCallbacks).Select(changeToken => changeToken.RegisterChangeCallback(callback, state)).ToList());
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
                private readonly IEnumerable<IDisposable> _disposables;

                public Disposables(IEnumerable<IDisposable> disposables)
                {
                    _disposables = disposables;
                }
                public void Dispose()
                {
                    if (_disposables != null)
                    {
                        foreach (var disposable in _disposables)
                        {
                            disposable.Dispose();
                        }
                    }
                }
            }
        }
    }
}
