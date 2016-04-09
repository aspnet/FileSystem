// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileProviders.Physical.Watcher;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Extensions.FileProviders.Physical
{
    public class PhysicalFilesWatcher : IDisposable
    {
        private readonly ConcurrentDictionary<string, FileChangeToken> _tokenCache =
            new ConcurrentDictionary<string, FileChangeToken>(StringComparer.OrdinalIgnoreCase);
        private readonly IFileSystemWatcher _fileWatcher;
        private readonly object _lockObject = new object();
        private readonly string _root;

        public PhysicalFilesWatcher(string root)
            : this(root, FileWatcherFactory.CreateWatcher(root))
        {
        }

        public PhysicalFilesWatcher(string root, IFileSystemWatcher watcher)
        {
            _root = root;
            _fileWatcher = watcher;
            _fileWatcher.OnFileChange += OnChanged;
            _fileWatcher.OnError += OnError;
        }

        internal IChangeToken CreateFileChangeToken(string filter)
        {
            filter = NormalizeFilter(filter);
            var pattern = WildcardToRegexPattern(filter);

            FileChangeToken changeToken;
            if (!_tokenCache.TryGetValue(pattern, out changeToken))
            {
                changeToken = _tokenCache.GetOrAdd(pattern, new FileChangeToken(pattern));
                lock (_lockObject)
                {
                    if (_tokenCache.Count > 0 && !_fileWatcher.EnableRaisingEvents)
                    {
                        // Perf: Turn on the file monitoring if there is something to monitor.
                        _fileWatcher.EnableRaisingEvents = true;
                    }
                }
            }

            return changeToken;
        }

        public void Dispose()
        {
            _fileWatcher.Dispose();
        }

        private void OnChanged(object sender, string fullPath)
        {
            OnFileSystemEntryChange(fullPath);
        }

        private void OnError(object sender, EventArgs args)
        {
            // Notify all cache entries on error.
            foreach (var token in _tokenCache.Values)
            {
                ReportChangeForMatchedEntries(token.Pattern);
            }
        }

        private void OnFileSystemEntryChange(string fullPath)
        {
            var fileSystemInfo = new FileInfo(fullPath);
            if (FileSystemInfoHelper.IsHiddenFile(fileSystemInfo))
            {
                return;
            }

            var relativePath = fullPath.Substring(_root.Length);
            if (_tokenCache.ContainsKey(relativePath))
            {
                ReportChangeForMatchedEntries(relativePath);
            }
            else
            {
                foreach (var token in _tokenCache.Values.Where(t => t.IsMatch(relativePath)))
                {
                    ReportChangeForMatchedEntries(token.Pattern);
                }
            }
        }

        private void ReportChangeForMatchedEntries(string pattern)
        {
            FileChangeToken changeToken;
            if (_tokenCache.TryRemove(pattern, out changeToken))
            {
                changeToken.Changed();
                if (_tokenCache.Count == 0)
                {
                    lock (_lockObject)
                    {
                        if (_tokenCache.Count == 0 && _fileWatcher.EnableRaisingEvents)
                        {
                            // Perf: Turn off the file monitoring if no files to monitor.
                            _fileWatcher.EnableRaisingEvents = false;
                        }
                    }
                }
            }
        }

        private string NormalizeFilter(string filter)
        {
            // If the searchPath ends with \ or /, we treat searchPath as a directory,
            // and will include everything under it, recursively.
            if (IsDirectoryPath(filter))
            {
                filter = filter + "**" + Path.DirectorySeparatorChar + "*";
            }

            filter = Path.DirectorySeparatorChar == '/' ?
                filter.Replace('\\', Path.DirectorySeparatorChar) :
                filter.Replace('/', Path.DirectorySeparatorChar);

            return filter;
        }

        private bool IsDirectoryPath(string path)
        {
            return path != null && path.Length >= 1 && (path[path.Length - 1] == Path.DirectorySeparatorChar || path[path.Length - 1] == Path.AltDirectorySeparatorChar);
        }

        private string WildcardToRegexPattern(string wildcard)
        {
            var regex = Regex.Escape(wildcard);

            if (Path.DirectorySeparatorChar == '/')
            {
                // regex wildcard adjustments for *nix-style file systems.
                regex = regex
                    .Replace(@"\*\*/", "(.*/)?") //For recursive wildcards /**/, include the current directory.
                    .Replace(@"\*\*", ".*") // For recursive wildcards that don't end in a slash e.g. **.txt would be treated as a .txt file at any depth
                    .Replace(@"\*\.\*", @"\*") // "*.*" is equivalent to "*"
                    .Replace(@"\*", @"[^/]*(/)?") // For non recursive searches, limit it any character that is not a directory separator
                    .Replace(@"\?", "."); // ? translates to a single any character
            }
            else
            {
                // regex wildcard adjustments for Windows-style file systems.
                regex = regex
                    .Replace("/", @"\\") // On Windows, / is treated the same as \.
                    .Replace(@"\*\*\\", @"(.*\\)?") //For recursive wildcards \**\, include the current directory.
                    .Replace(@"\*\*", ".*") // For recursive wildcards that don't end in a slash e.g. **.txt would be treated as a .txt file at any depth
                    .Replace(@"\*\.\*", @"\*") // "*.*" is equivalent to "*"
                    .Replace(@"\*", @"[^\\]*(\\)?") // For non recursive searches, limit it any character that is not a directory separator
                    .Replace(@"\?", "."); // ? translates to a single any character
            }

            return regex;
        }
    }
}