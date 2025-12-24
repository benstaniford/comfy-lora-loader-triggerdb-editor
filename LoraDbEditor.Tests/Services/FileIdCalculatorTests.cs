using LoraDbEditor.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Text;

namespace LoraDbEditor.Tests.Services
{
    [TestClass]
    public class FileIdCalculatorTests
    {
        private string _testDirectory = null!;

        [TestInitialize]
        public void Setup()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(FileNotFoundException))]
        public void CalculateFileId_NonExistentFile_ThrowsFileNotFoundException()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "nonexistent.txt");

            // Act
            FileIdCalculator.CalculateFileId(filePath);
        }

        [TestMethod]
        public void CalculateFileId_SmallFile_ReturnsValidHash()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "small.txt");
            File.WriteAllText(filePath, "Hello World");

            // Act
            var fileId = FileIdCalculator.CalculateFileId(filePath);

            // Assert
            Assert.IsNotNull(fileId);
            Assert.AreEqual(40, fileId.Length); // SHA1 hash is 40 characters in hex
            Assert.IsTrue(fileId.All(c => "0123456789abcdef".Contains(c)));
        }

        [TestMethod]
        public void CalculateFileId_LargeFile_ReturnsValidHash()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "large.bin");
            // Create a file larger than 1MB
            var data = new byte[2 * 1024 * 1024]; // 2MB
            new Random().NextBytes(data);
            File.WriteAllBytes(filePath, data);

            // Act
            var fileId = FileIdCalculator.CalculateFileId(filePath);

            // Assert
            Assert.IsNotNull(fileId);
            Assert.AreEqual(40, fileId.Length);
            Assert.IsTrue(fileId.All(c => "0123456789abcdef".Contains(c)));
        }

        [TestMethod]
        public void CalculateFileId_SameContent_ReturnsSameHash()
        {
            // Arrange
            var filePath1 = Path.Combine(_testDirectory, "file1.txt");
            var filePath2 = Path.Combine(_testDirectory, "file2.txt");
            var content = "Test Content";
            File.WriteAllText(filePath1, content);
            File.WriteAllText(filePath2, content);

            // Act
            var fileId1 = FileIdCalculator.CalculateFileId(filePath1);
            var fileId2 = FileIdCalculator.CalculateFileId(filePath2);

            // Assert
            Assert.AreEqual(fileId1, fileId2);
        }

        [TestMethod]
        public void CalculateFileId_DifferentContent_ReturnsDifferentHash()
        {
            // Arrange
            var filePath1 = Path.Combine(_testDirectory, "file1.txt");
            var filePath2 = Path.Combine(_testDirectory, "file2.txt");
            File.WriteAllText(filePath1, "Content A");
            File.WriteAllText(filePath2, "Content B");

            // Act
            var fileId1 = FileIdCalculator.CalculateFileId(filePath1);
            var fileId2 = FileIdCalculator.CalculateFileId(filePath2);

            // Assert
            Assert.AreNotEqual(fileId1, fileId2);
        }

        [TestMethod]
        public void CalculateFileId_EmptyFile_ReturnsValidHash()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "empty.txt");
            File.WriteAllText(filePath, string.Empty);

            // Act
            var fileId = FileIdCalculator.CalculateFileId(filePath);

            // Assert
            Assert.IsNotNull(fileId);
            Assert.AreEqual(40, fileId.Length);
        }
    }
}
