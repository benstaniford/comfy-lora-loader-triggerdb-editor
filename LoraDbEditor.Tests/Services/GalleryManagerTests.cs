using LoraDbEditor.Models;
using LoraDbEditor.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LoraDbEditor.Tests.Services
{
    [TestClass]
    public class GalleryManagerTests
    {
        private GalleryManager _manager = null!;

        [TestInitialize]
        public void Setup()
        {
            _manager = new GalleryManager();
        }

        [TestMethod]
        public void IsImageFile_WithValidExtensions_ReturnsTrue()
        {
            // Arrange
            var validExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };

            // Act & Assert
            foreach (var ext in validExtensions)
            {
                var result = _manager.IsImageFile($"test{ext}");
                Assert.IsTrue(result, $"Extension {ext} should be valid");
            }
        }

        [TestMethod]
        public void IsImageFile_WithInvalidExtension_ReturnsFalse()
        {
            // Arrange
            var filePath = "test.txt";

            // Act
            var result = _manager.IsImageFile(filePath);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void GenerateSafePath_ReplacesSlashesWithUnderscores()
        {
            // Arrange
            var path = "folder/subfolder/file";

            // Act
            var result = _manager.GenerateSafePath(path);

            // Assert
            Assert.AreEqual("folder_subfolder_file", result);
        }

        [TestMethod]
        public void GenerateSafePath_ReplacesBackslashesWithUnderscores()
        {
            // Arrange
            var path = @"folder\subfolder\file";

            // Act
            var result = _manager.GenerateSafePath(path);

            // Assert
            Assert.AreEqual("folder_subfolder_file", result);
        }

        [TestMethod]
        public void GenerateSafePath_HandlesMixedSlashes()
        {
            // Arrange
            var path = @"folder/subfolder\file";

            // Act
            var result = _manager.GenerateSafePath(path);

            // Assert
            Assert.AreEqual("folder_subfolder_file", result);
        }

        [TestMethod]
        public void LoadGalleryImages_WithNullGallery_ReturnsEmptyList()
        {
            // Arrange
            var entry = new LoraEntry { Gallery = null };
            var basePath = @"C:\TestPath";

            // Act
            var result = _manager.LoadGalleryImages(entry, basePath);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void LoadGalleryImages_WithEmptyGallery_ReturnsEmptyList()
        {
            // Arrange
            var entry = new LoraEntry { Gallery = new List<string>() };
            var basePath = @"C:\TestPath";

            // Act
            var result = _manager.LoadGalleryImages(entry, basePath);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void LoadGalleryImages_WithNullEntry_ReturnsEmptyList()
        {
            // Arrange
            LoraEntry? entry = null;
            var basePath = @"C:\TestPath";

            // Act
            var result = _manager.LoadGalleryImages(entry, basePath);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }
    }
}
