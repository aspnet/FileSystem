// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Extensions.FileProviders.Physical
{
    /// <summary>
    ///     <para>
    ///     A change token that polls for file system changes.
    ///     </para>
    ///     <para>
    ///     This change token does not raise any change callbacks. Callers should watch for <see cref="HasChanged" /> to turn
    ///     from false to true
    ///     and dispose the token after this happens.
    ///     </para>
    /// </summary>
    /// <remarks>
    /// Polling occurs every 4 seconds.
    /// </remarks>
    public class PollingFileChangeToken : IPollingChangeToken
    {
        private readonly FileInfo _fileInfo;
        private DateTime _initialWriteTimeUtc;
        private CancellationChangeToken _changeToken;

        /// <summary>
        /// Initializes a new instance of <see cref="PollingFileChangeToken" /> that polls the specified file for changes as
        /// determined by <see cref="System.IO.FileSystemInfo.LastWriteTimeUtc" />.
        /// </summary>
        /// <param name="fileInfo">The <see cref="System.IO.FileInfo"/> to poll</param>
        /// <param name="cancellationTokenSource">The <see cref="System.Threading.CancellationTokenSource"/>.</param>
        public PollingFileChangeToken(FileInfo fileInfo, CancellationTokenSource cancellationTokenSource)
        {
            _fileInfo = fileInfo;
            CancellationTokenSource = cancellationTokenSource;
            _changeToken = new CancellationChangeToken(cancellationTokenSource.Token);

            _initialWriteTimeUtc = GetLastWriteTimeUtc();
        }

        private DateTime GetLastWriteTimeUtc()
        {
            _fileInfo.Refresh();
            return _fileInfo.Exists ? _fileInfo.LastWriteTimeUtc : DateTime.MinValue;
        }

        /// <inheritdoc />
        public bool ActiveChangeCallbacks => true;

        /// <summary>
        /// Gets the <see cref="System.Threading.CancellationTokenSource"/>.
        /// </summary>
        public CancellationTokenSource CancellationTokenSource { get; }

        /// <summary>
        /// True when the file has changed since the change token was created. Once the file changes, this value is always true
        /// </summary>
        /// <remarks>
        /// Once true, the value will always be true. Change tokens should not re-used once expired. The caller should discard this
        /// instance once it sees <see cref="HasChanged" /> is true.
        /// </remarks>
        public bool HasChanged { get; private set; }

        /// <summary>
        /// Updates <see cref="HasChanged"/>.
        /// </summary>
        /// <returns>The updated value of <see cref="HasChanged"/>.</returns>
        public bool UpdateHasChanged()
        {
            HasChanged |= CalculateChange();
            return HasChanged;
        }

        private bool CalculateChange()
        {
            var lastWriteTimeUtc = GetLastWriteTimeUtc();
            return _initialWriteTimeUtc != lastWriteTimeUtc;
        }

        /// <inheritdoc />
        public IDisposable RegisterChangeCallback(Action<object> callback, object state)
        {
            return _changeToken.RegisterChangeCallback(callback, state);
        }
    }
}