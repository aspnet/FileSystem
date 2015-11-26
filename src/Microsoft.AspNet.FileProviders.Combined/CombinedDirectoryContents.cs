// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.AspNet.FileProviders
{
    internal class CombinedDirectoryContents : IDirectoryContents
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
}