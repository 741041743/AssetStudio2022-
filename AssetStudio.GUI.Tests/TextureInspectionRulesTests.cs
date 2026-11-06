using System;
using System.IO;
using AssetStudio;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetStudio.GUI.Tests
{
    [TestClass]
    public class TextureInspectionRulesTests
    {
        [TestMethod]
        public void All_DoesNotAddACondition()
        {
            Assert.IsTrue(TextureInspectionRules.Matches(
                TextureFilterRule.All, TextureFormat.DXT1, 1, 1,
                false, false, 1, 2048));
        }

        [DataTestMethod]
        [DataRow(TextureFormat.RGBA32)]
        [DataRow(TextureFormat.ARGB32)]
        [DataRow(TextureFormat.RGB24)]
        [DataRow(TextureFormat.RGBA4444)]
        [DataRow(TextureFormat.ARGB4444)]
        [DataRow(TextureFormat.RGB565)]
        [DataRow(TextureFormat.R16)]
        [DataRow(TextureFormat.RGBAHalf)]
        [DataRow(TextureFormat.RGBAFloat)]
        [DataRow(TextureFormat.BGRA32)]
        [DataRow(TextureFormat.RG16)]
        [DataRow(TextureFormat.R8)]
        public void NonBlockCompressed_MatchesTheLegacyFormatSet(TextureFormat format)
        {
            Assert.IsTrue(TextureInspectionRules.Matches(
                TextureFilterRule.NonBlockCompressed, format, 256, 256,
                false, false, 1, 2048));
        }

        [TestMethod]
        public void NonBlockCompressed_RejectsBlockCompressedFormats()
        {
            Assert.IsFalse(TextureInspectionRules.Matches(
                TextureFilterRule.NonBlockCompressed, TextureFormat.DXT1,
                256, 256, false, false, 1, 2048));
        }

        [DataTestMethod]
        [DataRow(2048, 32, 2048, true)]
        [DataRow(32, 2048, 2048, true)]
        [DataRow(2047, 2047, 2048, false)]
        [DataRow(1024, 32, 1024, true)]
        [DataRow(4095, 4095, 4096, false)]
        [DataRow(4096, 1, 4096, true)]
        public void Large_UsesInclusiveEitherDimensionThreshold(
            int width, int height, int threshold, bool expected)
        {
            Assert.AreEqual(expected, TextureInspectionRules.Matches(
                TextureFilterRule.Large, TextureFormat.DXT1,
                width, height, false, false, 1, threshold));
        }

        [TestMethod]
        public void ReadWriteEnabled_UsesUnityReadableFlag()
        {
            Assert.IsTrue(TextureInspectionRules.Matches(
                TextureFilterRule.ReadWriteEnabled, TextureFormat.DXT1,
                256, 256, true, false, 1, 2048));
            Assert.IsFalse(TextureInspectionRules.Matches(
                TextureFilterRule.ReadWriteEnabled, TextureFormat.DXT1,
                256, 256, false, false, 1, 2048));
        }

        [DataTestMethod]
        [DataRow(true, 1, true)]
        [DataRow(false, 2, true)]
        [DataRow(false, 1, false)]
        public void MipmapsEnabled_AcceptsFlagOrMultipleLevels(
            bool mipMap, int mipCount, bool expected)
        {
            Assert.AreEqual(expected, TextureInspectionRules.Matches(
                TextureFilterRule.MipmapsEnabled, TextureFormat.DXT1,
                256, 256, false, mipMap, mipCount, 2048));
        }

        [DataTestMethod]
        [DataRow(TextureFormat.RGBA32, 1000, 1024, false, 2, true)]
        [DataRow(TextureFormat.RGBA32, 1024, 1024, false, 2, false)]
        [DataRow(TextureFormat.DXT1, 1000, 1024, false, 2, false)]
        [DataRow(TextureFormat.RGBA32, 1000, 1024, false, 1, false)]
        [DataRow(TextureFormat.RGBA32, 1000, 1024, true, 1, true)]
        public void SuspectedNpotCompressionFallback_UsesExactLegacyHeuristic(
            TextureFormat format, int width, int height,
            bool mipMap, int mipCount, bool expected)
        {
            Assert.AreEqual(expected, TextureInspectionRules.Matches(
                TextureFilterRule.SuspectedNpotCompressionFallback,
                format, width, height, false, mipMap, mipCount, 2048));
        }

        [DataTestMethod]
        [DataRow(-1, false)]
        [DataRow(0, false)]
        [DataRow(1, true)]
        [DataRow(1024, true)]
        [DataRow(2048, true)]
        [DataRow(4096, true)]
        [DataRow(1000, false)]
        [DataRow(2047, false)]
        [DataRow(3000, false)]
        public void IsPowerOfTwo_HandlesBoundaries(int value, bool expected)
        {
            Assert.AreEqual(expected, TextureInspectionRules.IsPowerOfTwo(value));
        }

        [TestMethod]
        public void DescriptionsExposeTheActualRule()
        {
            Assert.AreEqual("全部纹理", TextureInspectionRules.GetDisplayName(TextureFilterRule.All));
            Assert.AreEqual("大尺寸", TextureInspectionRules.GetDisplayName(TextureFilterRule.Large));
            Assert.AreEqual(
                "宽或高任一边 ≥ 4096",
                TextureInspectionRules.GetDescription(TextureFilterRule.Large, 4096));
            Assert.AreEqual(
                "RGBA32、包含 Mipmaps，且宽或高不是 2 的幂",
                TextureInspectionRules.GetDescription(
                    TextureFilterRule.SuspectedNpotCompressionFallback, 2048));
        }

        [TestMethod]
        public void LargeTexturePresets_AreFixedAndOrdered()
        {
            CollectionAssert.AreEqual(
                new[] { 1024, 2048, 4096 },
                TextureInspectionRules.LargeTextureSizePresets);
        }
    }
}

