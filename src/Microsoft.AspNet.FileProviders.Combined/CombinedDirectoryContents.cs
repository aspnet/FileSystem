// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.AspNet.FileProviders
{
    internal class CombinedDirectoryContents : IDirectoryContents
    {
        private readonly IFileProvider[] _fileProviders;
        private readonly string _subPath;
        private List<IFileInfo> _files;
        private bool _exists;
        private List<IDirectoryContents> _directories;

        public CombinedDirectoryContents(IFileProvider[] fileProviders, string subpath)
        {
            _fileProviders = fileProviders;
            _subPath = subpath;
        }

        private void EnsureDirectoriesAreInitialized()
        {
            if (_directories == null)
            {
                _directories = new List<IDirectoryContents>();
                foreach (var fileProvider in _fileProviders)
                {
                    var directoryContents = fileProvider.GetDirectoryContents(_subPath);
                    if (directoryContents != null && directoryContents.Exists)
                    {
                        _exists = true;
                        _directories.Add(directoryContents);
                    }
                }
            }
        }

        private void EnsureFilesAreInitialized()
        {
            EnsureDirectoriesAreInitialized();
            if (_files == null)
            {
                _files = new List<IFileInfo>();
                var names = new HashSet<string>();
                for (var i = 0; i < _directories.Count; i++)
                {
                    var directoryContents = _directories[i];
                    foreach (var file in directoryContents)
                    {
                        if (names.Add(file.Name))
                        {
                            _files.Add(file);
                        }
                    }
                }
            }
        }

        public IEnumerator<IFileInfo> GetEnumerator()
        {
            EnsureFilesAreInitialized();
            return _files.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            EnsureFilesAreInitialized();
            return _files.GetEnumerator();
        }

        public bool Exists
        {
            get
            {
                EnsureDirectoriesAreInitialized();
                return _exists;
            }
        }
    }
}