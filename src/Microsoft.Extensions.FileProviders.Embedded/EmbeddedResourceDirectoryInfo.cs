// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace Microsoft.Extensions.FileProviders.Embedded
{
    public class EmbeddedResourceDirectoryInfo : IFileInfo
    {
        public EmbeddedResourceDirectoryInfo(string name, DateTimeOffset lastModified)
        {
            Name = name;
            LastModified = lastModified;
        }

        public bool Exists => true;

        public long Length => -1;

        public string PhysicalPath => null;

        public string Name { get; }

        public DateTimeOffset LastModified { get; }

        public bool IsDirectory => true;

        public Stream CreateReadStream()
        {
            throw new InvalidOperationException("Cannot create a stream for a directory.");
        }
    }
}