namespace AssetStudio.GUI.Tests
{
    [TestClass]
    public class TextureFilterUiSourceTests
    {
        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "AssetStudio.sln")))
            {
                directory = directory.Parent;
            }

            Assert.IsNotNull(directory, "Could not locate AssetStudio.sln from the test output directory.");
            return directory.FullName;
        }

        [TestMethod]
        public void AssetListDeclaresContextualTextureFilterControls()
        {
            var root = FindRepositoryRoot();
            var designer = File.ReadAllText(Path.Combine(root, "AssetStudio.GUI", "MainForm.Designer.cs"));
            var mainForm = File.ReadAllText(Path.Combine(root, "AssetStudio.GUI", "MainForm.cs"));

            StringAssert.Contains(designer, "textureRuleComboBox = new ComboBox");
            StringAssert.Contains(designer, "textureSizePresetComboBox = new ComboBox");
            StringAssert.Contains(designer, "textureRuleDescriptionLabel = new Label");
            StringAssert.Contains(mainForm, "UpdateAssetTypeFilterControls");
            StringAssert.Contains(mainForm, "UpdateTextureFilterControls");
        }

        [TestMethod]
        public void AssetListComposesTextureRuleWithExistingFilters()
        {
            var root = FindRepositoryRoot();
            var mainForm = File.ReadAllText(Path.Combine(root, "AssetStudio.GUI", "MainForm.cs"));

            StringAssert.Contains(mainForm, "MatchesSelectedTextureRule");
            StringAssert.Contains(mainForm, "TextureInspectionRules.Matches");
            StringAssert.Contains(mainForm, "x.Asset is Texture2D texture && MatchesSelectedTextureRule(texture)");
            StringAssert.Contains(mainForm, "x.FullSize / (1024f * 1024f) > minSizeMB");
            StringAssert.Contains(mainForm, "regex.IsMatch(x.Text)");
        }

        [TestMethod]
        public void TextureFilteringDoesNotCreateReplacementAssetItems()
        {
            var root = FindRepositoryRoot();
            var mainForm = File.ReadAllText(Path.Combine(root, "AssetStudio.GUI", "MainForm.cs"));
            var filterStart = mainForm.IndexOf("private void FilterAssetList()", StringComparison.Ordinal);
            var filterEnd = mainForm.IndexOf("private static string ToMiB", filterStart, StringComparison.Ordinal);
            Assert.IsTrue(filterStart >= 0 && filterEnd > filterStart);

            var filterMethod = mainForm.Substring(filterStart, filterEnd - filterStart);
            Assert.IsFalse(filterMethod.Contains("new AssetItem("));
        }

        [TestMethod]
        public void AssetListFilterEventsUseNamedHandlersInsteadOfReboundLambdas()
        {
            var root = FindRepositoryRoot();
            var mainForm = File.ReadAllText(Path.Combine(root, "AssetStudio.GUI", "MainForm.cs"));

            Assert.IsFalse(mainForm.Contains("SelectedIndexChanged -= (s, e) => FilterAssetList()"));
            StringAssert.Contains(mainForm, "classIDTypeComboBox_SelectedIndexChanged");
            StringAssert.Contains(mainForm, "textureFormatComboBox_SelectedIndexChanged");
            StringAssert.Contains(mainForm, "textureRuleComboBox_SelectedIndexChanged");
            StringAssert.Contains(mainForm, "textureSizePresetComboBox_SelectedIndexChanged");
        }
        [TestMethod]
        public void StandaloneTextureAuditTabAndDuplicatePipelineAreRemoved()
        {
            var root = FindRepositoryRoot();
            var designer = File.ReadAllText(Path.Combine(root, "AssetStudio.GUI", "MainForm.Designer.cs"));
            var mainForm = File.ReadAllText(Path.Combine(root, "AssetStudio.GUI", "MainForm.cs"));

            Assert.IsFalse(designer.Contains("纹理专项检测"));
            StringAssert.Contains(designer, "tabControl1.Controls.Add(tabPage8);");
            Assert.IsFalse(designer.Contains("uncompressedTexturesListView"));
            Assert.IsFalse(designer.Contains("textureDetectionComboBox"));
            Assert.IsFalse(mainForm.Contains("uncompressedTexturesListView"));
            Assert.IsFalse(mainForm.Contains("uncompressedTextures"));
            Assert.IsFalse(mainForm.Contains("textureDetectionComboBox"));
            Assert.IsFalse(mainForm.Contains("PerformTextureDetection"));
            Assert.IsFalse(mainForm.Contains("DetectUncompressedTextures"));
            Assert.IsFalse(mainForm.Contains("Detect2048x4096Textures"));
            Assert.IsFalse(mainForm.Contains("DetectReadWriteEnabledTextures"));
            Assert.IsFalse(mainForm.Contains("DetectMipmapEnabledTextures"));
            Assert.IsFalse(mainForm.Contains("DetectInvalidCompressionTextures"));
        }

        [TestMethod]
        public void ResetRestoresTextureDefaultsWithoutResettingOnEveryTypeChange()
        {
            var root = FindRepositoryRoot();
            var mainForm = File.ReadAllText(Path.Combine(root, "AssetStudio.GUI", "MainForm.cs"));

            var resetStart = mainForm.IndexOf("public void ResetForm()", StringComparison.Ordinal);
            var resetEnd = mainForm.IndexOf("private void", resetStart + 1, StringComparison.Ordinal);
            Assert.IsTrue(resetStart >= 0 && resetEnd > resetStart);
            var resetMethod = mainForm.Substring(resetStart, resetEnd - resetStart);

            StringAssert.Contains(resetMethod, "textureRuleComboBox.SelectedIndex = (int)TextureFilterRule.All");
            StringAssert.Contains(resetMethod, "textureSizePresetComboBox.SelectedIndex = 1");

            var typeUpdateStart = mainForm.IndexOf("private void UpdateAssetTypeFilterControls()", StringComparison.Ordinal);
            var typeUpdateEnd = mainForm.IndexOf("private void", typeUpdateStart + 1, StringComparison.Ordinal);
            Assert.IsTrue(typeUpdateStart >= 0 && typeUpdateEnd > typeUpdateStart);
            var typeUpdateMethod = mainForm.Substring(typeUpdateStart, typeUpdateEnd - typeUpdateStart);

            Assert.IsFalse(typeUpdateMethod.Contains("textureRuleComboBox.SelectedIndex ="));
            Assert.IsFalse(typeUpdateMethod.Contains("textureSizePresetComboBox.SelectedIndex ="));
        }

    }
}
