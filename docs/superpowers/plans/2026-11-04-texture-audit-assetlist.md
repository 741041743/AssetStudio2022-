# Texture Audit AssetList Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the standalone texture-audit tab and expose its five existing checks as a concise, single-select `Texture2D` filter inside Asset List, including configurable 1024/2048/4096 size presets and visible rule explanations.

**Architecture:** Keep Asset List as the only texture result surface. Put rule semantics in a small pure `TextureInspectionRules` component, let `MainForm` own only filter state and WinForms visibility, and compose the selected texture rule with the existing type, format, search, and resource-size filters. Delete the duplicate texture tab, duplicate `AssetItem` collection, scan methods, sorting, selection, and virtual-list plumbing.

**Tech Stack:** C#; .NET 7/8; Windows Forms; MSTest (`Microsoft.NET.Test.Sdk`, `MSTest.TestAdapter`, `MSTest.TestFramework`); `dotnet` CLI.

## Global Constraints

- Preserve the current detection results except for the approved naming changes and the new 1024/2048/4096 large-texture presets.
- Texture audit rules are single-select and combine with type, texture format, search, and `Size >` filters using AND semantics.
- Reset or loading a new resource set restores `全部纹理`, texture format `All`, and size preset `≥2048`.
- Leaving `Texture2D` hides texture-only controls but preserves their state until Reset or a new load.
- Do not add a general rule-registration or dynamic-form framework.
- Do not change export formats, GPU-memory calculations, or the existing `AssetItem.FullSize` meaning.
- Preserve existing uncommitted Mesh, APK, BundleFile, FileReader, and other unrelated changes; never replace entire `MainForm.cs` or `MainForm.Designer.cs` files.
- Use explicit field references for newly touched Asset List filter controls instead of adding more `Controls[index]` dependencies.
- Do not commit unless the repository owner explicitly requests commits; the commit steps below are handoff checkpoints, not authorization to commit.

---

## File Map

- Create `AssetStudio.GUI/Components/TextureInspectionRules.cs` — pure texture-rule enum, size presets, display text, description text, and predicate logic.
- Create `AssetStudio.GUI.Tests/AssetStudio.GUI.Tests.csproj` — Windows-targeted MSTest project referencing the GUI project.
- Create `AssetStudio.GUI.Tests/TextureInspectionRulesTests.cs` — unit coverage for all six rules and size boundaries.
- Modify `AssetStudio.sln` — add the test project and solution configurations.
- Modify `AssetStudio.GUI/MainForm.Designer.cs` — add explicit Asset List texture-rule controls, extend the filter area to three rows, and delete the standalone texture-audit tab.
- Modify `AssetStudio.GUI/MainForm.cs` — bind controls once, compose the texture predicate into `FilterAssetList`, implement state/reset behavior, and remove old texture-tab code.
- Modify `docs/superpowers/specs/2026-11-04-texture-audit-assetlist-design.md` only if implementation uncovers a genuine design contradiction; do not silently change accepted behavior.

---

### Task 1: Add a pure, tested texture-rule component

**Files:**
- Create: `AssetStudio.GUI/Components/TextureInspectionRules.cs`
- Create: `AssetStudio.GUI.Tests/AssetStudio.GUI.Tests.csproj`
- Create: `AssetStudio.GUI.Tests/TextureInspectionRulesTests.cs`
- Modify: `AssetStudio.sln:6-19,29-108`

**Interfaces:**
- Consumes: `AssetStudio.TextureFormat` from the core project.
- Produces:
  - `internal enum TextureFilterRule`
  - `internal static class TextureInspectionRules`
  - `internal static readonly int[] LargeTextureSizePresets`
  - `internal static bool Matches(TextureFilterRule rule, TextureFormat format, int width, int height, bool isReadable, bool mipMap, int mipCount, int largeTextureThreshold)`
  - `internal static string GetDisplayName(TextureFilterRule rule)`
  - `internal static string GetDescription(TextureFilterRule rule, int largeTextureThreshold)`
  - `internal static bool IsPowerOfTwo(int value)`

- [ ] **Step 1: Create the Windows-targeted MSTest project**

Create `AssetStudio.GUI.Tests/AssetStudio.GUI.Tests.csproj` with exact contents:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <IsTestProject>true</IsTestProject>
    <IsPackable>false</IsPackable>
    <Nullable>disable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="MSTest.TestAdapter" Version="3.6.4" />
    <PackageReference Include="MSTest.TestFramework" Version="3.6.4" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\AssetStudio.GUI\AssetStudio.GUI.csproj" />
  </ItemGroup>
