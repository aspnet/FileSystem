// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.FileProviders.Embedded;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Extensions.FileProviders
{
    /// <summary>
    /// Looks up files using embedded resources in the specified assembly.
    /// This file provider is case sensitive.
    /// </summary>
    public class EmbeddedFileProvider : IFileProvider
    {
        private static readonly char[] _invalidFileNameChars = Path.GetInvalidFileNameChars()
            .Where(c => c != '/' && c != '\\').ToArray();
        private readonly Assembly _assembly;
        private readonly string _baseNamespace;
        private readonly DateTimeOffset _lastModified;
        private readonly Dictionary<string, IFileInfo> _allEntries;         // Values may be 'null' if information must be dynamically calculated
        private readonly Dictionary<string, List<IFileInfo>> _directories;  // Values are directory children

        /// <summary>
        /// Initializes a new instance of the <see cref="EmbeddedFileProvider" /> class using the specified
        /// assembly and empty base namespace.
        /// </summary>
        /// <param name="assembly"></param>
        public EmbeddedFileProvider(Assembly assembly)
            : this(assembly, assembly?.GetName()?.Name)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EmbeddedFileProvider" /> class using the specified
        /// assembly and base namespace.
        /// </summary>
        /// <param name="assembly">The assembly that contains the embedded resources.</param>
        /// <param name="baseNamespace">The base namespace that contains the embedded resources.</param>
        public EmbeddedFileProvider(Assembly assembly, string baseNamespace)
            : this(assembly, baseNamespace, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EmbeddedFileProvider" /> class using the specified
        /// assembly and base namespace.
        /// </summary>
        /// <param name="assembly">The assembly that contains the embedded resources.</param>
        /// <param name="resourcePathSplitter">Function that takes the resource path and boolean (False for a file, True for a directory), and returns a <see cref="SplitResourcePath" /> from it.</param>
        public EmbeddedFileProvider(Assembly assembly, Func<string, bool, SplitResourcePath> resourcePathSplitter)
            : this(assembly, assembly?.GetName()?.Name, resourcePathSplitter)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EmbeddedFileProvider" /> class using the specified
        /// assembly and base namespace.
        /// </summary>
        /// <param name="assembly">The assembly that contains the embedded resources.</param>
        /// <param name="baseNamespace">The base namespace that contains the embedded resources.</param>
        /// <param name="resourcePathSplitter">Function that takes the resource path and boolean (False for a file, True for a directory), and returns a <see cref="SplitResourcePath" /> from it.</param>
        public EmbeddedFileProvider(Assembly assembly, string baseNamespace, Func<string, bool, SplitResourcePath> resourcePathSplitter)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException("assembly");
            }

            _baseNamespace = string.IsNullOrEmpty(baseNamespace) ? string.Empty : baseNamespace + ".";
            _assembly = assembly;

            _lastModified = DateTimeOffset.UtcNow;


            // need to keep netstandard1.0 until ASP.NET Core 2.0 because it is a breaking change if we remove it
#if NETSTANDARD1_5 || NET451
            if (!string.IsNullOrEmpty(_assembly.Location))
            {
                try
                {
                    _lastModified = File.GetLastWriteTimeUtc(_assembly.Location);
                }
                catch (PathTooLongException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
#endif

            _allEntries = new Dictionary<string, IFileInfo>();
            _directories = new Dictionary<string, List<IFileInfo>>();

            LoadRoot();
            LoadFiles(resourcePathSplitter);
            LoadDirectories(resourcePathSplitter);
        }

        /// <summary>
        /// Locates a file at the given path.
        /// </summary>
        /// <param name="subpath">The path that identifies the file. </param>
        /// <returns>The file information. Caller must check Exists property.</returns>
        public IFileInfo GetFileInfo(string subpath)
        {
            if (string.IsNullOrEmpty(subpath))
            {
                return new NotFoundFileInfo(subpath);
            }

            var resourcePath = PrepareSubpath(subpath);
            if (HasInvalidPathChars(resourcePath))
            {
                return new NotFoundFileInfo(resourcePath);
            }

            IFileInfo fileInfo = null;
            if (_allEntries.TryGetValue(resourcePath, out fileInfo) && fileInfo == null)
            {
                // If there is no 'splitter', the file name is calculated dynamically because a same
                // resource file "foo.bar" will have different names, depending on the sub-path:
                // GetFileInfo("/foo/bar") --> "bar"
                // GetFileInfo("/foo.bar") --> "foo.bar"
                var fullResourcePath = _baseNamespace + resourcePath;
                var name = Path.GetFileName(subpath);
                fileInfo = new EmbeddedResourceFileInfo(_assembly, fullResourcePath, name, _lastModified);
            }

            if (fileInfo == null)
            {
                var name = Path.GetFileName(subpath);
                return new NotFoundFileInfo(name);
            }

            return fileInfo;
        }

        /// <summary>
        /// Enumerate a directory at the given path, if any.
        /// This file provider uses a flat directory structure. Everything under the base namespace is considered to be one directory.
        /// </summary>
        /// <param name="subpath">The path that identifies the directory</param>
        /// <returns>Contents of the directory. Caller must check Exists property.</returns>
        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            // The file name is assumed to be the remainder of the resource name.
            if (subpath == null)
            {
                return new NotFoundDirectoryContents();
            }

            var resourcePath = PrepareSubpath(subpath);
            if (HasInvalidPathChars(resourcePath))
            {
                return new NotFoundDirectoryContents();
            }

            List<IFileInfo> entries;
            if (!_directories.TryGetValue(resourcePath, out entries))
            {
                return new NotFoundDirectoryContents();
            }

            return new EnumerableDirectoryContents(entries);
        }

        public IChangeToken Watch(string pattern)
        {
            return NullChangeToken.Singleton;
        }

        private static bool HasInvalidPathChars(string path)
        {
            return path.IndexOfAny(_invalidFileNameChars) != -1;
        }

        private static string PrepareSubpath(string subpath)
        {
            var builder = new StringBuilder(subpath.Length);

            // Relative paths starting with a leading slash okay
            if (subpath.StartsWith("/", StringComparison.Ordinal))
            {
                builder.Append(subpath, 1, subpath.Length - 1);
            }
            else
            {
                builder.Append(subpath);
            }

            for (var i = 0; i < builder.Length; i++)
            {
                if (builder[i] == '/' || builder[i] == '\\')
                {
                    builder[i] = '.';
                }
            }

            return builder.ToString();
        }

        private void LoadRoot()
        {
            // The root directory must never fail, so load the minimum...
            _directories.Add(string.Empty, new List<IFileInfo>());
            _allEntries.Add(string.Empty, new EmbeddedResourceDirectoryInfo(string.Empty, _lastModified));
        }

        private void LoadFiles(Func<string, bool, SplitResourcePath> resourcePathSplitter)
        {
            var resources = _assembly.GetManifestResourceNames();
            if (resources != null)
            {
                for (int i = 0; i < resources.Length; i++)
                {
                    if (resources[i].StartsWith(_baseNamespace, StringComparison.Ordinal))
                    {
                        var relativeResourceName = resources[i].Substring(_baseNamespace.Length);
                        string fileName;
                        string parentPath;
                        string fullPath;
                        bool dynamicFileInfo;

                        if (resourcePathSplitter != null)
                        {
                            var split = resourcePathSplitter(relativeResourceName, false);
                            if (string.IsNullOrEmpty(split.Name))
                            {
                                throw new ArgumentNullException(nameof(split.Name));
                            }
                            fileName = split.Name;

                            dynamicFileInfo = false;

                            if (!string.IsNullOrEmpty(split.ParentDirectory))
                            {
                                parentPath = split.ParentDirectory;
                                fullPath = split.ParentDirectory + '.' + split.Name;
                            }
                            else
                            {
                                parentPath = string.Empty;
                                fullPath = split.Name;
                            }
                        }
                        else
                        {
                            fileName = relativeResourceName;
                            parentPath = string.Empty;
                            fullPath = relativeResourceName;
                            dynamicFileInfo = true;
                        }

                        var fileInfo = new EmbeddedResourceFileInfo(
                            _assembly,
                            resources[i],
                            fileName,
                            _lastModified);

                        AddEntry(fullPath, (!dynamicFileInfo) ? fileInfo : null);
                        AddDirectoryChild(parentPath, fileInfo);
                    }
                }
            }
        }

        private void AddEntry(string entryPath, IFileInfo entry)
        {
            IFileInfo existing;
            if (!_allEntries.TryGetValue(entryPath, out existing))
            {
                _allEntries.Add(entryPath, entry);
            }
        }

        private void AddDirectoryChild(string directory, IFileInfo child)
        {
            List<IFileInfo> children;
            if (!_directories.TryGetValue(directory, out children))
            {
                children = new List<IFileInfo>();
                _directories.Add(directory, children);
            }

            if (!children.Exists(e => e.Name == child.Name))
            {
                children.Add(child);
            }
        }

        private void LoadDirectories(Func<string, bool, SplitResourcePath> resourcePathSplitter)
        {
            if (resourcePathSplitter != null)
            {
                var toAnalyze = _directories.Keys.ToArray();
                for (int i = 0; i < toAnalyze.Length; i++)
                {
                    string path = toAnalyze[i];
                    SplitResourcePath split;

                    do
                    {
                        split = resourcePathSplitter(path, true);

                        if (!string.IsNullOrEmpty(split.Name))
                        {
                            var parent = split.ParentDirectory ?? string.Empty;
                            var fileInfo = new EmbeddedResourceDirectoryInfo(split.Name, _lastModified);

                            AddEntry(path, fileInfo);
                            AddDirectoryChild(parent, fileInfo);
                        }

                        path = split.ParentDirectory;
                    }
                    while (!string.IsNullOrEmpty(path));
                }
            }
        }
    }
}
