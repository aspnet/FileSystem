using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Microsoft.Extensions.FileProviders.Physical.Tests
{
    public class CachingFileProviderTests
    {
        [Fact]
        public void GetFileInfo_ReturnsCachedFileInfoForSecondCall()
        {
            var provider = new CachingFileProvider(new MockFileProvider(), 10, 10, "/*");

            var fileInfo1 = provider.GetFileInfo("File.txt");
            var fileInfo2 = provider.GetFileInfo("File.txt");

            Assert.Equal(fileInfo1, fileInfo2);
        }

        [Fact]
        public void GetDirectoryContents_ReturnsCachedDirectoryContentsForSecondCall()
        {
            var provider = new CachingFileProvider(new MockFileProvider(), 10, 10, "/*");

            var directoryContents1 = provider.GetDirectoryContents("Dir");
            var directoryContents2 = provider.GetDirectoryContents("Dir");

            Assert.Same(directoryContents1, directoryContents2);
        }

        [Fact]
        public void GetFileInfo_IsCaseSensetive()
        {
            var provider = new CachingFileProvider(new MockFileProvider(), 10, 10, "/*");

            var fileInfo1 = provider.GetFileInfo("File.txt");
            var fileInfo2 = provider.GetFileInfo("file.txt");

            Assert.NotEqual(fileInfo1, fileInfo2);
        }

        [Fact]
        public void GetDirectoryContents_IsCaseSensetive()
        {
            var provider = new CachingFileProvider(new MockFileProvider(), 10, 10, "/*");

            var directoryContents1 = provider.GetDirectoryContents("Dir");
            var directoryContents2 = provider.GetDirectoryContents("dir");

            Assert.NotSame(directoryContents1, directoryContents2);
        }

        [Fact]
        public void CachingFileProvider_PassesFilterToBaseProvider()
        {
            var mockFileProvider = new MockFileProvider();
            var provider = new CachingFileProvider(mockFileProvider, 10, 10, "/*");

            Assert.Equal("/*", mockFileProvider.LastToken.Filter);
        }

        [Fact]
        public void GetFileInfo_CacheIsClearedWhenTokenFires()
        {
            var mockFileProvider = new MockFileProvider();
            var provider = new CachingFileProvider(mockFileProvider, 10, 10, "/*");

            var fileInfo1 = provider.GetFileInfo("File.txt");
            mockFileProvider.LastToken.Fire();
            var fileInfo2 = provider.GetFileInfo("File.txt");

            Assert.NotEqual(fileInfo1, fileInfo2);
        }

        [Fact]
        public void GetDirectoryContents_CacheIsClearedWhenTokenFires()
        {
            var mockFileProvider = new MockFileProvider();
            var provider = new CachingFileProvider(mockFileProvider, 10, 10, "/*");

            var directoryContents1 = provider.GetDirectoryContents("Dir");
            mockFileProvider.LastToken.Fire();
            var directoryContents2 = provider.GetDirectoryContents("Dir");

            Assert.NotSame(directoryContents1, directoryContents2);
        }

        [Fact]
        public void CachingFileProvider_WithNullFilterDoesNotWatch()
        {
            var mockFileProvider = new MockFileProvider();
            var provider = new CachingFileProvider(mockFileProvider, 10, 10, null);

            var fileInfo1 = provider.GetFileInfo("File.txt");
            mockFileProvider.LastToken?.Fire();
            var fileInfo2 = provider.GetFileInfo("File.txt");

            Assert.Equal(fileInfo1, fileInfo2);
            Assert.Null(mockFileProvider.LastToken);
        }

        private class MockFileProvider: IFileProvider
        {
            public MockChangeToken LastToken { get; private set; }

            public IFileInfo GetFileInfo(string subpath)
            {
                return new MockFileInfo();
            }

            public IDirectoryContents GetDirectoryContents(string subpath)
            {
                return new MockDirectoryInfo();
            }

            public IChangeToken Watch(string filter)
            {
                return LastToken = new MockChangeToken(filter);
            }
        }

        private class MockChangeToken : IChangeToken
        {
            public string Filter { get; }

            public MockChangeToken(string filter)
            {
                Filter = filter;
            }

            private List<Action<object>> _callbacks = new List<Action<object>>();

            public IDisposable RegisterChangeCallback(Action<object> callback, object state)
            {
                _callbacks.Add(callback);
                return null;
            }

            public void Fire()
            {
                foreach (var callback in _callbacks)
                {
                    callback(null);
                }
            }

            public bool HasChanged { get; }
            public bool ActiveChangeCallbacks { get; }
        }

        private class MockDirectoryInfo : IDirectoryContents
        {
            public IEnumerator<IFileInfo> GetEnumerator()
            {
                throw new NotImplementedException();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public bool Exists { get; }
        }
        private class MockFileInfo : IFileInfo
        {
            public bool Exists { get; }
            public long Length { get; }
            public string PhysicalPath { get; }
            public string Name { get; }
            public DateTimeOffset LastModified { get; }
            public bool IsDirectory { get; }
            public Stream CreateReadStream()
            {
                return null;
            }
        }
    }
}