</Project>
```

Add this assembly attribute at the bottom of the new production file in Step 4, outside the namespace, so the test project can access internal APIs:

```csharp
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("AssetStudio.GUI.Tests")]
```

Add the project to the solution using the CLI rather than hand-editing GUIDs:

```powershell
dotnet sln .\AssetStudio.sln add .\AssetStudio.GUI.Tests\AssetStudio.GUI.Tests.csproj
```

Expected: `Project 'AssetStudio.GUI.Tests\AssetStudio.GUI.Tests.csproj' added to the solution.`

- [ ] **Step 2: Write failing rule tests**

Create `AssetStudio.GUI.Tests/TextureInspectionRulesTests.cs`:

```csharp
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
```

- [ ] **Step 3: Run the tests to verify they fail for the missing production API**

Run:

```powershell
dotnet test .\AssetStudio.GUI.Tests\AssetStudio.GUI.Tests.csproj -c Debug -f net8.0-windows
```

Expected: build fails with errors such as `CS0103: The name 'TextureInspectionRules' does not exist in the current context` and `CS0103: The name 'TextureFilterRule' does not exist in the current context`.

- [ ] **Step 4: Implement the minimal pure rule component**

Create `AssetStudio.GUI/Components/TextureInspectionRules.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace AssetStudio.GUI
{
    internal enum TextureFilterRule
    {
        All,
        NonBlockCompressed,
        Large,
        ReadWriteEnabled,
        MipmapsEnabled,
        SuspectedNpotCompressionFallback
    }

    internal static class TextureInspectionRules
    {
        internal static readonly int[] LargeTextureSizePresets = { 1024, 2048, 4096 };

        private static readonly HashSet<TextureFormat> NonBlockCompressedFormats = new()
        {
            TextureFormat.RGBA32,
            TextureFormat.ARGB32,
            TextureFormat.RGB24,
            TextureFormat.RGBA4444,
            TextureFormat.ARGB4444,
            TextureFormat.RGB565,
            TextureFormat.R16,
            TextureFormat.RGBAHalf,
            TextureFormat.RGBAFloat,
            TextureFormat.BGRA32,
            TextureFormat.RG16,
            TextureFormat.R8
        };

        internal static bool Matches(
            TextureFilterRule rule,
            TextureFormat format,
            int width,
            int height,
            bool isReadable,
            bool mipMap,
            int mipCount,
            int largeTextureThreshold)
        {
            var hasMipmaps = mipMap || mipCount > 1;
            return rule switch
            {
                TextureFilterRule.All => true,
                TextureFilterRule.NonBlockCompressed => NonBlockCompressedFormats.Contains(format),
                TextureFilterRule.Large => width >= largeTextureThreshold || height >= largeTextureThreshold,
                TextureFilterRule.ReadWriteEnabled => isReadable,
                TextureFilterRule.MipmapsEnabled => hasMipmaps,
                TextureFilterRule.SuspectedNpotCompressionFallback =>
                    format == TextureFormat.RGBA32 &&
                    hasMipmaps &&
                    (!IsPowerOfTwo(width) || !IsPowerOfTwo(height)),
                _ => true
            };
        }

        internal static string GetDisplayName(TextureFilterRule rule)
        {
            return rule switch
            {
                TextureFilterRule.All => "全部纹理",
                TextureFilterRule.NonBlockCompressed => "非块压缩格式",
                TextureFilterRule.Large => "大尺寸",
                TextureFilterRule.ReadWriteEnabled => "Read/Write 已开启",
                TextureFilterRule.MipmapsEnabled => "Mipmaps 已开启",
                TextureFilterRule.SuspectedNpotCompressionFallback => "疑似 NPOT 压缩退化",
                _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, null)
            };
        }

        internal static string GetDescription(TextureFilterRule rule, int largeTextureThreshold)
        {
            return rule switch
            {
                TextureFilterRule.All => "显示全部符合其他筛选条件的纹理",
                TextureFilterRule.NonBlockCompressed => "使用原始像素格式，可能增加包体或内存占用",
                TextureFilterRule.Large => $"宽或高任一边 ≥ {largeTextureThreshold}",
                TextureFilterRule.ReadWriteEnabled => "Unity Read/Write Enabled 已开启，可能保留 CPU 端副本",
                TextureFilterRule.MipmapsEnabled => "已启用或包含多个 Mipmap 层级",
                TextureFilterRule.SuspectedNpotCompressionFallback =>
                    "RGBA32、包含 Mipmaps，且宽或高不是 2 的幂",
                _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, null)
            };
        }

        internal static bool IsPowerOfTwo(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }
    }
}

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("AssetStudio.GUI.Tests")]
```

- [ ] **Step 5: Run the tests and verify all rule cases pass**

Run:

```powershell
dotnet test .\AssetStudio.GUI.Tests\AssetStudio.GUI.Tests.csproj -c Debug -f net8.0-windows
```

Expected: all tests in `TextureInspectionRulesTests` pass with zero failures.

- [ ] **Step 6: Review the task diff and create a checkpoint only if commits are authorized**

Run:

```powershell
git diff -- AssetStudio.sln AssetStudio.GUI/Components/TextureInspectionRules.cs AssetStudio.GUI.Tests
```

Expected: only the new rule component, test project, tests, and solution registration are shown.

If commits are explicitly authorized:

```powershell
git add AssetStudio.sln AssetStudio.GUI/Components/TextureInspectionRules.cs AssetStudio.GUI.Tests
git commit -m "test: define texture audit rules"
```

---

### Task 2: Add the contextual Texture2D filter UI to Asset List

**Files:**
- Modify: `AssetStudio.GUI/MainForm.Designer.cs:940-977`
- Modify: `AssetStudio.GUI/MainForm.cs:29-38,110-130,542-577,2441-2483`

**Interfaces:**
- Consumes: `TextureFilterRule`, `TextureInspectionRules.GetDisplayName`, `TextureInspectionRules.GetDescription`, and `TextureInspectionRules.LargeTextureSizePresets` from Task 1.
- Produces:
  - Explicit fields `textureRuleComboBox`, `textureSizePresetComboBox`, and `textureRuleDescriptionLabel`.
  - `InitializeAssetListFilterControls()` for one-time item population and named event binding.
  - `UpdateAssetTypeFilterControls()` for Texture/Mesh visibility.
  - `UpdateTextureFilterControls()` for large-size parameter visibility and rule description.
  - Named event handlers that trigger exactly one filter refresh per user action.

- [ ] **Step 1: Add an automated source regression test for removal-prone UI contracts**

Append the following test class to `AssetStudio.GUI.Tests/TextureInspectionRulesTests.cs`. It deliberately checks source text because the repository has no established WinForms UI-test harness and constructing `MainForm` initializes OpenGL/FMOD resources.

```csharp
using System.IO;
using System.Linq;

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
    }
}
```

Ensure the file-level using block includes:

```csharp
using System;
using System.IO;
```

Remove the unused `using System.Linq;` from the snippet if the compiler reports it; it is not required by the final test.

- [ ] **Step 2: Run the UI source tests and verify they fail before implementation**

Run:

```powershell
dotnet test .\AssetStudio.GUI.Tests\AssetStudio.GUI.Tests.csproj -c Debug -f net8.0-windows --filter TextureFilterUiSourceTests
```

Expected: failures report missing `textureRuleComboBox`, `UpdateAssetTypeFilterControls`, and named handlers; the legacy lambda-unsubscription assertion also fails.

- [ ] **Step 3: Replace local Asset List filter controls with explicit fields and add the third row**

In `MainForm.Designer.cs`, replace the local declarations for the existing type, total-size, texture-format, and size controls with assignments to fields:

```csharp
var filterPanel = new Panel() { Dock = DockStyle.Top, Height = 75 };

