using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AssetStudio.GUI
{
    /// <summary>
    /// 从 APK 里选择单个 bundle（可带依赖闭包），抽取到临时目录后交给
    /// AssetStudio 当普通目录加载。
    /// </summary>
    public sealed class LoadApkForm : Form
    {
        private const string DefaultPackage = "HotfixPkg";

        private readonly TextBox apkTextBox;
        private readonly Button browseButton;
        private readonly ComboBox packageCombo;
        private readonly Button readButton;
        private readonly Label summaryLabel;
        private readonly TextBox searchTextBox;
        private readonly ListView bundleListView;
        private readonly TextBox detailTextBox;
        private readonly CheckBox withDependenciesCheckBox;
        private readonly ProgressBar progressBar;
        private readonly Label statusLabel;
        private readonly Button loadButton;
        private readonly Button loadAllButton;
        private readonly Button cancelButton;

        private ApkBundleSource _source;
        private readonly List<int> _filtered = new List<int>();
        private bool _busy;
        private CancellationTokenSource _extractCts;
        private static string _lastApkPath;

        /// <summary>抽取目录，DialogResult 为 OK 时有效</summary>
        public string ExtractDirectory { get; private set; }

        /// <summary>选中的 bundle 名</summary>
        public string SelectedBundleName { get; private set; }

        public LoadApkForm()
        {
            Text = "Load APK";
            ClientSize = new System.Drawing.Size(840, 650);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            Label apkLabel = new Label
            {
                Text = "APK:",
                Location = new System.Drawing.Point(12, 15),
                AutoSize = true
            };

            apkTextBox = new TextBox
            {
                Location = new System.Drawing.Point(70, 12),
                Size = new System.Drawing.Size(640, 23)
            };

            browseButton = new Button
            {
                Text = "浏览",
                Location = new System.Drawing.Point(720, 11),
                Size = new System.Drawing.Size(100, 26)
            };
            browseButton.Click += BrowseButton_Click;

            Label packageLabel = new Label
            {
                Text = "包名:",
                Location = new System.Drawing.Point(12, 48),
                AutoSize = true
            };

            packageCombo = new ComboBox
            {
                Location = new System.Drawing.Point(70, 45),
                Size = new System.Drawing.Size(220, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            packageCombo.SelectedIndexChanged += PackageCombo_SelectedIndexChanged;

            readButton = new Button
            {
                Text = "读取清单",
                Location = new System.Drawing.Point(300, 44),
                Size = new System.Drawing.Size(100, 26),
                Enabled = false
            };
            readButton.Click += ReadButton_Click;

            summaryLabel = new Label
            {
                Location = new System.Drawing.Point(410, 48),
                Size = new System.Drawing.Size(420, 20),
                Text = string.Empty
            };

            Label searchLabel = new Label
            {
                Text = "搜索:",
                Location = new System.Drawing.Point(12, 81),
                AutoSize = true
            };

            searchTextBox = new TextBox
            {
                Location = new System.Drawing.Point(70, 78),
                Size = new System.Drawing.Size(750, 23),
                PlaceholderText = "bundle 名 / 资源 Address / AssetPath"
            };
            searchTextBox.TextChanged += SearchTextBox_TextChanged;

            bundleListView = new ListView
            {
                Location = new System.Drawing.Point(12, 110),
                Size = new System.Drawing.Size(808, 300),
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                MultiSelect = false,
                VirtualMode = true
            };
            bundleListView.Columns.Add("Bundle", 560);
            bundleListView.Columns.Add("大小", 90, HorizontalAlignment.Right);
            bundleListView.Columns.Add("状态", 70);
            bundleListView.Columns.Add("依赖", 60);
            bundleListView.RetrieveVirtualItem += BundleListView_RetrieveVirtualItem;
            bundleListView.SelectedIndexChanged += BundleListView_SelectedIndexChanged;
            bundleListView.DoubleClick += BundleListView_DoubleClick;

            Label detailLabel = new Label
            {
                Text = "详情:",
                Location = new System.Drawing.Point(12, 418),
                AutoSize = true
            };

            detailTextBox = new TextBox
            {
                Location = new System.Drawing.Point(12, 438),
                Size = new System.Drawing.Size(808, 120),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                WordWrap = false,
                Font = new System.Drawing.Font("Consolas", 9f)
            };

            withDependenciesCheckBox = new CheckBox
            {
                Text = "连同依赖一起抽取",
                Location = new System.Drawing.Point(12, 567),
                Size = new System.Drawing.Size(180, 24),
                Checked = true
            };

            progressBar = new ProgressBar
            {
                Location = new System.Drawing.Point(200, 568),
                Size = new System.Drawing.Size(400, 20)
            };

            statusLabel = new Label
            {
                Location = new System.Drawing.Point(610, 570),
                Size = new System.Drawing.Size(220, 20),
                Text = string.Empty
            };

            loadButton = new Button
            {
                Text = "加载选中",
                Location = new System.Drawing.Point(540, 600),
                Size = new System.Drawing.Size(90, 30),
                Enabled = false
            };
            loadButton.Click += LoadButton_Click;

            loadAllButton = new Button
            {
                Text = "加载全部 AB",
                Location = new System.Drawing.Point(635, 600),
                Size = new System.Drawing.Size(110, 30),
                Enabled = false
            };
            loadAllButton.Click += LoadAllButton_Click;

            cancelButton = new Button
            {
                Text = "取消",
                Location = new System.Drawing.Point(750, 600),
                Size = new System.Drawing.Size(70, 30)
            };
            cancelButton.Click += CancelButton_Click;

            Controls.Add(apkLabel);
            Controls.Add(apkTextBox);
            Controls.Add(browseButton);
            Controls.Add(packageLabel);
            Controls.Add(packageCombo);
            Controls.Add(readButton);
            Controls.Add(summaryLabel);
            Controls.Add(searchLabel);
            Controls.Add(searchTextBox);
            Controls.Add(bundleListView);
            Controls.Add(detailLabel);
            Controls.Add(detailTextBox);
            Controls.Add(withDependenciesCheckBox);
            Controls.Add(progressBar);
            Controls.Add(statusLabel);
            Controls.Add(loadButton);
            Controls.Add(loadAllButton);
            Controls.Add(cancelButton);

            AcceptButton = loadButton;
            CancelButton = cancelButton;

            if (!string.IsNullOrEmpty(_lastApkPath))
            {
                apkTextBox.Text = _lastApkPath;
                FillPackages();
            }
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "APK (*.apk)|*.apk|All files (*.*)|*.*",
                InitialDirectory = GetInitialDirectory()
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                apkTextBox.Text = dialog.FileName;
                _lastApkPath = dialog.FileName;
                FillPackages();
                ResetSource();
            }
        }

        private void PackageCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            ResetSource();
        }

        private void SearchTextBox_TextChanged(object sender, EventArgs e)
        {
            RebuildFilter();
        }

        private async void ReadButton_Click(object sender, EventArgs e)
        {
            if (_busy)
            {
                return;
            }

            string apkPath = apkTextBox.Text.Trim();
            if (!File.Exists(apkPath))
            {
                MessageBox.Show(this, "APK 不存在：" + apkPath, "Load APK",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (packageCombo.Items.Count == 0)
            {
                FillPackages();
            }

            string packageName = packageCombo.SelectedItem as string ?? DefaultPackage;
            try
            {
                _lastApkPath = apkPath;
                SetBusy(true);
                statusLabel.Text = "读取清单…";

                ApkBundleSource source = await Task.Run(() => ApkBundleSource.Read(apkPath, packageName));
                _source = source;
                summaryLabel.Text =
                    $"版本 {source.Version} · bundle {source.Manifest.BundleList.Count} · " +
                    $"资源 {source.Manifest.AssetList.Count} · 内置 {source.BuiltinBundleIds.Count}";
                searchTextBox.Text = string.Empty;
                RebuildFilter();
                statusLabel.Text = "就绪";
            }
            catch (Exception ex)
            {
                _source = null;
                summaryLabel.Text = string.Empty;
                ResetSource();
                Logger.Error($"读取 APK 清单失败: {ex.Message}");
                MessageBox.Show(this, ex.Message, "Load APK",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void LoadButton_Click(object sender, EventArgs e)
        {
            if (_busy || _source == null)
            {
                return;
            }

            int bundleId = GetSelectedBundleId();
            if (bundleId < 0)
            {
                return;
            }

            List<int> bundleIds = withDependenciesCheckBox.Checked
                ? _source.GetClosure(bundleId)
                : new List<int> { bundleId };

            List<int> missing = bundleIds
                .Where(id => _source.GetBuiltinEntryName(_source.Manifest.BundleList[id]) == null)
                .ToList();
            if (missing.Count > 0)
            {
                string names = string.Join("\n", missing.Take(8)
                    .Select(id => "  " + _source.Manifest.BundleList[id].BundleName));
                if (missing.Count > 8)
                {
                    names += "\n  ...";
                }

                DialogResult choice = MessageBox.Show(
                    this,
                    "有 " + missing.Count + " 个 bundle 不在 APK 内：\n" + names +
                    "\n\n仍然只加载 APK 内存在的部分？",
                    "Load APK",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Warning);
                if (choice != DialogResult.OK)
                {
                    return;
                }
            }

            await RunExtractAsync(
                bundleIds,
                _source.Manifest.BundleList[bundleId].BundleName);
        }

        private async void LoadAllButton_Click(object sender, EventArgs e)
        {
            if (_busy || _source == null)
            {
                return;
            }

            List<int> bundleIds = _source.GetAllBuiltinBundleIds();
            if (bundleIds.Count == 0)
            {
                MessageBox.Show(this, "APK 内没有可加载的 bundle。", "Load APK",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            long bytes = bundleIds.Sum(id => _source.Manifest.BundleList[id].FileSize);
            DialogResult choice = MessageBox.Show(
                this,
                "将从 APK 抽取全部 " + bundleIds.Count + " 个 bundle，" +
                "约 " + FormatSize(bytes) + "。\n" +
                "文件较多时耗时较长，可在抽取过程中点\"取消\"中断。\n\n继续？",
                "Load APK",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);
            if (choice != DialogResult.OK)
            {
                return;
            }

            await RunExtractAsync(bundleIds, "全部 " + bundleIds.Count + " 个 bundle");
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            if (_busy)
            {
                _extractCts?.Cancel();
                statusLabel.Text = "正在取消…";
                return;
            }

            DialogResult = DialogResult.Cancel;
        }

        /// <summary>
        /// 后台抽取指定 bundle 集合，成功后以抽取目录作为加载结果返回
        /// </summary>
        private async Task RunExtractAsync(List<int> bundleIds, string displayName)
        {
            string destDir = GetTempDirectory();
            _extractCts = new CancellationTokenSource();
            CancellationToken cancellationToken = _extractCts.Token;

            try
            {
                SetBusy(true);
                progressBar.Maximum = Math.Max(1, bundleIds.Count);
                progressBar.Value = 0;
                statusLabel.Text = "正在抽取…";

                IProgress<int> progress = new System.Progress<int>(value =>
                {
                    if (!IsDisposed && !progressBar.IsDisposed)
                    {
                        progressBar.Value = Math.Min(value, progressBar.Maximum);
                    }
                });

                ApkExtractResult result = await Task.Run(
                    () => _source.Extract(bundleIds, destDir, progress, cancellationToken));

                Logger.Info(
                    $"Load APK: extracted {result.Extracted} bundles " +
                    $"({FormatSize(result.Bytes)}), missing {result.Missing.Count}, dir: {destDir}");

                if (result.Extracted == 0)
                {
                    MessageBox.Show(this, "没有抽取到任何 bundle，无法加载。", "Load APK",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                ExtractDirectory = destDir;
                SelectedBundleName = displayName;
                DialogResult = DialogResult.OK;
            }
            catch (OperationCanceledException)
            {
                statusLabel.Text = "已取消";
                Logger.Info("Load APK: 抽取已取消");
            }
            catch (Exception ex)
            {
                Logger.Error($"抽取 APK bundle 失败: {ex.Message}");
                MessageBox.Show(this, ex.Message, "Load APK",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _extractCts?.Dispose();
                _extractCts = null;
                SetBusy(false);
            }
        }

        private void BundleListView_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            if (_source == null || e.ItemIndex < 0 || e.ItemIndex >= _filtered.Count)
            {
                e.Item = new ListViewItem(string.Empty);
                return;
            }

            ApkBundle bundle = _source.Manifest.BundleList[_filtered[e.ItemIndex]];
            string state = _source.GetBuiltinEntryName(bundle) != null ? "内置" : "缺失";
            int dependCount = bundle.DependIDs == null ? 0 : bundle.DependIDs.Length;
            e.Item = new ListViewItem(new[]
            {
                bundle.BundleName,
                FormatSize(bundle.FileSize),
                state,
                dependCount.ToString()
            });
        }

        private void BundleListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateDetail();
        }

        private void BundleListView_DoubleClick(object sender, EventArgs e)
        {
            if (GetSelectedBundleId() >= 0)
            {
                LoadButton_Click(sender, e);
            }
        }

        private void RebuildFilter()
        {
            _filtered.Clear();
            if (_source == null)
            {
                bundleListView.VirtualListSize = 0;
                UpdateDetail();
                return;
            }

            string query = (searchTextBox.Text ?? string.Empty).Trim().ToLowerInvariant();
            for (int i = 0; i < _source.Manifest.BundleList.Count; i++)
            {
                if (query.Length > 0 &&
                    _source.GetSearchText(i).IndexOf(query, StringComparison.Ordinal) < 0)
                {
                    continue;
                }
                _filtered.Add(i);
            }

            bundleListView.VirtualListSize = 0;
            bundleListView.VirtualListSize = _filtered.Count;
            UpdateDetail();
        }

        private void UpdateDetail()
        {
            int bundleId = GetSelectedBundleId();
            loadButton.Enabled = !_busy && bundleId >= 0;

            if (_source == null || bundleId < 0)
            {
                detailTextBox.Text = _source == null
                    ? "先选择 APK 并读取清单。"
                    : "选择一个 bundle 查看详情。";
                statusLabel.Text = _filtered.Count + " 项";
                return;
            }

            ApkBundle bundle = _source.Manifest.BundleList[bundleId];
            List<int> closure = _source.GetClosure(bundleId);
            int missing = closure.Count(id =>
                _source.GetBuiltinEntryName(_source.Manifest.BundleList[id]) == null);
            long closureBytes = closure.Sum(id => _source.Manifest.BundleList[id].FileSize);

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Bundle: " + bundle.BundleName);
            builder.AppendLine(
                "大小: " + FormatSize(bundle.FileSize) +
                "    Encrypted: " + bundle.Encrypted +
                "    直接依赖: " + (bundle.DependIDs == null ? 0 : bundle.DependIDs.Length));
            builder.AppendLine(
                "依赖闭包: " + closure.Count + " 个（APK 内置 " + (closure.Count - missing) +
                " / 缺失 " + missing + "），约 " + FormatSize(closureBytes));

            IReadOnlyList<ApkBundleAsset> assets = _source.GetAssets(bundleId);
            builder.AppendLine("清单登记资源: " + assets.Count);
            int shown = Math.Min(assets.Count, 60);
            for (int i = 0; i < shown; i++)
            {
                ApkBundleAsset asset = assets[i];
                builder.AppendLine(string.IsNullOrEmpty(asset.Address)
                    ? "  " + asset.AssetPath
                    : "  " + asset.Address + "  ->  " + asset.AssetPath);
            }
            if (assets.Count > shown)
            {
                builder.AppendLine("  ...");
            }

            detailTextBox.Text = builder.ToString();
            statusLabel.Text = _filtered.Count + " 项 / 闭包 " + closure.Count +
                               (missing > 0 ? "（缺 " + missing + "）" : string.Empty);
        }

        private void ResetSource()
        {
            _source = null;
            _filtered.Clear();
            bundleListView.VirtualListSize = 0;
            summaryLabel.Text = string.Empty;
            detailTextBox.Clear();
            statusLabel.Text = string.Empty;
            progressBar.Value = 0;
            loadButton.Enabled = false;
            loadAllButton.Enabled = false;
        }

        private int GetSelectedBundleId()
        {
            if (bundleListView.SelectedIndices.Count == 0)
            {
                return -1;
            }

            int index = bundleListView.SelectedIndices[0];
            if (index < 0 || index >= _filtered.Count)
            {
                return -1;
            }
            return _filtered[index];
        }

        private void FillPackages()
        {
            string apkPath = apkTextBox.Text.Trim();
            packageCombo.Items.Clear();
            readButton.Enabled = false;
            if (!File.Exists(apkPath))
            {
                return;
            }

            try
            {
                string[] packages = ApkBundleSource.GetPackages(apkPath);
                if (packages.Length == 0)
                {
                    statusLabel.Text = "APK 内没有 assets/yoo";
                    return;
                }

                packageCombo.Items.AddRange(packages);
                int hotfixIndex = Array.FindIndex(
                    packages, x => string.Equals(x, DefaultPackage, StringComparison.OrdinalIgnoreCase));
                packageCombo.SelectedIndex = hotfixIndex >= 0 ? hotfixIndex : 0;
                readButton.Enabled = true;
                statusLabel.Text = "就绪";
            }
            catch (Exception ex)
            {
                statusLabel.Text = "读取 APK 失败";
                Logger.Error($"读取 APK 包列表失败: {ex.Message}");
                MessageBox.Show(this, ex.Message, "Load APK",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetBusy(bool busy)
        {
            if (IsDisposed)
            {
                return;
            }

            _busy = busy;
            browseButton.Enabled = !busy;
            readButton.Enabled = !busy && packageCombo.Items.Count > 0;
            packageCombo.Enabled = !busy;
            searchTextBox.Enabled = !busy;
            bundleListView.Enabled = !busy;
            withDependenciesCheckBox.Enabled = !busy;
            cancelButton.Enabled = true;
            loadButton.Enabled = !busy && GetSelectedBundleId() >= 0;
            loadAllButton.Enabled = !busy && _source != null && _source.BuiltinBundleIds.Count > 0;
            UseWaitCursor = busy;
        }

        private string GetTempDirectory()
        {
            string identity = _source.ApkPath + "|" + _source.PackageName + "|" + _source.Version;
            return Path.Combine(
                Path.GetTempPath(),
                "AssetStudioLoadApk",
                ShortHash(identity));
        }

        private static string GetInitialDirectory()
        {
            if (!string.IsNullOrEmpty(_lastApkPath))
            {
                string dir = Path.GetDirectoryName(_lastApkPath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    return dir;
                }
            }
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static string ShortHash(string text)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
                StringBuilder builder = new StringBuilder(12);
                for (int i = 0; i < 6; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 0)
            {
                return "-";
            }
            if (bytes < 1024)
            {
                return bytes + " B";
            }
            if (bytes < 1024L * 1024L)
            {
                return (bytes / 1024f).ToString("0.0") + " KB";
            }
            if (bytes < 1024L * 1024L * 1024L)
            {
                return (bytes / (1024f * 1024f)).ToString("0.0") + " MB";
            }
            return (bytes / (1024f * 1024f * 1024f)).ToString("0.00") + " GB";
        }
    }
}
