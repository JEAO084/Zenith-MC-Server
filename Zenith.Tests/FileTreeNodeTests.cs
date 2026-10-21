using System.IO;
using Xunit;
using Zenith.Core.Models;

namespace Zenith.Tests
{
    public class FileTreeNodeTests
    {
        [Fact]
        public void BuildTree_PopulatesRelativePathAndChildrenCorrectly()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "ZenithTestTree_" + System.Guid.NewGuid());
            Directory.CreateDirectory(tempDir);
            string subDir = Path.Combine(tempDir, "subfolder");
            Directory.CreateDirectory(subDir);
            string filePath = Path.Combine(subDir, "test.txt");
            File.WriteAllText(filePath, "hello world");

            try
            {
                var root = FileTreeNode.BuildTree(tempDir);
                Assert.NotNull(root);
                Assert.True(root.IsDirectory);
                Assert.Single(root.Children);

                var childDir = root.Children[0];
                Assert.Equal("subfolder", childDir.Name);
                Assert.Equal("subfolder", childDir.RelativePath);
                Assert.Single(childDir.Children);

                var childFile = childDir.Children[0];
                Assert.Equal("test.txt", childFile.Name);
                Assert.Equal(Path.Combine("subfolder", "test.txt"), childFile.RelativePath);
                Assert.False(childFile.IsDirectory);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
    }
}
