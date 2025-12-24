using LoraDbEditor.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LoraDbEditor.Tests.Services
{
    [TestClass]
    public class TreeViewManagerTests
    {
        private TreeViewManager _manager = null!;

        [TestInitialize]
        public void Setup()
        {
            _manager = new TreeViewManager();
        }

        [TestMethod]
        public void BuildTreeView_WithEmptyList_ReturnsEmptyCollection()
        {
            // Arrange
            var filePaths = new List<string>();
            var basePath = @"C:\TestPath";

            // Act
            var result = _manager.BuildTreeView(filePaths, basePath);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void BuildTreeView_WithSingleFile_CreatesOneNode()
        {
            // Arrange
            var filePaths = new List<string> { "test-file" };
            var basePath = @"C:\TestPath";

            // Act
            var result = _manager.BuildTreeView(filePaths, basePath);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("test-file", result[0].Name);
            Assert.IsTrue(result[0].IsFile);
        }

        [TestMethod]
        public void BuildTreeView_WithNestedPaths_CreatesCorrectHierarchy()
        {
            // Arrange
            var filePaths = new List<string>
            {
                "folder1/file1",
                "folder1/file2",
                "folder2/subfolder/file3"
            };
            var basePath = @"C:\TestPath";

            // Act
            var result = _manager.BuildTreeView(filePaths, basePath);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count); // folder1 and folder2

            var folder1 = result.FirstOrDefault(n => n.Name == "folder1");
            Assert.IsNotNull(folder1);
            Assert.IsFalse(folder1.IsFile);
            Assert.AreEqual(2, folder1.Children.Count);

            var folder2 = result.FirstOrDefault(n => n.Name == "folder2");
            Assert.IsNotNull(folder2);
            Assert.IsFalse(folder2.IsFile);
            Assert.AreEqual(1, folder2.Children.Count);

            var subfolder = folder2.Children.FirstOrDefault(n => n.Name == "subfolder");
            Assert.IsNotNull(subfolder);
            Assert.AreEqual(1, subfolder.Children.Count);
        }

        [TestMethod]
        public void SortTreeNodes_PlacesFoldersBeforeFiles()
        {
            // Arrange
            var filePaths = new List<string>
            {
                "file1",
                "folder1/file2",
                "file3"
            };
            var basePath = @"C:\TestPath";

            // Act
            var result = _manager.BuildTreeView(filePaths, basePath);

            // Assert - folders should come before files
            Assert.IsNotNull(result);
            var folder = result.FirstOrDefault(n => !n.IsFile);
            var firstFile = result.FirstOrDefault(n => n.IsFile);

            if (folder != null && firstFile != null)
            {
                var folderIndex = result.IndexOf(folder);
                var fileIndex = result.IndexOf(firstFile);
                Assert.IsTrue(folderIndex < fileIndex, "Folders should be sorted before files");
            }
        }
    }
}
