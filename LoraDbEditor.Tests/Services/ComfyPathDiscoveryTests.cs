using LoraDbEditor.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;

namespace LoraDbEditor.Tests.Services
{
    [TestClass]
    public class ComfyPathDiscoveryTests
    {
        // ---------------------------------------------------------------- ParseLoraDirsFromYaml

        [TestMethod]
        public void ParseLoraDirsFromYaml_RelativeLoras_CombinedWithBasePath()
        {
            var yaml = string.Join("\n",
                "comfyui:",
                @"  base_path: C:\Shared\models",
                "  is_default: true",
                "  loras: models/loras",
                "  checkpoints: models/checkpoints");

            var result = ComfyPathDiscovery.ParseLoraDirsFromYaml(yaml);

            CollectionAssert.AreEqual(
                new List<string> { Path.Combine(@"C:\Shared\models", "models/loras") },
                result);
        }

        [TestMethod]
        public void ParseLoraDirsFromYaml_AbsoluteLoras_UsedAsIs()
        {
            var yaml = string.Join("\n",
                "comfyui:",
                @"  base_path: C:\Shared\models",
                @"  loras: D:\Extra\loras");

            var result = ComfyPathDiscovery.ParseLoraDirsFromYaml(yaml);

            CollectionAssert.AreEqual(new List<string> { @"D:\Extra\loras" }, result);
        }

        [TestMethod]
        public void ParseLoraDirsFromYaml_BlockScalar_ReturnsAllPaths()
        {
            var yaml = string.Join("\n",
                "comfyui:",
                @"  base_path: C:\Shared\models",
                "  loras: |",
                "    models/loras",
                @"    D:\Extra\loras",
                "  checkpoints: models/checkpoints");

            var result = ComfyPathDiscovery.ParseLoraDirsFromYaml(yaml);

            CollectionAssert.AreEqual(
                new List<string>
                {
                    Path.Combine(@"C:\Shared\models", "models/loras"),
                    @"D:\Extra\loras",
                },
                result);
        }

        [TestMethod]
        public void ParseLoraDirsFromYaml_CommentsAndQuotes_Ignored()
        {
            var yaml = string.Join("\n",
                "# top comment",
                "comfyui:",
                @"  base_path: ""C:\Shared\models""  # where models live",
                "  loras: 'models/loras'",
                "");

            var result = ComfyPathDiscovery.ParseLoraDirsFromYaml(yaml);

            CollectionAssert.AreEqual(
                new List<string> { Path.Combine(@"C:\Shared\models", "models/loras") },
                result);
        }

        [TestMethod]
        public void ParseLoraDirsFromYaml_RelativeLorasWithoutBasePath_Skipped()
        {
            var yaml = string.Join("\n",
                "comfyui:",
                "  loras: models/loras");

            var result = ComfyPathDiscovery.ParseLoraDirsFromYaml(yaml);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void ParseLoraDirsFromYaml_MultipleSections_EachUsesOwnBasePath()
        {
            var yaml = string.Join("\n",
                "comfyui:",
                @"  base_path: C:\A",
                "  loras: models/loras",
                "other:",
                @"  base_path: C:\B",
                "  loras: models/loras");

            var result = ComfyPathDiscovery.ParseLoraDirsFromYaml(yaml);

            CollectionAssert.AreEqual(
                new List<string>
                {
                    Path.Combine(@"C:\A", "models/loras"),
                    Path.Combine(@"C:\B", "models/loras"),
                },
                result);
        }

        [TestMethod]
        public void ParseLoraDirsFromYaml_Empty_ReturnsEmpty()
        {
            Assert.AreEqual(0, ComfyPathDiscovery.ParseLoraDirsFromYaml("").Count);
            Assert.AreEqual(0, ComfyPathDiscovery.ParseLoraDirsFromYaml("   ").Count);
        }

        // ---------------------------------------------------------------- ReadBasePathFromConfig

        [TestMethod]
        public void ReadBasePathFromConfig_ValidBasePath_Returned()
        {
            var json = @"{ ""basePath"": ""C:\\Users\\ben\\ComfyUI"", ""detectedGpu"": ""nvidia"" }";
            Assert.AreEqual(@"C:\Users\ben\ComfyUI", ComfyPathDiscovery.ReadBasePathFromConfig(json));
        }

        [TestMethod]
        public void ReadBasePathFromConfig_MissingKey_ReturnsNull()
        {
            Assert.IsNull(ComfyPathDiscovery.ReadBasePathFromConfig(@"{ ""other"": ""x"" }"));
        }

        [TestMethod]
        public void ReadBasePathFromConfig_EmptyBasePath_ReturnsNull()
        {
            Assert.IsNull(ComfyPathDiscovery.ReadBasePathFromConfig(@"{ ""basePath"": """" }"));
        }

        [TestMethod]
        public void ReadBasePathFromConfig_Malformed_ReturnsNull()
        {
            Assert.IsNull(ComfyPathDiscovery.ReadBasePathFromConfig("not json at all"));
            Assert.IsNull(ComfyPathDiscovery.ReadBasePathFromConfig(""));
        }

        // ---------------------------------------------------------------- ResolveFirstExisting

        [TestMethod]
        public void ResolveFirstExisting_ReturnsFirstThatExists()
        {
            var existing = new HashSet<string> { "b", "c" };
            var result = ComfyPathDiscovery.ResolveFirstExisting(
                new[] { "a", "b", "c" }, "fallback", p => existing.Contains(p));

            Assert.AreEqual("b", result);
        }

        [TestMethod]
        public void ResolveFirstExisting_NoneExist_ReturnsFallback()
        {
            var result = ComfyPathDiscovery.ResolveFirstExisting(
                new[] { "a", "b" }, "fallback", _ => false);

            Assert.AreEqual("fallback", result);
        }

        [TestMethod]
        public void ResolveFirstExisting_SkipsNullAndWhitespaceCandidates()
        {
            var result = ComfyPathDiscovery.ResolveFirstExisting(
                new[] { "", "   ", "real" }, "fallback", p => p == "real" || p == "   ");

            Assert.AreEqual("real", result);
        }
    }
}
