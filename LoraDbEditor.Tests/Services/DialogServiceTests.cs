using LoraDbEditor.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LoraDbEditor.Tests.Services
{
    [TestClass]
    public class DialogServiceTests
    {
        private DialogService _service = null!;

        [TestInitialize]
        public void Setup()
        {
            _service = new DialogService();
        }

        [TestMethod]
        public void DialogService_CanBeInstantiated()
        {
            // Assert
            Assert.IsNotNull(_service);
        }

        // Note: Most DialogService methods require a Window owner and show modal dialogs,
        // which cannot be easily tested in a unit test environment without UI automation.
        // These tests verify the service can be instantiated and methods exist.

        [TestMethod]
        public void ShowConfirmDialog_MethodExists()
        {
            // Arrange
            var methods = typeof(DialogService).GetMethods();

            // Act
            var method = methods.FirstOrDefault(m => m.Name == "ShowConfirmDialog");

            // Assert
            Assert.IsNotNull(method);
            Assert.AreEqual(typeof(bool), method.ReturnType);
        }

        [TestMethod]
        public void ShowRenameSingleFileDialog_MethodExists()
        {
            // Arrange
            var methods = typeof(DialogService).GetMethods();

            // Act
            var method = methods.FirstOrDefault(m => m.Name == "ShowRenameSingleFileDialog");

            // Assert
            Assert.IsNotNull(method);
            Assert.AreEqual(typeof(string), method.ReturnType);
        }

        [TestMethod]
        public void ShowRenameFolderDialog_MethodExists()
        {
            // Arrange
            var methods = typeof(DialogService).GetMethods();

            // Act
            var method = methods.FirstOrDefault(m => m.Name == "ShowRenameFolderDialog");

            // Assert
            Assert.IsNotNull(method);
            Assert.AreEqual(typeof(string), method.ReturnType);
        }

        [TestMethod]
        public void ShowCreateFolderDialog_MethodExists()
        {
            // Arrange
            var methods = typeof(DialogService).GetMethods();

            // Act
            var method = methods.FirstOrDefault(m => m.Name == "ShowCreateFolderDialog");

            // Assert
            Assert.IsNotNull(method);
            Assert.AreEqual(typeof(string), method.ReturnType);
        }

        [TestMethod]
        public void ShowFolderSelectionDialog_MethodExists()
        {
            // Arrange
            var methods = typeof(DialogService).GetMethods();

            // Act
            var method = methods.FirstOrDefault(m => m.Name == "ShowFolderSelectionDialog");

            // Assert
            Assert.IsNotNull(method);
            Assert.AreEqual(typeof(string), method.ReturnType);
        }
    }
}