var typeFilterPanel = new Panel() { Dock = DockStyle.Top, Height = 25 };
classIDTypeComboBox = new ComboBox()
{
    Dock = DockStyle.Left,
    Width = 250,
    DropDownStyle = ComboBoxStyle.DropDownList
};
totalSizeLabel = new Label()
{
    Dock = DockStyle.Fill,
    TextAlign = System.Drawing.ContentAlignment.MiddleRight
};
typeFilterPanel.Controls.Add(totalSizeLabel);
typeFilterPanel.Controls.Add(classIDTypeComboBox);

var subFilterPanel = new Panel() { Dock = DockStyle.Top, Height = 25 };
textureFormatComboBox = new ComboBox()
{
    Dock = DockStyle.Left,
    Width = 250,
    DropDownStyle = ComboBoxStyle.DropDownList,
    Visible = false
};
var sizeFilterLabel = new Label()
{
    Text = "Size >",
    Dock = DockStyle.Left,
    Width = 50,
    TextAlign = System.Drawing.ContentAlignment.MiddleLeft
};
sizeFilterTextBox = new TextBox() { Text = "0", Dock = DockStyle.Left, Width = 50 };
var sizeFilterUnitLabel = new Label()
{
    Text = "MB",
    Dock = DockStyle.Left,
    Width = 30,
    TextAlign = System.Drawing.ContentAlignment.MiddleLeft
};

var textureRulePanel = new Panel() { Dock = DockStyle.Top, Height = 25, Visible = false };
var textureRuleLabel = new Label()
{
    Text = "检测",
    Dock = DockStyle.Left,
    Width = 45,
    TextAlign = System.Drawing.ContentAlignment.MiddleLeft
};
textureRuleComboBox = new ComboBox()
{
    Dock = DockStyle.Left,
    Width = 175,
    DropDownStyle = ComboBoxStyle.DropDownList
};
textureSizePresetComboBox = new ComboBox()
{
    Dock = DockStyle.Left,
    Width = 80,
    DropDownStyle = ComboBoxStyle.DropDownList,
    Visible = false
};
textureRuleDescriptionLabel = new Label()
{
    Dock = DockStyle.Fill,
    AutoEllipsis = true,
    TextAlign = System.Drawing.ContentAlignment.MiddleLeft
};
var textureRuleDescriptionToolTip = new ToolTip();
textureRuleDescriptionToolTip.SetToolTip(textureRuleDescriptionLabel, string.Empty);

