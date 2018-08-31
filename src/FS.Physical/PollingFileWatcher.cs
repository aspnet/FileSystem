// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using Microsoft.Extensions.FileProviders.Physical.Internal;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Extensions.FileProviders.Physical
{
    internal sealed class PollingFileWatcher : IFileWatcher
    {
        public static readonly TimeSpan DefaultPollingInterval = TimeSpan.FromSeconds(4);
        private readonly string _root;
        private readonly Timer _timer;

        public PollingFileWatcher(string root, TimeSpan pollingInterval)
        {
            _root = root;
            PollingChangeTokens = new ConcurrentDictionary<string, IPollingChangeToken>(StringComparer.Ordinal);

            // PhysicalFileProvider lazy initializes IFileWatcher instances. It's OK for us to initialize this in the ctor since we anticipate
            // GetFileChangeToken to be invoked right after.
            _timer = new Timer(RaiseChangeEvents, state: PollingChangeTokens, dueTime: pollingInterval, period: pollingInterval);
        }

        public ConcurrentDictionary<string, IPollingChangeToken> PollingChangeTokens { get; }

        public IChangeToken CreateFileChangeToken(string filter)
        {
            if (filter == null)
            {
                throw new ArgumentNullException(nameof(filter));
            }

            filter = NormalizePath(filter);

            // Absolute paths and paths traversing above root not permitted.
            if (Path.IsPathRooted(filter) || PathUtils.PathNavigatesAboveRoot(filter))
            {
                return NullChangeToken.Singleton;
            }

            var changeToken = GetOrAddChangeToken(filter);
            return changeToken;
        }

        public IChangeToken GetOrAddChangeToken(string pattern)
        {
            if (PollingChangeTokens.TryGetValue(pattern, out var changeToken) && !changeToken.HasChanged)
            {
                return changeToken;
            }

            var isWildCard = pattern.IndexOf('*') != -1;
            var tokenSource = new CancellationTokenSource();
            if (isWildCard || IsDirectoryPath(pattern))
            {
                changeToken = new PollingWildCardChangeToken(_root, pattern, tokenSource);
            }
            else
            {
                var fileInfo = new FileInfo(Path.Combine(_root, pattern));
                changeToken = new PollingFileChangeToken(fileInfo, tokenSource);
            }

            return PollingChangeTokens.GetOrAdd(pattern, changeToken);
        }

        public void Dispose()
        {
            _timer.Dispose();
        }

        ~PollingFileWatcher() => Dispose();

        private static string NormalizePath(string filter) => filter = filter.Replace('\\', '/');

        private static bool IsDirectoryPath(string path)
        {
            return path.Length > 0 &&
                (path[path.Length - 1] == Path.DirectorySeparatorChar ||
                path[path.Length - 1] == Path.AltDirectorySeparatorChar);
        }

        internal static void RaiseChangeEvents(object state)
        {
            // Iterating over a concurrent bag gives us a point in time snapshot making it safe
            // to remove items from it.
            var changeTokens = (ConcurrentDictionary<string, IPollingChangeToken>)state;
            foreach (var item in changeTokens)
            {
                var token = item.Value;

                if (!token.UpdateHasChanged())
                {
                    continue;
                }

                if (!changeTokens.TryRemove(item.Key, out _))
                {
                    // Move on if we couldn't remove the item.
                    continue;
                }

                // We're already on a background thread, don't need to spawn a background Task to cancel the CTS
                try
                {
                    token.CancellationTokenSource.Cancel();
                }
                catch
                {

                }
            }
        }
    }
}
