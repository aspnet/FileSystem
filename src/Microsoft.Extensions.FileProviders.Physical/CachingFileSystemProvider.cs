using System;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Extensions.FileProviders.Physical
{
    public class CachingFileProvider: IFileProvider, IDisposable
    {
        private readonly IFileProvider _baseProvider;
        private readonly ConcurrentLruCache<string, IFileInfo> _fileCache;
        private readonly ConcurrentLruCache<string, IDirectoryContents> _directoryCache;
        private Func<string, IFileInfo> _getFileInfo;
        private Func<string, IDirectoryContents> _getDirectoryContents;
        private IDisposable _changeCallbackRegistration;

        public CachingFileProvider(IFileProvider baseProvider, int fileCapacity, int directoryCapacity, string watchFilter)
        {
            _baseProvider = baseProvider;
            _fileCache = new ConcurrentLruCache<string, IFileInfo>(fileCapacity);
            _directoryCache = new ConcurrentLruCache<string, IDirectoryContents>(directoryCapacity);
            if (watchFilter != null)
            {
                _changeCallbackRegistration = _baseProvider.Watch(watchFilter).RegisterChangeCallback(Invalidate, null);
            }
            _getDirectoryContents = _baseProvider.GetDirectoryContents;
            _getFileInfo = _baseProvider.GetFileInfo;
        }

        private void Invalidate(object obj)
        {
            _fileCache.Clear();
            _directoryCache.Clear();
        }

        public IFileInfo GetFileInfo(string subpath)
        {
            return _fileCache.GetOrAdd(subpath, subpath, _getFileInfo);
        }

        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            return _directoryCache.GetOrAdd(subpath, subpath, _getDirectoryContents);
        }

        public IChangeToken Watch(string filter)
        {
            return _baseProvider.Watch(filter);
        }

        public virtual void Dispose()
        {
            _changeCallbackRegistration?.Dispose();
        }
    }
}