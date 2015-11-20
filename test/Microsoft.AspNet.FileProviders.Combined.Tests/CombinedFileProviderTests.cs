// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.AspNet.FileProviders.Combined.Tests.TestUtility;
using Xunit;

namespace Microsoft.AspNet.FileProviders.Embedded.Tests
{
    public class CombinedFileProviderTests
    {
        [Fact]
        public void GetFileInfo_ReturnsNotFoundFileInfo_IfNoFileProviderSpecified()
        {
            // Arrange
            var provider = new CombinedFileProvider();

            // Act
            var fileInfo = provider.GetFileInfo("DoesNotExist.txt");

            // Assert
            Assert.NotNull(fileInfo);
            Assert.False(fileInfo.Exists);
        }

        [Fact]
        public void GetFileInfo_ReturnsNotFoundFileInfo_IfFileDoesNotExist()
        {
            // Arrange
            var provider = new CombinedFileProvider(new MockFileProvider(new MockFileInfo("DoesExist.txt")));

            // Act
            var fileInfo = provider.GetFileInfo("DoesNotExist.txt");

            // Assert
            Assert.NotNull(fileInfo);
            Assert.False(fileInfo.Exists);
        }

        [Fact]
        public void GetFileInfo_ReturnsTheFirstFoundFileInfo()
        {
            // Arrange
            var fileName = "File1";
            var expectedFileInfo = new MockFileInfo(fileName);
            var provider = new CombinedFileProvider(
                new MockFileProvider(
                    new MockFileInfo("FileA"),
                    new MockFileInfo("FileB")
                    ),
                new MockFileProvider(
                    expectedFileInfo,
                    new MockFileInfo("File2")
                    ),
                new MockFileProvider(
                    new MockFileInfo(fileName),
                    new MockFileInfo("File3")
                    )
                );

            // Act
            var fileInfo = provider.GetFileInfo(fileName);

            // Assert
            Assert.NotNull(fileInfo);
            Assert.True(fileInfo.Exists);
            Assert.Equal(expectedFileInfo, fileInfo);
        }

        [Fact]
        public void GetDirectoryContents_ReturnsNonExistingEmptySequence_IfNoFileProviderSpecified()
        {
            // Arrange
            var provider = new CombinedFileProvider();

            // Act
            var files = provider.GetDirectoryContents(string.Empty);

            // Assert
            Assert.NotNull(files);
            Assert.False(files.Exists);
            Assert.Empty(files);
        }

        [Fact]
        public void GetDirectoryContents_ReturnsNonExistingEmptySequence_IfResourcesDoNotExist()
        {
            // Arrange
            var provider = new CombinedFileProvider();

            // Act
            var files = provider.GetDirectoryContents("DoesNotExist");

            // Assert
            Assert.NotNull(files);
            Assert.False(files.Exists);
            Assert.Empty(files);
        }

        [Fact]
        public void GetDirectoryContents_ReturnsCombinaisionOFFiles()
        {
            // Arrange
            IFileInfo file1 = new MockFileInfo("File1"),
                file2 = new MockFileInfo("File2"),
                file2Bis = new MockFileInfo("File2"),
                file3 = new MockFileInfo("File3");
            var provider = new CombinedFileProvider(
                new MockFileProvider(
                    file1,
                    file2
                    ),
                new MockFileProvider(
                    file2Bis,
                    file3
                    )
                );

            // Act
            var files = provider.GetDirectoryContents(string.Empty);

            // Assert
            Assert.NotNull(files);
            Assert.True(files.Exists);
            Assert.Collection(files.OrderBy(f => f.Name, StringComparer.Ordinal),
                file => Assert.Equal(file1, file),
                file => Assert.Equal(file2, file),
                file => Assert.Equal(file3, file));
        }

        [Fact]
        public void GetDirectoryContents_ReturnsCombinaisionOFFiles_WhenSomeFileProviderRetunsNoContent()
        {
            // Arrange
            IFileInfo folderAFile1 = new MockFileInfo("FolderA/File1"),
                folderAFile2 = new MockFileInfo("FolderA/File2"),
                folderAFile2Bis = new MockFileInfo("FolderA/File2"),
                folderBFile1 = new MockFileInfo("FolderB/File1"),
                folderBFile2 = new MockFileInfo("FolderB/File2"),
                folderCFile3 = new MockFileInfo("FolderC/File3");
            var provider = new CombinedFileProvider(
                new MockFileProvider(
                    folderAFile1,
                    folderAFile2,
                    folderBFile2
                    ),
                new MockFileProvider(
                    folderAFile2Bis,
                    folderBFile1,
                    folderCFile3
                    )
                );

            // Act
            var files = provider.GetDirectoryContents("FolderC/");

            // Assert
            Assert.NotNull(files);
            Assert.True(files.Exists);
            Assert.Collection(files.OrderBy(f => f.Name, StringComparer.Ordinal),
                file => Assert.Equal(folderCFile3, file));
        }
    }
}