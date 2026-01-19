
using Newtonsoft.Json;
using OpenTK.Audio.OpenAL;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.WinForms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using static AssetStudio.GUI.Studio;

namespace AssetStudio.GUI
{
    partial class MainForm : Form
    {
        private ComboBox classIDTypeComboBox;
        private Label totalSizeLabel;
        private TextBox sizeFilterTextBox;
        private ComboBox textureFormatComboBox;
        private ListView uncompressedTexturesListView;
        private Label uncompressedTotalLabel;
        private ComboBox textureDetectionComboBox;
        private ComboBox duplicateDetectionComboBox;
        private Label redundantTotalLabel;
        private TextBox redundantSearch;
        private TextBox fileSizeFilterTextBox;
        private TextBox countFilterTextBox;
        private TextBox redundantFilterTextBox;
        private TreeView assetBundleTreeView;
        private Label assetBundleTotalLabel;
        private TextBox assetBundleSearch;
        private ComboBox assetBundleSortComboBox;
        private TextBox bundleSizeFilterTextBox;
        private System.Timers.Timer bundleSizeFilterTimer;
        private Dictionary<string, List<AssetItem>> assetBundleDict = new Dictionary<string, List<AssetItem>>();
        private List<AssetItem> uncompressedTextures = new List<AssetItem>();
        private List<AssetItem> allRedundantAssets = new List<AssetItem>(); // 存储所有重复资源，用于筛选
        private AssetItem lastSelectedItem;
        private AssetBrowser assetBrowser;
        private DirectBitmap imageTexture;
        private string tempClipboard;

        private FMOD.System system;
        private FMOD.Sound sound;
        private FMOD.Channel channel;
        private FMOD.SoundGroup masterSoundGroup;
        private FMOD.MODE loopMode = FMOD.MODE.LOOP_OFF;
        private uint FMODlenms;
        private float FMODVolume = 0.8f;

        #region TexControl
        private static char[] textureChannelNames = new[] { 'B', 'G', 'R', 'A' };
        private bool[] textureChannels = new[] { true, true, true, true };
        #endregion

        #region GLControl
        private bool glControlLoaded;
        private int mdx, mdy;
        private bool lmdown, rmdown;
        private int pgmID, pgmColorID, pgmBlackID;
        private int attributeVertexPosition;
        private int attributeNormalDirection;
        private int attributeVertexColor;
        private int uniformModelMatrix;
        private int uniformViewMatrix;
        private int uniformProjMatrix;
        private int vao;
        private OpenTK.Mathematics.Vector3[] vertexData;
        private OpenTK.Mathematics.Vector3[] normalData;
        private OpenTK.Mathematics.Vector3[] normal2Data;
        private OpenTK.Mathematics.Vector4[] colorData;
        private Matrix4 modelMatrixData;
        private Matrix4 viewMatrixData;
        private Matrix4 projMatrixData;
        private int[] indiceData;
        private int wireFrameMode;
        private int shadeMode;
        private int normalMode;
        #endregion

        //asset list sorting
        private int sortColumn = -1;
        private bool reverseSort;

        //tree search
        private int nextGObject;
        private List<TreeNode> treeSrcResults = new List<TreeNode>();

        private string openDirectoryBackup = string.Empty;
        private string saveDirectoryBackup = string.Empty;

        private GUILogger logger;

        public MainForm()
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            InitializeComponent();

           var tabPage2Panel = (Panel)tabPage2.Controls[0];
           var filterPanel = (Panel)tabPage2Panel.Controls[2];
           var typeFilterPanel = (Panel)filterPanel.Controls[0];
           var subFilterPanel = (Panel)filterPanel.Controls[1];
           
           classIDTypeComboBox = (ComboBox)typeFilterPanel.Controls[1];
           totalSizeLabel = (Label)typeFilterPanel.Controls[0];
           textureFormatComboBox = (ComboBox)subFilterPanel.Controls[3];
           sizeFilterTextBox = (TextBox)subFilterPanel.Controls[1];
           
           // 绑定筛选事件
           sizeFilterTextBox.TextChanged += (s, e) => FilterAssetList();
           textureFormatComboBox.SelectedIndexChanged += (s, e) => FilterAssetList();
           
           // 获取AB专项检测TreeView、Label、搜索框、排序下拉框和筛选输入框
           var tabPage8 = tabControl1.TabPages[4];
           var assetBundlePanel = (Panel)tabPage8.Controls[0];
           assetBundleTreeView = (TreeView)assetBundlePanel.Controls[0];
           assetBundleSearch = (TextBox)assetBundlePanel.Controls[1];
           var assetBundleFilterPanel = (FlowLayoutPanel)assetBundlePanel.Controls[2];
           assetBundleTotalLabel = (Label)assetBundlePanel.Controls[3];
           
           assetBundleSortComboBox = (ComboBox)assetBundleFilterPanel.Controls[0];
           bundleSizeFilterTextBox = (TextBox)assetBundleFilterPanel.Controls[2];
           
           // 初始化防抖Timer
           bundleSizeFilterTimer = new System.Timers.Timer(500); // 500毫秒延迟
           bundleSizeFilterTimer.AutoReset = false;
           bundleSizeFilterTimer.Elapsed += (s, e) =>
           {
               if (InvokeRequired)
               {
                   BeginInvoke(new Action(FilterAssetBundleTree));
               }
               else
               {
                   FilterAssetBundleTree();
               }
           };
           
           assetBundleSearch.KeyPress += assetBundleSearch_KeyPress;
           
           // 获取纹理专项检测ListView、Label和ComboBox
           var tabPage7 = tabControl1.TabPages[5];
           var uncompressedPanel = (Panel)tabPage7.Controls[0];
           uncompressedTexturesListView = (ListView)uncompressedPanel.Controls[0];
           uncompressedTotalLabel = (Label)uncompressedPanel.Controls[1];
           textureDetectionComboBox = (ComboBox)uncompressedPanel.Controls[2];
           
           // 获取重复资源检测下拉框、Label、搜索框和筛选输入框
           var tabPage6 = tabControl1.TabPages[2];
           var redundanteRessourcenPanel = (Panel)tabPage6.Controls[0];
           duplicateDetectionComboBox = (ComboBox)redundanteRessourcenPanel.Controls[4];
           redundantTotalLabel = (Label)redundanteRessourcenPanel.Controls[3];
           redundantSearch = (TextBox)redundanteRessourcenPanel.Controls[1];
           var redundantFilterPanel = (Panel)redundanteRessourcenPanel.Controls[2];
           fileSizeFilterTextBox = (TextBox)redundantFilterPanel.Controls[1];
           countFilterTextBox = (TextBox)redundantFilterPanel.Controls[4];
           redundantFilterTextBox = (TextBox)redundantFilterPanel.Controls[6];
           
           duplicateDetectionComboBox.SelectedIndexChanged += new EventHandler(duplicateDetectionComboBox_SelectedIndexChanged);
           fileSizeFilterTextBox.TextChanged += new EventHandler(FilterRedundantAssets);
           countFilterTextBox.TextChanged += new EventHandler(FilterRedundantAssets);
           redundantFilterTextBox.TextChanged += new EventHandler(FilterRedundantAssets);

           Text = $"Studio v{Application.ProductVersion}";
            InitializeExportOptions();
            InitializeProgressBar();
            InitializeLogger();
            InitalizeOptions();
            FMODinit();
        }

        private void InitializeExportOptions()
        {
            enableConsole.Checked = Properties.Settings.Default.enableConsole;
            enableFileLogging.Checked = Properties.Settings.Default.enableFileLogging;
            displayAll.Checked = Properties.Settings.Default.displayAll;
            displayInfo.Checked = Properties.Settings.Default.displayInfo;
            enablePreview.Checked = Properties.Settings.Default.enablePreview;
            enableModelPreview.Checked = Properties.Settings.Default.enableModelPreview;
            modelsOnly.Checked = Properties.Settings.Default.modelsOnly;
            enableResolveDependencies.Checked = Properties.Settings.Default.enableResolveDependencies;
            allowDuplicates.Checked = Properties.Settings.Default.allowDuplicates;
            skipContainer.Checked = Properties.Settings.Default.skipContainer;
            assetsManager.ResolveDependencies = enableResolveDependencies.Checked;
            SkipContainer = Properties.Settings.Default.skipContainer;
            MiHoYoBinData.Encrypted = Properties.Settings.Default.encrypted;
            MiHoYoBinData.Key = Properties.Settings.Default.key;
            AssetsHelper.Minimal = Properties.Settings.Default.minimalAssetMap;
        }

        private void InitializeLogger()
        {
            logger = new GUILogger(StatusStripUpdate);
            ConsoleHelper.AllocConsole();
            ConsoleHelper.SetConsoleTitle("Debug Console");
            var handle = ConsoleHelper.GetConsoleWindow();
            if (enableConsole.Checked)
            {
                Logger.Default = new ConsoleLogger();
                ConsoleHelper.ShowWindow(handle, ConsoleHelper.SW_SHOW);
            }
            else
            {
                Logger.Default = logger;
                ConsoleHelper.ShowWindow(handle, ConsoleHelper.SW_HIDE);
            }
            var loggerEventType = (LoggerEvent)Properties.Settings.Default.loggerEventType;
            var loggerEventTypes = Enum.GetValues<LoggerEvent>().ToArray()[1..^1];
            foreach (var loggerEvent in loggerEventTypes)
            {
                var menuItem = new ToolStripMenuItem(loggerEvent.ToString()) { CheckOnClick = true, Checked = loggerEventType.HasFlag(loggerEvent), Tag = (int)loggerEvent };
                loggedEventsMenuItem.DropDownItems.Add(menuItem);
            }
            Logger.Flags = loggerEventType;
            Logger.FileLogging = enableFileLogging.Checked;
        }

        private void InitializeProgressBar()
        {
            Progress.Default = new Progress<int>(SetProgressBarValue);
            Studio.StatusStripUpdate = StatusStripUpdate;
        }

