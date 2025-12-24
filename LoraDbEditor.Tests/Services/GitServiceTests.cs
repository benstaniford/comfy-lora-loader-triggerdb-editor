using LoraDbEditor.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace LoraDbEditor.Tests.Services
{
    [TestClass]
    public class GitServiceTests
    {
        private GitService _service = null!;

        [TestInitialize]
        public void Setup()
        {
            _service = new GitService();
        }

        [TestMethod]
        public async Task IsGitAvailableAsync_ReturnsResult()
        {
            // Act
            var result = await _service.IsGitAvailableAsync();

            // Assert
            // Result depends on whether git is installed on the system
            // Just verify it returns without throwing
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task IsGitRepositoryAsync_NonGitDirectory_ReturnsFalse()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                // Act
                var result = await _service.IsGitRepositoryAsync(tempDir);

                // Assert
                Assert.IsFalse(result);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        public async Task RunGitCommandAsync_InvalidCommand_ReturnsNull()
        {
            // Arrange
            var tempDir = Path.GetTempPath();

            // Act
            var result = await _service.RunGitCommandAsync("invalid-command-that-does-not-exist", tempDir);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task HasUncommittedChangesAsync_NonGitDirectory_ReturnsFalse()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                // Act
                var result = await _service.HasUncommittedChangesAsync(tempDir, "test.txt");

                // Assert
                Assert.IsFalse(result);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        public async Task CommitChangesAsync_NonGitDirectory_ReturnsFalse()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                // Act
                var result = await _service.CommitChangesAsync(tempDir, "Test commit", "test.txt");

                // Assert
                Assert.IsFalse(result);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