textureRulePanel.Controls.Add(textureRuleDescriptionLabel);
textureRulePanel.Controls.Add(textureSizePresetComboBox);
textureRulePanel.Controls.Add(textureRuleComboBox);
textureRulePanel.Controls.Add(textureRuleLabel);
```

Keep the current Mesh checkboxes in `subFilterPanel`. Add panels in dock order so the visual order is type row, format/size row, rule row:

```csharp
filterPanel.Controls.Add(textureRulePanel);
filterPanel.Controls.Add(subFilterPanel);
filterPanel.Controls.Add(typeFilterPanel);
```

Store `textureRulePanel` and `textureRuleDescriptionToolTip` as fields too, because `MainForm` must toggle the row and update the full tooltip without searching `Controls[index]`:

```csharp
private Panel textureRulePanel;
private ToolTip textureRuleDescriptionToolTip;
```

Assign those fields instead of local variables in the Designer block.

- [ ] **Step 4: Declare the filter fields in MainForm and remove constructor index lookup**

At the top of `MainForm.cs`, keep the existing explicit Mesh fields and replace the old texture-tab fields with:

```csharp
private ComboBox classIDTypeComboBox;
private Label totalSizeLabel;
private TextBox sizeFilterTextBox;
private ComboBox textureFormatComboBox;
private Panel textureRulePanel;
private ComboBox textureRuleComboBox;
private ComboBox textureSizePresetComboBox;
private Label textureRuleDescriptionLabel;
private ToolTip textureRuleDescriptionToolTip;
private CheckBox meshDoubleSidedCheckBox;
private CheckBox meshReadWriteCheckBox;
private Dictionary<Mesh, bool> meshDoubleSidedCache;
private bool updatingAssetListFilters;
```

Delete the constructor block that walks `tabPage2.Controls[0]`, `filterPanel.Controls[...]`, and `subFilterPanel.Controls[...]`. Also delete the three constructor fields/bindings for `uncompressedTexturesListView`, `uncompressedTotalLabel`, and `textureDetectionComboBox`; the complete old-tab removal happens in Task 4.

Immediately after `InitializeComponent();`, call:

```csharp
InitializeAssetListFilterControls();
```

- [ ] **Step 5: Populate filter controls and bind named handlers exactly once**

Add the following methods near `UpdateTextureFormatComboBox()`:

```csharp
private void InitializeAssetListFilterControls()
{
    updatingAssetListFilters = true;
    try
    {
        textureRuleComboBox.Items.Clear();
        foreach (TextureFilterRule rule in Enum.GetValues(typeof(TextureFilterRule)))
        {
            textureRuleComboBox.Items.Add(TextureInspectionRules.GetDisplayName(rule));
        }
        textureRuleComboBox.SelectedIndex = (int)TextureFilterRule.All;

        textureSizePresetComboBox.Items.Clear();
        foreach (var size in TextureInspectionRules.LargeTextureSizePresets)
        {
            textureSizePresetComboBox.Items.Add($"≥{size}");
        }
        textureSizePresetComboBox.SelectedIndex = 1;

        textureFormatComboBox.Items.Clear();
        textureFormatComboBox.Items.Add("All");
        textureFormatComboBox.SelectedIndex = 0;
    }
    finally
    {
        updatingAssetListFilters = false;
    }

    classIDTypeComboBox.SelectedIndexChanged += classIDTypeComboBox_SelectedIndexChanged;
    textureFormatComboBox.SelectedIndexChanged += textureFormatComboBox_SelectedIndexChanged;
    textureRuleComboBox.SelectedIndexChanged += textureRuleComboBox_SelectedIndexChanged;
    textureSizePresetComboBox.SelectedIndexChanged += textureSizePresetComboBox_SelectedIndexChanged;
    sizeFilterTextBox.TextChanged += sizeFilterTextBox_TextChanged;
    meshDoubleSidedCheckBox.CheckedChanged += meshFilterCheckBox_CheckedChanged;
    meshReadWriteCheckBox.CheckedChanged += meshFilterCheckBox_CheckedChanged;

    UpdateAssetTypeFilterControls();
}

private void classIDTypeComboBox_SelectedIndexChanged(object sender, EventArgs e)
{
    if (updatingAssetListFilters)
    {
        return;
    }

    UpdateTextureFormatComboBox();
    UpdateAssetTypeFilterControls();
    FilterAssetList();
}

private void textureFormatComboBox_SelectedIndexChanged(object sender, EventArgs e)
{
    if (!updatingAssetListFilters)
    {
        FilterAssetList();
    }
}

private void textureRuleComboBox_SelectedIndexChanged(object sender, EventArgs e)
{
    if (updatingAssetListFilters)
    {
        return;
    }

    UpdateTextureFilterControls();
    FilterAssetList();
}

private void textureSizePresetComboBox_SelectedIndexChanged(object sender, EventArgs e)
{
    if (updatingAssetListFilters)
    {
        return;
    }

    UpdateTextureFilterControls();
    FilterAssetList();
}

private void sizeFilterTextBox_TextChanged(object sender, EventArgs e)
{
    if (!updatingAssetListFilters)
    {
        FilterAssetList();
    }
}

private void meshFilterCheckBox_CheckedChanged(object sender, EventArgs e)
{
    if (!updatingAssetListFilters)
    {
        FilterAssetList();
    }
}
```

Remove the constructor lambdas currently binding these controls.

- [ ] **Step 6: Implement contextual visibility and description updates**

Replace `UpdateMeshFilterCheckBoxes()` with one unified method:

```csharp
private void UpdateAssetTypeFilterControls()
{
    var selectedText = classIDTypeComboBox.SelectedItem?.ToString();
    var isTexture = selectedText != null && selectedText.StartsWith("Texture2D (");
    var isMesh = selectedText != null && selectedText.StartsWith("Mesh (");

    textureFormatComboBox.Visible = isTexture;
    textureRulePanel.Visible = isTexture;
    meshDoubleSidedCheckBox.Visible = isMesh;
    meshReadWriteCheckBox.Visible = isMesh;

    if (isTexture)
    {
        UpdateTextureFilterControls();
    }
}

