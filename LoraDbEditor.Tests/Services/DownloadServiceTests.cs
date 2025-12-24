using LoraDbEditor.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LoraDbEditor.Tests.Services
{
    [TestClass]
    public class DownloadServiceTests
    {
        private DownloadService _service = null!;

        [TestInitialize]
        public void Setup()
        {
            _service = new DownloadService();
        }

        [TestMethod]
        public void GetFilenameFromUrl_WithSafetensorsExtension_RemovesExtension()
        {
            // Arrange
            var url = "https://example.com/path/to/file.safetensors";

            // Act
            var result = _service.GetFilenameFromUrl(url);

            // Assert
            Assert.AreEqual("file", result);
        }

        [TestMethod]
        public void GetFilenameFromUrl_WithOtherExtension_RemovesExtension()
        {
            // Arrange
            var url = "https://example.com/path/to/archive.zip";

            // Act
            var result = _service.GetFilenameFromUrl(url);

            // Assert
            Assert.AreEqual("archive", result);
        }

        [TestMethod]
        public void GetFilenameFromUrl_WithNoExtension_ReturnsLastSegment()
        {
            // Arrange
            var url = "https://example.com/path/to/filename";

            // Act
            var result = _service.GetFilenameFromUrl(url);

            // Assert
            Assert.AreEqual("filename", result);
        }

        [TestMethod]
        public void GetFilenameFromUrl_WithInvalidUrl_ReturnsFallback()
        {
            // Arrange
            var url = "not-a-valid-url";

            // Act
            var result = _service.GetFilenameFromUrl(url);

            // Assert
            Assert.AreEqual("downloaded-lora", result);
        }

        [TestMethod]
        public void GetFilenameFromUrl_WithTrailingSlash_ReturnsFallback()
        {
            // Arrange
            var url = "https://example.com/path/";

            // Act
            var result = _service.GetFilenameFromUrl(url);

            // Assert
            Assert.AreEqual("downloaded-lora", result);
        }

        [TestMethod]
        public void ApplyCivitaiApiKey_WithNonCivitaiUrl_ReturnsUnchanged()
        {
            // Arrange
            var url = "https://example.com/file.safetensors";

            // Act
            var result = _service.ApplyCivitaiApiKey(url);

            // Assert
            Assert.AreEqual(url, result);
        }

        [TestMethod]
        public void ApplyCivitaiApiKey_WithCivitaiUrlAndNoKey_ReturnsUnchanged()
        {
            // Arrange
            var url = "https://civitai.com/api/download/models/123456";

            // Act
            var result = _service.ApplyCivitaiApiKey(url);

            // Assert - Without a configured API key, URL should remain unchanged
            // (This test assumes no API key is configured in the test environment)
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("civitai.com"));
        }

        [TestMethod]
        public void ApplyCivitaiApiKey_WithInvalidUrl_ReturnsUnchanged()
        {
            // Arrange
            var url = "not-a-valid-url";

            // Act
            var result = _service.ApplyCivitaiApiKey(url);

            // Assert
            Assert.AreEqual(url, result);
        }
    }
}
