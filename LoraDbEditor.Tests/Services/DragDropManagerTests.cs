using LoraDbEditor.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LoraDbEditor.Tests.Services
{
    [TestClass]
    public class DragDropManagerTests
    {
        private DragDropManager _manager = null!;
        private TreeViewManager _treeViewManager = null!;

        [TestInitialize]
        public void Setup()
        {
            _treeViewManager = new TreeViewManager();
            _manager = new DragDropManager(_treeViewManager);
        }

        [TestMethod]
        public void IsSafetensorsFile_WithValidExtension_ReturnsTrue()
        {
            // Arrange
            var filePath = @"C:\path\to\file.safetensors";

            // Act
            var result = _manager.IsSafetensorsFile(filePath);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsSafetensorsFile_WithInvalidExtension_ReturnsFalse()
        {
            // Arrange
            var filePath = @"C:\path\to\file.txt";

            // Act
            var result = _manager.IsSafetensorsFile(filePath);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsSafetensorsFile_WithUppercaseExtension_ReturnsTrue()
        {
            // Arrange
            var filePath = @"C:\path\to\file.SAFETENSORS";

            // Act
            var result = _manager.IsSafetensorsFile(filePath);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsImageFile_WithValidJpgExtension_ReturnsTrue()
        {
            // Arrange
            var filePath = @"C:\path\to\image.jpg";

            // Act
            var result = _manager.IsImageFile(filePath);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsImageFile_WithValidPngExtension_ReturnsTrue()
        {
            // Arrange
            var filePath = @"C:\path\to\image.png";

            // Act
            var result = _manager.IsImageFile(filePath);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsImageFile_WithInvalidExtension_ReturnsFalse()
        {
            // Arrange
            var filePath = @"C:\path\to\file.txt";

            // Act
            var result = _manager.IsImageFile(filePath);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsImageFile_WithAllSupportedFormats_ReturnsTrue()
        {
            // Arrange
            var supportedFormats = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };

            // Act & Assert
            foreach (var format in supportedFormats)
            {
                var filePath = $@"C:\path\to\image{format}";
                var result = _manager.IsImageFile(filePath);
                Assert.IsTrue(result, $"Format {format} should be recognized as an image");
            }
        }
    }
}