private void UpdateTextureFilterControls()
{
    var rule = GetSelectedTextureRule();
    var threshold = GetSelectedLargeTextureThreshold();
    var description = TextureInspectionRules.GetDescription(rule, threshold);

    textureSizePresetComboBox.Visible = rule == TextureFilterRule.Large;
    textureRuleDescriptionLabel.Text = description;
    textureRuleDescriptionToolTip.SetToolTip(textureRuleDescriptionLabel, description);
}

private TextureFilterRule GetSelectedTextureRule()
{
    return Enum.IsDefined(typeof(TextureFilterRule), textureRuleComboBox.SelectedIndex)
        ? (TextureFilterRule)textureRuleComboBox.SelectedIndex
        : TextureFilterRule.All;
}

private int GetSelectedLargeTextureThreshold()
{
    var index = textureSizePresetComboBox.SelectedIndex;
    return index >= 0 && index < TextureInspectionRules.LargeTextureSizePresets.Length
        ? TextureInspectionRules.LargeTextureSizePresets[index]
        : 2048;
}
```

`UpdateTextureFormatComboBox()` must no longer hide/show controls or detach/reattach lambdas. It should only rebuild the format items while suppressing events:

```csharp
private void UpdateTextureFormatComboBox()
{
    var selectedText = classIDTypeComboBox.SelectedItem?.ToString();
    if (selectedText == null || !selectedText.StartsWith("Texture2D ("))
    {
        return;
    }

    var selectedFormat = textureFormatComboBox.SelectedItem?.ToString();
    var textureFormats = exportableAssets
        .Where(x => x.Type == ClassIDType.Texture2D && x.Asset is Texture2D)
        .Select(x => ((Texture2D)x.Asset).m_TextureFormat)
        .GroupBy(format => format)
        .Select(group => new { Format = group.Key, Count = group.Count() })
        .OrderByDescending(item => item.Count)
        .ToList();

    updatingAssetListFilters = true;
    try
    {
        textureFormatComboBox.Items.Clear();
        textureFormatComboBox.Items.Add("All");
        foreach (var format in textureFormats)
        {
            textureFormatComboBox.Items.Add($"{format.Format} ({format.Count})");
        }

        var selectedIndex = selectedFormat == null
            ? -1
            : textureFormatComboBox.Items.IndexOf(selectedFormat);
        textureFormatComboBox.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
    }
    finally
    {
        updatingAssetListFilters = false;
    }
}
```

- [ ] **Step 7: Replace load-time lambda rebinding with state initialization**

In `BuildAssetStructures()`, after rebuilding `classIDTypeComboBox.Items`, replace the lambda detach/attach block and texture-format reset with:

```csharp
updatingAssetListFilters = true;
try
{
    classIDTypeComboBox.SelectedIndex = 0;
    textureFormatComboBox.Items.Clear();
    textureFormatComboBox.Items.Add("All");
    textureFormatComboBox.SelectedIndex = 0;
    textureRuleComboBox.SelectedIndex = (int)TextureFilterRule.All;
    textureSizePresetComboBox.SelectedIndex = 1;
    meshDoubleSidedCheckBox.Checked = false;
    meshReadWriteCheckBox.Checked = false;
}
finally
{
    updatingAssetListFilters = false;
}

