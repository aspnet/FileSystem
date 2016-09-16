// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.FileProviders.Physical.Internal;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Extensions.FileProviders.Physical
{
    public class PollingWildCardChangeToken : IChangeToken
    {
        public static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(4);
        private const int DefaultBufferSize = 256;
        private readonly object _enumerationLock = new object();
        private readonly DirectoryInfoBase _directoryInfo;
        private readonly Matcher _matcher;
        private bool _changed;
        private DateTime _lastScanTimeUtc;
        private byte[] _fileNameBuffer;
        private byte[] _previousHash;

        public PollingWildCardChangeToken(
            string root,
            string pattern)
            : this(
                new DirectoryInfoWrapper(new DirectoryInfo(root)),
                pattern,
                Internal.Clock.Instance)
        {
        }

        public PollingWildCardChangeToken(
            DirectoryInfoBase directoryInfo,
            string pattern,
            IClock clock)
        {
            _directoryInfo = directoryInfo;
            Clock = clock;

            _matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            _matcher.AddInclude(pattern);
            CalculateChanges();
        }

        /// <inheritdoc />
        public bool ActiveChangeCallbacks => false;

        public IClock Clock { get; }

        /// <inheritdoc />
        public bool HasChanged
        {
            get
            {
                if (_changed)
                {
                    return _changed;
                }

                if (Clock.UtcNow - _lastScanTimeUtc >= PollingInterval)
                {
                    lock (_enumerationLock)
                    {
                        _changed = CalculateChanges();
                    }
                }

                _lastScanTimeUtc = Clock.UtcNow;
                return _changed;
            }
        }

        private bool CalculateChanges()
        {
            var result = _matcher.Execute(_directoryInfo);
            var hasChanges = false;

            var files = result.Files.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase);
#if NET451
            using (var sha256 = new IncrementalHash())
#else
            using (var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
#endif
            {
                foreach (var file in files)
                {
                    if (!hasChanges &&
                        _lastScanTimeUtc < GetLastWriteUtc(file.Path))
                    {
                        // _lastScanTimeUtc is the greatest timestamp that any last writes could have been.
                        // If a file has a newer timestamp than this value, it must've changed.
                        hasChanges = true;
                    }

                    ComputeHash(sha256, file.Path);
                }

                var currentHash = sha256.GetHashAndReset();
                hasChanges |= !ArrayEquals(_previousHash, currentHash);
                _previousHash = currentHash;
            }

            return hasChanges;
        }

        protected virtual DateTime GetLastWriteUtc(string path)
        {
            return File.GetLastWriteTimeUtc(Path.Combine(_directoryInfo.FullName, path));
        }

        private bool ArrayEquals(byte[] _previousHash, byte[] currentHash)
        {
            if (_previousHash == null)
            {
                // First run
                return true;
            }

            Debug.Assert(_previousHash.Length == currentHash.Length);
            for (var i = 0; i < _previousHash.Length; i++)
            {
                if (_previousHash[i] != currentHash[i])
                {
                    return false;
                }
            }

            return true;
        }

        private void ComputeHash(IncrementalHash sha256, string path)
        {
            var byteCount = Encoding.UTF8.GetByteCount(path);
            if (_fileNameBuffer == null || byteCount > _fileNameBuffer.Length)
            {
                _fileNameBuffer = new byte[Math.Max(byteCount, 256)];
            }

            var length = Encoding.UTF8.GetBytes(path, 0, path.Length, _fileNameBuffer, 0);
            sha256.AppendData(_fileNameBuffer, 0, length);
        }

        /// <inheritdoc />
        public IDisposable RegisterChangeCallback(Action<object> callback, object state) => EmptyDisposable.Instance;
    }
}
