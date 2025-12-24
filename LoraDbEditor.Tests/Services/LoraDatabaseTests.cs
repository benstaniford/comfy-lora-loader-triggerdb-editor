using LoraDbEditor.Models;
using LoraDbEditor.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace LoraDbEditor.Tests.Services
{
    [TestClass]
    public class LoraDatabaseTests
    {
        [TestMethod]
        public void LoraDatabase_Constructor_SetsProperties()
        {
            // Act
            var database = new LoraDatabase();

            // Assert
            Assert.IsNotNull(database.DatabasePath);
            Assert.IsNotNull(database.LorasBasePath);
        }

        [TestMethod]
        public async Task LoadAsync_NonExistentFile_CreatesEmptyDictionary()
        {
            // Arrange
            var database = new LoraDatabase();

            // Act
            await database.LoadAsync();

            // Assert
            Assert.IsNotNull(database.GetAllEntries());
        }

        [TestMethod]
        public void GetAllEntries_ReturnsCollection()
        {
            // Arrange
            var database = new LoraDatabase();

            // Act
            var entries = database.GetAllEntries();

            // Assert
            Assert.IsNotNull(entries);
        }

        [TestMethod]
        public void GetEntry_NonExistentPath_ReturnsNull()
        {
            // Arrange
            var database = new LoraDatabase();

            // Act
            var entry = database.GetEntry("nonexistent/path");

            // Assert
            Assert.IsNull(entry);
        }

        [TestMethod]
        public void AddEntry_AddsToDatabase()
        {
            // Arrange
            var database = new LoraDatabase();
            var entry = new LoraEntry
            {
                ActiveTriggers = "test",
                AllTriggers = "test"
            };

            // Act
            database.AddEntry("test/path", entry);
            var retrieved = database.GetEntry("test/path");

            // Assert
            Assert.IsNotNull(retrieved);
            Assert.AreEqual("test/path", retrieved.Path);
        }

        [TestMethod]
        public void RemoveEntry_RemovesFromDatabase()
        {
            // Arrange
            var database = new LoraDatabase();
            var entry = new LoraEntry
            {
                ActiveTriggers = "test",
                AllTriggers = "test"
            };
            database.AddEntry("test/path", entry);

            // Act
            database.RemoveEntry("test/path");
            var retrieved = database.GetEntry("test/path");

            // Assert
            Assert.IsNull(retrieved);
        }

        [TestMethod]
        public void UpdateFileId_UpdatesEntry()
        {
            // Arrange
            var database = new LoraDatabase();
            var entry = new LoraEntry
            {
                ActiveTriggers = "test",
                AllTriggers = "test",
                FileId = "old-id",
                CalculatedFileId = "new-id"
            };
            database.AddEntry("test/path", entry);

            // Act
            database.UpdateFileId("test/path", "new-id");
            var retrieved = database.GetEntry("test/path");

            // Assert
            Assert.IsNotNull(retrieved);
            Assert.AreEqual("new-id", retrieved.FileId);
            Assert.IsTrue(retrieved.FileIdValid);
        }

        [TestMethod]
        public void UpdateFileId_NonExistentPath_DoesNotThrow()
        {
            // Arrange
            var database = new LoraDatabase();

            // Act & Assert (should not throw)
            database.UpdateFileId("nonexistent/path", "some-id");
        }
    }
}
