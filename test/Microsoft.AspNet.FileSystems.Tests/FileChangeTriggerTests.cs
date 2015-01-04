// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace Microsoft.AspNet.FileSystems
{
    public class FileChangeTriggerTests
    {
        [Fact]
        public void IsExpired_ReturnsFalse_IfCancellationTokenSourceIsNotExpired_AndFileIsUnchanged()
        {
            // Arrange
            var path = "my/path/tofile.txt";
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(f => f.GetFileInfo(path))
                      .Returns(new NotFoundFileInfo(path))
                      .Verifiable();
            var trigger = new TestFileChangeTrigger(fileSystem.Object, path);

            // Act - 1
            trigger.AddTime(TimeSpan.FromSeconds(1));
            var result = trigger.IsExpired;

            // Assert - 1
            Assert.False(result);
            fileSystem.Verify(f => f.GetFileInfo(path), Times.Once());

            // Act - 2
            trigger.AddTime(TimeSpan.FromSeconds(5));
            result = trigger.IsExpired;

            // Assert - 1
            Assert.False(result);
            fileSystem.Verify(f => f.GetFileInfo(path), Times.Exactly(2));
        }

        [Fact]
        public async Task IsExpired_ReturnsTrue_IfCancellationTokenSourceIsExpired()
        {
            // Arrange
            var path = "my/path/tofile.txt";
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(f => f.GetFileInfo(path))
                      .Returns(new NotFoundFileInfo(path))
                      .Verifiable();
            var trigger = new TestFileChangeTrigger(fileSystem.Object, path);
            await trigger.Changed();

            // Act
            var result = trigger.IsExpired;

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsExpired_ReturnsTrue_IfFileHasChangedButCancellationTokenHasNotFired()
        {
            // Arrange
            var path = "my/path/tofile.txt";
            var newFile = new Mock<IFileInfo>();
            newFile.SetupGet(f => f.LastModified)
                   .Returns(DateTimeOffset.UtcNow.AddSeconds(10));
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(f => f.GetFileInfo(path))
                      .Returns(new NotFoundFileInfo(path))
                      .Verifiable();
            var trigger = new TestFileChangeTrigger(fileSystem.Object, path);

            // Act - 1
            var result = trigger.IsExpired;

            // Assert - 1
            Assert.False(result);

            // Act - 2
            fileSystem.Setup(f => f.GetFileInfo(path))
                      .Returns(newFile.Object)
                      .Verifiable();
            trigger.AddTime(TimeSpan.FromSeconds(4));
            result = trigger.IsExpired;

            // Assert - 2
            Assert.True(result);
        }

        private class TestFileChangeTrigger : FileChangeTrigger
        {
            private DateTime _utcNow = DateTime.UtcNow;

            public TestFileChangeTrigger(IFileSystem fileSystem, string path) 
                : base(fileSystem, path)
            {
            }

            protected override DateTime UtcNow
            {
                get { return _utcNow; }
            }

            public void AddTime(TimeSpan timeSpan)
            {
                _utcNow = _utcNow.Add(timeSpan);
            }
        }
    }
}