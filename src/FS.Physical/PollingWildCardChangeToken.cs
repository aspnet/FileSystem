// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Extensions.FileProviders.Physical
{
    /// <summary>
    /// A polling based <see cref="IChangeToken"/> for wildcard patterns.
    /// </summary>
    public class PollingWildCardChangeToken : IPollingChangeToken
    {
        private static readonly byte[] Separator = Encoding.Unicode.GetBytes("|");
        private readonly DirectoryInfoBase _directoryInfo;
        private readonly Matcher _matcher;
        private byte[] _byteBuffer;
        private byte[] _previousHash;
        private CancellationChangeToken _changeToken;
        private DateTime _lastScanTimeUtc;

        /// <summary>
        /// Initializes a new instance of <see cref="PollingWildCardChangeToken"/>.
        /// </summary>
        /// <param name="root">The root of the file system.</param>
        /// <param name="pattern">The pattern to watch.</param>
        /// <param name="cancellationTokenSource">The <see cref="System.Threading.CancellationTokenSource"/>.</param>
        public PollingWildCardChangeToken(
            string root,
            string pattern,
            CancellationTokenSource cancellationTokenSource)
            : this(
                new DirectoryInfoWrapper(new DirectoryInfo(root)),
                pattern,
                Physical.Clock.Instance,
                cancellationTokenSource)
        {
        }

        // Internal for unit testing.
        internal PollingWildCardChangeToken(
            DirectoryInfoBase directoryInfo,
            string pattern,
            IClock clock,
            CancellationTokenSource cancellationTokenSource)
        {
            _directoryInfo = directoryInfo;
            Clock = clock;

            _matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            _matcher.AddInclude(pattern);
            CancellationTokenSource = cancellationTokenSource;
            _changeToken = new CancellationChangeToken(cancellationTokenSource.Token);
            CalculateChanges();
        }

        /// <inheritdoc />
        public bool ActiveChangeCallbacks => true;

        internal CancellationTokenSource CancellationTokenSource { get; }

        CancellationTokenSource IPollingChangeToken.CancellationTokenSource => CancellationTokenSource;

        private IClock Clock { get; }

        /// <inheritdoc />
        public bool HasChanged { get; private set; }

        /// <summary>
        /// Updates <see cref="HasChanged"/>.
        /// </summary>
        /// <returns>The updated value of <see cref="HasChanged"/>.</returns>
        public bool UpdateHasChanged()
        {
            HasChanged |= CalculateChanges();
            return HasChanged;
        }

        private bool CalculateChanges()
        {
            var result = _matcher.Execute(_directoryInfo);

            var files = result.Files.OrderBy(f => f.Path, StringComparer.Ordinal);

            // To verify if directory contents changed,
            // a) Determine if no file is newer than the last time this method scanned for files
            // b) Determine if files were added, removed or changed. To do this, we'll diff the hash of all the file paths as a result of a scan.
            using (var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                foreach (var file in files)
                {
                    var lastWriteTimeUtc = GetLastWriteUtc(file.Path);
                    if (_lastScanTimeUtc != null && _lastScanTimeUtc < lastWriteTimeUtc)
                    {
                        // _lastScanTimeUtc is the greatest timestamp that any last writes could have been.
                        // If a file has a newer timestamp than this value, it must've changed.
                        return true;
                    }

                    ComputeHash(sha256, file.Path, lastWriteTimeUtc);
                }

                var currentHash = sha256.GetHashAndReset();
                if (!ArrayEquals(_previousHash, currentHash))
                {
                    return true;
                }

                _previousHash = currentHash;
                _lastScanTimeUtc = Clock.UtcNow;
            }

            return false;
        }

        /// <summary>
        /// Gets the last write time of the file at the specified <paramref name="path"/>.
        /// </summary>
        /// <param name="path">The root relative path.</param>
        /// <returns>The <see cref="DateTime"/> that the file was last modified.</returns>
        protected virtual DateTime GetLastWriteUtc(string path)
        {
            return File.GetLastWriteTimeUtc(Path.Combine(_directoryInfo.FullName, path));
        }

        private static bool ArrayEquals(byte[] previousHash, byte[] currentHash)
        {
            if (previousHash == null)
            {
                // First run
                return true;
            }

            Debug.Assert(previousHash.Length == currentHash.Length);
            for (var i = 0; i < previousHash.Length; i++)
            {
                if (previousHash[i] != currentHash[i])
                {
                    return false;
                }
            }

            return true;
        }

        private void ComputeHash(IncrementalHash sha256, string path, DateTime lastChangedUtc)
        {
            var byteCount = Encoding.Unicode.GetByteCount(path);
            if (_byteBuffer == null || byteCount > _byteBuffer.Length)
            {
                _byteBuffer = new byte[Math.Max(byteCount, 256)];
            }

            var length = Encoding.Unicode.GetBytes(path, 0, path.Length, _byteBuffer, 0);
            sha256.AppendData(_byteBuffer, 0, length);
            sha256.AppendData(Separator, 0, Separator.Length);

            Debug.Assert(_byteBuffer.Length > sizeof(long));
            unsafe
            {
                fixed (byte* b = _byteBuffer)
                {
                    *((long*)b) = lastChangedUtc.Ticks;
                }
            }
            sha256.AppendData(_byteBuffer, 0, sizeof(long));
            sha256.AppendData(Separator, 0, Separator.Length);
        }

        IDisposable IChangeToken.RegisterChangeCallback(Action<object> callback, object state)
        {
            if (!ActiveChangeCallbacks)
            {
                return EmptyDisposable.Instance;
            }

            return _changeToken.RegisterChangeCallback(callback, state);
        }
    }
}
