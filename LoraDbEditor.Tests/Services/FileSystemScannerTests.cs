using LoraDbEditor.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace LoraDbEditor.Tests.Services
{
    [TestClass]
    public class FileSystemScannerTests
    {
        private string _testDirectory = null!;
        private FileSystemScanner _scanner = null!;

        [TestInitialize]
        public void Setup()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
            _scanner = new FileSystemScanner(_testDirectory);
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
        public void ScanForLoraFiles_NonExistentDirectory_ReturnsEmptyList()
        {
            // Arrange
            var scanner = new FileSystemScanner(Path.Combine(_testDirectory, "nonexistent"));

            // Act
            var result = scanner.ScanForLoraFiles();

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void ScanForLoraFiles_EmptyDirectory_ReturnsEmptyList()
        {
            // Act
            var result = _scanner.ScanForLoraFiles();

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void ScanForLoraFiles_WithSafetensorsFiles_ReturnsRelativePaths()
        {
            // Arrange
            File.WriteAllText(Path.Combine(_testDirectory, "file1.safetensors"), "test");
            File.WriteAllText(Path.Combine(_testDirectory, "file2.safetensors"), "test");

            // Act
            var result = _scanner.ScanForLoraFiles();

            // Assert
            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.Contains("file1"));
            Assert.IsTrue(result.Contains("file2"));
        }

        [TestMethod]
        public void ScanForLoraFiles_WithSubdirectories_ReturnsRelativePathsWithSlashes()
        {
            // Arrange
            var subDir = Path.Combine(_testDirectory, "subfolder");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(subDir, "nested.safetensors"), "test");

            // Act
            var result = _scanner.ScanForLoraFiles();

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("subfolder/nested", result[0]);
        }

        [TestMethod]
        public void ScanForLoraFiles_MixedFiles_ReturnsOnlySafetensors()
        {
            // Arrange
            File.WriteAllText(Path.Combine(_testDirectory, "lora.safetensors"), "test");
            File.WriteAllText(Path.Combine(_testDirectory, "text.txt"), "test");
            File.WriteAllText(Path.Combine(_testDirectory, "image.png"), "test");

            // Act
            var result = _scanner.ScanForLoraFiles();

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("lora", result[0]);
        }

        [TestMethod]
        public void ScanForLoraFiles_ResultsAreSorted()
        {
            // Arrange
            File.WriteAllText(Path.Combine(_testDirectory, "zebra.safetensors"), "test");
            File.WriteAllText(Path.Combine(_testDirectory, "apple.safetensors"), "test");
            File.WriteAllText(Path.Combine(_testDirectory, "middle.safetensors"), "test");

            // Act
            var result = _scanner.ScanForLoraFiles();

            // Assert
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual("apple", result[0]);
            Assert.AreEqual("middle", result[1]);
            Assert.AreEqual("zebra", result[2]);
        }

        [TestMethod]
        public void FuzzySearch_EmptySearchText_ReturnsAllPaths()
        {
            // Arrange
            var paths = new List<string> { "path1", "path2", "path3" };

            // Act
            var result = FileSystemScanner.FuzzySearch(paths, "");

            // Assert
            Assert.AreEqual(3, result.Count);
        }

        [TestMethod]
        public void FuzzySearch_ExactMatch_ReturnsMatchFirst()
        {
            // Arrange
            var paths = new List<string> { "test", "testing", "contest" };

            // Act
            var result = FileSystemScanner.FuzzySearch(paths, "test");

            // Assert
            Assert.AreEqual("test", result[0]);
        }

        [TestMethod]
        public void FuzzySearch_StartsWith_RanksHigh()
        {
            // Arrange
            var paths = new List<string> { "contains_test", "test_start", "unrelated" };

            // Act
            var result = FileSystemScanner.FuzzySearch(paths, "test");

            // Assert
            Assert.IsTrue(result.IndexOf("test_start") < result.IndexOf("contains_test"));
        }

        [TestMethod]
        public void FuzzySearch_Contains_RanksMiddle()
        {
            // Arrange
            var paths = new List<string> { "abc_test_xyz", "test_start", "t_e_s_t" };

            // Act
            var result = FileSystemScanner.FuzzySearch(paths, "test");

            // Assert
            Assert.IsTrue(result.Contains("abc_test_xyz"));
            Assert.IsTrue(result.IndexOf("test_start") < result.IndexOf("abc_test_xyz"));
        }

        [TestMethod]
        public void FuzzySearch_NoMatch_ReturnsEmpty()
        {
            // Arrange
            var paths = new List<string> { "path1", "path2", "path3" };

            // Act
            var result = FileSystemScanner.FuzzySearch(paths, "xyz");

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void FuzzySearch_CaseInsensitive()
        {
            // Arrange
            var paths = new List<string> { "TestPath", "TESTPATH", "testpath" };

            // Act
            var result = FileSystemScanner.FuzzySearch(paths, "test");

            // Assert
            Assert.AreEqual(3, result.Count);
        }

        [TestMethod]
        public void FuzzySearch_OrderedCharacters_Matches()
        {
            // Arrange
            var paths = new List<string> { "t_e_s_t", "abcd", "test" };

            // Act
            var result = FileSystemScanner.FuzzySearch(paths, "test");

            // Assert
            Assert.IsTrue(result.Contains("t_e_s_t"));
            Assert.AreEqual("test", result[0]); // Exact match ranks first
        }
    }
}
