using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace YouTubeDownloader
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool firstInstance;
            using (var instance = new Mutex(true, @"Local\YouTubeDownloader.WinForms", out firstInstance))
            {
                if (!firstInstance)
                {
                    MessageBox.Show("下载器已打开。", "YouTube 下载器", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                using (var window = new MainWindow(args.Length > 0 ? args[0] : ""))
                    Application.Run(window);
            }
        }
    }

    internal sealed class MainWindow : Form
    {
        private static readonly Color Ink = Color.FromArgb(22, 31, 52);
        private static readonly Color Muted = Color.FromArgb(77, 90, 115);
        private static readonly Color Canvas = Color.FromArgb(242, 245, 251);
        private static readonly Color Border = Color.FromArgb(209, 217, 230);
        private static readonly Color Blue = Color.FromArgb(42, 75, 215);
        private static readonly Color Navy = Color.FromArgb(19, 29, 55);
        private static readonly string[] Resolutions = { "", "2160", "1440", "1080", "720", "480" };

        private readonly Downloader downloader = new Downloader(AppDomain.CurrentDomain.BaseDirectory);
        private readonly TextBox urlBox = new TextBox();
        private readonly TextBox folderBox = new TextBox();
        private readonly ComboBox formatBox = new ComboBox();
        private readonly ComboBox qualityBox = new ComboBox();
        private readonly Button downloadButton = MakeButton("开始下载", true);
        private readonly Button cancelButton = MakeButton("取消");
        private readonly Button pasteButton = MakeButton("粘贴");
        private readonly Button chooseButton = MakeButton("浏览…");
        private readonly Button folderButton = MakeButton("打开文件夹");
        private readonly Button updateButton = MakeButton("更新 yt-dlp");
        private readonly LinkLabel detailsLink = new LinkLabel();
        private readonly Label statusTitle = MakeLabel("等待下载", Ink);
        private readonly Label statusDetail = MakeLabel("", Muted);
        private readonly ProgressBar progress = new ProgressBar();
        private readonly StringBuilder log = new StringBuilder();
        private Process activeProcess;
        private bool cancelling;
        private bool closeWhenFinished;
        private string savedFile;
        private string lastError;

        internal MainWindow(string initialUrl)
        {
            Text = "YouTube 下载器";
            Font = new Font("Microsoft YaHei UI", 10F);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(740, 514);
            MinimumSize = new Size(615, 553);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Canvas;

            urlBox.Text = initialUrl;
            folderBox.Text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "下载");
            formatBox.Items.AddRange(new object[] { "MP4 视频", "MP3 音频" });
            qualityBox.Items.AddRange(new object[] { "自动（最高可用）", "2160p", "1440p", "1080p", "720p", "480p" });
            formatBox.SelectedIndex = 0;
            qualityBox.SelectedIndex = 0;

            var root = Grid(1, 2);
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 85));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(BuildHeader(), 0, 0);

            var body = Grid(1, 3);
            body.Padding = new Padding(26, 20, 26, 20);
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.Controls.Add(BuildFields(), 0, 0);
            body.Controls.Add(BuildActions(), 0, 1);
            body.Controls.Add(BuildStatus(), 0, 2);
            root.Controls.Add(body, 0, 1);
            Controls.Add(root);

            pasteButton.Click += (sender, args) => PasteLink();
            chooseButton.Click += (sender, args) => ChooseFolder();
            folderButton.Click += (sender, args) => OpenFolder();
            downloadButton.Click += async (sender, args) => await StartDownloadAsync();
            updateButton.Click += async (sender, args) => await UpdateAsync();
            cancelButton.Click += async (sender, args) => await CancelAsync();
            formatBox.SelectedIndexChanged += (sender, args) => qualityBox.Enabled = activeProcess == null && !AudioOnly;
            detailsLink.LinkClicked += (sender, args) => ShowDetails();
            FormClosing += ClosingWindow;
            Shown += (sender, args) => { urlBox.Focus(); urlBox.SelectAll(); };

            AcceptButton = downloadButton;
            cancelButton.Enabled = false;
            string[] missing = downloader.MissingTools();
            if (missing.Length > 0) SetStatus("缺少下载组件", string.Join("、", missing), 0);
        }

        private bool AudioOnly { get { return formatBox.SelectedIndex == 1; } }

        private Control BuildHeader()
        {
            var header = Grid(3, 1);
            header.BackColor = Navy;
            header.Padding = new Padding(26, 0, 26, 0);
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 59));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 117));

            var play = new Label {
                Text = "▶", Font = new Font("Segoe UI Symbol", 19F), ForeColor = Color.White,
                BackColor = Blue, TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(43, 43), Anchor = AnchorStyles.Left, Margin = Padding.Empty,
                AccessibleName = ""
            };
            var title = MakeLabel("YouTube 下载器", Color.White);
            title.Font = new Font(Font.FontFamily, 21F, FontStyle.Bold);
            updateButton.ForeColor = Color.White;
            updateButton.BackColor = Color.FromArgb(39, 54, 86);
            updateButton.FlatAppearance.BorderColor = Color.FromArgb(82, 102, 140);
            updateButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(57, 75, 113);
            updateButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(57, 75, 113);
            updateButton.Size = new Size(112, 34);
            updateButton.Anchor = AnchorStyles.Right;
            header.Controls.Add(play, 0, 0);
            header.Controls.Add(title, 1, 0);
            header.Controls.Add(updateButton, 2, 0);
            return header;
        }

        private Control BuildFields()
        {
            var fields = Card(3);
            for (int row = 0; row < 3; row++) fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));

            urlBox.AccessibleName = "YouTube 视频链接";
            folderBox.AccessibleName = "保存到";
            var formatFields = Grid(3, 1);
            formatFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            formatFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 15));
            formatFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            ConfigureChoice(formatBox, "格式");
            ConfigureChoice(qualityBox, "清晰度");
            formatFields.Controls.Add(Field("格式", formatBox), 0, 0);
            formatFields.Controls.Add(Field("清晰度", qualityBox), 2, 0);

            fields.Controls.Add(Field("YouTube 视频链接", InputWithButton(urlBox, pasteButton)), 0, 0);
            fields.Controls.Add(formatFields, 0, 1);
            fields.Controls.Add(Field("保存到", InputWithButton(folderBox, chooseButton)), 0, 2);
            return fields;
        }

        private Control BuildActions()
        {
            var actions = Grid(4, 1);
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 133));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            downloadButton.Size = new Size(123, 37);
            cancelButton.Size = new Size(73, 37);
            folderButton.Size = new Size(117, 37);
            downloadButton.Anchor = cancelButton.Anchor = AnchorStyles.Left;
            folderButton.Anchor = AnchorStyles.Right;
            actions.Controls.Add(downloadButton, 0, 0);
            actions.Controls.Add(cancelButton, 1, 0);
            actions.Controls.Add(folderButton, 3, 0);
            return actions;
        }

        private Control BuildStatus()
        {
            var status = Card(3, 2);
            status.Padding = new Padding(16, 10, 16, 9);
            status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            status.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
            status.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            status.RowStyles.Add(new RowStyle(SizeType.Absolute, 8));
            status.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            statusTitle.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
            statusDetail.Font = new Font(Font.FontFamily, 9F);
            detailsLink.Text = "查看详情";
            detailsLink.Dock = DockStyle.Fill;
            detailsLink.Margin = Padding.Empty;
            detailsLink.TextAlign = ContentAlignment.MiddleRight;
            detailsLink.LinkColor = Blue;
            detailsLink.DisabledLinkColor = Muted;
            detailsLink.Enabled = false;
            progress.Dock = DockStyle.Fill;
            progress.Margin = Padding.Empty;
            progress.MarqueeAnimationSpeed = 26;
            progress.AccessibleName = "下载进度";
            status.Controls.Add(statusTitle, 0, 0);
            status.Controls.Add(detailsLink, 1, 0);
            status.Controls.Add(progress, 0, 1);
            status.SetColumnSpan(progress, 2);
            status.Controls.Add(statusDetail, 0, 2);
            status.SetColumnSpan(statusDetail, 2);
            return status;
        }

        private async Task StartDownloadAsync()
        {
            if (activeProcess != null) return;
            string url;
            if (!Downloader.TryGetVideoUrl(urlBox.Text, out url))
            {
                SetStatus("请粘贴 YouTube 视频链接", "支持普通视频、Shorts 和 youtu.be 分享链接。", 0);
                urlBox.Focus();
                return;
            }

            string[] missing = downloader.MissingTools();
            if (missing.Length > 0)
            {
                SetStatus("缺少下载组件", "请在程序旁的 tools 文件夹中放回 " + string.Join("、", missing), 0);
                return;
            }

            string folder;
            try
            {
                folder = GetFolder();
                Directory.CreateDirectory(folder);
            }
            catch (Exception error)
            {
                log.Clear();
                AppendLog(error.Message);
                SetStatus("无法使用保存位置", error.Message, 0);
                folderBox.Focus();
                return;
            }

            await RunAsync(downloader.Download(url, folder, AudioOnly, Resolutions[qualityBox.SelectedIndex]), false);
        }

        private async Task UpdateAsync()
        {
            if (activeProcess != null) return;
            if (Array.IndexOf(downloader.MissingTools(), "yt-dlp.exe") >= 0)
            {
                SetStatus("缺少 yt-dlp.exe", "请将 yt-dlp.exe 放回程序旁的 tools 文件夹。", 0);
                return;
            }
            await RunAsync(downloader.Update(), true);
        }

        private async Task RunAsync(Process process, bool updating)
        {
            activeProcess = process;
            cancelling = false;
            lastError = null;
            savedFile = null;
            log.Clear();
            detailsLink.Enabled = false;
            SetBusy(true);
            SetStatus(updating ? "正在检查 yt-dlp 更新" : "正在读取视频信息", "", null);

            try
            {
                int code = await Downloader.RunAsync(process, new Progress<string>(ReadLine));
                if (!updating && code == 0 && File.Exists(savedFile))
                    SetStatus("下载完成", Path.GetFileName(savedFile), 100);
                else if (cancelling)
                    SetStatus("已取消", "", 0);
                else if (updating && code == 0)
                    SetStatus("更新检查完成", "可在详情中查看 yt-dlp 版本。", 100);
                else
                    SetStatus(updating ? "更新失败" : "下载失败", lastError ?? "请查看详情。", 0);
            }
            catch (Exception error)
            {
                AppendLog(error.Message);
                SetStatus(updating ? "无法运行更新" : "无法启动下载", error.Message, 0);
            }
            finally
            {
                activeProcess = null;
                process.Dispose();
                SetBusy(false);
                if (closeWhenFinished) Close();
            }
        }

        private void ReadLine(string line)
        {
            if (line.StartsWith(Downloader.FileMarker, StringComparison.Ordinal))
            {
                savedFile = line.Substring(Downloader.FileMarker.Length);
                AppendLog("已保存 " + savedFile);
            }
            else if (line.StartsWith(Downloader.TitleMarker, StringComparison.Ordinal))
            {
                string title = line.Substring(Downloader.TitleMarker.Length);
                AppendLog(title);
                if (!cancelling) SetStatus("正在下载", title, null);
            }
            else if (line.StartsWith(Downloader.ProgressMarker, StringComparison.Ordinal))
            {
                if (cancelling) return;
                string[] fields = line.Substring(Downloader.ProgressMarker.Length).Split('|');
                if (fields.Length != 3) return;
                double value;
                if (!double.TryParse(fields[0].Trim().TrimEnd('%'), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out value) || double.IsNaN(value)) return;
                int percent = (int)Math.Max(0, Math.Min(100, value));
                string speed = fields[1].Trim();
                string eta = fields[2].Trim();
                string message = "当前文件 " + percent + "%";
                if (speed.Length > 0 && speed != "NA" && !speed.StartsWith("Unknown")) message += "   ·   " + speed;
                if (eta.Length > 0 && eta != "NA" && eta != "Unknown" && percent < 100) message += "   ·   剩余 " + eta;
                SetStatus("正在下载", message, percent);
            }
            else if (line.StartsWith(Downloader.PostprocessMarker, StringComparison.Ordinal))
            {
                if (!cancelling) SetStatus(AudioOnly ? "正在转换为 MP3" : "正在处理 MP4", "", null);
            }
            else
            {
                if (line.StartsWith("ERROR:", StringComparison.Ordinal)) lastError = line.Substring(6).Trim();
                AppendLog(line);
            }
        }

        private async Task CancelAsync()
        {
            Process process = activeProcess;
            if (process == null || cancelling) return;
            cancelling = true;
            cancelButton.Enabled = false;
            SetStatus("正在取消", "", null);
            try
            {
                await Task.Run(() => Downloader.Stop(process));
            }
            catch (Exception error)
            {
                if (activeProcess != process) return;
                cancelling = false;
                closeWhenFinished = false;
                cancelButton.Enabled = true;
                AppendLog(error.Message);
                SetStatus("取消失败", error.Message, null);
            }
        }

        private async void ClosingWindow(object sender, FormClosingEventArgs args)
        {
            if (activeProcess == null) return;
            args.Cancel = true;
            closeWhenFinished = true;
            await CancelAsync();
        }

        private void SetBusy(bool busy)
        {
            urlBox.ReadOnly = folderBox.ReadOnly = busy;
            pasteButton.Enabled = chooseButton.Enabled = formatBox.Enabled = !busy;
            downloadButton.Enabled = updateButton.Enabled = !busy;
            qualityBox.Enabled = !busy && !AudioOnly;
            cancelButton.Enabled = busy;
        }

        private void SetStatus(string title, string description, int? percent)
        {
            statusTitle.Text = title;
            statusDetail.Text = description;
            progress.Style = percent.HasValue ? ProgressBarStyle.Continuous : ProgressBarStyle.Marquee;
            if (percent.HasValue) progress.Value = percent.Value;
        }

        private void PasteLink()
        {
            try
            {
                if (Clipboard.ContainsText()) urlBox.Text = Clipboard.GetText().Trim();
                urlBox.Focus();
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                SetStatus("暂时无法访问剪贴板", "请点击链接输入框后按 Ctrl+V。", 0);
            }
        }

        private string GetFolder()
        {
            string value = folderBox.Text.Trim().Trim('"');
            if (!Path.IsPathRooted(value)) throw new ArgumentException("请输入完整的文件夹路径。");
            return Path.GetFullPath(value);
        }

        private void ChooseFolder()
        {
            using (var picker = new FolderBrowserDialog { Description = "选择视频保存的位置" })
            {
                if (Directory.Exists(folderBox.Text)) picker.SelectedPath = folderBox.Text;
                if (picker.ShowDialog(this) == DialogResult.OK) folderBox.Text = picker.SelectedPath;
            }
        }

        private void OpenFolder()
        {
            try
            {
                string folder = GetFolder();
                Directory.CreateDirectory(folder);
                var explorer = new ProcessStartInfo {
                    FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
                    Arguments = Downloader.QuoteArgument(folder),
                    UseShellExecute = false
                };
                using (Process.Start(explorer)) { }
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "无法打开文件夹", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AppendLog(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            log.AppendLine(line);
            if (log.Length > 32000) log.Remove(0, log.Length - 32000);
            detailsLink.Enabled = true;
        }

        private void ShowDetails()
        {
            using (var dialog = new Form {
                Text = "下载详情", StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(650, 320),
                MinimizeBox = false, ShowInTaskbar = false, AutoScaleMode = AutoScaleMode.Dpi
            })
            using (var contents = new TextBox {
                Text = log.ToString(), Multiline = true, ReadOnly = true, WordWrap = false,
                ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new Font("Consolas", 10F)
            })
            {
                dialog.Controls.Add(contents);
                contents.SelectionStart = contents.TextLength;
                contents.ScrollToCaret();
                dialog.ShowDialog(this);
            }
        }

        private static TableLayoutPanel Grid(int columns, int rows)
        {
            var grid = new TableLayoutPanel {
                ColumnCount = columns, RowCount = rows, Dock = DockStyle.Fill, Margin = Padding.Empty,
                GrowStyle = TableLayoutPanelGrowStyle.FixedSize
            };
            if (columns == 1) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            if (rows == 1) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            return grid;
        }

        private static TableLayoutPanel Card(int rows, int columns = 1)
        {
            var grid = Grid(columns, rows);
            grid.Padding = new Padding(16, 13, 16, 11);
            grid.BackColor = Color.White;
            return grid;
        }

        private static Control Field(string caption, Control input)
        {
            var field = Grid(1, 2);
            field.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
            field.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            field.Controls.Add(MakeLabel(caption, Ink), 0, 0);
            field.Controls.Add(input, 0, 1);
            return field;
        }

        private static Control InputWithButton(TextBox input, Button button)
        {
            var row = Grid(2, 1);
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
            var frame = new Panel {
                BackColor = Canvas, BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Fill,
                Padding = new Padding(9, 7, 9, 2), Margin = Padding.Empty
            };
            input.BorderStyle = BorderStyle.None;
            input.ForeColor = Ink;
            input.BackColor = Canvas;
            input.Dock = DockStyle.Top;
            frame.Controls.Add(input);
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(9, 0, 0, 0);
            row.Controls.Add(frame, 0, 0);
            row.Controls.Add(button, 1, 0);
            return row;
        }

        private static void ConfigureChoice(ComboBox choice, string name)
        {
            choice.AccessibleName = name;
            choice.DropDownStyle = ComboBoxStyle.DropDownList;
            choice.ForeColor = Ink;
            choice.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            choice.Margin = Padding.Empty;
        }

        private static Label MakeLabel(string text, Color color)
        {
            return new Label {
                Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = color, Margin = Padding.Empty, AutoEllipsis = true
            };
        }

        private static Button MakeButton(string text, bool primary = false)
        {
            var button = new Button {
                Text = text, FlatStyle = FlatStyle.Flat, Margin = Padding.Empty,
                ForeColor = primary ? Color.White : Ink, BackColor = primary ? Blue : Color.White,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderColor = primary ? Blue : Border;
            button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(29, 59, 183) : Canvas;
            return button;
        }
    }
}
