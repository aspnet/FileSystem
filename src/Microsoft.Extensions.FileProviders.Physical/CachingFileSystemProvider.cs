using Microsoft.CodeAnalysis.InternalUtilities;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Extensions.FileProviders.Physical
{
    public class CachingFileProvider: IFileProvider
    {
        private readonly IFileProvider _baseProvider;
        private readonly ConcurrentLruCache<string, IFileInfo> _fileCache;
        private readonly ConcurrentLruCache<string, IDirectoryContents> _directoryCache;

        public CachingFileProvider(IFileProvider baseProvider, int fileCapacity, int directoryCapacity)
        {
            _baseProvider = baseProvider;
            _fileCache = new ConcurrentLruCache<string, IFileInfo>(fileCapacity);
            _directoryCache = new ConcurrentLruCache<string, IDirectoryContents>(directoryCapacity);
            _baseProvider.Watch("/*").RegisterChangeCallback(Invalidate, null);
        }

        private void Invalidate(object obj)
        {
            _fileCache.Clear();
            _directoryCache.Clear();
        }

        public IFileInfo GetFileInfo(string subpath)
        {
            return _fileCache.GetOrAdd(subpath, subpath, _baseProvider.GetFileInfo);
        }

        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            return _directoryCache.GetOrAdd(subpath, subpath, _baseProvider.GetDirectoryContents);
        }

        public IChangeToken Watch(string filter)
        {
            return _baseProvider.Watch(filter);
        }
    }
}