using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AssetStudio.GUI
{
    public partial class LoadServerForm : Form
    {
        public string ServerUrl { get; private set; }
        public string Version { get; private set; }
        public bool UseLocalCache { get; private set; }
        public string LocalCachePath { get; private set; }
        public string ReplaceBaseUrl { get; private set; }

        private ComboBox localCacheComboBox;
        private TextBox urlTextBox;
        private TextBox versionTextBox;
        private TextBox replaceBaseUrlTextBox;
        private Label resultLabel;
        private Button confirmButton;
        private Button cancelButton;

        public LoadServerForm()
        {
            InitializeComponent();
            LoadLocalCacheVersions();
            LoadCachedValues();
        }

        private void InitializeComponent()
        {
            this.Text = "Load Server";
            this.Size = new System.Drawing.Size(500, 370);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Local Cache Label
            var localCacheLabel = new Label();
            localCacheLabel.Text = "本地缓存版本:";
            localCacheLabel.Location = new System.Drawing.Point(20, 20);
            localCacheLabel.AutoSize = true;
            this.Controls.Add(localCacheLabel);

            // Local Cache ComboBox
            localCacheComboBox = new ComboBox();
            localCacheComboBox.Location = new System.Drawing.Point(20, 45);
            localCacheComboBox.Size = new System.Drawing.Size(440, 23);
            localCacheComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            localCacheComboBox.SelectedIndexChanged += LocalCacheComboBox_SelectedIndexChanged;
            this.Controls.Add(localCacheComboBox);

            // URL Label
            var urlLabel = new Label();
            urlLabel.Text = "远程地址:";
            urlLabel.Location = new System.Drawing.Point(20, 80);
            urlLabel.AutoSize = true;
            this.Controls.Add(urlLabel);

            // URL TextBox
            urlTextBox = new TextBox();
            urlTextBox.Location = new System.Drawing.Point(20, 105);
            urlTextBox.Size = new System.Drawing.Size(440, 23);
            this.Controls.Add(urlTextBox);

            // Version Label
            var versionLabel = new Label();
            versionLabel.Text = "版本号:";
            versionLabel.Location = new System.Drawing.Point(20, 140);
            versionLabel.AutoSize = true;
            this.Controls.Add(versionLabel);

            // Version TextBox
            versionTextBox = new TextBox();
            versionTextBox.Location = new System.Drawing.Point(20, 165);
            versionTextBox.Size = new System.Drawing.Size(440, 23);
            this.Controls.Add(versionTextBox);

            // Replace Base URL Label
            var replaceBaseUrlLabel = new Label();
            replaceBaseUrlLabel.Text = "替换资源URL (可选):";
            replaceBaseUrlLabel.Location = new System.Drawing.Point(20, 200);
            replaceBaseUrlLabel.AutoSize = true;
            this.Controls.Add(replaceBaseUrlLabel);

            // Replace Base URL TextBox
            replaceBaseUrlTextBox = new TextBox();
            replaceBaseUrlTextBox.Location = new System.Drawing.Point(20, 225);
            replaceBaseUrlTextBox.Size = new System.Drawing.Size(440, 23);
            replaceBaseUrlTextBox.PlaceholderText = "例如: http://192.168.20.209:8001/xMan_overseas_iOS_Release/iOS";
            this.Controls.Add(replaceBaseUrlTextBox);

            // Result Label
            resultLabel = new Label();
            resultLabel.Location = new System.Drawing.Point(20, 260);
            resultLabel.Size = new System.Drawing.Size(440, 23);
            resultLabel.Text = "";
            resultLabel.ForeColor = System.Drawing.Color.Blue;
            this.Controls.Add(resultLabel);

            // Confirm Button
            confirmButton = new Button();
            confirmButton.Text = "确认";
            confirmButton.Location = new System.Drawing.Point(280, 295);
            confirmButton.Size = new System.Drawing.Size(85, 30);
            confirmButton.Click += ConfirmButton_Click;
            this.Controls.Add(confirmButton);

            // Cancel Button
            cancelButton = new Button();
            cancelButton.Text = "取消";
            cancelButton.Location = new System.Drawing.Point(375, 295);
            cancelButton.Size = new System.Drawing.Size(85, 30);
            cancelButton.DialogResult = DialogResult.Cancel;
            this.Controls.Add(cancelButton);

            this.AcceptButton = confirmButton;
            this.CancelButton = cancelButton;
        }

        private void LoadLocalCacheVersions()
        {
            try
            {
                var serverCachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ServerCache");
                
                localCacheComboBox.Items.Clear();
                localCacheComboBox.Items.Add("-- 从服务器下载 --");
                
                if (Directory.Exists(serverCachePath))
                {
                    var directories = Directory.GetDirectories(serverCachePath)
                        .Select(d => new DirectoryInfo(d).Name)
                        .OrderByDescending(name => name)
                        .ToList();
                    
                    foreach (var dir in directories)
                    {
                        localCacheComboBox.Items.Add(dir);
                    }
                }
                
                localCacheComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                Logger.Error($"加载本地缓存版本失败: {ex.Message}");
            }
        }

        private void LocalCacheComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (localCacheComboBox.SelectedIndex == 0)
            {
                // 选择"从服务器下载"，启用输入框
                urlTextBox.Enabled = true;
                versionTextBox.Enabled = true;
                replaceBaseUrlTextBox.Enabled = true;
                urlTextBox.Text = Properties.Settings.Default.serverUrl ?? "";
                versionTextBox.Text = Properties.Settings.Default.serverVersion ?? "";
                replaceBaseUrlTextBox.Text = Properties.Settings.Default.replaceBaseUrl ?? "";
            }
            else
            {
                // 选择本地缓存版本，禁用输入框
                urlTextBox.Enabled = false;
                versionTextBox.Enabled = false;
                replaceBaseUrlTextBox.Enabled = false;
                urlTextBox.Text = "使用本地缓存";
                versionTextBox.Text = localCacheComboBox.SelectedItem.ToString();
                replaceBaseUrlTextBox.Text = "";
            }
        }

        private void LoadCachedValues()
        {
            // 从设置中加载缓存的值
            if (localCacheComboBox.SelectedIndex == 0)
            {
                urlTextBox.Text = Properties.Settings.Default.serverUrl ?? "";
                versionTextBox.Text = Properties.Settings.Default.serverVersion ?? "";
                replaceBaseUrlTextBox.Text = Properties.Settings.Default.replaceBaseUrl ?? "";
            }
        }

        private void SaveCachedValues()
        {
            // 保存到设置中
            Properties.Settings.Default.serverUrl = ServerUrl;
            Properties.Settings.Default.serverVersion = Version;
            Properties.Settings.Default.replaceBaseUrl = ReplaceBaseUrl;
            Properties.Settings.Default.Save();
        }

        private void ConfirmButton_Click(object sender, EventArgs e)
        {
            if (localCacheComboBox.SelectedIndex == 0)
            {
                // 从服务器下载模式
                ServerUrl = urlTextBox.Text.Trim();
                Version = versionTextBox.Text.Trim();
                ReplaceBaseUrl = replaceBaseUrlTextBox.Text.Trim();
                UseLocalCache = false;

                if (string.IsNullOrEmpty(ServerUrl))
                {
                    resultLabel.Text = "请输入远程地址";
                    resultLabel.ForeColor = System.Drawing.Color.Red;
                    return;
                }

                if (string.IsNullOrEmpty(Version))
                {
                    resultLabel.Text = "请输入版本号";
                    resultLabel.ForeColor = System.Drawing.Color.Red;
                    return;
                }

                // 保存缓存
                SaveCachedValues();

                resultLabel.Text = "正在连接服务器...";
                resultLabel.ForeColor = System.Drawing.Color.Blue;
            }
            else
            {
                // 使用本地缓存模式
                UseLocalCache = true;
                ReplaceBaseUrl = "";
                var selectedVersion = localCacheComboBox.SelectedItem.ToString();
                Version = selectedVersion;
                
                var serverCachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ServerCache");
                LocalCachePath = Path.Combine(serverCachePath, selectedVersion);
                
                if (!Directory.Exists(LocalCachePath))
                {
                    resultLabel.Text = "本地缓存目录不存在";
                    resultLabel.ForeColor = System.Drawing.Color.Red;
                    return;
                }

                resultLabel.Text = $"使用本地缓存: {selectedVersion}";
                resultLabel.ForeColor = System.Drawing.Color.Green;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        public void SetResultMessage(string message, bool isError = false)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => SetResultMessage(message, isError)));
                return;
            }

            resultLabel.Text = message;
            resultLabel.ForeColor = isError ? System.Drawing.Color.Red : System.Drawing.Color.Green;
        }
    }
}