        private void InitalizeOptions()
        {
            var assetMapType = (ExportListType)Properties.Settings.Default.assetMapType;
            var assetMapTypes = Enum.GetValues<ExportListType>().ToArray()[1..];
            foreach (var mapType in assetMapTypes)
            {
                var menuItem = new ToolStripMenuItem(mapType.ToString()) { CheckOnClick = true, Checked = assetMapType.HasFlag(mapType), Tag = (int)mapType };
                assetMapTypeMenuItem.DropDownItems.Add(menuItem);
            }

            specifyGame.Items.AddRange(GameManager.GetGames());
            specifyGame.SelectedIndex = Properties.Settings.Default.selectedGame;
            specifyGame.SelectedIndexChanged += new EventHandler(specifyGame_SelectedIndexChanged);
            Studio.Game = GameManager.GetGame(Properties.Settings.Default.selectedGame);
            TypeFlags.SetTypes(JsonConvert.DeserializeObject<Dictionary<ClassIDType, (bool, bool)>>(Properties.Settings.Default.types));
            Logger.Info($"Target Game type is {Studio.Game.Type}");

            if (Studio.Game.Type.IsUnityCN())
            {
                UnityCNManager.SetKey(Properties.Settings.Default.selectedUnityCNKey);
            }

            MapNameComboBox.SelectedIndexChanged += new EventHandler(specifyNameComboBox_SelectedIndexChanged);
            if (!string.IsNullOrEmpty(Properties.Settings.Default.selectedCABMapName))
            {
                if (!AssetsHelper.LoadCABMapInternal(Properties.Settings.Default.selectedCABMapName))
                {
                    Properties.Settings.Default.selectedCABMapName = "";
                    Properties.Settings.Default.Save();
                }
                else
                {
                    MapNameComboBox.Text = Properties.Settings.Default.selectedCABMapName;
                }
            }
        }
        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private async void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (paths.Length > 0)
            {
                LoadPaths(paths);
            }
        }

        public async void LoadPaths(params string[] paths)
        {
            ResetForm();
            assetsManager.SpecifyUnityVersion = specifyUnityVersion.Text;
            assetsManager.Game = Studio.Game;
            if (paths.Length == 1 && Directory.Exists(paths[0]))
            {
                await Task.Run(() => assetsManager.LoadFolder(paths[0]));
            }
            else
            {
                await Task.Run(() => assetsManager.LoadFiles(paths));
            }
            BuildAssetStructures();
        }

        private async void loadFile_Click(object sender, EventArgs e)
        {
            openFileDialog1.InitialDirectory = openDirectoryBackup;
            if (openFileDialog1.ShowDialog(this) == DialogResult.OK)
            {
                var paths = openFileDialog1.FileNames;
                ResetForm();
                openDirectoryBackup = Path.GetDirectoryName(paths[0]);
                assetsManager.SpecifyUnityVersion = specifyUnityVersion.Text;
                assetsManager.Game = Studio.Game;
                if (paths.Length == 1 && File.Exists(paths[0]) && Path.GetExtension(paths[0]) == ".txt")
                {
                    paths = File.ReadAllLines(paths[0]);
                }
                await Task.Run(() => assetsManager.LoadFiles(paths));
                BuildAssetStructures();
            }
        }

        private async void loadFolder_Click(object sender, EventArgs e)
        {
            var openFolderDialog = new OpenFolderDialog();
            openFolderDialog.InitialFolder = openDirectoryBackup;
            if (openFolderDialog.ShowDialog(this) == DialogResult.OK)
            {
                ResetForm();
                openDirectoryBackup = openFolderDialog.Folder;
                assetsManager.SpecifyUnityVersion = specifyUnityVersion.Text;
                assetsManager.Game = Studio.Game;
                await Task.Run(() => assetsManager.LoadFolder(openFolderDialog.Folder));
                BuildAssetStructures();
            }
        }

        private async void loadServer_Click(object sender, EventArgs e)
        {
            var loadServerForm = new LoadServerForm();
            if (loadServerForm.ShowDialog(this) == DialogResult.OK)
            {
                ResetForm();
                
                if (loadServerForm.UseLocalCache)
                {
                    // 使用本地缓存模式
                    var cachePath = loadServerForm.LocalCachePath;
                    var version = loadServerForm.Version;
                    
                    StatusStripUpdate($"正在加载本地缓存: {version}");
                    Logger.Info($"从本地缓存加载: {cachePath}");
                    
                    try
                    {
                        assetsManager.SpecifyUnityVersion = specifyUnityVersion.Text;
                        assetsManager.Game = Studio.Game;
                        await Task.Run(() => assetsManager.LoadFolder(cachePath));
                        BuildAssetStructures();
                        StatusStripUpdate($"成功加载本地缓存版本: {version}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"加载本地缓存失败: {ex.Message}");
                        StatusStripUpdate($"加载失败: {ex.Message}");
                    }
                }
                else
                {
                    // 从服务器下载模式
                    var url = loadServerForm.ServerUrl;
                    var version = loadServerForm.Version;
                    var replaceBaseUrl = loadServerForm.ReplaceBaseUrl;
                    
                    StatusStripUpdate($"正在从服务器加载: {url} (版本: {version})");
                    
                    try
                    {
                        var cachePath = await ServerLoader.LoadFromServer(url, version, Progress.Default, replaceBaseUrl);
                        
                        if (!string.IsNullOrEmpty(cachePath))
                        {
                            StatusStripUpdate("下载完成，正在加载资源...");
                            Logger.Info($"开始加载下载的资源: {cachePath}");
                            
                            assetsManager.SpecifyUnityVersion = specifyUnityVersion.Text;
                            assetsManager.Game = Studio.Game;
                            await Task.Run(() => assetsManager.LoadFolder(cachePath));
                            BuildAssetStructures();
                            
                            StatusStripUpdate($"成功从服务器加载版本: {version}");
                        }
                        else
                        {
                            StatusStripUpdate("从服务器下载资源失败");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"从服务器加载失败: {ex.Message}");
                        StatusStripUpdate($"错误: {ex.Message}");
                    }
                }
            }
        }

        private async void extractFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog(this) == DialogResult.OK)
            {
                var saveFolderDialog = new OpenFolderDialog();
                saveFolderDialog.Title = "Select the save folder";
                if (saveFolderDialog.ShowDialog(this) == DialogResult.OK)
                {
                    var fileNames = openFileDialog1.FileNames;
                    var savePath = saveFolderDialog.Folder;
                    var extractedCount = await Task.Run(() => ExtractFile(fileNames, savePath));
                    StatusStripUpdate($"Finished extracting {extractedCount} files.");
                }
            }
        }

        private async void extractFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var openFolderDialog = new OpenFolderDialog();
            if (openFolderDialog.ShowDialog(this) == DialogResult.OK)
            {
                var saveFolderDialog = new OpenFolderDialog();
                saveFolderDialog.Title = "Select the save folder";
                if (saveFolderDialog.ShowDialog(this) == DialogResult.OK)
                {
                    var path = openFolderDialog.Folder;
                    var savePath = saveFolderDialog.Folder;
                    var extractedCount = await Task.Run(() => ExtractFolder(path, savePath));
                    StatusStripUpdate($"Finished extracting {extractedCount} files.");
                }
            }
        }

        private async void BuildAssetStructures()
        {
            if (assetsManager.assetsFileList.Count == 0)
            {
                StatusStripUpdate("No Unity file can be loaded.");
                return;
            }

            (var productName, var treeNodeCollection) = await Task.Run(BuildAssetData);
            var typeMap = await Task.Run(BuildClassStructure);

            if (string.IsNullOrEmpty(productName))
            {
                if (!Studio.Game.Type.IsNormal())
                {
                    productName = Studio.Game.Name;
                }
                else if (Studio.Game.Type.IsUnityCN() && UnityCNManager.TryGetEntry(Properties.Settings.Default.selectedUnityCNKey, out var unityCN))
                {
                    productName = unityCN.Name;
                }
                else
                {
                    productName = "no productName";
                }
            }

            Text = $"Studio v{Application.ProductVersion} - {productName} - {assetsManager.assetsFileList[0].unityVersion} - {assetsManager.assetsFileList[0].m_TargetPlatform}";

            assetListView.VirtualListSize = visibleAssets.Count;
            redundanteRessourcenListView.VirtualListSize = redundanzAssets.Count;
            
            // 初始化时存储所有重复资源
            allRedundantAssets = new List<AssetItem>(redundanzAssets);
            
            // 更新重复资源统计信息
            UpdateRedundantStatistics();
            
            // 执行纹理专项检测（根据下拉框选择）
            PerformTextureDetection();
            
            // 构建AssetBundle数据
            BuildAssetBundleData();

            sceneTreeView.BeginUpdate();
            sceneTreeView.Nodes.AddRange(treeNodeCollection.ToArray());
            sceneTreeView.EndUpdate();
            treeNodeCollection.Clear();

            classesListView.BeginUpdate();
            foreach (var version in typeMap)
            {
                var versionGroup = new ListViewGroup(version.Key);
                classesListView.Groups.Add(versionGroup);

                foreach (var uclass in version.Value)
                {
                    uclass.Value.Group = versionGroup;
                    classesListView.Items.Add(uclass.Value);
                }
            }
            typeMap.Clear();
            classesListView.EndUpdate();

          // 计算每个类型的数量和总大小并排序
          var typeSizes = exportableAssets
              .GroupBy(x => x.Type)
              .Select(g => new { Type = g.Key, Count = g.Count(), TotalSize = g.Sum(a => a.FullSize) })
              .OrderByDescending(x => x.TotalSize)
              .ToList();

          // 清空并重新填充ComboBox
          classIDTypeComboBox.Items.Clear();
          classIDTypeComboBox.Items.Add("All");
          foreach (var typeSize in typeSizes)
          {
              var sizeInMB = typeSize.TotalSize / (1024f * 1024f);
              classIDTypeComboBox.Items.Add($"{typeSize.Type} ({typeSize.Count}) ({sizeInMB:F2} MB)");
          }
          classIDTypeComboBox.SelectedIndexChanged -= (s, e) => FilterAssetList();
          classIDTypeComboBox.SelectedIndex = 0;
          classIDTypeComboBox.SelectedIndexChanged += (s, e) =>
          {
              FilterAssetList();
              UpdateTextureFormatComboBox();
          };
          
          // 初始化纹理格式下拉框（初始为不可见）
          textureFormatComboBox.Items.Clear();
          textureFormatComboBox.Items.Add("All");
          textureFormatComboBox.SelectedIndex = 0;
          textureFormatComboBox.Visible = false;

           var types = exportableAssets.Select(x => x.Type).Distinct().OrderBy(x => x.ToString()).ToArray();
           foreach (var type in types)
           {
               var typeItem = new ToolStripMenuItem
               {
                   CheckOnClick = true,
                   Name = type.ToString(),
                   Size = new Size(180, 22),
                   Text = type.ToString()
               };
               typeItem.Click += typeToolStripMenuItem_Click;
               filterTypeToolStripMenuItem.DropDownItems.Add(typeItem);
           }
            allToolStripMenuItem.Checked = true;
            var log = $"Finished loading {assetsManager.assetsFileList.Count} files with {assetListView.Items.Count} exportable assets";
            var m_ObjectsCount = assetsManager.assetsFileList.Sum(x => x.m_Objects.Count);
            var objectsCount = assetsManager.assetsFileList.Sum(x => x.Objects.Count);
            if (m_ObjectsCount != objectsCount)
            {
                log += $" and {m_ObjectsCount - objectsCount} assets failed to read";
            }
            StatusStripUpdate(log);
        }

        private void typeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var typeItem = (ToolStripMenuItem)sender;
            if (typeItem != allToolStripMenuItem)
            {
                allToolStripMenuItem.Checked = false;
            }
            else if (allToolStripMenuItem.Checked)
            {
                for (var i = 1; i < filterTypeToolStripMenuItem.DropDownItems.Count; i++)
                {
                    var item = (ToolStripMenuItem)filterTypeToolStripMenuItem.DropDownItems[i];
                    item.Checked = false;
                }
            }
            FilterAssetList();
        }

        private void AssetStudioForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (glControl.Visible)
            {
                if (e.Control)
                {
                    switch (e.KeyCode)
                    {
                        case Keys.W:
                            //Toggle WireFrame
                            wireFrameMode = (wireFrameMode + 1) % 3;
                            glControl.Invalidate();
                            break;
                        case Keys.S:
                            //Toggle Shade
                            shadeMode = (shadeMode + 1) % 2;
                            glControl.Invalidate();
                            break;
                        case Keys.N:
                            //Normal mode
                            normalMode = (normalMode + 1) % 2;
                            CreateVAO();
                            glControl.Invalidate();
                            break;
                    }
                }
            }
            else if (previewPanel.Visible)
            {
                if (e.Control)
                {
                    var need = false;
                    switch (e.KeyCode)
                    {
                        case Keys.B:
                            textureChannels[0] = !textureChannels[0];
                            need = true;
                            break;
                        case Keys.G:
                            textureChannels[1] = !textureChannels[1];
                            need = true;
                            break;
                        case Keys.R:
                            textureChannels[2] = !textureChannels[2];
                            need = true;
                            break;
                        case Keys.A:
                            textureChannels[3] = !textureChannels[3];
                            need = true;
                            break;
                    }
                    if (need)
                    {
                        if (lastSelectedItem != null)
                        {
                            PreviewAsset(lastSelectedItem);
                            assetInfoLabel.Text = lastSelectedItem.InfoText;
                        }
                    }
                }
            }
        }

        private void exportClassStructuresMenuItem_Click(object sender, EventArgs e)
        {
            if (classesListView.Items.Count > 0)
            {
                var saveFolderDialog = new OpenFolderDialog();
                if (saveFolderDialog.ShowDialog(this) == DialogResult.OK)
                {
                    var savePath = saveFolderDialog.Folder;
                    var count = classesListView.Items.Count;
                    int i = 0;
                    Progress.Reset();
                    foreach (TypeTreeItem item in classesListView.Items)
                    {
                        var versionPath = Path.Combine(savePath, item.Group.Header);
                        Directory.CreateDirectory(versionPath);

                        var saveFile = $"{versionPath}{Path.DirectorySeparatorChar}{item.SubItems[1].Text} {item.Text}.txt";
                        File.WriteAllText(saveFile, item.ToString());

                        Progress.Report(++i, count);
                    }

                    StatusStripUpdate("Finished exporting class structures");
                }
            }
        }

        private void displayAll_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.displayAll = displayAll.Checked;
            Properties.Settings.Default.Save();
        }

        private void enablePreview_Check(object sender, EventArgs e)
        {
            if (lastSelectedItem != null)
            {
                switch (lastSelectedItem.Type)
                {
                    case ClassIDType.Texture2D:
                    case ClassIDType.Sprite:
                        {
                            if (enablePreview.Checked && imageTexture != null)
                            {
                                previewPanel.BackgroundImage = imageTexture.Bitmap;
                            }
                            else
                            {
                                previewPanel.BackgroundImage = Properties.Resources.preview;
                                previewPanel.BackgroundImageLayout = ImageLayout.Center;
                            }
                        }
                        break;
                    case ClassIDType.Shader:
                    case ClassIDType.TextAsset:
                    case ClassIDType.MonoBehaviour:
                    case ClassIDType.MiHoYoBinData:
                        textPreviewBox.Visible = !textPreviewBox.Visible;
                        break;
                    case ClassIDType.Font:
                        fontPreviewBox.Visible = !fontPreviewBox.Visible;
                        break;
                    case ClassIDType.AudioClip:
                        {
                            FMODpanel.Visible = !FMODpanel.Visible;

                            if (sound != null && channel != null)
                            {
                                var result = channel.isPlaying(out var playing);
                                if (result == FMOD.RESULT.OK && playing)
                                {
                                    channel.stop();
                                    FMODreset();
                                }
                            }
                            else if (FMODpanel.Visible)
                            {
                                PreviewAsset(lastSelectedItem);
                            }

                            break;
                        }

                }

            }
            else if (lastSelectedItem != null && enablePreview.Checked)
            {
                PreviewAsset(lastSelectedItem);
            }

            Properties.Settings.Default.enablePreview = enablePreview.Checked;
            Properties.Settings.Default.Save();
        }
        private void displayAssetInfo_Check(object sender, EventArgs e)
        {
            if (displayInfo.Checked && assetInfoLabel.Text != null)
            {
                assetInfoLabel.Visible = true;
            }
            else
            {
                assetInfoLabel.Visible = false;
            }

            Properties.Settings.Default.displayInfo = displayInfo.Checked;
            Properties.Settings.Default.Save();
        }

        private void showExpOpt_Click(object sender, EventArgs e)
        {
            var exportOpt = new ExportOptions();
            if (exportOpt.ShowDialog(this) == DialogResult.OK && exportOpt.Resetted)
            {
                InitializeExportOptions();
                InitializeLogger();
                InitalizeOptions();
            }
        }

        private void assetListView_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            if (e.ItemIndex >= 0 && e.ItemIndex < visibleAssets.Count)
            {
                e.Item = visibleAssets[e.ItemIndex];
            }
            else
            {
                e.Item = new AssetItem(null) { Text = "Error" };
            }
        }

        private void redundanteRessourcenListView_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            if (e.ItemIndex >= 0 && e.ItemIndex < redundanzAssets.Count)
            {
                e.Item = redundanzAssets[e.ItemIndex];
            }
            else
            {
                e.Item = new AssetItem(null) { Text = "Error" };
            }
        }

        private void uncompressedTexturesListView_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            if (e.ItemIndex >= 0 && e.ItemIndex < uncompressedTextures.Count)
            {
                e.Item = uncompressedTextures[e.ItemIndex];
            }
            else
            {
                e.Item = new AssetItem(null) { Text = "Error" };
            }
        }

        private void tabPageSelected(object sender, TabControlEventArgs e)
        {
            switch (e.TabPageIndex)
            {
                case 0:
                    treeSearch.Select();
                    break;
                case 1:
                    listSearch.Select();
                    break;
            }
        }

        private void treeSearch_TextChanged(object sender, EventArgs e)
        {
            treeSrcResults.Clear();
            nextGObject = 0;
        }

        private void treeSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !string.IsNullOrEmpty(treeSearch.Text))
            {
                if (treeSrcResults.Count == 0)
                {
                    try
                    {
                        Regex.Match("", treeSearch.Text, RegexOptions.IgnoreCase);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Invalid Regex.\n" + ex.Message);
                        return;
                    }
                    var regex = new Regex(treeSearch.Text, RegexOptions.IgnoreCase);
                    foreach (TreeNode node in sceneTreeView.Nodes)
                    {
                        TreeNodeSearch(regex, node);
                    }
                }
                if (treeSrcResults.Count > 0)
                {
                    if (e.Shift)
                    {
                        foreach (var node in treeSrcResults)
                        {
                            var tempNode = node;
                            if (e.Alt)
                            {
                                while (tempNode.Parent != null)
                                {
                                    tempNode = tempNode.Parent;
                                }
                            }
                            tempNode.EnsureVisible();
                            tempNode.Checked = e.Control;
                        }
                        sceneTreeView.SelectedNode = treeSrcResults[0];
                    }
                    else
                    {
                        if (nextGObject >= treeSrcResults.Count)
                        {
                            nextGObject = 0;
                        }
                        var node = treeSrcResults[nextGObject];
                        if (e.Alt)
                        {
                            while (node.Parent != null)
                            {
                                node = node.Parent;
                            }
                        }

                        node.EnsureVisible();
                        node.Checked = e.Control;
                        sceneTreeView.SelectedNode = treeSrcResults[nextGObject];
                        nextGObject++;
                    }
                }
            }
        }

        private void TreeNodeSearch(Regex regex, TreeNode treeNode)
        {
            if (regex.IsMatch(treeNode.Text))
            {
                treeSrcResults.Add(treeNode);
            }

            foreach (TreeNode node in treeNode.Nodes)
            {
                TreeNodeSearch(regex, node);
            }
        }

        private void sceneTreeView_AfterCheck(object sender, TreeViewEventArgs e)
        {
            foreach (TreeNode childNode in e.Node.Nodes)
            {
                childNode.Checked = e.Node.Checked;
            }
        }

        private void sceneHierarchy_Click(object sender, EventArgs e)
        {
            var saveFileDialog = new SaveFileDialog() { FileName = "scene.json", Filter = "Scene Hierarchy dump | *.json" };
            if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
            {
                var path = saveFileDialog.FileName;
                var nodes = new Dictionary<string, object>();
                foreach (TreeNode node in sceneTreeView.Nodes)
                {
                    var value = GetNode(node);
                    nodes.Add(node.Text, value);
                }
                var json = JsonConvert.SerializeObject(nodes, Formatting.Indented);
                File.WriteAllText(path, json);
                Logger.Info("Scene Hierarchy dumped sucessfully !!");
            }
        }

        private object GetNode(TreeNode treeNode)
        {
            var nodes = new Dictionary<string, object>();
            foreach (TreeNode node in treeNode.Nodes)
            {
                if (HasGameObjectNode(node))
                {
                    nodes.TryAdd(node.Text, GetNode(node));
                }
            }
            return nodes.Count == 0 ? string.Empty : nodes;
        }

        private bool HasGameObjectNode(TreeNode treeNode)
        {
            if (treeNode is GameObjectTreeNode gameObjectNode && !(bool)gameObjectNode.gameObject.m_Transform?.m_Father.IsNull)
            {
                return gameObjectNode.gameObject.m_Animator != null;
            }
            else
            {
                foreach (TreeNode node in treeNode.Nodes)
                {
                    return HasGameObjectNode(node);
                }
                return false;
            }
        }

        private void listSearch_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                Invoke(new Action(FilterAssetList));
            }
        }

        private void assetListView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (sortColumn != e.Column)
            {
                reverseSort = false;
            }
            else
            {
                reverseSort = !reverseSort;
            }
            sortColumn = e.Column;
            assetListView.BeginUpdate();
            assetListView.SelectedIndices.Clear();
            if (sortColumn == 4) //FullSize
            {
                visibleAssets.Sort((a, b) =>
                {
                    var asf = a.FullSize;
                    var bsf = b.FullSize;
                    return reverseSort ? bsf.CompareTo(asf) : asf.CompareTo(bsf);
                });
            }
            else if (sortColumn == 3) // PathID
            {
                visibleAssets.Sort((x, y) =>
                {
                    long pathID_X = x.m_PathID;
                    long pathID_Y = y.m_PathID;
                    return reverseSort ? pathID_Y.CompareTo(pathID_X) : pathID_X.CompareTo(pathID_Y);
                });
            }
            else
            {
                visibleAssets.Sort((a, b) =>
                {
                    var at = a.SubItems[sortColumn].Text;
                    var bt = b.SubItems[sortColumn].Text;
                    return reverseSort ? bt.CompareTo(at) : at.CompareTo(bt);
                });
            }
            assetListView.EndUpdate();
        }

        private void redundanteRessourcenListView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (sortColumn != e.Column)
            {
                reverseSort = false;
            }
            else
            {
                reverseSort = !reverseSort;
            }
            sortColumn = e.Column;
            
            redundanteRessourcenListView.BeginUpdate();
            redundanteRessourcenListView.SelectedIndices.Clear();
            
            // 根据列进行排序
            switch (sortColumn)
            {
                case 0: // Name
                    redundanzAssets.Sort((a, b) =>
                    {
                        var at = a.Text;
                        var bt = b.Text;
                        return reverseSort ? bt.CompareTo(at) : at.CompareTo(bt);
                    });
                    break;
                case 1: // PathID
                    redundanzAssets.Sort((a, b) =>
                    {
                        var ap = a.m_PathID;
                        var bp = b.m_PathID;
                        return reverseSort ? bp.CompareTo(ap) : ap.CompareTo(bp);
                    });
                    break;
                case 2: // 资源类型
                    redundanzAssets.Sort((a, b) =>
                    {
                        var at = a.TypeString;
                        var bt = b.TypeString;
                        return reverseSort ? bt.CompareTo(at) : at.CompareTo(bt);
                    });
                    break;
                case 3: // 相同数量
                    redundanzAssets.Sort((a, b) =>
                    {
                        var ac = a.Gesamtzahl;
                        var bc = b.Gesamtzahl;
                        return reverseSort ? bc.CompareTo(ac) : ac.CompareTo(bc);
                    });
                    break;
                case 4: // 单个大小
                    redundanzAssets.Sort((a, b) =>
                    {
                        var asf = a.FullSize;
                        var bsf = b.FullSize;
                        return reverseSort ? bsf.CompareTo(asf) : asf.CompareTo(bsf);
                    });
                    break;
                case 5: // 冗余大小
                    redundanzAssets.Sort((a, b) =>
                    {
                        var asf = a.FullSize * (a.Gesamtzahl - 1);
                        var bsf = b.FullSize * (b.Gesamtzahl - 1);
                        return reverseSort ? bsf.CompareTo(asf) : asf.CompareTo(bsf);
                    });
                    break;
            }
            
            // 确保VirtualListSize与列表大小一致
            redundanteRessourcenListView.VirtualListSize = redundanzAssets.Count;
            redundanteRessourcenListView.EndUpdate();
        }
        
        private void redundantSearch_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                Invoke(new Action(FilterRedundantList));
            }
        }
        
        private void FilterRedundantList()
        {
            if (allRedundantAssets == null || allRedundantAssets.Count == 0)
            {
                return;
            }
            
            redundanteRessourcenListView.BeginUpdate();
            redundanteRessourcenListView.SelectedIndices.Clear();
            
            var filteredAssets = allRedundantAssets;
            
            // 应用搜索过滤
            if (!string.IsNullOrEmpty(redundantSearch.Text))
            {
                try
                {
                    Regex.Match("", redundantSearch.Text, RegexOptions.IgnoreCase);
                }
                catch (Exception ex)
                {
                    Logger.Error("Invalid Regex.\n" + ex.Message);
                    redundantSearch.Text = "";
                    redundanteRessourcenListView.EndUpdate();
                    return;
                }
                var regex = new Regex(redundantSearch.Text, RegexOptions.IgnoreCase);
                filteredAssets = filteredAssets.Where(x =>
                    regex.IsMatch(x.Text) ||
                    regex.IsMatch(x.TypeString) ||
                    regex.IsMatch(x.Container)).ToList();
            }
            
            // 应用大小和数量筛选
            if (float.TryParse(fileSizeFilterTextBox.Text, out float minFileSizeMB))
            {
                filteredAssets = filteredAssets.Where(asset =>
                    asset.FullSize / (1024f * 1024f) > minFileSizeMB).ToList();
            }
            
            if (int.TryParse(countFilterTextBox.Text, out int minCount))
            {
                filteredAssets = filteredAssets.Where(asset =>
                    asset.Gesamtzahl > minCount).ToList();
            }
            
            if (float.TryParse(redundantFilterTextBox.Text, out float minRedundantSizeMB))
            {
                filteredAssets = filteredAssets.Where(asset =>
                {
                    var redundantSizeMB = (asset.FullSize * (asset.Gesamtzahl - 1)) / (1024f * 1024f);
                    return redundantSizeMB > minRedundantSizeMB;
                }).ToList();
            }
            
            redundanzAssets = filteredAssets;
            redundanteRessourcenListView.VirtualListSize = redundanzAssets.Count;
            redundanteRessourcenListView.EndUpdate();
            
            UpdateRedundantStatistics();
        }

        private void uncompressedTexturesListView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // 如果点击的是 Container 列(第6列，索引5)，则不进行排序
            if (e.Column == 5)
            {
                return;
            }
            
            if (sortColumn != e.Column)
            {
                reverseSort = false;
            }
            else
            {
                reverseSort = !reverseSort;
            }
            sortColumn = e.Column;
            
            uncompressedTexturesListView.BeginUpdate();
            uncompressedTexturesListView.SelectedIndices.Clear();
            
            // 完全照搬AssetList的排序逻辑
            if (sortColumn == 4) //FullSize
            {
                uncompressedTextures.Sort((a, b) =>
                {
                    var asf = a.FullSize;
                    var bsf = b.FullSize;
                    return reverseSort ? bsf.CompareTo(asf) : asf.CompareTo(bsf);
                });
            }
            else if (sortColumn == 3) //Heigth
            {
                uncompressedTextures.Sort((a, b) =>
                {
                    var asf = a.SubItemValues[1];
                    var bsf = b.SubItemValues[1];
                    return reverseSort ? bsf.CompareTo(asf) : asf.CompareTo(bsf);
                });
            }
            else if (sortColumn == 2) //Width
            {
                uncompressedTextures.Sort((a, b) =>
                {
                    var asf = a.SubItemValues[0];
                    var bsf = b.SubItemValues[0];
                    return reverseSort ? bsf.CompareTo(asf) : asf.CompareTo(bsf);
                });
            }
            else
            {
                uncompressedTextures.Sort((a, b) =>
                {
                    var at = a.SubItems[sortColumn].Text;
                    var bt = b.SubItems[sortColumn].Text;
                    return reverseSort ? bt.CompareTo(at) : at.CompareTo(bt);
                });
            }
            
            uncompressedTexturesListView.EndUpdate();
        }

        private void redundanteRessourcenListViewSelectAsset(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            previewPanel.BackgroundImage = Properties.Resources.preview;
            previewPanel.BackgroundImageLayout = ImageLayout.Center;
            classTextBox.Visible = false;
            assetInfoLabel.Visible = false;
            assetInfoLabel.Text = null;
            textPreviewBox.Visible = false;
            fontPreviewBox.Visible = false;
            FMODpanel.Visible = false;
            glControl.Visible = false;
            StatusStripUpdate("");

            FMODreset();

            lastSelectedItem = (AssetItem)e.Item;

            if (e.IsSelected)
            {
                if (tabControl2.SelectedIndex == 1)
                {
                    dumpTextBox.Text = DumpAsset(lastSelectedItem.Asset);
                }
                if (enablePreview.Checked)
                {
                    // 显示包含此资源的 AssetBundle 信息
                    PreviewText(("包含此资源的AssetBundle:\n" + lastSelectedItem.AllContainer).Replace("\n", "\r\n").Replace("\0", ""));
                    
                    if (displayInfo.Checked && lastSelectedItem.InfoText != null)
                    {
                        assetInfoLabel.Text = lastSelectedItem.InfoText;
                        assetInfoLabel.Visible = true;
                    }
                }
            }
        }


        private void selectAsset(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            previewPanel.BackgroundImage = Properties.Resources.preview;
            previewPanel.BackgroundImageLayout = ImageLayout.Center;
            classTextBox.Visible = false;
            assetInfoLabel.Visible = false;
            assetInfoLabel.Text = null;
            textPreviewBox.Visible = false;
            fontPreviewBox.Visible = false;
            FMODpanel.Visible = false;
            glControl.Visible = false;
            StatusStripUpdate("");

            FMODreset();

            lastSelectedItem = (AssetItem)e.Item;

            if (e.IsSelected)
            {
                if (tabControl2.SelectedIndex == 1)
                {
                    dumpTextBox.Text = DumpAsset(lastSelectedItem.Asset);
                }
                if (enablePreview.Checked)
                {
                    PreviewAsset(lastSelectedItem);
                    if (displayInfo.Checked && lastSelectedItem.InfoText != null)
                    {
                        assetInfoLabel.Text = lastSelectedItem.InfoText;
                        assetInfoLabel.Visible = true;
                    }
                }
            }
        }

        private void classesListView_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            classTextBox.Visible = true;
            assetInfoLabel.Visible = false;
            assetInfoLabel.Text = null;
            textPreviewBox.Visible = false;
            fontPreviewBox.Visible = false;
            FMODpanel.Visible = false;
            glControl.Visible = false;
            StatusStripUpdate("");
            if (e.IsSelected)
            {
                classTextBox.Text = ((TypeTreeItem)classesListView.SelectedItems[0]).ToString();
            }
        }

        private void preview_Resize(object sender, EventArgs e)
        {
            if (glControlLoaded && glControl.Visible)
            {
                ChangeGLSize(glControl.Size);
                glControl.Invalidate();
            }
        }

        private void PreviewAsset(AssetItem assetItem)
        {
            if (assetItem == null)
                return;
            try
            {
                switch (assetItem.Asset)
                {
                    case GameObject m_GameObject when Properties.Settings.Default.enableModelPreview:
                        PreviewGameObject(m_GameObject);
                        break;
                    case Texture2D m_Texture2D:
                        PreviewTexture2D(assetItem, m_Texture2D);
                        break;
                    case AudioClip m_AudioClip:
                        PreviewAudioClip(assetItem, m_AudioClip);
                        break;
                    case Shader m_Shader:
                        PreviewShader(m_Shader);
                        break;
                    case TextAsset m_TextAsset:
                        PreviewTextAsset(m_TextAsset);
                        break;
                    case MonoBehaviour m_MonoBehaviour:
                        PreviewMonoBehaviour(m_MonoBehaviour);
                        break;
                    case Font m_Font:
                        PreviewFont(m_Font);
                        break;
                    case Mesh m_Mesh:
                        PreviewMesh(m_Mesh);
                        break;
                    case VideoClip _:
                    case MovieTexture _:
                        StatusStripUpdate("Only supported export.");
                        break;
                    case Sprite m_Sprite:
                        PreviewSprite(assetItem, m_Sprite);
                        break;
                    case Animator m_Animator when Properties.Settings.Default.enableModelPreview:
                        //StatusStripUpdate("Can be exported to FBX file.");
                        PreviewAnimator(m_Animator);
                        break;
                    case AnimationClip m_AnimationClip:
                        PreviewAnimationClip(m_AnimationClip);
                        break;
                    case MiHoYoBinData m_MiHoYoBinData:
                        PreviewText(m_MiHoYoBinData.AsString);
                        StatusStripUpdate("Can be exported/previewed as JSON if data is a valid JSON (check XOR).");
                        break;
                    default:
                        var str = assetItem.Asset.Dump();
                        if (str != null)
                        {
                            textPreviewBox.Text = str;
                            textPreviewBox.Visible = true;
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                Logger.Error($"Preview {assetItem.Type}:{assetItem.Text} error\r\n{e.Message}\r\n{e.StackTrace}");
            }
        }

        private void PreviewTexture2D(AssetItem assetItem, Texture2D m_Texture2D)
        {
            var image = m_Texture2D.ConvertToImage(true);
            if (image != null)
            {
                var bitmap = new DirectBitmap(image.ConvertToBytes(), m_Texture2D.m_Width, m_Texture2D.m_Height);
                image.Dispose();
                assetItem.InfoText = $"Width: {m_Texture2D.m_Width}\nHeight: {m_Texture2D.m_Height}\nFormat: {m_Texture2D.m_TextureFormat}";
                switch (m_Texture2D.m_TextureSettings.m_FilterMode)
                {
                    case 0: assetItem.InfoText += "\nFilter Mode: Point "; break;
                    case 1: assetItem.InfoText += "\nFilter Mode: Bilinear "; break;
                    case 2: assetItem.InfoText += "\nFilter Mode: Trilinear "; break;
                }
                assetItem.InfoText += $"\nAnisotropic level: {m_Texture2D.m_TextureSettings.m_Aniso}\nMip map bias: {m_Texture2D.m_TextureSettings.m_MipBias}";
                switch (m_Texture2D.m_TextureSettings.m_WrapMode)
                {
                    case 0: assetItem.InfoText += "\nWrap mode: Repeat"; break;
                    case 1: assetItem.InfoText += "\nWrap mode: Clamp"; break;
                }
                assetItem.InfoText += "\nChannels: ";
                int validChannel = 0;
                for (int i = 0; i < 4; i++)
                {
                    if (textureChannels[i])
                    {
                        assetItem.InfoText += textureChannelNames[i];
                        validChannel++;
                    }
                }
                if (validChannel == 0)
                    assetItem.InfoText += "None";
                if (validChannel != 4)
                {
                    var bytes = bitmap.Bits;
                    for (int i = 0; i < bitmap.Height; i++)
                    {
                        int offset = Math.Abs(bitmap.Stride) * i;
                        for (int j = 0; j < bitmap.Width; j++)
                        {
                            bytes[offset] = textureChannels[0] ? bytes[offset] : validChannel == 1 && textureChannels[3] ? byte.MaxValue : byte.MinValue;
                            bytes[offset + 1] = textureChannels[1] ? bytes[offset + 1] : validChannel == 1 && textureChannels[3] ? byte.MaxValue : byte.MinValue;
                            bytes[offset + 2] = textureChannels[2] ? bytes[offset + 2] : validChannel == 1 && textureChannels[3] ? byte.MaxValue : byte.MinValue;
                            bytes[offset + 3] = textureChannels[3] ? bytes[offset + 3] : byte.MaxValue;
                            offset += 4;
                        }
                    }
                }
                PreviewTexture(bitmap);

                StatusStripUpdate("'Ctrl'+'R'/'G'/'B'/'A' for Channel Toggle");
            }
            else
            {
                StatusStripUpdate("Unsupported image for preview");
            }
        }

        private void PreviewAudioClip(AssetItem assetItem, AudioClip m_AudioClip)
        {
            //Info
            assetItem.InfoText = "Compression format: ";
            if (m_AudioClip.version[0] < 5)
            {
                switch (m_AudioClip.m_Type)
                {
                    case FMODSoundType.ACC:
                        assetItem.InfoText += "Acc";
                        break;
                    case FMODSoundType.AIFF:
                        assetItem.InfoText += "AIFF";
                        break;
                    case FMODSoundType.IT:
                        assetItem.InfoText += "Impulse tracker";
                        break;
                    case FMODSoundType.MOD:
                        assetItem.InfoText += "Protracker / Fasttracker MOD";
                        break;
                    case FMODSoundType.MPEG:
                        assetItem.InfoText += "MP2/MP3 MPEG";
                        break;
                    case FMODSoundType.OGGVORBIS:
                        assetItem.InfoText += "Ogg vorbis";
                        break;
                    case FMODSoundType.S3M:
                        assetItem.InfoText += "ScreamTracker 3";
                        break;
                    case FMODSoundType.WAV:
                        assetItem.InfoText += "Microsoft WAV";
                        break;
                    case FMODSoundType.XM:
                        assetItem.InfoText += "FastTracker 2 XM";
                        break;
                    case FMODSoundType.XMA:
                        assetItem.InfoText += "Xbox360 XMA";
                        break;
                    case FMODSoundType.VAG:
                        assetItem.InfoText += "PlayStation Portable ADPCM";
                        break;
                    case FMODSoundType.AUDIOQUEUE:
                        assetItem.InfoText += "iPhone";
                        break;
                    default:
                        assetItem.InfoText += "Unknown";
                        break;
                }
            }
            else
            {
                switch (m_AudioClip.m_CompressionFormat)
                {
                    case AudioCompressionFormat.PCM:
                        assetItem.InfoText += "PCM";
                        break;
                    case AudioCompressionFormat.Vorbis:
                        assetItem.InfoText += "Vorbis";
                        break;
                    case AudioCompressionFormat.ADPCM:
                        assetItem.InfoText += "ADPCM";
                        break;
                    case AudioCompressionFormat.MP3:
                        assetItem.InfoText += "MP3";
                        break;
                    case AudioCompressionFormat.PSMVAG:
                        assetItem.InfoText += "PlayStation Portable ADPCM";
                        break;
                    case AudioCompressionFormat.HEVAG:
                        assetItem.InfoText += "PSVita ADPCM";
                        break;
                    case AudioCompressionFormat.XMA:
                        assetItem.InfoText += "Xbox360 XMA";
                        break;
                    case AudioCompressionFormat.AAC:
                        assetItem.InfoText += "AAC";
                        break;
                    case AudioCompressionFormat.GCADPCM:
                        assetItem.InfoText += "Nintendo 3DS/Wii DSP";
                        break;
                    case AudioCompressionFormat.ATRAC9:
                        assetItem.InfoText += "PSVita ATRAC9";
                        break;
                    default:
                        assetItem.InfoText += "Unknown";
                        break;
                }
            }

            var m_AudioData = m_AudioClip.m_AudioData.GetData();
            if (m_AudioData == null || m_AudioData.Length == 0)
                return;
            var exinfo = new FMOD.CREATESOUNDEXINFO();

            exinfo.cbsize = Marshal.SizeOf(exinfo);
            exinfo.length = (uint)m_AudioClip.m_Size;

            var result = system.createSound(m_AudioData, FMOD.MODE.OPENMEMORY | loopMode, ref exinfo, out sound);
            if (ERRCHECK(result)) return;

            sound.getNumSubSounds(out var numsubsounds);

            if (numsubsounds > 0)
            {
                result = sound.getSubSound(0, out var subsound);
                if (result == FMOD.RESULT.OK)
                {
                    sound = subsound;
                }
            }

            result = sound.getLength(out FMODlenms, FMOD.TIMEUNIT.MS);
            if (ERRCHECK(result)) return;

            result = system.playSound(sound, null, true, out channel);
            if (ERRCHECK(result)) return;

            FMODpanel.Visible = true;

            result = channel.getFrequency(out var frequency);
            if (ERRCHECK(result)) return;

            FMODinfoLabel.Text = frequency + " Hz";
            FMODtimerLabel.Text = $"0:0.0 / {FMODlenms / 1000 / 60}:{FMODlenms / 1000 % 60}.{FMODlenms / 10 % 100}";
        }

        private void PreviewShader(Shader m_Shader)
        {
            if (m_Shader.byteSize > 0xFFFFFFF)
            {
                PreviewText("Shader is too large to parse");
                return;
            }

            var str = m_Shader.Convert();
            PreviewText(str == null ? "Serialized Shader can't be read" : str.Replace("\n", "\r\n"));
        }

        private void PreviewTextAsset(TextAsset m_TextAsset)
        {
            var text = Encoding.UTF8.GetString(m_TextAsset.m_Script);
            text = text.Replace("\n", "\r\n").Replace("\0", "");
            PreviewText(text);
        }

        private void PreviewMonoBehaviour(MonoBehaviour m_MonoBehaviour)
        {
            var obj = m_MonoBehaviour.ToType();
            if (obj == null)
            {
                var type = MonoBehaviourToTypeTree(m_MonoBehaviour);
                obj = m_MonoBehaviour.ToType(type);
            }
            var str = JsonConvert.SerializeObject(obj, Formatting.Indented);
            PreviewText(str);
        }

        private void PreviewFont(Font m_Font)
        {
            if (m_Font.m_FontData != null)
            {
                var data = Marshal.AllocCoTaskMem(m_Font.m_FontData.Length);
                Marshal.Copy(m_Font.m_FontData, 0, data, m_Font.m_FontData.Length);

                uint cFonts = 0;
                var re = FontHelper.AddFontMemResourceEx(data, (uint)m_Font.m_FontData.Length, IntPtr.Zero, ref cFonts);
                if (re != IntPtr.Zero)
                {
                    using (var pfc = new PrivateFontCollection())
                    {
                        pfc.AddMemoryFont(data, m_Font.m_FontData.Length);
                        Marshal.FreeCoTaskMem(data);
                        if (pfc.Families.Length > 0)
                        {
                            fontPreviewBox.SelectionStart = 0;
                            fontPreviewBox.SelectionLength = 80;
                            fontPreviewBox.SelectionFont = new System.Drawing.Font(pfc.Families[0], 16, FontStyle.Regular);
                            fontPreviewBox.SelectionStart = 81;
                            fontPreviewBox.SelectionLength = 56;
                            fontPreviewBox.SelectionFont = new System.Drawing.Font(pfc.Families[0], 12, FontStyle.Regular);
                            fontPreviewBox.SelectionStart = 138;
                            fontPreviewBox.SelectionLength = 56;
                            fontPreviewBox.SelectionFont = new System.Drawing.Font(pfc.Families[0], 18, FontStyle.Regular);
                            fontPreviewBox.SelectionStart = 195;
                            fontPreviewBox.SelectionLength = 56;
                            fontPreviewBox.SelectionFont = new System.Drawing.Font(pfc.Families[0], 24, FontStyle.Regular);
                            fontPreviewBox.SelectionStart = 252;
                            fontPreviewBox.SelectionLength = 56;
                            fontPreviewBox.SelectionFont = new System.Drawing.Font(pfc.Families[0], 36, FontStyle.Regular);
                            fontPreviewBox.SelectionStart = 309;
                            fontPreviewBox.SelectionLength = 56;
                            fontPreviewBox.SelectionFont = new System.Drawing.Font(pfc.Families[0], 48, FontStyle.Regular);
                            fontPreviewBox.SelectionStart = 366;
                            fontPreviewBox.SelectionLength = 56;
                            fontPreviewBox.SelectionFont = new System.Drawing.Font(pfc.Families[0], 60, FontStyle.Regular);
                            fontPreviewBox.SelectionStart = 423;
                            fontPreviewBox.SelectionLength = 55;
                            fontPreviewBox.SelectionFont = new System.Drawing.Font(pfc.Families[0], 72, FontStyle.Regular);
                            fontPreviewBox.Visible = true;
                        }
                    }
                    return;
                }
            }
            StatusStripUpdate("Unsupported font for preview. Try to export.");
        }

        private void PreviewMesh(Mesh m_Mesh)
        {
            if (m_Mesh.m_VertexCount > 0)
            {
                viewMatrixData = Matrix4.CreateRotationY(-(float)Math.PI / 4) * Matrix4.CreateRotationX(-(float)Math.PI / 6);
                #region Vertices
                if (m_Mesh.m_Vertices == null || m_Mesh.m_Vertices.Length == 0)
                {
                    StatusStripUpdate("Mesh can't be previewed.");
                    return;
                }
                int count = 3;
                if (m_Mesh.m_Vertices.Length == m_Mesh.m_VertexCount * 4)
                {
                    count = 4;
                }
                vertexData = new OpenTK.Mathematics.Vector3[m_Mesh.m_VertexCount];
                // Calculate Bounding
                float[] min = new float[3];
                float[] max = new float[3];
                for (int i = 0; i < 3; i++)
                {
                    min[i] = m_Mesh.m_Vertices[i];
                    max[i] = m_Mesh.m_Vertices[i];
                }
                for (int v = 0; v < m_Mesh.m_VertexCount; v++)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        min[i] = Math.Min(min[i], m_Mesh.m_Vertices[v * count + i]);
                        max[i] = Math.Max(max[i], m_Mesh.m_Vertices[v * count + i]);
                    }
                    vertexData[v] = new OpenTK.Mathematics.Vector3(
                        m_Mesh.m_Vertices[v * count],
                        m_Mesh.m_Vertices[v * count + 1],
                        m_Mesh.m_Vertices[v * count + 2]);
                }

                // Calculate modelMatrix
                var dist = OpenTK.Mathematics.Vector3.One;
                var offset = OpenTK.Mathematics.Vector3.Zero;
                for (int i = 0; i < 3; i++)
                {
                    dist[i] = max[i] - min[i];
                    offset[i] = (max[i] + min[i]) / 2;
                }
                float d = Math.Max(1e-5f, dist.Length);
                modelMatrixData = Matrix4.CreateTranslation(-offset) * Matrix4.CreateScale(2f / d);
                #endregion
                #region Indicies
                indiceData = new int[m_Mesh.m_Indices.Count];
                for (int i = 0; i < m_Mesh.m_Indices.Count; i = i + 3)
                {
                    indiceData[i] = (int)m_Mesh.m_Indices[i];
                    indiceData[i + 1] = (int)m_Mesh.m_Indices[i + 1];
                    indiceData[i + 2] = (int)m_Mesh.m_Indices[i + 2];
                }
                #endregion
                #region Normals
                if (m_Mesh.m_Normals != null && m_Mesh.m_Normals.Length > 0)
                {
                    if (m_Mesh.m_Normals.Length == m_Mesh.m_VertexCount * 3)
                        count = 3;
                    else if (m_Mesh.m_Normals.Length == m_Mesh.m_VertexCount * 4)
                        count = 4;
                    normalData = new OpenTK.Mathematics.Vector3[m_Mesh.m_VertexCount];
                    for (int n = 0; n < m_Mesh.m_VertexCount; n++)
                    {
                        normalData[n] = new OpenTK.Mathematics.Vector3(
                            m_Mesh.m_Normals[n * count],
                            m_Mesh.m_Normals[n * count + 1],
                            m_Mesh.m_Normals[n * count + 2]);
                    }
                }
                else
                    normalData = null;
                // calculate normal by ourself
                normal2Data = new OpenTK.Mathematics.Vector3[m_Mesh.m_VertexCount];
                int[] normalCalculatedCount = new int[m_Mesh.m_VertexCount];
                for (int i = 0; i < m_Mesh.m_VertexCount; i++)
                {
                    normal2Data[i] = OpenTK.Mathematics.Vector3.Zero;
                    normalCalculatedCount[i] = 0;
                }
                for (int i = 0; i < m_Mesh.m_Indices.Count; i = i + 3)
                {
                    var dir1 = vertexData[indiceData[i + 1]] - vertexData[indiceData[i]];
                    var dir2 = vertexData[indiceData[i + 2]] - vertexData[indiceData[i]];
                    var normal = OpenTK.Mathematics.Vector3.Cross(dir1, dir2);
                    normal.Normalize();
                    for (int j = 0; j < 3; j++)
                    {
                        normal2Data[indiceData[i + j]] += normal;
                        normalCalculatedCount[indiceData[i + j]]++;
                    }
                }
                for (int i = 0; i < m_Mesh.m_VertexCount; i++)
                {
                    if (normalCalculatedCount[i] == 0)
                        normal2Data[i] = new OpenTK.Mathematics.Vector3(0, 1, 0);
                    else
                        normal2Data[i] /= normalCalculatedCount[i];
                }
                #endregion
                #region Colors
                if (m_Mesh.m_Colors != null && m_Mesh.m_Colors.Length == m_Mesh.m_VertexCount * 3)
                {
                    colorData = new OpenTK.Mathematics.Vector4[m_Mesh.m_VertexCount];
                    for (int c = 0; c < m_Mesh.m_VertexCount; c++)
                    {
                        colorData[c] = new OpenTK.Mathematics.Vector4(
                            m_Mesh.m_Colors[c * 3],
                            m_Mesh.m_Colors[c * 3 + 1],
                            m_Mesh.m_Colors[c * 3 + 2],
                            1.0f);
                    }
                }
                else if (m_Mesh.m_Colors != null && m_Mesh.m_Colors.Length == m_Mesh.m_VertexCount * 4)
                {
                    colorData = new OpenTK.Mathematics.Vector4[m_Mesh.m_VertexCount];
                    for (int c = 0; c < m_Mesh.m_VertexCount; c++)
                    {
                        colorData[c] = new OpenTK.Mathematics.Vector4(
                        m_Mesh.m_Colors[c * 4],
                        m_Mesh.m_Colors[c * 4 + 1],
                        m_Mesh.m_Colors[c * 4 + 2],
                        m_Mesh.m_Colors[c * 4 + 3]);
                    }
                }
                else
                {
                    colorData = new OpenTK.Mathematics.Vector4[m_Mesh.m_VertexCount];
                    for (int c = 0; c < m_Mesh.m_VertexCount; c++)
                    {
                        colorData[c] = new OpenTK.Mathematics.Vector4(0.5f, 0.5f, 0.5f, 1.0f);
                    }
                }
                #endregion
                glControl.Visible = true;
                CreateVAO();
                StatusStripUpdate("Using OpenGL Version: " + GL.GetString(StringName.Version) + "\n"
                                  + "'Mouse Left'=Rotate | 'Mouse Right'=Move | 'Mouse Wheel'=Zoom \n"
                                  + "'Ctrl W'=Wireframe | 'Ctrl S'=Shade | 'Ctrl N'=ReNormal ");
            }
            else
            {
                StatusStripUpdate("Unable to preview this mesh");
            }
        }

        private void PreviewGameObject(GameObject m_GameObject)
        {
            var options = new ModelConverter.Options()
            {
                imageFormat = Properties.Settings.Default.convertType,
                game = Studio.Game,
                collectAnimations = Properties.Settings.Default.collectAnimations,
                exportMaterials = false,
                materials = new HashSet<Material>(),
                uvs = JsonConvert.DeserializeObject<Dictionary<string, (bool, int)>>(Properties.Settings.Default.uvs),
                texs = JsonConvert.DeserializeObject<Dictionary<string, int>>(Properties.Settings.Default.texs),
            };
            var model = new ModelConverter(m_GameObject, options, Array.Empty<AnimationClip>());
            PreviewModel(model);
        }
        private void PreviewAnimator(Animator m_Animator)
        {
            var options = new ModelConverter.Options()
            {
                imageFormat = Properties.Settings.Default.convertType,
                game = Studio.Game,
                collectAnimations = Properties.Settings.Default.collectAnimations,
                exportMaterials = false,
                materials = new HashSet<Material>(),
                uvs = JsonConvert.DeserializeObject<Dictionary<string, (bool, int)>>(Properties.Settings.Default.uvs),
                texs = JsonConvert.DeserializeObject<Dictionary<string, int>>(Properties.Settings.Default.texs),
            };
            var model = new ModelConverter(m_Animator, options, Array.Empty<AnimationClip>());
            PreviewModel(model);
        }

        private void PreviewAnimationClip(AnimationClip clip)
        {
            var str = clip.Convert();
            if (string.IsNullOrEmpty(str))
                str = "Legacy animation is not supported";
            PreviewText(str.Replace("\n", "\r\n"));
        }

        private void PreviewModel(ModelConverter model)
        {
            if (model.MeshList.Count > 0)
            {
                viewMatrixData = Matrix4.CreateRotationY(-(float)Math.PI / 4) * Matrix4.CreateRotationX(-(float)Math.PI / 6);
                #region Vertices
                vertexData = model.MeshList.SelectMany(x => x.VertexList).Select(x => new OpenTK.Mathematics.Vector3(x.Vertex.X, x.Vertex.Y, x.Vertex.Z)).ToArray();
                // Calculate Bounding
                var min = vertexData.Aggregate(OpenTK.Mathematics.Vector3.ComponentMin);
                var max = vertexData.Aggregate(OpenTK.Mathematics.Vector3.ComponentMax);

                // Calculate modelMatrix
                var dist = max - min;
                var offset = (max - min) / 2;
                float d = Math.Max(1e-5f, dist.Length);
                modelMatrixData = Matrix4.CreateTranslation(-offset) * Matrix4.CreateScale(2f / d);
                #endregion
                #region Indicies
                int meshOffset = 0;
                var indices = new List<int>();
                foreach (var mesh in model.MeshList)
                {
                    foreach (var submesh in mesh.SubmeshList)
                    {
                        foreach (var face in submesh.FaceList)
                        {
                            foreach (var index in face.VertexIndices)
                            {
                                indices.Add(submesh.BaseVertex + index + meshOffset);
                            }
                        }
                    }
                    meshOffset += mesh.VertexList.Count;
                }
                indiceData = indices.ToArray();
                #endregion
                #region Normals
                normalData = model.MeshList.SelectMany(x => x.VertexList).Select(x => new OpenTK.Mathematics.Vector3(x.Normal.X, x.Normal.Y, x.Normal.Z)).ToArray();
                // calculate normal by ourself
                normal2Data = new OpenTK.Mathematics.Vector3[vertexData.Length];
                int[] normalCalculatedCount = new int[vertexData.Length];
                Array.Fill(normal2Data, OpenTK.Mathematics.Vector3.Zero);
                Array.Fill(normalCalculatedCount, 0);
                for (int j = 0; j < indiceData.Length; j += 3)
                {
                    var dir1 = vertexData[indiceData[j + 1]] - vertexData[indiceData[j]];
                    var dir2 = vertexData[indiceData[j + 2]] - vertexData[indiceData[j]];
                    var normal = OpenTK.Mathematics.Vector3.Cross(dir1, dir2);
                    normal.Normalize();
                    for (int k = 0; k < 3; k++)
                    {
                        normal2Data[indiceData[j + k]] += normal;
                        normalCalculatedCount[indiceData[j + k]]++;
                    }
                }
                for (int j = 0; j < vertexData.Length; j++)
                {
                    if (normalCalculatedCount[j] == 0)
                        normal2Data[j] = new OpenTK.Mathematics.Vector3(0, 1, 0);
                    else
                        normal2Data[j] /= normalCalculatedCount[j];
                }
                #endregion
                #region Colors
                colorData = model.MeshList.SelectMany(x => x.VertexList).Select(x => new OpenTK.Mathematics.Vector4(x.Color.R, x.Color.G, x.Color.B, x.Color.A)).ToArray();
                #endregion
                glControl.Visible = true;
                CreateVAO();
                StatusStripUpdate("Using OpenGL Version: " + GL.GetString(StringName.Version) + "\n"
                                  + "'Mouse Left'=Rotate | 'Mouse Right'=Move | 'Mouse Wheel'=Zoom \n"
                                  + "'Ctrl W'=Wireframe | 'Ctrl S'=Shade | 'Ctrl N'=ReNormal ");
            }
            else
            {
                StatusStripUpdate("Unable to preview this model");
            }
        }

        private void PreviewSprite(AssetItem assetItem, Sprite m_Sprite)
        {
            var image = m_Sprite.GetImage();
            if (image != null)
            {
                var bitmap = new DirectBitmap(image.ConvertToBytes(), image.Width, image.Height);
                image.Dispose();
                assetItem.InfoText = $"Width: {bitmap.Width}\nHeight: {bitmap.Height}\n";
                PreviewTexture(bitmap);
            }
            else
            {
                StatusStripUpdate("Unsupported sprite for preview.");
            }
        }

        private void PreviewTexture(DirectBitmap bitmap)
        {
            imageTexture?.Dispose();
            imageTexture = bitmap;
            previewPanel.BackgroundImage = imageTexture.Bitmap;
            if (imageTexture.Width > previewPanel.Width || imageTexture.Height > previewPanel.Height)
                previewPanel.BackgroundImageLayout = ImageLayout.Zoom;
            else
                previewPanel.BackgroundImageLayout = ImageLayout.Center;
        }

        private void PreviewText(string text)
        {
            textPreviewBox.Text = text;
            textPreviewBox.Visible = true;
        }

        private void SetProgressBarValue(int value)
        {
            if (InvokeRequired)
            {
                
                var result = BeginInvoke(new Action(() => { progressBar1.Value = value; }));
                result.AsyncWaitHandle.WaitOne();
            }
            else
            {
                progressBar1.Value = value;
            }
        }

        private void StatusStripUpdate(string statusText)
        {
            if (InvokeRequired)
            {
                var result = BeginInvoke(() => { toolStripStatusLabel1.Text = statusText; });
                result.AsyncWaitHandle.WaitOne();
            }
            else
            {
                toolStripStatusLabel1.Text = statusText;
            }
        }

        public void ResetForm()
        {
            Text = $"Studio v{Application.ProductVersion}";
            assetsManager.Clear();
            assemblyLoader.Clear();
            exportableAssets.Clear();
            visibleAssets.Clear();
            Studio.redundanzAssets.Clear();
            allRedundantAssets.Clear();
            redundanzAssets.Clear();
            sceneTreeView.Nodes.Clear();
            assetListView.VirtualListSize = 0;
            assetListView.Items.Clear();
            redundanteRessourcenListView.VirtualListSize = 0;
            redundanteRessourcenListView.Items.Clear();
            uncompressedTexturesListView.VirtualListSize = 0;
            uncompressedTexturesListView.Items.Clear();
            uncompressedTextures.Clear();
            assetBundleDict.Clear();
            assetBundleTreeView.Nodes.Clear();
            assetBundleSearch.Text = string.Empty;
            bundleSizeFilterTextBox.Text = "0";
            assetBundleTotalLabel.Text = string.Empty;
            classesListView.Items.Clear();
            classesListView.Groups.Clear();
            previewPanel.BackgroundImage = Properties.Resources.preview;
            imageTexture?.Dispose();
            imageTexture = null;
            previewPanel.BackgroundImageLayout = ImageLayout.Center;
            assetInfoLabel.Visible = false;
            assetInfoLabel.Text = null;
            textPreviewBox.Visible = false;
            fontPreviewBox.Visible = false;
            glControl.Visible = false;
            lastSelectedItem = null;
            sortColumn = -1;
            reverseSort = false;
            listSearch.Text = string.Empty;
            redundantSearch.Text = string.Empty;
            sizeFilterTextBox.Text = "0";
            fileSizeFilterTextBox.Text = "0";
            countFilterTextBox.Text = "0";
            redundantFilterTextBox.Text = "0";
            
            textureFormatComboBox.Items.Clear();
            textureFormatComboBox.Items.Add("All");
            textureFormatComboBox.SelectedIndex = 0;
            textureFormatComboBox.Visible = false;

            var count = filterTypeToolStripMenuItem.DropDownItems.Count;
            for (var i = 1; i < count; i++)
            {
                filterTypeToolStripMenuItem.DropDownItems.RemoveAt(1);
            }

            FMODreset();
            StatusStripUpdate("Reset successfully !!");
        }

        private void assetListView_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && assetListView.SelectedIndices.Count > 0)
            {
                goToSceneHierarchyToolStripMenuItem.Visible = false;
                showOriginalFileToolStripMenuItem.Visible = false;
                exportAnimatorwithselectedAnimationClipMenuItem.Visible = false;

                if (assetListView.SelectedIndices.Count == 1)
                {
                    goToSceneHierarchyToolStripMenuItem.Visible = true;
                    showOriginalFileToolStripMenuItem.Visible = true;
                }
                if (assetListView.SelectedIndices.Count >= 1)
                {
                    var selectedAssets = GetSelectedAssets();
                    if (selectedAssets.Any(x => x.Type == ClassIDType.Animator) && selectedAssets.Any(x => x.Type == ClassIDType.AnimationClip))
                    {
                        exportAnimatorwithselectedAnimationClipMenuItem.Visible = true;
                    }
                }

                tempClipboard = assetListView.HitTest(new Point(e.X, e.Y)).SubItem.Text;
                contextMenuStrip1.Show(assetListView, e.X, e.Y);
            }
        }

        private void redundanteRessourcenListView_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && redundanteRessourcenListView.SelectedIndices.Count > 0)
            {
                goToSceneHierarchyToolStripMenuItem.Visible = false;
                showOriginalFileToolStripMenuItem.Visible = false;
                exportAnimatorwithselectedAnimationClipMenuItem.Visible = false;

                if (redundanteRessourcenListView.SelectedIndices.Count == 1)
                {
                    goToSceneHierarchyToolStripMenuItem.Visible = true;
                    showOriginalFileToolStripMenuItem.Visible = true;
                }
                if (redundanteRessourcenListView.SelectedIndices.Count >= 1)
                {
                    var selectedAssets = GetSelectedAssets();
                    if (selectedAssets.Any(x => x.Type == ClassIDType.Animator) && selectedAssets.Any(x => x.Type == ClassIDType.AnimationClip))
                    {
                        exportAnimatorwithselectedAnimationClipMenuItem.Visible = true;
                    }
                }

                tempClipboard = redundanteRessourcenListView.HitTest(new Point(e.X, e.Y)).SubItem.Text;
                contextMenuStrip1.Show(redundanteRessourcenListView, e.X, e.Y);
            }
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Clipboard.SetDataObject(tempClipboard);
        }

        private void exportSelectedAssetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.Selected, ExportType.Convert);
        }

        private void showOriginalFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AssetItem selectasset = null;
            
            // 判断是从哪个ListView触发的
            if (assetListView.SelectedIndices.Count > 0)
            {
                selectasset = (AssetItem)assetListView.Items[assetListView.SelectedIndices[0]];
            }
            else if (redundanteRessourcenListView.SelectedIndices.Count > 0)
            {
                selectasset = redundanzAssets[redundanteRessourcenListView.SelectedIndices[0]];
            }
            else if (uncompressedTexturesListView.SelectedIndices.Count > 0)
            {
                selectasset = uncompressedTextures[uncompressedTexturesListView.SelectedIndices[0]];
            }
            
            if (selectasset != null)
            {
                var args = $"/select, \"{selectasset.SourceFile.originalPath ?? selectasset.SourceFile.fullName}\"";
                var pfi = new ProcessStartInfo("explorer.exe", args);
                Process.Start(pfi);
            }
        }

        private void exportAnimatorwithAnimationClipMenuItem_Click(object sender, EventArgs e)
        {
            AssetItem animator = null;
            List<AssetItem> animationList = new List<AssetItem>();
            var selectedAssets = GetSelectedAssets();
            foreach (var assetPreloadData in selectedAssets)
            {
                if (assetPreloadData.Type == ClassIDType.Animator)
                {
                    animator = assetPreloadData;
                }
                else if (assetPreloadData.Type == ClassIDType.AnimationClip)
                {
                    animationList.Add(assetPreloadData);
                }
            }

            if (animator != null)
            {
                var saveFolderDialog = new OpenFolderDialog();
                saveFolderDialog.InitialFolder = saveDirectoryBackup;
                if (saveFolderDialog.ShowDialog(this) == DialogResult.OK)
                {
                    saveDirectoryBackup = saveFolderDialog.Folder;
                    var exportPath = Path.Combine(saveFolderDialog.Folder, "Animator") + Path.DirectorySeparatorChar;
                    ExportAnimatorWithAnimationClip(animator, animationList, exportPath);
                }
            }
        }

        private void exportSelectedObjectsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportObjects(false);
        }

        private void exportObjectswithAnimationClipMenuItem_Click(object sender, EventArgs e)
        {
            ExportObjects(true);
        }

        private void ExportObjects(bool animation)
        {
            if (sceneTreeView.Nodes.Count > 0)
            {
                var saveFolderDialog = new OpenFolderDialog();
                saveFolderDialog.InitialFolder = saveDirectoryBackup;
                if (saveFolderDialog.ShowDialog(this) == DialogResult.OK)
                {
                    saveDirectoryBackup = saveFolderDialog.Folder;
                    var exportPath = Path.Combine(saveFolderDialog.Folder, "GameObject") + Path.DirectorySeparatorChar;
                    List<AssetItem> animationList = null;
                    if (animation)
                    {
                        animationList = GetSelectedAssets().Where(x => x.Type == ClassIDType.AnimationClip).ToList();
                        if (animationList.Count == 0)
                        {
                            animationList = null;
                        }
                    }
                    ExportObjectsWithAnimationClip(exportPath, sceneTreeView.Nodes, animationList);
                }
            }
            else
            {
                StatusStripUpdate("No Objects available for export");
            }
        }

        private void exportSelectedObjectsmergeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportMergeObjects(false);
        }

        private void exportSelectedObjectsmergeWithAnimationClipToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportMergeObjects(true);
        }

        private void ExportMergeObjects(bool animation)
        {
            if (sceneTreeView.Nodes.Count > 0)
            {
                var gameObjects = new List<GameObject>();
                GetSelectedParentNode(sceneTreeView.Nodes, gameObjects);
                if (gameObjects.Count > 0)
                {
                    var saveFileDialog = new SaveFileDialog();
                    saveFileDialog.FileName = gameObjects[0].m_Name + " (merge).fbx";
                    saveFileDialog.AddExtension = false;
                    saveFileDialog.Filter = "Fbx file (*.fbx)|*.fbx";
                    saveFileDialog.InitialDirectory = saveDirectoryBackup;
                    if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        saveDirectoryBackup = Path.GetDirectoryName(saveFileDialog.FileName);
                        var exportPath = saveFileDialog.FileName;
                        List<AssetItem> animationList = null;
                        if (animation)
                        {
                            animationList = GetSelectedAssets().Where(x => x.Type == ClassIDType.AnimationClip).ToList();
                            if (animationList.Count == 0)
                            {
                                animationList = null;
                            }
                        }
                        ExportObjectsMergeWithAnimationClip(exportPath, gameObjects, animationList);
                    }
                }
                else
                {
                    StatusStripUpdate("No Object selected for export.");
                }
            }
        }

        private void exportSelectedNodessplitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportNodes(false);
        }

        private void exportSelectedNodessplitSelectedAnimationClipsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportNodes(true);
        }

        private void ExportNodes(bool animation)
        {
            if (sceneTreeView.Nodes.Count > 0)
            {
                var saveFolderDialog = new OpenFolderDialog();
                saveFolderDialog.InitialFolder = saveDirectoryBackup;
                if (saveFolderDialog.ShowDialog(this) == DialogResult.OK)
                {
                    saveDirectoryBackup = saveFolderDialog.Folder;
                    var exportPath = Path.Combine(saveFolderDialog.Folder, "GameObject") + Path.DirectorySeparatorChar;
                    var roots = sceneTreeView.Nodes.Cast<TreeNode>().Where(x => x.Level == 0 && x.Checked).ToList();
                    if (roots.Count == 0)
                    {
                        Logger.Info("No root nodes found selected.");
                        return;
                    }
                    List<AssetItem> animationList = null;
                    if (animation)
                    {
                        animationList = GetSelectedAssets().Where(x => x.Type == ClassIDType.AnimationClip).ToList();
                        if (animationList.Count == 0)
                        {
                            animationList = null;
                        }
                    }
                    ExportNodesWithAnimationClip(exportPath, roots, animationList);
                }
            }
        }

        private void goToSceneHierarchyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AssetItem selectasset = null;
            
            // 判断是从哪个ListView触发的
            if (assetListView.SelectedIndices.Count > 0)
            {
                selectasset = (AssetItem)assetListView.Items[assetListView.SelectedIndices[0]];
            }
            else if (redundanteRessourcenListView.SelectedIndices.Count > 0)
            {
                selectasset = redundanzAssets[redundanteRessourcenListView.SelectedIndices[0]];
            }
            else if (uncompressedTexturesListView.SelectedIndices.Count > 0)
            {
                selectasset = uncompressedTextures[uncompressedTexturesListView.SelectedIndices[0]];
            }
            
            if (selectasset?.TreeNode != null)
            {
                sceneTreeView.SelectedNode = selectasset.TreeNode;
                tabControl1.SelectedTab = tabPage1;
            }
        }

        private void exportAllAssetsMenuItem_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.All, ExportType.Convert);
        }

        private void exportSelectedAssetsMenuItem_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.Selected, ExportType.Convert);
        }

        private void exportFilteredAssetsMenuItem_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.Filtered, ExportType.Convert);
        }

        private void toolStripMenuItem4_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.All, ExportType.Raw);
        }

        private void toolStripMenuItem5_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.Selected, ExportType.Raw);
        }

        private void toolStripMenuItem6_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.Filtered, ExportType.Raw);
        }

        private void toolStripMenuItem7_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.All, ExportType.Dump);
        }

        private void toolStripMenuItem8_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.Selected, ExportType.Dump);
        }

        private void toolStripMenuItem9_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.Filtered, ExportType.Dump);
        }
        private void toolStripMenuItem17_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.All, ExportType.JSON);
        }

        private void toolStripMenuItem24_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.Selected, ExportType.JSON);
        }

        private void toolStripMenuItem25_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.Filtered, ExportType.JSON);
        }

        private void toolStripMenuItem11_Click(object sender, EventArgs e)
        {
            ExportAssetsList(ExportFilter.All);
        }

        private void toolStripMenuItem12_Click(object sender, EventArgs e)
        {
            ExportAssetsList(ExportFilter.Selected);
        }

        private void toolStripMenuItem13_Click(object sender, EventArgs e)
        {
            ExportAssetsList(ExportFilter.Filtered);
        }

        private void exportAllObjectssplitToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (sceneTreeView.Nodes.Count > 0)
            {
                var saveFolderDialog = new OpenFolderDialog();
                saveFolderDialog.InitialFolder = saveDirectoryBackup;
                if (saveFolderDialog.ShowDialog(this) == DialogResult.OK)
                {
                    saveDirectoryBackup = saveFolderDialog.Folder;
                    var savePath = saveFolderDialog.Folder + Path.DirectorySeparatorChar;
                    ExportSplitObjects(savePath, sceneTreeView.Nodes);
                }
            }
            else
            {
                StatusStripUpdate("No Objects available for export");
            }
        }

        private List<AssetItem> GetSelectedAssets()
        {
            var selectedAssets = new List<AssetItem>(assetListView.SelectedIndices.Count);
            foreach (int index in assetListView.SelectedIndices)
            {
                selectedAssets.Add((AssetItem)assetListView.Items[index]);
            }

            return selectedAssets;
        }

        private void UpdateTextureFormatComboBox()
        {
            var selectedText = classIDTypeComboBox.SelectedItem?.ToString();
            if (selectedText != null && selectedText.StartsWith("Texture2D"))
            {
                // 只在选择Texture2D类型时显示纹理格式筛选
                textureFormatComboBox.Visible = true;
                
                // 获取所有Texture2D的格式
                var textureFormats = exportableAssets
                    .Where(x => x.Type == ClassIDType.Texture2D && x.Asset is Texture2D)
                    .Select(x => ((Texture2D)x.Asset).m_TextureFormat)
                    .GroupBy(f => f)
                    .Select(g => new { Format = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToList();
                
                textureFormatComboBox.SelectedIndexChanged -= (s, e) => FilterAssetList();
                textureFormatComboBox.Items.Clear();
                textureFormatComboBox.Items.Add("All");
                
                foreach (var format in textureFormats)
                {
                    textureFormatComboBox.Items.Add($"{format.Format} ({format.Count})");
                }
                
                textureFormatComboBox.SelectedIndex = 0;
                textureFormatComboBox.SelectedIndexChanged += (s, e) => FilterAssetList();
            }
            else
            {
                textureFormatComboBox.Visible = false;
            }
        }
        
        private void FilterAssetList()
        {
            // 检查 SelectedItem 是否为 null，避免在 ResetForm 期间出错
            if (classIDTypeComboBox.SelectedItem == null)
            {
                return;
            }
            
            assetListView.BeginUpdate();
            assetListView.SelectedIndices.Clear();
            var selectedText = classIDTypeComboBox.SelectedItem.ToString();
            
            if (selectedText != "All")
            {
                // 从选项文本中提取类型名称（格式为 "TypeName (count) (size MB)"）
                var typeName = selectedText.Substring(0, selectedText.IndexOf(" ("));
                var type = (ClassIDType)Enum.Parse(typeof(ClassIDType), typeName);
                visibleAssets = exportableAssets.FindAll(x => x.Type == type);
                
                // 如果是Texture2D类型，应用纹理格式筛选
                if (type == ClassIDType.Texture2D && textureFormatComboBox.Visible)
                {
                    var formatText = textureFormatComboBox.SelectedItem?.ToString();
                    if (formatText != null && formatText != "All")
                    {
                        var formatName = formatText.Substring(0, formatText.IndexOf(" ("));
                        var textureFormat = (TextureFormat)Enum.Parse(typeof(TextureFormat), formatName);
                        visibleAssets = visibleAssets.FindAll(x =>
                            x.Asset is Texture2D tex && tex.m_TextureFormat == textureFormat);
                    }
                }
            }
            else
            {
                visibleAssets = exportableAssets;
            }
            if (Properties.Settings.Default.modelsOnly)
            {
                var models = visibleAssets.FindAll(x => x.Type == ClassIDType.Animator || x.Type == ClassIDType.GameObject);
                foreach (var model in models)
                {
                    var hasModel = model.Asset switch
                    {
                        GameObject m_GameObject => m_GameObject.HasModel(),
                        Animator m_Animator => m_Animator.m_GameObject.TryGet(out var gameObject) && gameObject.HasModel(),
                        _ => throw new NotImplementedException()
                    };
                    if (!hasModel)
                    {
                        visibleAssets.Remove(model);
                    }
                }
            }
            if (!string.IsNullOrEmpty(listSearch.Text))
            {
                try
                {
                    Regex.Match("", listSearch.Text, RegexOptions.IgnoreCase);
                }
                catch (Exception ex)
                {
                    Logger.Error("Invalid Regex.\n" + ex.Message);
                    listSearch.Text = "";
                }
                var regex = new Regex(listSearch.Text, RegexOptions.IgnoreCase);
                visibleAssets = visibleAssets.FindAll(
                    x => regex.IsMatch(x.Text) ||
                    regex.IsMatch(x.SubItems[1].Text) ||
                    regex.IsMatch(x.SubItems[3].Text));
            }
            
            // 应用文件大小筛选
            if (float.TryParse(sizeFilterTextBox.Text, out float minSizeMB) && minSizeMB > 0)
            {
                visibleAssets = visibleAssets.FindAll(x => x.FullSize / (1024f * 1024f) > minSizeMB);
            }
            
            assetListView.VirtualListSize = visibleAssets.Count;
            assetListView.EndUpdate();

           long totalSize = 0;
           foreach (var asset in visibleAssets)
           {
               totalSize += asset.FullSize;
           }
           totalSizeLabel.Text = $"Total Size: {ToMiB(totalSize)}";
        }

       private static string ToMiB(long size)
       {
           return $"{size / (1024f * 1024f):F2} MiB";
       }

        private async void ExportAssets(ExportFilter type, ExportType exportType)
        {
            if (exportableAssets.Count > 0)
            {
                var saveFolderDialog = new OpenFolderDialog();
                saveFolderDialog.InitialFolder = saveDirectoryBackup;
                if (saveFolderDialog.ShowDialog(this) == DialogResult.OK)
                {
                    timer.Stop();
                    saveDirectoryBackup = saveFolderDialog.Folder;
                    List<AssetItem> toExportAssets = null;
                    switch (type)
                    {
                        case ExportFilter.All:
                            toExportAssets = exportableAssets;
                            break;
                        case ExportFilter.Selected:
                            toExportAssets = GetSelectedAssets();
                            break;
                        case ExportFilter.Filtered:
                            toExportAssets = visibleAssets;
                            break;
                    }
                    await Studio.ExportAssets(saveFolderDialog.Folder, toExportAssets, exportType, Properties.Settings.Default.openAfterExport);
                }
            }
            else
            {
                StatusStripUpdate("No exportable assets loaded");
            }
        }

        private void ExportAssetsList(ExportFilter type)
        {
            // XXX: Only exporting as XML for now, but would JSON(/CSV/other) be useful too?

            if (exportableAssets.Count > 0)
            {
                var saveFolderDialog = new OpenFolderDialog();
                saveFolderDialog.InitialFolder = saveDirectoryBackup;
                if (saveFolderDialog.ShowDialog(this) == DialogResult.OK)
                {
                    timer.Stop();
                    saveDirectoryBackup = saveFolderDialog.Folder;
                    List<AssetItem> toExportAssets = null;
                    switch (type)
                    {
                        case ExportFilter.All:
                            toExportAssets = exportableAssets;
                            break;
                        case ExportFilter.Selected:
                            toExportAssets = GetSelectedAssets();
                            break;
                        case ExportFilter.Filtered:
                            toExportAssets = visibleAssets;
                            break;
                    }
                    Studio.ExportAssetsList(saveFolderDialog.Folder, toExportAssets, ExportListType.XML);
                }
            }
            else
            {
                StatusStripUpdate("No exportable assets loaded");
            }
        }

        private void toolStripMenuItem15_Click(object sender, EventArgs e)
        {
            logger.ShowErrorMessage = toolStripMenuItem15.Checked;
        }
        private async void toolStripMenuItem19_DropDownOpening(object sender, EventArgs e)
        {
            if (specifyAIVersion.Enabled && await AIVersionManager.FetchVersions())
            {
                UpdateVersionList();
            }
        }

        private void miscToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            if (miscToolStripMenuItem.Enabled)
            {
                MapNameComboBox.Items.Clear();
                MapNameComboBox.Items.AddRange(AssetsHelper.GetMaps());
            }
        }

        private async void toolStripComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (specifyAIVersion.SelectedIndex == 0)
            {
                return;
            }
            if (skipContainer.Checked)
            {
                Logger.Info("Skip container is enabled, aborting...");
                return;
            }
            optionsToolStripMenuItem.DropDown.Visible = false;
            var version = specifyAIVersion.SelectedItem.ToString();

            if (version.Contains(' '))
            {
                version = version.Split(' ')[0];
            }

            Logger.Info($"Loading AI v{version}");
            InvokeUpdate(specifyAIVersion, false);
            var path = await AIVersionManager.FetchAI(version);
            await Task.Run(() => ResourceIndex.FromFile(path));
            UpdateContainers();
            UpdateVersionList();
            InvokeUpdate(specifyAIVersion, true);
        }

        private void UpdateVersionList()
        {
            var selectedIndex = specifyAIVersion.SelectedIndex;
            specifyAIVersion.Items.Clear();
            specifyAIVersion.Items.Add("None");

            var versions = AIVersionManager.GetVersions();
            foreach (var version in versions)
            {
                specifyAIVersion.Items.Add(version.Item1 + (version.Item2 ? " (cached)" : ""));
            }

            specifyAIVersion.SelectedIndexChanged -= new EventHandler(toolStripComboBox1_SelectedIndexChanged);
            specifyAIVersion.SelectedIndex = selectedIndex;
            specifyAIVersion.SelectedIndexChanged += new EventHandler(toolStripComboBox1_SelectedIndexChanged);
        }

        private void UpdateContainers()
        {
            if (exportableAssets.Count > 0)
            {
                Logger.Info("Updating Containers...");
                assetListView.BeginUpdate();
                foreach (var asset in exportableAssets)
                {
                    if (int.TryParse(asset.Container, out var value))
                    {
                        var last = unchecked((uint)value);
                        var name = Path.GetFileNameWithoutExtension(asset.SourceFile.originalPath);
                        if (uint.TryParse(name, out var id))
                        {
                            var path = ResourceIndex.GetContainer(id, last);
                            if (!string.IsNullOrEmpty(path))
                            {
                                asset.Container = path;
                                asset.SubItems[1].Text = path;
                                if (asset.Type == ClassIDType.MiHoYoBinData)
                                {
                                    asset.Text = Path.GetFileNameWithoutExtension(path);
                                }
                            }
                        }
                    }
                }
                assetListView.EndUpdate();
                Logger.Info("Updated !!");
            }
        }

        private void InvokeUpdate(ToolStripItem item, bool value)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => { item.Enabled = value; }));
            }
            else
            {
                item.Enabled = value;
            }
        }

        private void tabControl2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl2.SelectedIndex == 1 && lastSelectedItem != null)
            {
                dumpTextBox.Text = DumpAsset(lastSelectedItem.Asset);
            }
        }
        private void enableResolveDependencies_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.enableResolveDependencies = enableResolveDependencies.Checked;
            Properties.Settings.Default.Save();

            assetsManager.ResolveDependencies = enableResolveDependencies.Checked;
        }
        private void allowDuplicates_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.allowDuplicates = allowDuplicates.Checked;
            Properties.Settings.Default.Save();
        }
        private void skipContainer_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.skipContainer = skipContainer.Checked;
            Properties.Settings.Default.Save();

            SkipContainer = skipContainer.Checked;
        }
        private void assetMapTypeMenuItem_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            var assetMapType = Properties.Settings.Default.assetMapType;
            if (e.ClickedItem is ToolStripMenuItem item)
            {
                if (item.Checked)
                {
                    assetMapType -= (int)item.Tag;
                }
                else
                {
                    assetMapType += (int)item.Tag;
                }

                Properties.Settings.Default.assetMapType = assetMapType;
                Properties.Settings.Default.Save();
            }

        }
        private void modelsOnly_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.modelsOnly = modelsOnly.Checked;
            Properties.Settings.Default.Save();

            if (visibleAssets.Count > 0)
            {
                FilterAssetList();
            }
        }
        private void enableModelPreview_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.enableModelPreview = enableModelPreview.Checked;
            Properties.Settings.Default.Save();
        }

        private void specifyGame_SelectedIndexChanged(object sender, EventArgs e)
        {
            optionsToolStripMenuItem.DropDown.Visible = false;
            Properties.Settings.Default.selectedGame = specifyGame.SelectedIndex;
            Properties.Settings.Default.Save();

            ResetForm();

            Studio.Game = GameManager.GetGame(Properties.Settings.Default.selectedGame);
            Logger.Info($"Target Game is {Studio.Game.Name}");

            if (Studio.Game.Type.IsUnityCN())
            {
                UnityCNManager.SetKey(Properties.Settings.Default.selectedUnityCNKey);
            }

            assetsManager.SpecifyUnityVersion = specifyUnityVersion.Text;
            assetsManager.Game = Studio.Game;
        }

        private async void specifyNameComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            miscToolStripMenuItem.DropDown.Visible = false;
            InvokeUpdate(miscToolStripMenuItem, false);

            ResetForm();

            var name = MapNameComboBox.SelectedItem.ToString();
            await Task.Run(() =>
            {
                if (AssetsHelper.LoadCABMapInternal(name))
                {
                    Properties.Settings.Default.selectedCABMapName = name;
                    Properties.Settings.Default.Save();
                }
            });

            assetsManager.SpecifyUnityVersion = specifyUnityVersion.Text;
            assetsManager.Game = Studio.Game;

            InvokeUpdate(miscToolStripMenuItem, true);
        }

        private async void buildMapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            miscToolStripMenuItem.DropDown.Visible = false;
            InvokeUpdate(miscToolStripMenuItem, false);

            var input = MapNameComboBox.Text;
            var selectedText = MapNameComboBox.SelectedText;
            var name = "";

            if (!string.IsNullOrEmpty(selectedText))
            {
                name = selectedText;
            }
            else if (!string.IsNullOrEmpty(input))
            {
                if (input.IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
                {
                    Logger.Warning("Name has invalid characters !!");
                    InvokeUpdate(miscToolStripMenuItem, true);
                    return;
                }

                name = input;
            }
            else
            {
                Logger.Error("Map name is empty, please enter any name in ComboBox above");
                InvokeUpdate(miscToolStripMenuItem, true);
                return;
            }

            if (File.Exists(Path.Combine(AssetsHelper.MapName, $"{name}.bin")))
            {
                var acceptOverride = MessageBox.Show("Map already exist, Do you want to override it ?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (acceptOverride != DialogResult.Yes)
                {
                    InvokeUpdate(miscToolStripMenuItem, true);
                    return;
                }
            }

            var version = specifyUnityVersion.Text;
            var openFolderDialog = new OpenFolderDialog();
            openFolderDialog.Title = "Select Game Folder";
            if (openFolderDialog.ShowDialog(this) == DialogResult.OK)
            {
                Logger.Info("Scanning for files...");
                var files = Directory.GetFiles(openFolderDialog.Folder, "*.*", SearchOption.AllDirectories).ToArray();
                Logger.Info($"Found {files.Length} files");
                AssetsHelper.SetUnityVersion(version);
                await Task.Run(() => AssetsHelper.BuildCABMap(files, name, openFolderDialog.Folder, Studio.Game));
            }
            InvokeUpdate(miscToolStripMenuItem, true);
        }

        private async void buildBothToolStripMenuItem_Click(object sender, EventArgs e)
        {
            miscToolStripMenuItem.DropDown.Visible = false;
            InvokeUpdate(miscToolStripMenuItem, false);

            var input = MapNameComboBox.Text;
            var selectedText = MapNameComboBox.SelectedText;
            var exportListType = (ExportListType)assetMapTypeMenuItem.DropDownItems.Cast<ToolStripMenuItem>().Select(x => x.Checked ? (int)x.Tag : 0).Sum();
            var name = "";

            if (!string.IsNullOrEmpty(selectedText))
            {
                name = selectedText;
            }
            else if (!string.IsNullOrEmpty(input))
            {
                if (input.IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
                {
                    Logger.Warning("Name has invalid characters !!");
                    InvokeUpdate(miscToolStripMenuItem, true);
                    return;
                }

                name = input;
            }
            else
            {
                Logger.Error("Map name is empty, please enter any name in ComboBox above");
                InvokeUpdate(miscToolStripMenuItem, true);
                return;
            }

            if (File.Exists(Path.Combine(AssetsHelper.MapName, $"{name}.bin")))
            {
                var acceptOverride = MessageBox.Show("Map already exist, Do you want to override it ?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (acceptOverride != DialogResult.Yes)
                {
                    InvokeUpdate(miscToolStripMenuItem, true);
                    return;
                }
            }

            var version = specifyUnityVersion.Text;
            var openFolderDialog = new OpenFolderDialog();
            openFolderDialog.Title = "Select Game Folder";
            if (openFolderDialog.ShowDialog(this) == DialogResult.OK)
            {
                Logger.Info("Scanning for files...");
                var files = Directory.GetFiles(openFolderDialog.Folder, "*.*", SearchOption.AllDirectories).ToArray();
                Logger.Info($"Found {files.Length} files");

                var saveFolderDialog = new OpenFolderDialog();
                saveFolderDialog.InitialFolder = saveDirectoryBackup;
                saveFolderDialog.Title = "Select Output Folder";
                if (saveFolderDialog.ShowDialog(this) == DialogResult.OK)
                {
                    saveDirectoryBackup = saveFolderDialog.Folder;
                    AssetsHelper.SetUnityVersion(version);
                    await Task.Run(() => AssetsHelper.BuildBoth(files, name, openFolderDialog.Folder, Studio.Game, saveFolderDialog.Folder, exportListType));
                }
            }
            InvokeUpdate(miscToolStripMenuItem, true);
        }

        private void clearMapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            miscToolStripMenuItem.DropDown.Visible = false;
            InvokeUpdate(miscToolStripMenuItem, false);

            var acceptDelete = MessageBox.Show("Map will be deleted, this can't be undone, continue ?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (acceptDelete != DialogResult.Yes)
            {
                InvokeUpdate(miscToolStripMenuItem, true);
                return;
            }

            var name = MapNameComboBox.Text.ToString();
            var path = Path.Combine(AssetsHelper.MapName, $"{name}.bin");
            if (File.Exists(path))
            {
                File.Delete(path);
                Logger.Info($"{name} deleted successfully !!");
                MapNameComboBox.SelectedIndexChanged -= new EventHandler(specifyNameComboBox_SelectedIndexChanged);
                MapNameComboBox.SelectedIndex = 0;
                MapNameComboBox.SelectedIndexChanged += new EventHandler(specifyNameComboBox_SelectedIndexChanged);
            }

            InvokeUpdate(miscToolStripMenuItem, true);
        }

        private void resetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ResetForm();
            AssetsHelper.Clear();
            assetBrowser?.Clear();
            assetsManager.SpecifyUnityVersion = specifyUnityVersion.Text;
            assetsManager.Game = Studio.Game;
        }

        private void enableConsole_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.enableConsole = enableConsole.Checked;
            Properties.Settings.Default.Save();

            var handle = ConsoleHelper.GetConsoleWindow();
            if (enableConsole.Checked)
            {
                Logger.Default = new ConsoleLogger();
                ConsoleHelper.ShowWindow(handle, ConsoleHelper.SW_SHOW);
            }
            else
            {
                Logger.Default = logger;
                ConsoleHelper.ShowWindow(handle, ConsoleHelper.SW_HIDE);
            }
        }

        private void enableFileLogging_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.enableFileLogging = enableFileLogging.Checked;
            Properties.Settings.Default.Save();

            Logger.FileLogging = enableFileLogging.Checked;
        }

        private void loggedEventsMenuItem_DropDownClosing(object sender, ToolStripDropDownClosingEventArgs e)
        {
            if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
            {
                e.Cancel = true;
            }
        }

        private void loggedEventsMenuItem_DropDownClosed(object sender, EventArgs e)
        {
            Properties.Settings.Default.loggerEventType = loggedEventsMenuItem.DropDownItems.Cast<ToolStripMenuItem>().Select(x => x.Checked ? (int)x.Tag : 0).Sum();
            Properties.Settings.Default.Save();

            Logger.Flags = (LoggerEvent)Properties.Settings.Default.loggerEventType;
        }

        private void abortStripMenuItem_Click(object sender, EventArgs e)
        {
            Logger.Info("Aborting....");
            assetsManager.tokenSource.Cancel();
            AssetsHelper.tokenSource.Cancel();
        }

        private async void loadAIToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (skipContainer.Checked)
            {
                Logger.Info("Skip container is enabled, aborting...");
                return;
            }
            miscToolStripMenuItem.DropDown.Visible = false;

            var openFileDialog = new OpenFileDialog() { Multiselect = false, Filter = "Asset Index JSON File|*.json" };
            if (openFileDialog.ShowDialog(this) == DialogResult.OK)
            {
                var path = openFileDialog.FileName;
                Logger.Info($"Loading AI...");
                InvokeUpdate(loadAIToolStripMenuItem, false);
                await Task.Run(() => ResourceIndex.FromFile(path));
                UpdateContainers();
                InvokeUpdate(loadAIToolStripMenuItem, true);
            }
        }

        private async void loadCABMapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            miscToolStripMenuItem.DropDown.Visible = false;

            var openFileDialog = new OpenFileDialog() { Multiselect = false, Filter = "CABMap File|*.bin" };
            if (openFileDialog.ShowDialog(this) == DialogResult.OK)
            {
                var path = openFileDialog.FileName;
                InvokeUpdate(loadCABMapToolStripMenuItem, false);
                await Task.Run(() => AssetsHelper.LoadCABMap(path));
                InvokeUpdate(loadCABMapToolStripMenuItem, true);
            }
        }

        private void clearConsoleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Console.Clear();
        }

        private async void buildAssetMapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            miscToolStripMenuItem.DropDown.Visible = false;
            InvokeUpdate(miscToolStripMenuItem, false);

            var input = assetMapNameTextBox.Text;
            var exportListType = (ExportListType)assetMapTypeMenuItem.DropDownItems.Cast<ToolStripMenuItem>().Select(x => x.Checked ? (int)x.Tag : 0).Sum();
            var name = "assets_map";

            if (!string.IsNullOrEmpty(input))
            {
                if (input.IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
                {
                    Logger.Warning("Name has invalid characters !!");
                    InvokeUpdate(miscToolStripMenuItem, true);
                    return;
                }

                name = input;
            }

            var version = specifyUnityVersion.Text;
            var openFolderDialog = new OpenFolderDialog();
            openFolderDialog.Title = $"Select Game Folder";
            if (openFolderDialog.ShowDialog(this) == DialogResult.OK)
            {
                Logger.Info("Scanning for files...");
                var files = Directory.GetFiles(openFolderDialog.Folder, "*.*", SearchOption.AllDirectories).ToArray();
                Logger.Info($"Found {files.Length} files");

                var saveFolderDialog = new OpenFolderDialog();
                saveFolderDialog.InitialFolder = saveDirectoryBackup;
                saveFolderDialog.Title = "Select Output Folder";
                if (saveFolderDialog.ShowDialog(this) == DialogResult.OK)
                {
                    AssetsHelper.SetUnityVersion(version);
                    await Task.Run(() => AssetsHelper.BuildAssetMap(files, name, Studio.Game, saveFolderDialog.Folder, exportListType));
                }
            }
            InvokeUpdate(miscToolStripMenuItem, true);
        }

        private void loadAssetMapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            assetBrowser = new AssetBrowser(this);
            assetBrowser.Show();
        }

        private void specifyUnityCNKey_Click(object sender, EventArgs e)
        {
            var unitycn = new UnityCNForm();
            unitycn.Show();
        }

        #region FMOD
        private void FMODinit()
        {
            FMODreset();

            var result = FMOD.Factory.System_Create(out system);
            if (ERRCHECK(result)) { return; }

            result = system.getVersion(out var version);
            ERRCHECK(result);
            if (version < FMOD.VERSION.number)
            {
                Logger.Error($"Error!  You are using an old version of FMOD {version:X}.  This program requires {FMOD.VERSION.number:X}.");
                Application.Exit();
            }

            result = system.init(2, FMOD.INITFLAGS.NORMAL, IntPtr.Zero);
            if (ERRCHECK(result)) { return; }

            result = system.getMasterSoundGroup(out masterSoundGroup);
            if (ERRCHECK(result)) { return; }

            result = masterSoundGroup.setVolume(FMODVolume);
            if (ERRCHECK(result)) { return; }
        }

        private void FMODreset()
        {
            timer.Stop();
            FMODprogressBar.Value = 0;
            FMODtimerLabel.Text = "0:00.0 / 0:00.0";
            FMODstatusLabel.Text = "Stopped";
            FMODinfoLabel.Text = "";

            if (sound != null && sound.isValid())
            {
                var result = sound.release();
                ERRCHECK(result);
                sound = null;
            }
        }

        private void FMODplayButton_Click(object sender, EventArgs e)
        {
            if (sound != null && channel != null)
            {
                timer.Start();
                var result = channel.isPlaying(out var playing);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    if (ERRCHECK(result)) { return; }
                }

                if (playing)
                {
                    result = channel.stop();
                    if (ERRCHECK(result)) { return; }

                    result = system.playSound(sound, null, false, out channel);
                    if (ERRCHECK(result)) { return; }

                    FMODpauseButton.Text = "Pause";
                }
                else
                {
                    result = system.playSound(sound, null, false, out channel);
                    if (ERRCHECK(result)) { return; }
                    FMODstatusLabel.Text = "Playing";

                    if (FMODprogressBar.Value > 0)
                    {
                        uint newms = FMODlenms / 1000 * (uint)FMODprogressBar.Value;

                        result = channel.setPosition(newms, FMOD.TIMEUNIT.MS);
                        if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                        {
                            if (ERRCHECK(result)) { return; }
                        }

                    }
                }
            }
        }

        private void FMODpauseButton_Click(object sender, EventArgs e)
        {
            if (sound != null && channel != null)
            {
                var result = channel.isPlaying(out var playing);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    if (ERRCHECK(result)) { return; }
                }

                if (playing)
                {
                    result = channel.getPaused(out var paused);
                    if (ERRCHECK(result)) { return; }
                    result = channel.setPaused(!paused);
                    if (ERRCHECK(result)) { return; }

                    if (paused)
                    {
                        FMODstatusLabel.Text = "Playing";
                        FMODpauseButton.Text = "Pause";
                        timer.Start();
                    }
                    else
                    {
                        FMODstatusLabel.Text = "Paused";
                        FMODpauseButton.Text = "Resume";
                        timer.Stop();
                    }
                }
            }
        }

        private void FMODstopButton_Click(object sender, EventArgs e)
        {
            if (channel != null)
            {
                var result = channel.isPlaying(out var playing);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    if (ERRCHECK(result)) { return; }
                }

                if (playing)
                {
                    result = channel.stop();
                    if (ERRCHECK(result)) { return; }
                    //channel = null;
                    //don't FMODreset, it will nullify the sound
                    timer.Stop();
                    FMODprogressBar.Value = 0;
                    FMODtimerLabel.Text = "0:00.0 / 0:00.0";
                    FMODstatusLabel.Text = "Stopped";
                    FMODpauseButton.Text = "Pause";
                }
            }
        }

        private void FMODloopButton_CheckedChanged(object sender, EventArgs e)
        {
            FMOD.RESULT result;

            loopMode = FMODloopButton.Checked ? FMOD.MODE.LOOP_NORMAL : FMOD.MODE.LOOP_OFF;

            if (sound != null)
            {
                result = sound.setMode(loopMode);
                if (ERRCHECK(result)) { return; }
            }

            if (channel != null)
            {
                result = channel.isPlaying(out var playing);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    if (ERRCHECK(result)) { return; }
                }

                result = channel.getPaused(out var paused);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    if (ERRCHECK(result)) { return; }
                }

                if (playing || paused)
                {
                    result = channel.setMode(loopMode);
                    if (ERRCHECK(result)) { return; }
                }
            }
        }

        private void FMODvolumeBar_ValueChanged(object sender, EventArgs e)
        {
            FMODVolume = Convert.ToSingle(FMODvolumeBar.Value) / 10;

            var result = masterSoundGroup.setVolume(FMODVolume);
            if (ERRCHECK(result)) { return; }
        }

        private void FMODprogressBar_Scroll(object sender, EventArgs e)
        {
            if (channel != null)
            {
                uint newms = FMODlenms / 1000 * (uint)FMODprogressBar.Value;
                FMODtimerLabel.Text = $"{newms / 1000 / 60}:{newms / 1000 % 60}.{newms / 10 % 100}/{FMODlenms / 1000 / 60}:{FMODlenms / 1000 % 60}.{FMODlenms / 10 % 100}";
            }
        }

        private void FMODprogressBar_MouseDown(object sender, MouseEventArgs e)
        {
            timer.Stop();
        }

        private void FMODprogressBar_MouseUp(object sender, MouseEventArgs e)
        {
            if (channel != null)
            {
                uint newms = FMODlenms / 1000 * (uint)FMODprogressBar.Value;

                var result = channel.setPosition(newms, FMOD.TIMEUNIT.MS);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    if (ERRCHECK(result)) { return; }
                }


                result = channel.isPlaying(out var playing);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    if (ERRCHECK(result)) { return; }
                }

                if (playing) { timer.Start(); }
            }
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            uint ms = 0;
            bool playing = false;
            bool paused = false;

            if (channel != null)
            {
                var result = channel.getPosition(out ms, FMOD.TIMEUNIT.MS);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    ERRCHECK(result);
                }

                result = channel.isPlaying(out playing);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    ERRCHECK(result);
                }

                result = channel.getPaused(out paused);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    ERRCHECK(result);
                }
            }

            FMODtimerLabel.Text = $"{ms / 1000 / 60}:{ms / 1000 % 60}.{ms / 10 % 100} / {FMODlenms / 1000 / 60}:{FMODlenms / 1000 % 60}.{FMODlenms / 10 % 100}";
            FMODprogressBar.Value = (int)(ms * 1000 / FMODlenms);
            FMODstatusLabel.Text = paused ? "Paused " : playing ? "Playing" : "Stopped";

            if (system != null && channel != null)
            {
                system.update();
            }
        }

        private bool ERRCHECK(FMOD.RESULT result)
        {
            if (result != FMOD.RESULT.OK)
            {
                FMODreset();
                StatusStripUpdate($"FMOD error! {result} - {FMOD.Error.String(result)}");
                return true;
            }
            return false;
        }
        #endregion

        #region GLControl
        private void InitOpenTK()
        {
            ChangeGLSize(glControl.Size);
            GL.ClearColor(System.Drawing.Color.CadetBlue);
            pgmID = GL.CreateProgram();
            LoadShader("vs", ShaderType.VertexShader, pgmID, out int vsID);
            LoadShader("fs", ShaderType.FragmentShader, pgmID, out int fsID);
            GL.LinkProgram(pgmID);

            pgmColorID = GL.CreateProgram();
            LoadShader("vs", ShaderType.VertexShader, pgmColorID, out vsID);
            LoadShader("fsColor", ShaderType.FragmentShader, pgmColorID, out fsID);
            GL.LinkProgram(pgmColorID);

            pgmBlackID = GL.CreateProgram();
            LoadShader("vs", ShaderType.VertexShader, pgmBlackID, out vsID);
            LoadShader("fsBlack", ShaderType.FragmentShader, pgmBlackID, out fsID);
            GL.LinkProgram(pgmBlackID);

            attributeVertexPosition = GL.GetAttribLocation(pgmID, "vertexPosition");
            attributeNormalDirection = GL.GetAttribLocation(pgmID, "normalDirection");
            attributeVertexColor = GL.GetAttribLocation(pgmColorID, "vertexColor");
            uniformModelMatrix = GL.GetUniformLocation(pgmID, "modelMatrix");
            uniformViewMatrix = GL.GetUniformLocation(pgmID, "viewMatrix");
            uniformProjMatrix = GL.GetUniformLocation(pgmID, "projMatrix");
        }

        private static void LoadShader(string filename, ShaderType type, int program, out int address)
        {
            address = GL.CreateShader(type);
            var str = (string)Properties.Resources.ResourceManager.GetObject(filename);
            GL.ShaderSource(address, str);
            GL.CompileShader(address);
            GL.AttachShader(program, address);
            GL.DeleteShader(address);
        }

        private static void CreateVBO(out int vboAddress, OpenTK.Mathematics.Vector3[] data, int address)
        {
            GL.GenBuffers(1, out vboAddress);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboAddress);
            GL.BufferData(BufferTarget.ArrayBuffer,
                                    (IntPtr)(data.Length * OpenTK.Mathematics.Vector3.SizeInBytes),
                                    data,
                                    BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(address, 3, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(address);
        }

        private static void CreateVBO(out int vboAddress, OpenTK.Mathematics.Vector4[] data, int address)
        {
            GL.GenBuffers(1, out vboAddress);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboAddress);
            GL.BufferData(BufferTarget.ArrayBuffer,
                                    (IntPtr)(data.Length * OpenTK.Mathematics.Vector4.SizeInBytes),
                                    data,
                                    BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(address, 4, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(address);
        }

        private static void CreateVBO(out int vboAddress, Matrix4 data, int address)
        {
            GL.GenBuffers(1, out vboAddress);
            GL.UniformMatrix4(address, false, ref data);
        }

        private static void CreateEBO(out int address, int[] data)
        {
            GL.GenBuffers(1, out address);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, address);
            GL.BufferData(BufferTarget.ElementArrayBuffer,
                            (IntPtr)(data.Length * sizeof(int)),
                            data,
                            BufferUsageHint.StaticDraw);
        }

        private void CreateVAO()
        {
            GL.DeleteVertexArray(vao);
            GL.GenVertexArrays(1, out vao);
            GL.BindVertexArray(vao);
            CreateVBO(out var vboPositions, vertexData, attributeVertexPosition);
            if (normalMode == 0)
            {
                CreateVBO(out var vboNormals, normal2Data, attributeNormalDirection);
            }
            else
            {
                if (normalData != null)
                    CreateVBO(out var vboNormals, normalData, attributeNormalDirection);
            }
            CreateVBO(out var vboColors, colorData, attributeVertexColor);
            CreateVBO(out var vboModelMatrix, modelMatrixData, uniformModelMatrix);
            CreateVBO(out var vboViewMatrix, viewMatrixData, uniformViewMatrix);
            CreateVBO(out var vboProjMatrix, projMatrixData, uniformProjMatrix);
            CreateEBO(out var eboElements, indiceData);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);
        }

        private void ChangeGLSize(Size size)
        {
            GL.Viewport(0, 0, size.Width, size.Height);

            if (size.Width <= size.Height)
            {
                float k = 1.0f * size.Width / size.Height;
                projMatrixData = Matrix4.CreateScale(1, k, 1);
            }
            else
            {
                float k = 1.0f * size.Height / size.Width;
                projMatrixData = Matrix4.CreateScale(k, 1, 1);
            }
        }

        private void glControl_Load(object sender, EventArgs e)
        {
            InitOpenTK();
            glControlLoaded = true;
        }

        private void glControl_Paint(object sender, PaintEventArgs e)
        {
            glControl.MakeCurrent();
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.BindVertexArray(vao);
            if (wireFrameMode == 0 || wireFrameMode == 2)
            {
                GL.UseProgram(shadeMode == 0 ? pgmID : pgmColorID);
                GL.UniformMatrix4(uniformModelMatrix, false, ref modelMatrixData);
                GL.UniformMatrix4(uniformViewMatrix, false, ref viewMatrixData);
                GL.UniformMatrix4(uniformProjMatrix, false, ref projMatrixData);
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                GL.DrawElements(PrimitiveType.Triangles, indiceData.Length, DrawElementsType.UnsignedInt, 0);
            }
            //Wireframe
            if (wireFrameMode == 1 || wireFrameMode == 2)
            {
                GL.Enable(EnableCap.PolygonOffsetLine);
                GL.PolygonOffset(-1, -1);
                GL.UseProgram(pgmBlackID);
                GL.UniformMatrix4(uniformModelMatrix, false, ref modelMatrixData);
                GL.UniformMatrix4(uniformViewMatrix, false, ref viewMatrixData);
                GL.UniformMatrix4(uniformProjMatrix, false, ref projMatrixData);
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                GL.DrawElements(PrimitiveType.Triangles, indiceData.Length, DrawElementsType.UnsignedInt, 0);
                GL.Disable(EnableCap.PolygonOffsetLine);
            }
            GL.BindVertexArray(0);
            GL.Flush();
            glControl.SwapBuffers();
        }

        private void glControl_MouseWheel(object sender, MouseEventArgs e)
        {
            if (glControl.Visible)
            {
                viewMatrixData *= Matrix4.CreateScale(1 + e.Delta / 1000f);
                glControl.Invalidate();
            }
        }

        private void glControl_MouseDown(object sender, MouseEventArgs e)
        {
            mdx = e.X;
            mdy = e.Y;
            if (e.Button == MouseButtons.Left)
            {
                lmdown = true;
            }
            if (e.Button == MouseButtons.Right)
            {
                rmdown = true;
            }
        }

        private void glControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (lmdown || rmdown)
            {
                float dx = mdx - e.X;
                float dy = mdy - e.Y;
                mdx = e.X;
                mdy = e.Y;
                if (lmdown)
                {
                    dx *= 0.01f;
                    dy *= 0.01f;
                    viewMatrixData *= Matrix4.CreateRotationX(dy);
                    viewMatrixData *= Matrix4.CreateRotationY(dx);
                }
                if (rmdown)
                {
                    dx *= 0.003f;
                    dy *= 0.003f;
                    viewMatrixData *= Matrix4.CreateTranslation(-dx, dy, 0);
                }
                glControl.Invalidate();
            }
        }

        private void glControl_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                lmdown = false;
            }
            if (e.Button == MouseButtons.Right)
            {
                rmdown = false;
            }
        }
        #endregion

        private void textureDetectionComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            // 当下拉框选择改变时，重新执行检测
            if (exportableAssets.Count > 0)
            {
                PerformTextureDetection();
            }
        }

        private void PerformTextureDetection()
        {
            uncompressedTextures.Clear();
            
            switch (textureDetectionComboBox.SelectedIndex)
            {
                case 0:
                    // 选项一：未压缩纹理检测
                    DetectUncompressedTextures();
                    break;
                case 1:
                    // 选项二：2048x4096纹理检测
                    Detect2048x4096Textures();
                    break;
                case 2:
                    // 选项三：启用读写的纹理检测
                    DetectReadWriteEnabledTextures();
                    break;
                case 3:
                    // 选项四：启用Mipmaps的纹理检测
                    DetectMipmapEnabledTextures();
                    break;
                case 4:
                    // 选项五：图片规范压缩失效纹理检测
                    DetectInvalidCompressionTextures();
                    break;
            }
        }

        private void DetectUncompressedTextures()
        {
            var uncompressedFormats = new[] {
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
            uncompressedTextures.Clear();
            long totalSize = 0;
            foreach (var asset in exportableAssets)
            {
                if (asset.Type == ClassIDType.Texture2D && asset.Asset is Texture2D texture2D)
                {
                    if (uncompressedFormats.Contains(texture2D.m_TextureFormat))
                    {
                        // 创建新AssetItem并手动设置FullSize
                        var item = new AssetItem(asset.Asset);
                        item.Text = asset.Text;
                        item.Container = asset.Container;
                        item.FullSize = asset.FullSize;  // 手动设置FullSize
                        item.SubItems.AddRange(new string[] {
                            texture2D.m_TextureFormat.ToString(),
                            texture2D.m_Width.ToString(),
                            texture2D.m_Height.ToString(),
                            $"{asset.FullSize / 1024f:F2} KB",
                            asset.Container
                        });
                        item.SubItemValues.AddRange(new long[] {
                            texture2D.m_Width,
                            texture2D.m_Height
                        });
                        
                        uncompressedTextures.Add(item);
                        totalSize += asset.FullSize;
                    }
                }
            }
            uncompressedTexturesListView.VirtualListSize = uncompressedTextures.Count;
            uncompressedTextures.Sort((a, b) => b.FullSize.CompareTo(a.FullSize));

            uncompressedTotalLabel.Text = $"未压缩纹理总数: {uncompressedTextures.Count}  总大小: {totalSize / (1024f * 1024f):F2} MB";
            
            // 记录当前排序状态：Size列降序
            sortColumn = 4;
            reverseSort = true;
        }

        private void Detect2048x4096Textures()
        {
            long totalSize = 0;
            foreach (var asset in exportableAssets)
            {
                if (asset.Type == ClassIDType.Texture2D && asset.Asset is Texture2D texture2D)
                {
                    // 修改条件：只要宽度或高度大于等于2048就筛选出来
                    if (texture2D.m_Width >= 2048 || texture2D.m_Height >= 2048)
                    {
                        // 创建新AssetItem并手动设置FullSize
                        var item = new AssetItem(asset.Asset);
                        item.Text = asset.Text;
                        item.Container = asset.Container;
                        item.FullSize = asset.FullSize;  // 手动设置FullSize
                        item.SubItems.AddRange(new string[] {
                            texture2D.m_TextureFormat.ToString(),
                            texture2D.m_Width.ToString(),
                            texture2D.m_Height.ToString(),
                            $"{asset.FullSize / 1024f:F2} KB",
                            asset.Container
                        });
                        item.SubItemValues.AddRange(new long[] {
                            texture2D.m_Width,
                            texture2D.m_Height
                        });
                        
                        uncompressedTextures.Add(item);
                        totalSize += asset.FullSize;
                    }
                }
            }
            
            uncompressedTexturesListView.VirtualListSize = uncompressedTextures.Count;
            uncompressedTextures.Sort((a, b) => b.FullSize.CompareTo(a.FullSize));

            uncompressedTotalLabel.Text = $"大尺寸纹理(>=2048)总数: {uncompressedTextures.Count}  总大小: {totalSize / (1024f * 1024f):F2} MB";
            
            // 记录当前排序状态：Size列降序
            sortColumn = 4;
            reverseSort = true;
        }
        
        private void DetectReadWriteEnabledTextures()
        {
            long totalSize = 0;
            foreach (var asset in exportableAssets)
            {
                if (asset.Type == ClassIDType.Texture2D && asset.Asset is Texture2D texture2D)
                {
                    if (texture2D.m_IsReadable)
                    {
                        // 创建新AssetItem并手动设置FullSize
                        var item = new AssetItem(asset.Asset);
                        item.Text = asset.Text;
                        item.Container = asset.Container;
                        item.FullSize = asset.FullSize;
                        item.SubItems.AddRange(new string[] {
                            texture2D.m_TextureFormat.ToString(),
                            texture2D.m_Width.ToString(),
                            texture2D.m_Height.ToString(),
                            $"{asset.FullSize / 1024f:F2} KB",
                            asset.Container
                        });
                        item.SubItemValues.AddRange(new long[] {
                            texture2D.m_Width,
                            texture2D.m_Height
                        });
                        
                        uncompressedTextures.Add(item);
                        totalSize += asset.FullSize;
                    }
                }
            }
            
            uncompressedTexturesListView.VirtualListSize = uncompressedTextures.Count;
            uncompressedTextures.Sort((a, b) => b.FullSize.CompareTo(a.FullSize));

            uncompressedTotalLabel.Text = $"启用读写纹理总数: {uncompressedTextures.Count}  总大小: {totalSize / (1024f * 1024f):F2} MB";
            
            // 记录当前排序状态：Size列降序
            sortColumn = 4;
            reverseSort = true;
        }
        
        private void DetectMipmapEnabledTextures()
        {
            long totalSize = 0;
            foreach (var asset in exportableAssets)
            {
                if (asset.Type == ClassIDType.Texture2D && asset.Asset is Texture2D texture2D)
                {
                    // 检测是否启用了mipmaps (m_MipMap为true或m_MipCount>1)
                    bool hasMipmaps = texture2D.m_MipMap || texture2D.m_MipCount > 1;
                    if (hasMipmaps)
                    {
                        // 创建新AssetItem并手动设置FullSize
                        var item = new AssetItem(asset.Asset);
                        item.Text = asset.Text;
                        item.Container = asset.Container;
                        item.FullSize = asset.FullSize;
                        item.SubItems.AddRange(new string[] {
                            texture2D.m_TextureFormat.ToString(),
                            texture2D.m_Width.ToString(),
                            texture2D.m_Height.ToString(),
                            $"{asset.FullSize / 1024f:F2} KB",
                            asset.Container
                        });
                        item.SubItemValues.AddRange(new long[] {
                            texture2D.m_Width,
                            texture2D.m_Height
                        });
                        
                        uncompressedTextures.Add(item);
                        totalSize += asset.FullSize;
                    }
                }
            }
            
            uncompressedTexturesListView.VirtualListSize = uncompressedTextures.Count;
            uncompressedTextures.Sort((a, b) => b.FullSize.CompareTo(a.FullSize));

            uncompressedTotalLabel.Text = $"启用Mipmaps纹理总数: {uncompressedTextures.Count}  总大小: {totalSize / (1024f * 1024f):F2} MB";
            
            // 记录当前排序状态：Size列降序
            sortColumn = 4;
            reverseSort = true;
        }
        
        private void DetectInvalidCompressionTextures()
        {
            long totalSize = 0;
            foreach (var asset in exportableAssets)
            {
                if (asset.Type == ClassIDType.Texture2D && asset.Asset is Texture2D texture2D)
                {
                    // 检测条件：
                    // 1. 格式为RGBA32
                    // 2. 启用了Mipmaps (m_MipMap为true或m_MipCount>1)
                    // 3. 分辨率非2的幂次方
                    bool hasMipmaps = texture2D.m_MipMap || texture2D.m_MipCount > 1;
                    bool isRGBA32 = texture2D.m_TextureFormat == TextureFormat.RGBA32;
                    bool isPowerOfTwo = IsPowerOfTwo(texture2D.m_Width) && IsPowerOfTwo(texture2D.m_Height);
                    
                    if (isRGBA32 && hasMipmaps && !isPowerOfTwo)
                    {
                        // 创建新AssetItem并手动设置FullSize
                        var item = new AssetItem(asset.Asset);
                        item.Text = asset.Text;
                        item.Container = asset.Container;
                        item.FullSize = asset.FullSize;
                        item.SubItems.AddRange(new string[] {
                            texture2D.m_TextureFormat.ToString(),
                            texture2D.m_Width.ToString(),
                            texture2D.m_Height.ToString(),
                            $"{asset.FullSize / 1024f:F2} KB",
                            asset.Container
                        });
                        item.SubItemValues.AddRange(new long[] {
                            texture2D.m_Width,
                            texture2D.m_Height
                        });
                        
                        uncompressedTextures.Add(item);
                        totalSize += asset.FullSize;
                    }
                }
            }
            
            uncompressedTexturesListView.VirtualListSize = uncompressedTextures.Count;
            uncompressedTextures.Sort((a, b) => b.FullSize.CompareTo(a.FullSize));

            uncompressedTotalLabel.Text = $"图片规范压缩失效纹理总数: {uncompressedTextures.Count}  总大小: {totalSize / (1024f * 1024f):F2} MB";
            
            // 记录当前排序状态：Size列降序
            sortColumn = 4;
            reverseSort = true;
        }
        
        // 判断一个数是否为2的幂次方
        private bool IsPowerOfTwo(int n)
        {
            return n > 0 && (n & (n - 1)) == 0;
        }
        
        private void duplicateDetectionComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            // 当下拉框选择改变时，重新构建资产数据
            if (exportableAssets.Count > 0)
            {
                StatusStripUpdate("正在根据新规则检测重复资源...");
                allRedundantAssets.Clear();
                redundanzAssets.Clear();
                BuildDuplicateAssets();
                allRedundantAssets = new List<AssetItem>(redundanzAssets);
                FilterRedundantAssets(null, null);
                StatusStripUpdate($"检测完成，发现 {redundanzAssets.Count} 个重复资源组");
            }
        }
        
        private void UpdateRedundantStatistics()
        {
            // 计算冗余总大小和数量
            long totalRedundantSize = 0;
            int totalRedundantCount = 0;
            foreach (var asset in redundanzAssets)
            {
                if (asset.Gesamtzahl > 1)
                {
                    totalRedundantSize += asset.FullSize * (asset.Gesamtzahl - 1);
                    totalRedundantCount += asset.Gesamtzahl - 1;
                }
            }
            
            redundantTotalLabel.Text = $"重复资源组: {redundanzAssets.Count}  冗余数量: {totalRedundantCount}  冗余大小: {totalRedundantSize / (1024f * 1024f):F2} MB";
        }
        
        private void BuildDuplicateAssets()
        {
            var mode = duplicateDetectionComboBox.SelectedIndex;
            Studio.BuildRedundantAssets(mode);
        }
        
        private void FilterRedundantAssets(object sender, EventArgs e)
        {
            FilterRedundantList();
        }
        
        private void assetBundleSearch_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                Invoke(new Action(FilterAssetBundleTree));
            }
        }
        
        private void FilterAssetBundleTree()
        {
            if (assetBundleDict.Count == 0)
            {
                return;
            }
            
            assetBundleTreeView.BeginUpdate();
            assetBundleTreeView.Nodes.Clear();
            
            var searchText = assetBundleSearch.Text.Trim();
            var filteredDict = assetBundleDict;
            
            // 应用搜索过滤
            if (!string.IsNullOrEmpty(searchText))
            {
                try
                {
                    var regex = new Regex(searchText, RegexOptions.IgnoreCase);
                    filteredDict = assetBundleDict.Where(kvp =>
                        regex.IsMatch(kvp.Key) ||
                        kvp.Value.Any(asset => regex.IsMatch(asset.Text))
                    ).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                }
                catch (Exception ex)
                {
                    Logger.Error("Invalid Regex.\n" + ex.Message);
                    assetBundleSearch.Text = "";
                    assetBundleTreeView.EndUpdate();
                    return;
                }
            }
            
            // 应用大小筛选
            if (float.TryParse(bundleSizeFilterTextBox.Text, out float minSizeMB) && minSizeMB > 0)
            {
                filteredDict = filteredDict.Where(kvp =>
                    kvp.Value.Sum(a => a.FullSize) / (1024f * 1024f) > minSizeMB
                ).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
            
            BuildAssetBundleTree(filteredDict);
            assetBundleTreeView.EndUpdate();
        }
        
        private void bundleSizeFilterTextBox_TextChanged(object sender, EventArgs e)
        {
            // 使用防抖技术：重置Timer，延迟执行筛选
            bundleSizeFilterTimer.Stop();
            bundleSizeFilterTimer.Start();
        }
        
        private void BuildAssetBundleTree(Dictionary<string, List<AssetItem>> bundleDict)
        {
            long totalSize = 0;
            int totalBundles = 0;
            int totalAssets = 0;
            
            // 根据排序选项对AssetBundle进行排序
            IEnumerable<KeyValuePair<string, List<AssetItem>>> sortedBundles;
            var sortMode = assetBundleSortComboBox?.SelectedIndex ?? 0;
            
            switch (sortMode)
            {
                case 0: // 按大小排序
                    sortedBundles = bundleDict.OrderByDescending(x => x.Value.Sum(a => a.FullSize));
                    break;
                case 1: // 按名称排序
                    sortedBundles = bundleDict.OrderBy(x => x.Key);
                    break;
                case 2: // 按数量排序
                    sortedBundles = bundleDict.OrderByDescending(x => x.Value.Count);
                    break;
                default:
                    sortedBundles = bundleDict.OrderByDescending(x => x.Value.Sum(a => a.FullSize));
                    break;
            }
            
            foreach (var kvp in sortedBundles)
            {
                var bundleName = kvp.Key;
                var assets = kvp.Value;
                var bundleSize = assets.Sum(a => a.FullSize);
                
                var bundleNode = new TreeNode($"{bundleName} ({assets.Count} 资源, {bundleSize / (1024f * 1024f):F2} MB)");
                bundleNode.Tag = bundleName;
                
                foreach (var asset in assets.OrderByDescending(a => a.FullSize))
                {
                    var assetNode = new TreeNode($"{asset.Text} ({asset.TypeString}, {asset.FullSize / 1024f:F2} KB)");
                    assetNode.Tag = asset;
                    bundleNode.Nodes.Add(assetNode);
                }
                
                assetBundleTreeView.Nodes.Add(bundleNode);
                totalSize += bundleSize;
                totalBundles++;
                totalAssets += assets.Count;
            }
            
            assetBundleTotalLabel.Text = $"AssetBundle总数: {totalBundles}  资源总数: {totalAssets}  总大小: {totalSize / (1024f * 1024f):F2} MB";
        }
        
        private void assetBundleSortComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            // 重新构建树形结构，应用新的排序
            if (assetBundleDict.Count > 0)
            {
                assetBundleTreeView.BeginUpdate();
                assetBundleTreeView.Nodes.Clear();
                BuildAssetBundleTree(assetBundleDict);
                assetBundleTreeView.EndUpdate();
            }
        }
        
        private void assetBundleTreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node == null || e.Node.Tag == null)
            {
                return;
            }
            
            // 如果选中的是资源节点（第二层）
            if (e.Node.Tag is AssetItem assetItem)
            {
                // 清除之前的预览
                previewPanel.BackgroundImage = Properties.Resources.preview;
                previewPanel.BackgroundImageLayout = ImageLayout.Center;
                classTextBox.Visible = false;
                assetInfoLabel.Visible = false;
                assetInfoLabel.Text = null;
                textPreviewBox.Visible = false;
                fontPreviewBox.Visible = false;
                FMODpanel.Visible = false;
                glControl.Visible = false;
                StatusStripUpdate("");
                
                FMODreset();
                
                lastSelectedItem = assetItem;
                
                // 在右侧显示资源信息
                if (tabControl2.SelectedIndex == 1)
                {
                    dumpTextBox.Text = DumpAsset(lastSelectedItem.Asset);
                }
                if (enablePreview.Checked)
                {
                    PreviewAsset(lastSelectedItem);
                    if (displayInfo.Checked && lastSelectedItem.InfoText != null)
                    {
                        assetInfoLabel.Text = lastSelectedItem.InfoText;
                        assetInfoLabel.Visible = true;
                    }
                }
            }
        }
        
        private void BuildAssetBundleData()
        {
            StatusStripUpdate("正在收集AssetBundle数据...");
            assetBundleDict.Clear();
            
            // 遍历所有exportableAssets，按Container分组
            foreach (var asset in exportableAssets)
            {
                if (!string.IsNullOrEmpty(asset.Container))
                {
                    if (!assetBundleDict.ContainsKey(asset.Container))
                    {
                        assetBundleDict[asset.Container] = new List<AssetItem>();
                    }
                    assetBundleDict[asset.Container].Add(asset);
                }
            }
            
            // 构建树形结构
            BuildAssetBundleTree(assetBundleDict);
            
            StatusStripUpdate($"AssetBundle数据收集完成，共 {assetBundleDict.Count} 个AssetBundle");
        }
        
    }
}