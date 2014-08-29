using System;
using System.IO;
using NuGet;
using Xunit;

namespace Microsoft.Framework.Runtime.Tests
{
    public class PathUtilityFacts
    {
        [Fact]
        public void IsChildOfDirectoryWorksWithRelativePath()
        {
            var baseDir = Path.Combine("..", "BaseDir");
            var childPath = Path.Combine("..", "BaseDir", "ChildFile");
            var nonChildPath = Path.Combine("..", "AnotherBaseDir", "NonChildFile");

            Assert.True(PathUtility.IsChildOfDirectory(baseDir, childPath));
            Assert.False(PathUtility.IsChildOfDirectory(baseDir, nonChildPath));
        }

        [Fact]
        public void IsChildOfDirectoryWorksWithAbsolutePath()
        {
            var baseDir = Path.GetFullPath("BaseDir");
            var childPath = Path.GetFullPath(Path.Combine("BaseDir", "ChildFile"));
            var nonChildPath = Path.GetFullPath(Path.Combine("AnotherBaseDir", "NonChildFile"));

            Assert.True(PathUtility.IsChildOfDirectory(baseDir, childPath));
            Assert.False(PathUtility.IsChildOfDirectory(baseDir, nonChildPath));
        }

        [Fact]
        public void IsChildOfDirectoryWorksOnBaseDirWithoutTrailingPathSeparator()
        {
            var baseDir = Path.Combine("..", "foo");
            var childPath = Path.Combine("..", "foo", "ChildFile");
            var nonChildPath = Path.Combine("..", "food", "NonChildFile");

            Assert.True(PathUtility.IsChildOfDirectory(baseDir, childPath));
            Assert.False(PathUtility.IsChildOfDirectory(baseDir, nonChildPath));
        }
    }
}