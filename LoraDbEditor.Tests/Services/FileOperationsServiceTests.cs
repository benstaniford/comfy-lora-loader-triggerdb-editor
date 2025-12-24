using LoraDbEditor.Models;
using LoraDbEditor.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LoraDbEditor.Tests.Services
{
    [TestClass]
    public class FileOperationsServiceTests
    {
        private FileOperationsService _service = null!;

        [TestInitialize]
        public void Setup()
        {
            _service = new FileOperationsService();
        }

        [TestMethod]
        public void UpdateGalleryFilenames_WithMatchingPrefix_UpdatesFilenames()
        {
            // Arrange
            var oldPath = "folder/file";
            var newPath = "folder/renamed";
            var entry = new LoraEntry
            {
                Gallery = new List<string>
                {
                    "folder_file_20231201120000.png",
                    "folder_file_20231201120030.jpg"
                }
            };
            var basePath = @"C:\TestPath";

            // Act
            _service.UpdateGalleryFilenames(oldPath, newPath, entry, basePath);

            // Assert
            Assert.AreEqual(2, entry.Gallery.Count);
            Assert.AreEqual("folder_renamed_20231201120000.png", entry.Gallery[0]);
            Assert.AreEqual("folder_renamed_20231201120030.jpg", entry.Gallery[1]);
        }

        [TestMethod]
        public void UpdateGalleryFilenames_WithNonMatchingPrefix_KeepsFilenames()
        {
            // Arrange
            var oldPath = "folder/file";
            var newPath = "folder/renamed";
            var entry = new LoraEntry
            {
                Gallery = new List<string>
                {
                    "other_file_20231201120000.png"
                }
            };
            var basePath = @"C:\TestPath";

            // Act
            _service.UpdateGalleryFilenames(oldPath, newPath, entry, basePath);

            // Assert
            Assert.AreEqual(1, entry.Gallery.Count);
            Assert.AreEqual("other_file_20231201120000.png", entry.Gallery[0]);
        }

        [TestMethod]
        public void UpdateGalleryFilenames_WithNullGallery_DoesNotThrow()
        {
            // Arrange
            var oldPath = "folder/file";
            var newPath = "folder/renamed";
            var entry = new LoraEntry { Gallery = null };
            var basePath = @"C:\TestPath";

            // Act & Assert - should not throw
            _service.UpdateGalleryFilenames(oldPath, newPath, entry, basePath);
        }

        [TestMethod]
        public void UpdateGalleryFilenames_WithEmptyGallery_DoesNotThrow()
        {
            // Arrange
            var oldPath = "folder/file";
            var newPath = "folder/renamed";
            var entry = new LoraEntry { Gallery = new List<string>() };
            var basePath = @"C:\TestPath";

            // Act & Assert - should not throw
            _service.UpdateGalleryFilenames(oldPath, newPath, entry, basePath);
            Assert.AreEqual(0, entry.Gallery.Count);
        }

        [TestMethod]
        public void UpdateGalleryFilenames_WithMixedPaths_UpdatesOnlyMatching()
        {
            // Arrange
            var oldPath = "folder1/file";
            var newPath = "folder2/file";
            var entry = new LoraEntry
            {
                Gallery = new List<string>
                {
                    "folder1_file_123.png",
                    "other_file_456.png",
                    "folder1_file_789.jpg"
                }
            };
            var basePath = @"C:\TestPath";

            // Act
            _service.UpdateGalleryFilenames(oldPath, newPath, entry, basePath);

            // Assert
            Assert.AreEqual(3, entry.Gallery.Count);
            Assert.AreEqual("folder2_file_123.png", entry.Gallery[0]);
            Assert.AreEqual("other_file_456.png", entry.Gallery[1]); // unchanged
            Assert.AreEqual("folder2_file_789.jpg", entry.Gallery[2]);
        }
    }
}