meshDoubleSidedCache = null;
UpdateAssetTypeFilterControls();
FilterAssetList();
```

This intentionally resets texture state for a newly loaded resource set while preserving it during type changes inside the same load.

- [ ] **Step 8: Run the source tests and build the GUI**

Run:

```powershell
dotnet test .\AssetStudio.GUI.Tests\AssetStudio.GUI.Tests.csproj -c Debug -f net8.0-windows --filter TextureFilterUiSourceTests
dotnet build .\AssetStudio.GUI\AssetStudio.GUI.csproj -c Debug -f net8.0-windows
```

Expected: source tests pass and the GUI project builds with zero errors. Warnings already present before this task may remain, but record them for comparison.

- [ ] **Step 9: Review the task diff and create a checkpoint only if commits are authorized**

Run:

```powershell
git diff -- AssetStudio.GUI/MainForm.Designer.cs AssetStudio.GUI/MainForm.cs AssetStudio.GUI.Tests/TextureInspectionRulesTests.cs
```

Expected: Asset List receives explicit filter fields and one new contextual row; no standalone texture tab is removed yet, and unrelated Mesh/APK logic is preserved.

If commits are explicitly authorized:

```powershell
git add AssetStudio.GUI/MainForm.Designer.cs AssetStudio.GUI/MainForm.cs AssetStudio.GUI.Tests/TextureInspectionRulesTests.cs
git commit -m "feat: add texture filters to asset list"
```

---

### Task 3: Compose texture rules into the existing Asset List filter

**Files:**
- Modify: `AssetStudio.GUI/MainForm.cs:2693-2795`
- Modify: `AssetStudio.GUI.Tests/TextureInspectionRulesTests.cs`

**Interfaces:**
- Consumes: `TextureInspectionRules.Matches(...)`, `GetSelectedTextureRule()`, and `GetSelectedLargeTextureThreshold()`.
- Produces: `MatchesSelectedTextureRule(Texture2D texture)` and an updated `FilterAssetList()` that keeps original `AssetItem` references in `visibleAssets`.

- [ ] **Step 1: Add source tests that pin the Asset List integration contract**

Append to `TextureFilterUiSourceTests`:

```csharp
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
```

- [ ] **Step 2: Run the integration source tests and verify failure**

Run:

```powershell
dotnet test .\AssetStudio.GUI.Tests\AssetStudio.GUI.Tests.csproj -c Debug -f net8.0-windows --filter TextureFilterUiSourceTests
```

Expected: `AssetListComposesTextureRuleWithExistingFilters` fails because `MatchesSelectedTextureRule` and the predicate call do not yet exist.

- [ ] **Step 3: Add the Texture2D adapter to MainForm**

Add near the filter helpers:

```csharp
private bool MatchesSelectedTextureRule(Texture2D texture)
{
    return TextureInspectionRules.Matches(
        GetSelectedTextureRule(),
        texture.m_TextureFormat,
        texture.m_Width,
        texture.m_Height,
        texture.m_IsReadable,
        texture.m_MipMap,
        texture.m_MipCount,
        GetSelectedLargeTextureThreshold());
}
```

This is the only `MainForm` method that translates `Texture2D` fields into the pure rule component.

- [ ] **Step 4: Apply format and audit predicates in the Texture2D branch**

Replace the current Texture2D format-only section in `FilterAssetList()` with:

```csharp
if (type == ClassIDType.Texture2D)
{
    var formatText = textureFormatComboBox.SelectedItem?.ToString();
    if (formatText != null && formatText != "All")
    {
        var formatName = formatText.Substring(0, formatText.IndexOf(" (", StringComparison.Ordinal));
        var textureFormat = (TextureFormat)Enum.Parse(typeof(TextureFormat), formatName);
        visibleAssets = visibleAssets.FindAll(x =>
            x.Asset is Texture2D texture && texture.m_TextureFormat == textureFormat);
    }

    visibleAssets = visibleAssets.FindAll(x =>
        x.Asset is Texture2D texture && MatchesSelectedTextureRule(texture));
}
```

Do not use `textureFormatComboBox.Visible` as a business-logic guard. The selected type is the source of truth; visibility is only presentation.

Keep the existing Mesh filters, regex search, and `FullSize` filter after the type-specific conditions. Do not copy or recreate `AssetItem` objects.

- [ ] **Step 5: Run all rule and integration tests**

Run:

```powershell
dotnet test .\AssetStudio.GUI.Tests\AssetStudio.GUI.Tests.csproj -c Debug -f net8.0-windows
```

Expected: all tests pass.

- [ ] **Step 6: Build both GUI target frameworks**

Run:

```powershell
dotnet build .\AssetStudio.GUI\AssetStudio.GUI.csproj -c Debug -f net7.0-windows
dotnet build .\AssetStudio.GUI\AssetStudio.GUI.csproj -c Debug -f net8.0-windows
```

Expected: both builds finish with zero errors.

- [ ] **Step 7: Review the task diff and create a checkpoint only if commits are authorized**

Run:

```powershell
git diff -- AssetStudio.GUI/MainForm.cs AssetStudio.GUI.Tests/TextureInspectionRulesTests.cs
```

Expected: only the texture predicate adapter, the Texture2D filter branch, and source tests are added; `visibleAssets` still holds references from `exportableAssets`.

If commits are explicitly authorized:

```powershell
git add AssetStudio.GUI/MainForm.cs AssetStudio.GUI.Tests/TextureInspectionRulesTests.cs
git commit -m "feat: apply texture audit rules in asset list"
```

---

### Task 4: Remove the standalone texture-audit tab and duplicate data path

**Files:**
- Modify: `AssetStudio.GUI/MainForm.Designer.cs:1193-1262`
- Modify: `AssetStudio.GUI/MainForm.cs:36-38,52,159-164,516-517,828-838,1185-1245,2003-2057,3868-4145`
- Modify: `AssetStudio.GUI.Tests/TextureInspectionRulesTests.cs`

**Interfaces:**
- Consumes: Asset List texture filtering from Tasks 2 and 3.
- Produces: one texture result surface (`assetListView`/`visibleAssets`) with no old `uncompressedTextures*`, `textureDetectionComboBox`, `PerformTextureDetection`, or `Detect*Textures` code.

- [ ] **Step 1: Add source tests that require complete legacy removal**

Append to `TextureFilterUiSourceTests`:

```csharp
[TestMethod]
public void StandaloneTextureAuditTabAndDuplicatePipelineAreRemoved()
{
    var root = FindRepositoryRoot();
    var designer = File.ReadAllText(Path.Combine(root, "AssetStudio.GUI", "MainForm.Designer.cs"));
    var mainForm = File.ReadAllText(Path.Combine(root, "AssetStudio.GUI", "MainForm.cs"));

    Assert.IsFalse(designer.Contains("纹理专项检测"));
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
```

- [ ] **Step 2: Run the removal test and verify failure**

Run:

```powershell
dotnet test .\AssetStudio.GUI.Tests\AssetStudio.GUI.Tests.csproj -c Debug -f net8.0-windows --filter StandaloneTextureAuditTabAndDuplicatePipelineAreRemoved
```

Expected: failure because the old Designer tab and old methods still exist.

- [ ] **Step 3: Delete the standalone texture tab from the Designer**

Remove the complete block that creates:

```text
tabPage7
uncompressedTexturesPanel
uncompressedTexturesListView
textureDetectionComboBox
uncompressedTotalLabel
uncompressedColumnHeader1..6
```

Delete the `tabControl1.Controls.Add(tabPage7);` call, but preserve `tabControl1.Controls.Add(tabPage8);` and all AB-special-audit setup.

After deletion, do not renumber or reindex AB/redundant tabs by numeric access in new code. Existing unrelated index access may remain if changing it is outside this feature, but verify that deleting the dynamically created final tab does not alter indexes used for the earlier static tabs.

- [ ] **Step 4: Delete old texture-tab fields, constructor lookup, and collection**

Remove these declarations from `MainForm.cs`:

```csharp
private ListView uncompressedTexturesListView;
private Label uncompressedTotalLabel;
private ComboBox textureDetectionComboBox;
private List<AssetItem> uncompressedTextures = new List<AssetItem>();
```

Delete the constructor block that retrieves `tabControl1.TabPages[5]` and its controls.

- [ ] **Step 5: Delete old build, virtual-list, sort, reset, and scan paths**

Remove each of the following in full:

```text
BuildAssetStructures() call to PerformTextureDetection()
uncompressedTexturesListView_RetrieveVirtualItem(...)
uncompressedTexturesListView_ColumnClick(...)
textureDetectionComboBox_SelectedIndexChanged(...)
PerformTextureDetection()
DetectUncompressedTextures()
Detect2048x4096Textures()
DetectReadWriteEnabledTextures()
DetectMipmapEnabledTextures()
DetectInvalidCompressionTextures()
MainForm.IsPowerOfTwo(int)
```

`TextureInspectionRules.IsPowerOfTwo(int)` from Task 1 is the remaining implementation.

In `ResetForm()`, remove old texture-list clearing and replace the texture filter reset section with a guarded reset:

```csharp
updatingAssetListFilters = true;
try
{
    textureFormatComboBox.Items.Clear();
    textureFormatComboBox.Items.Add("All");
    textureFormatComboBox.SelectedIndex = 0;
    textureRuleComboBox.SelectedIndex = (int)TextureFilterRule.All;
    textureSizePresetComboBox.SelectedIndex = 1;

    meshDoubleSidedCheckBox.Checked = false;
    meshReadWriteCheckBox.Checked = false;
}
finally
{
    updatingAssetListFilters = false;
}

textureFormatComboBox.Visible = false;
textureRulePanel.Visible = false;
meshDoubleSidedCheckBox.Visible = false;
meshReadWriteCheckBox.Visible = false;
UpdateTextureFilterControls();
```

- [ ] **Step 6: Search for dangling legacy references**

Run:

```powershell
git grep -n -E "uncompressedTextures|textureDetectionComboBox|PerformTextureDetection|Detect(Uncompressed|2048x4096|ReadWriteEnabled|MipmapEnabled|InvalidCompression)Textures|纹理专项检测" -- AssetStudio.GUI
```

Expected: no output. If output remains, remove only references belonging to the deleted texture tab; do not remove unrelated documentation or test assertions that intentionally mention the old names.

- [ ] **Step 7: Run all tests and both framework builds**

Run:

```powershell
dotnet test .\AssetStudio.GUI.Tests\AssetStudio.GUI.Tests.csproj -c Debug -f net8.0-windows
dotnet build .\AssetStudio.GUI\AssetStudio.GUI.csproj -c Debug -f net7.0-windows
dotnet build .\AssetStudio.GUI\AssetStudio.GUI.csproj -c Debug -f net8.0-windows
```

Expected: all tests pass and both GUI targets build with zero errors.

- [ ] **Step 8: Review the task diff and create a checkpoint only if commits are authorized**

Run:

```powershell
git diff -- AssetStudio.GUI/MainForm.Designer.cs AssetStudio.GUI/MainForm.cs AssetStudio.GUI.Tests/TextureInspectionRulesTests.cs
```

Expected: the old tab and duplicate data path are gone; AB-special-audit, duplicate-resource, Mesh, APK, and other unrelated code remains intact.

If commits are explicitly authorized:

```powershell
git add AssetStudio.GUI/MainForm.Designer.cs AssetStudio.GUI/MainForm.cs AssetStudio.GUI.Tests/TextureInspectionRulesTests.cs
git commit -m "refactor: remove standalone texture audit tab"
```

---

### Task 5: Verify state behavior and user-visible workflow

**Files:**
- Modify if necessary: `AssetStudio.GUI/MainForm.cs`
- Modify if necessary: `AssetStudio.GUI/MainForm.Designer.cs`
- Modify if necessary: `AssetStudio.GUI.Tests/TextureInspectionRulesTests.cs`

**Interfaces:**
- Consumes: completed Asset List texture filter implementation.
- Produces: verified reset/preservation behavior and an acceptance record suitable for handoff.

- [ ] **Step 1: Add source tests for reset defaults and state-preserving type changes**

Append to `TextureFilterUiSourceTests`:

```csharp
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
```

- [ ] **Step 2: Run the full automated suite**

Run:

```powershell
dotnet test .\AssetStudio.GUI.Tests\AssetStudio.GUI.Tests.csproj -c Release -f net8.0-windows
```

Expected: all tests pass.

- [ ] **Step 3: Perform release builds for both supported GUI frameworks**

Run:

```powershell
dotnet build .\AssetStudio.GUI\AssetStudio.GUI.csproj -c Release -f net7.0-windows
dotnet build .\AssetStudio.GUI\AssetStudio.GUI.csproj -c Release -f net8.0-windows
```

Expected: both builds finish with zero errors. If full-solution build is attempted, note that the native FBX project requires Visual Studio C++ tools and Autodesk FBX SDK 2020.3.2; GUI-project builds are the required gate for this feature.

- [ ] **Step 4: Launch the net8 GUI with a representative Unity asset set**

Run:

```powershell
dotnet run --project .\AssetStudio.GUI\AssetStudio.GUI.csproj -c Debug -f net8.0-windows
```

Load a resource set known to contain:

- at least one RGBA32 texture;
- at least one DXT/ETC/ASTC texture;
- textures below and at/above 1024, 2048, and 4096 boundaries;
- at least one readable texture;
- textures with and without mipmaps;
- at least one RGBA32 + mipmaps + NPOT texture;
- at least one Mesh so Texture/Mesh visibility can be compared.

- [ ] **Step 5: Verify the UI visibility and rule explanations manually**

Check each item and record pass/fail:

```text
[ ] The “纹理专项检测” tab is absent.
[ ] Selecting Texture2D shows format, rule, and description controls.
[ ] Selecting Mesh hides Texture controls and shows the existing Mesh controls.
[ ] Selecting another type hides both Texture and Mesh type-specific controls.
[ ] Only “大尺寸” shows the ≥1024/≥2048/≥4096 preset control.
[ ] The description changes to the exact accepted wording for every rule.
[ ] A long description is available through the label tooltip when visually truncated.
```

- [ ] **Step 6: Verify filtering and state behavior manually**

Check each item and record pass/fail:

```text
[ ] “全部纹理” leaves all textures that satisfy other filters visible.
[ ] “非块压缩格式” includes the legacy raw-pixel format set and excludes DXT1.
[ ] “大尺寸” uses inclusive either-dimension boundaries at 1024, 2048, and 4096.
[ ] Read/Write uses Unity m_IsReadable semantics.
[ ] Mipmaps matches m_MipMap or m_MipCount > 1.
[ ] NPOT matches only RGBA32 + mipmaps + at least one non-power-of-two dimension.
[ ] Rule + specific texture format applies as AND.
[ ] Rule + Size > N MB applies as AND.
[ ] Rule + name/container/PathID search applies as AND.
[ ] Switching away from Texture2D and back preserves the selected rule and size preset.
[ ] Switching away from “大尺寸” and back preserves its selected preset.
[ ] Reset or loading a new resource set restores 全部纹理, All format, and ≥2048.
```

- [ ] **Step 7: Verify existing Asset List actions and nearby features manually**

Check each item and record pass/fail:

```text
[ ] Texture preview and Ctrl+R/G/B/A channel toggles still work.
[ ] Asset information display still works.
[ ] Asset List column sorting still works.
[ ] Right-click Copy text, Export selected, Go to scene hierarchy, and Show original file work where applicable.
[ ] Export Selected exports Asset List selection.
[ ] Export Filtered exports visibleAssets after all composed filters.
[ ] Existing Mesh Read/Write and 双面渲染 filters still work.
[ ] Duplicate-resource and AB-special-audit tabs still work.
```

- [ ] **Step 8: Inspect the final diff for scope and unrelated-change preservation**

Run:

```powershell
git status --short
git diff --check
git diff --stat
git diff -- AssetStudio.GUI/MainForm.cs AssetStudio.GUI/MainForm.Designer.cs AssetStudio.GUI/Components/TextureInspectionRules.cs AssetStudio.GUI.Tests AssetStudio.sln docs/superpowers/specs/2026-11-04-texture-audit-assetlist-design.md
```

Expected:

- `git diff --check` reports no whitespace errors.
- Existing unrelated modifications in `AssetStudio/BundleFile.cs`, `AssetStudio/Classes/Mesh.cs`, `AssetStudio/FileReader.cs`, APK files, and existing Mesh code are not reverted.
- Feature changes are limited to the file map above.

- [ ] **Step 9: Create the final checkpoint only if commits are authorized**

If commits are explicitly authorized:

```powershell
git add AssetStudio.sln AssetStudio.GUI/MainForm.cs AssetStudio.GUI/MainForm.Designer.cs AssetStudio.GUI/Components/TextureInspectionRules.cs AssetStudio.GUI.Tests docs/superpowers/specs/2026-11-04-texture-audit-assetlist-design.md docs/superpowers/plans/2026-11-04-texture-audit-assetlist.md
git commit -m "feat: integrate texture audit into asset list"
```

If commits are not authorized, leave the verified working tree uncommitted and report the exact modified/untracked files.

---

## Final Acceptance Summary

Implementation is complete only when all of the following are true:

1. The standalone texture-audit tab and its duplicate collection, sorting, scan, and selection code are absent.
2. Asset List is the sole texture result surface and retains original `AssetItem` references.
3. The six single-select entries and three size presets have the accepted names and exact predicates.
4. Texture format, rule, search, and resource-size conditions compose with AND semantics.
5. Texture state is preserved across type changes and reset on Reset/new load.
6. Automated tests pass, net7/net8 GUI builds pass, and the manual WinForms acceptance checklist passes.
7. Existing uncommitted work outside this feature remains untouched.
