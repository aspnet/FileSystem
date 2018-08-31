// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Xunit;

namespace Microsoft.Extensions.FileProviders.Physical
{
    public class PollingFileWatcherTest
    {
        private static readonly TimeSpan NeverStartTimer = TimeSpan.FromMilliseconds(-1);

        [Fact]
        public void GetOrAddFileChangeToken_WithFilePath_CreatesPollingFileChangeToken()
        {
            using (var fileSystem = new DisposableFileSystem())
            using (var fileWatcher = new PollingFileWatcher(fileSystem.RootPath, NeverStartTimer))
            {
                var changeToken = fileWatcher.GetOrAddChangeToken("test.txt");
                Assert.IsType<PollingFileChangeToken>(changeToken);
                Assert.False(changeToken.HasChanged);
            }
        }

        [Fact]
        public void GetOrAddFileChangeToken_WithGlobbingPattern_CreatesPollingWildCardChangeToken()
        {
            using (var fileSystem = new DisposableFileSystem())
            using (var fileWatcher = new PollingFileWatcher(fileSystem.RootPath, NeverStartTimer))
            {
                var changeToken = fileWatcher.GetOrAddChangeToken("*/*.txt");
                Assert.IsType<PollingWildCardChangeToken>(changeToken);
                Assert.False(changeToken.HasChanged);
            }
        }

        [Fact]
        public void RaiseChangeEvents_CancelsCancellationTokenSourceForExpiredTokens()
        {
            // Arrange
            var cts1 = new CancellationTokenSource();
            var cts2 = new CancellationTokenSource();
            var cts3 = new CancellationTokenSource();

            var token1 = new TestPollingChangeToken { Id = "1", CancellationTokenSource = cts1 };
            var token2 = new TestPollingChangeToken { Id = "2", HasChanged = true, CancellationTokenSource = cts2 };
            var token3 = new TestPollingChangeToken { Id = "3", CancellationTokenSource = cts3 };

            var tokens = CreateDictionary(token1, token2, token3);

            // Act
            PollingFileWatcher.RaiseChangeEvents(tokens);

            // Assert
            Assert.False(cts1.IsCancellationRequested);
            Assert.False(cts3.IsCancellationRequested);
            Assert.True(cts2.IsCancellationRequested);

            // Ensure token2 is removed from the collection.
            Assert.Equal(new[] { token1, token3, }, tokens.OrderBy(t => t.Key).Select(t => t.Value));
        }

        [Fact]
        public void RaiseChangeEvents_CancelsAndRemovesMultipleChangedTokens()
        {
            // Arrange
            var cts1 = new CancellationTokenSource();
            var cts2 = new CancellationTokenSource();
            var cts3 = new CancellationTokenSource();
            var cts4 = new CancellationTokenSource();
            var cts5 = new CancellationTokenSource();

            var token1 = new TestPollingChangeToken { Id = "1", HasChanged = true, CancellationTokenSource = cts1 };
            var token2 = new TestPollingChangeToken { Id = "2", CancellationTokenSource = cts2 };
            var token3 = new TestPollingChangeToken { Id = "3", CancellationTokenSource = cts3 };
            var token4 = new TestPollingChangeToken { Id = "4", HasChanged = true, CancellationTokenSource = cts4 };
            var token5 = new TestPollingChangeToken { Id = "5", HasChanged = true, CancellationTokenSource = cts5 };

            var tokens = CreateDictionary(token1, token2, token3, token4, token5);

            // Act
            PollingFileWatcher.RaiseChangeEvents(tokens);

            // Assert
            Assert.False(cts2.IsCancellationRequested);
            Assert.False(cts3.IsCancellationRequested);

            Assert.True(cts1.IsCancellationRequested);
            Assert.True(cts4.IsCancellationRequested);
            Assert.True(cts5.IsCancellationRequested);

            // Ensure changed tokens are removed
            Assert.Equal(new[] { token2, token3, }, tokens.OrderBy(t => t.Key).Select(t => t.Value));
        }

        private static ConcurrentDictionary<string, IPollingChangeToken> CreateDictionary(params TestPollingChangeToken[] tokens)
        {
            return new ConcurrentDictionary<string, IPollingChangeToken>(tokens.ToDictionary(t => t.Id, t => (IPollingChangeToken)t));
        }

        private class TestPollingChangeToken : IPollingChangeToken
        {
            public string Id { get; set; }

            public CancellationTokenSource CancellationTokenSource { get; set; }

            public bool HasChanged { get; set; }

            public bool ActiveChangeCallbacks => throw new NotImplementedException();

            public IDisposable RegisterChangeCallback(Action<object> callback, object state)
            {
                throw new NotImplementedException();
            }

            public bool UpdateHasChanged() => HasChanged;
        }
    }
}
