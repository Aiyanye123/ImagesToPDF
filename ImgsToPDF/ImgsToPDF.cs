using ImgsToPDF.Lang;
using ImgsToPDF.Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using WebPWrapper;
using Thread = System.Threading.Thread;

namespace ImgsToPDF
{
    public partial class ImgsToPDF : Form
    {
        private List<string> selectedImages = new List<string>();
        private readonly List<string> pathQueue = new List<string>();
        private int queueIndex = -1;
        private readonly Button previousQueueButton = new Button();
        private readonly Button nextQueueButton = new Button();
        private readonly Button clearCurrentButton = new Button();
        private readonly Label queueStatusLabel = new Label();
        private Image ownedPreviewImage;
        private readonly Timer topMostAnimationTimer = new Timer();
        private int topMostAnimationTick = 0;
        private const int TopMostAnimationFrames = 8;
        private int progressBarRightMargin = -1;
        private const int MainActionButtonGap = 10;
        private const int MainActionButtonHorizontalPadding = 26;
        private const int StartButtonMinWidth = 92;
        private const int GalleryButtonMinWidth = 118;
        private const int ProgressBarMinWidth = 160;

        public ImgsToPDF() {
            string language = Properties.Settings.Default.DefaultLanguage != "" ? Properties.Settings.Default.DefaultLanguage : System.Globalization.CultureInfo.CurrentCulture.Name;
            System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(language);

            this.StartPosition = FormStartPosition.CenterScreen; // Center the window.
            CheckForIllegalCrossThreadCalls = false; // Background task updates existing UI state.

            InitializeComponent();
            InitializeQueueControls();
            this.TopMost = Properties.Settings.Default.AlwaysOnTop;
            toolStripMenuTopMost.Checked = this.TopMost;
            UpdateTopMostMenuStyle();
            progressBarRightMargin = this.ClientSize.Width - progressBar.Right;
            AdjustMainActionButtonsLayout();
            UpdateQueueControls();
        }

        private void ImgsToPDF_Load(object sender, EventArgs e) {
            UpdateLanguageMenuState();
            MsgLabel.ForeColor = Color.Blue;
            ReloadGenerateModeOptions();
            ReloadQualityOptions();
            topMostAnimationTimer.Interval = 35;
            topMostAnimationTimer.Tick += TopMostAnimationTimer_Tick;
            toolStripMenuTopMost.Checked = this.TopMost;
            UpdateTopMostMenuStyle();
            if (toolStripMenuTopMost.Checked) {
                StartTopMostAnimation();
            }
            AdjustMainActionButtonsLayout();
        }

        protected override void OnFormClosed(FormClosedEventArgs e) {
            ReleaseOwnedPreviewImage();
            base.OnFormClosed(e);
        }

        static readonly string[] compressExtensions = { ".zip", ".rar", ".7z" };

        private void InitializeQueueControls() {
            previousQueueButton.Name = "previousQueueButton";
            nextQueueButton.Name = "nextQueueButton";
            clearCurrentButton.Name = "clearCurrentButton";
            queueStatusLabel.Name = "queueStatusLabel";
            previousQueueButton.Text = "‹";
            nextQueueButton.Text = "›";
            clearCurrentButton.Text = "清空当前";
            queueStatusLabel.TextAlign = ContentAlignment.MiddleCenter;
            queueStatusLabel.ForeColor = Color.FromArgb(80, 80, 80);

            StyleQueueButton(previousQueueButton);
            StyleQueueButton(nextQueueButton);
            StyleQueueButton(clearCurrentButton);

            previousQueueButton.SetBounds(18, 248, 42, 96);
            nextQueueButton.SetBounds(ClientSize.Width - 60, 248, 42, 96);
            nextQueueButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            clearCurrentButton.SetBounds(560, 42, 90, 28);
            queueStatusLabel.SetBounds(650, 42, 110, 28);

            previousQueueButton.Click += (s, e) => MoveQueue(-1);
            nextQueueButton.Click += (s, e) => MoveQueue(1);
            clearCurrentButton.Click += (s, e) => RemoveCurrentQueueItem();

            Controls.Add(previousQueueButton);
            Controls.Add(nextQueueButton);
            Controls.Add(clearCurrentButton);
            Controls.Add(queueStatusLabel);
            previousQueueButton.BringToFront();
            nextQueueButton.BringToFront();
            clearCurrentButton.BringToFront();
            queueStatusLabel.BringToFront();
        }

        private static void StyleQueueButton(Button button) {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.FromArgb(245, 245, 245);
            button.ForeColor = Color.FromArgb(35, 42, 52);
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Hand;
        }

        private void MoveQueue(int delta) {
            int nextIndex = queueIndex + delta;
            if (nextIndex < 0 || nextIndex >= pathQueue.Count) { return; }
            queueIndex = nextIndex;
            DisplayCurrentQueueItem();
        }

        private void RemoveCurrentQueueItem() {
            if (queueIndex < 0 || queueIndex >= pathQueue.Count) { return; }
            pathQueue.RemoveAt(queueIndex);
            if (queueIndex >= pathQueue.Count) {
                queueIndex = pathQueue.Count - 1;
            }
            if (pathQueue.Count == 0) {
                queueIndex = -1;
                ClearCurrentView();
            }
            else {
                DisplayCurrentQueueItem();
            }
            UpdateQueueControls();
        }

        private void ClearCurrentView() {
            ReleaseOwnedPreviewImage();
            PicInFolder.Image = Properties.Resources.folder;
            FolderImg.Image = null;
            PathLabel.Text = null;
            selectedImages.Clear();
            GalleryButton.Enabled = false;
            StartButton.Enabled = false;
            MsgLabel.Text = Extra.ApplyResource(this.GetType(), "MsgLabel.Text");
        }

        private void UpdateQueueControls() {
            bool hasQueue = pathQueue.Count > 0 && queueIndex >= 0;
            previousQueueButton.Visible = hasQueue && queueIndex > 0;
            nextQueueButton.Visible = hasQueue && queueIndex < pathQueue.Count - 1;
            clearCurrentButton.Visible = hasQueue;
            queueStatusLabel.Visible = hasQueue;
            queueStatusLabel.Text = hasQueue ? $"{queueIndex + 1} / {pathQueue.Count}" : string.Empty;
            StartButton.Enabled = hasQueue;
            GalleryButton.Enabled = hasQueue && Directory.Exists(PathLabel.Text);
        }

        private void ImgsToPDF_DragEnter(object sender, DragEventArgs e) {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) {
                e.Effect = DragDropEffects.None;
                return;
            }

            var paths = ((System.Array)e.Data.GetData(DataFormats.FileDrop)).Cast<object>().Select(x => x.ToString());
            if (paths.Any(IsSupportedInputPath)) {
                e.Effect = DragDropEffects.All;
            }
            else {
                e.Effect = DragDropEffects.None;
            }
        }
        private static bool IsSupportedInputPath(string path) {
            return Directory.Exists(path) || compressExtensions.Contains(Path.GetExtension(path)?.ToLower());
        }

        private void ChooseFileAction(string directoryPath) {
            pathQueue.Clear();
            pathQueue.Add(directoryPath);
            queueIndex = 0;
            DisplayCurrentQueueItem();
        }

        private void AppendToQueue(IEnumerable<string> paths) {
            foreach (string path in paths.Where(IsSupportedInputPath)) {
                if (!pathQueue.Contains(path, StringComparer.OrdinalIgnoreCase)) {
                    pathQueue.Add(path);
                }
            }
            if (queueIndex < 0 && pathQueue.Count > 0) {
                queueIndex = 0;
                DisplayCurrentQueueItem();
            }
            UpdateQueueControls();
        }

        private void DisplayCurrentQueueItem() {
            if (queueIndex < 0 || queueIndex >= pathQueue.Count) {
                ClearCurrentView();
                return;
            }
            DisplayPath(pathQueue[queueIndex]);
            UpdateQueueControls();
        }

        private void DisplayPath(string directoryPath) {
            ReleaseOwnedPreviewImage();

            PathLabel.Text = directoryPath;
            selectedImages.Clear();
            GalleryButton.Enabled = false;

            // Validate the selected path.
            if (Directory.Exists(directoryPath)) {
                PicInFolder.Image = Properties.Resources.no_photo;
                FolderImg.Image = Properties.Resources.folder;
                IEnumerable<string> imagepaths = Directory.EnumerateFiles(directoryPath)
                    .Where(ImageFileExtensions.IsSupported);
                foreach (var imagepath in imagepaths) {
                    try {
                        ownedPreviewImage = LoadPreviewBitmap(imagepath);
                        PicInFolder.Image = ownedPreviewImage;
                        break;
                    }
                    catch (Exception) {
                        // Skip invalid image files.
                        continue;
                    }
                }
            }
            else if (compressExtensions.Contains(Path.GetExtension(directoryPath)?.ToLower())) {
                PicInFolder.Image = Properties.Resources.compressedFile;
                FolderImg.Image = null;
            }
            else {
                PicInFolder.Image = Properties.Resources.no_photo;
                FolderImg.Image = null;
                MsgLabel.Text = "Invalid directory path";
                return;
            }

            StartButton.Enabled = true;
            GalleryButton.Enabled = Directory.Exists(directoryPath);
            MsgLabel.Text = Extra.ApplyResource(typeof(Extra), "strClickToStart");
        }

        private void ReleaseOwnedPreviewImage() {
            if (ownedPreviewImage == null) { return; }
            ownedPreviewImage.Dispose();
            ownedPreviewImage = null;
        }

        private static Bitmap LoadPreviewBitmap(string imagePath) {
            if (string.Equals(Path.GetExtension(imagePath), ".webp", StringComparison.OrdinalIgnoreCase)) {
                using (var webp = new WebP()) {
                    return webp.Load(imagePath);
                }
            }

            return new Bitmap(imagePath);
        }

        private void ImgsToPDF_DragDrop(object sender, DragEventArgs e) {
            var paths = ((System.Array)e.Data.GetData(DataFormats.FileDrop)).Cast<object>().Select(x => x.ToString());
            AppendToQueue(paths);
        }
        private async void StartButton_Click(object sender, EventArgs e) {
            progressBar.Visible = true;
            progressBar.Maximum = 100;
            StartButton.Enabled = false;
            GalleryButton.Enabled = false;
            while (queueIndex >= 0 && queueIndex < pathQueue.Count) {
                MsgLabel.Text = Extra.ApplyResource(typeof(Extra), "strPDFIsGenerating");
                progressBar.Value = 50;
                string pathToProcess = pathQueue[queueIndex];
                bool success = await Task.Run(() => ButtonClickAction(pathToProcess));
                if (!success) { break; }
                pathQueue.RemoveAt(queueIndex);
                if (queueIndex >= pathQueue.Count) {
                    queueIndex = pathQueue.Count - 1;
                }
                if (pathQueue.Count > 0) {
                    if (queueIndex < 0) { queueIndex = 0; }
                    DisplayCurrentQueueItem();
                }
            }
            progressBar.Value = 100;
            StartButton.Enabled = pathQueue.Count > 0;
            GalleryButton.Enabled = queueIndex >= 0 && Directory.Exists(PathLabel.Text);
            MsgLabel.Text = pathQueue.Count > 0
                ? Extra.ApplyResource(typeof(Extra), "strClickToStart")
                : Extra.ApplyResource(typeof(Extra), "strPDFGenerationSuccess");
            if (pathQueue.Count == 0) {
                queueIndex = -1;
                ClearCurrentView();
                MsgLabel.Text = Extra.ApplyResource(typeof(Extra), "strPDFGenerationSuccess");
            }
            UpdateQueueControls();
        }
        static List<string> RecursiveFolder(string path, List<string> dirs) {
            dirs.Add(path);
            var TheFolder = new DirectoryInfo(path);
            foreach (var childFolder in TheFolder.GetDirectories()) {
                RecursiveFolder(childFolder.FullName, dirs);
            }
            return dirs;
        }
        private bool ButtonClickAction(string pathToProcess) {
            var fileName = AppDomain.CurrentDomain.BaseDirectory + @"\Core\ImgsToPDFCore.exe";
            string tempFileListPath = null;
            try {
                if (selectedImages.Any()) {
                    var validSelections = selectedImages
                        .Where(File.Exists)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    if (validSelections.Count == 0) {
                        MessageBox.Show(Extra.ApplyResource(typeof(Extra), "strNoValidSelection"));
                        return false;
                    }
                    tempFileListPath = Path.Combine(Path.GetTempPath(), $"ImgsToPDF_selected_{Guid.NewGuid():N}.txt");
                    File.WriteAllLines(tempFileListPath, validSelections);
                    List<string> argsList = new List<string> {
                        "--file-list", tempFileListPath,
                        "-l", generateModeBox.SelectedIndex.ToString(),
                        "--quality", GetSelectedQualityValue().ToString()
                    };
                    if (UniformWidth.Checked) { argsList.Add("--uniform-width"); }
                    string[] args = argsList.ToArray();
                    var stderr = RunProcess(fileName, args);
                    if (stderr.Length > 0) {
                        MessageBox.Show(stderr);
                        return false;
                    }
                    return true;
                }

                if (Recursive.Checked && Directory.Exists(pathToProcess)) {
                    RecursiveFolder(pathToProcess, new List<string> { }).AsParallel().WithDegreeOfParallelism(4).ForAll(dirPath => {
                        List<string> argsList = new List<string> {
                            "-d", dirPath,
                            "-l", generateModeBox.SelectedIndex.ToString(),
                            "--quality", GetSelectedQualityValue().ToString()
                        };
                        if (UniformWidth.Checked) { argsList.Add("--uniform-width"); }
                        string[] args = argsList.ToArray();
                        var stderr = RunProcess(fileName, args);
                        if (stderr.Length > 0) {
                            MessageBox.Show(stderr);
                        }
                    });
                }
                else {
                    List<string> argsList = new List<string> {
                        "-d", pathToProcess,
                        "-l", generateModeBox.SelectedIndex.ToString(),
                        "--quality", GetSelectedQualityValue().ToString()
                    };
                    if (UniformWidth.Checked) { argsList.Add("--uniform-width"); }
                    string[] args = argsList.ToArray();
                    var stderr = RunProcess(fileName, args);
                    if (stderr.Length > 0) {
                        MessageBox.Show(stderr);
                        return false;
                    }
                }
                return true;
            }
            finally {
                if (tempFileListPath != null && File.Exists(tempFileListPath)) {
                    try {
                        File.Delete(tempFileListPath);
                    }
                    catch (Exception) {
                        // ignore cleanup errors
                    }
                }
            }
        }
        /// <summary>
        /// Runs the core generator process and returns stderr output.
        /// </summary>
        /// <param name="fileName">Executable path.</param>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>stderr output</returns>
        private static string RunProcess(string fileName, string[] args) {
            for (int i = 0; i < args.Length; i++) {
                if (args[i].EndsWith(@"\")) {
                    // Preserve a trailing slash after quoting.
                    args[i] += @"\";
                }
                args[i] = string.Format("\"{0}\"", args[i]);
            }
            Process p = new Process();
            p.StartInfo.FileName = fileName;
            p.StartInfo.Arguments = string.Join(" ", args);
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.CreateNoWindow = true;
            p.Start();
            return p.StandardError.ReadToEnd();
        }

        private void GalleryButton_Click(object sender, EventArgs e) {
            if (!Directory.Exists(PathLabel.Text)) {
                return;
            }
            using (var galleryForm = new GalleryForm(PathLabel.Text)) {
                var result = galleryForm.ShowDialog(this);
                if (result == DialogResult.OK) {
                    selectedImages = galleryForm.SelectedFiles;
                    MsgLabel.Text = string.Format(Extra.ApplyResource(typeof(Extra), "strGallerySelected"), selectedImages.Count);
                }
            }
        }
        private void toolStripMenuExit_Click(object sender, EventArgs e) {
            this.Close();
        }
        private void toolStripMenuConfigFile_Click(object sender, EventArgs e) {
            Process.Start(AppDomain.CurrentDomain.BaseDirectory + "/Core/Config.lua");
        }
        private void toolStripMenuAbout_Click(object sender, EventArgs e) {
            MessageBox.Show(
                "ImagesToPDF v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version + "\nCopyright (c) 2022-2024 Sinryou. At MIT License.",
                "About",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1,
                0,
                "https://github.com/Sinryou/ImagesToPDF"
            );
        }
        private void toolStripMenuOpenFolder_Click(object sender, EventArgs e) {
            FolderBrowserDialog dialog = new FolderBrowserDialog {
                Description = Extra.ApplyResource(typeof(Extra), "strSelectIMGFolder")
            };
            if (dialog.ShowDialog() == DialogResult.Cancel) {
                return;
            }
            string directoryPath = dialog.SelectedPath.Trim();
            ChooseFileAction(directoryPath);
        }
        private void toolStripMenuClearChosen_Click(object sender, EventArgs e) {
            pathQueue.Clear();
            queueIndex = -1;
            ClearCurrentView();
            UpdateQueueControls();
        }

        private void englishToolStripMenuItem_Click(object sender, EventArgs e) {
            SwitchLanguage("en-US");
        }

        private void chineseToolStripMenuItem_Click(object sender, EventArgs e) {
            SwitchLanguage("zh-CN");
        }

        private void toolStripMenuTopMost_Click(object sender, EventArgs e) {
            this.TopMost = toolStripMenuTopMost.Checked;
            Properties.Settings.Default.AlwaysOnTop = this.TopMost;
            Properties.Settings.Default.Save();
            if (this.TopMost) {
                this.BringToFront();
                this.Activate();
            }
            UpdateTopMostMenuStyle();
            StartTopMostAnimation();
        }

        private void UpdateLanguageMenuState() {
            bool isChinese = Thread.CurrentThread.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            chineseToolStripMenuItem.Checked = isChinese;
            chineseToolStripMenuItem.Enabled = !isChinese;
            englishToolStripMenuItem.Checked = !isChinese;
            englishToolStripMenuItem.Enabled = isChinese;
        }

        private void ReloadGenerateModeOptions() {
            int selectedIndex = generateModeBox.SelectedIndex;
            generateModeBox.Items.Clear();
            generateModeBox.Items.AddRange(new string[] {
                Extra.ApplyResource(typeof(Extra), "strSingle"),
                Extra.ApplyResource(typeof(Extra), "strDuplex"),
                Extra.ApplyResource(typeof(Extra), "strDuplexRightToLeft")
            });
            if (selectedIndex < 0 || selectedIndex >= generateModeBox.Items.Count) {
                selectedIndex = 0;
            }
            generateModeBox.SelectedIndex = selectedIndex;
        }

        private void ReloadQualityOptions() {
            int value = GetSelectedQualityValue();
            bool isChinese = Thread.CurrentThread.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            qualityLabel.Text = isChinese ? "PDF质量:" : "PDF Quality:";
            qualityBox.Items.Clear();
            qualityBox.Items.AddRange(new object[] {
                new PdfQualityOption(isChinese ? "快速 (70)" : "Fast (70)", 70),
                new PdfQualityOption(isChinese ? "默认 (80)" : "Default (80)", 80),
                new PdfQualityOption(isChinese ? "高质量 (90)" : "High Quality (90)", 90),
                new PdfQualityOption(isChinese ? "原图" : "Original", 0)
            });
            for (int i = 0; i < qualityBox.Items.Count; i++) {
                if (((PdfQualityOption)qualityBox.Items[i]).Value == value) {
                    qualityBox.SelectedIndex = i;
                    return;
                }
            }
            qualityBox.SelectedIndex = 1;
        }

        private int GetSelectedQualityValue() {
            return qualityBox?.SelectedItem is PdfQualityOption option ? option.Value : 80;
        }

        private void SwitchLanguage(string languageName) {
            if (string.IsNullOrWhiteSpace(languageName)) { return; }
            if (Thread.CurrentThread.CurrentUICulture.Name.Equals(languageName, StringComparison.OrdinalIgnoreCase)) { return; }

            var culture = new CultureInfo(languageName);
            Thread.CurrentThread.CurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;

            Properties.Settings.Default.DefaultLanguage = languageName;
            Properties.Settings.Default.Save();

            ApplyLocalizedResources();
        }

        private void ApplyLocalizedResources() {
            var resources = new ComponentResourceManager(typeof(ImgsToPDF));
            resources.ApplyResources(this, "$this");
            foreach (Control control in Controls) {
                ApplyResourcesRecursively(control, resources);
            }
            ApplyToolStripResources(menuStripMain.Items, resources);

            ReloadGenerateModeOptions();
            ReloadQualityOptions();
            UpdateLanguageMenuState();
            UpdateTopMostMenuStyle();
            AdjustMainActionButtonsLayout();

            if (selectedImages.Any()) {
                MsgLabel.Text = string.Format(Extra.ApplyResource(typeof(Extra), "strGallerySelected"), selectedImages.Count);
            }
            else if (StartButton.Enabled && Directory.Exists(PathLabel.Text)) {
                MsgLabel.Text = Extra.ApplyResource(typeof(Extra), "strClickToStart");
            }
        }

        private void AdjustMainActionButtonsLayout() {
            if (StartButton == null || GalleryButton == null || progressBar == null || generateModeBox == null) { return; }
            if (progressBarRightMargin < 0) {
                progressBarRightMargin = Math.Max(0, this.ClientSize.Width - progressBar.Right);
            }

            int startWidth = GetPreferredButtonWidth(StartButton, StartButtonMinWidth);
            int galleryWidth = GetPreferredButtonWidth(GalleryButton, GalleryButtonMinWidth);

            int buttonAreaLeft = generateModeBox.Right + 12;
            int progressRightEdge = this.ClientSize.Width - progressBarRightMargin;
            int progressLeft = progressBar.Right > progressRightEdge ? progressRightEdge - progressBar.Width : progressBar.Left;
            int requiredWidth = startWidth + MainActionButtonGap + galleryWidth;
            int availableWidth = progressLeft - MainActionButtonGap - buttonAreaLeft;

            if (requiredWidth > availableWidth) {
                int overflow = requiredWidth - availableWidth;
                int shrinkableProgress = Math.Max(0, progressBar.Width - ProgressBarMinWidth);
                int shrinkProgress = Math.Min(overflow, shrinkableProgress);
                if (shrinkProgress > 0) {
                    progressBar.Width -= shrinkProgress;
                    progressBar.Left = progressRightEdge - progressBar.Width;
                    progressLeft = progressBar.Left;
                    availableWidth = progressLeft - MainActionButtonGap - buttonAreaLeft;
                }
            }

            if (requiredWidth > availableWidth) {
                int availableForButtons = Math.Max(0, availableWidth - MainActionButtonGap);
                int minTotal = StartButtonMinWidth + GalleryButtonMinWidth;
                if (availableForButtons >= minTotal) {
                    float startRatio = (float)startWidth / (startWidth + galleryWidth);
                    startWidth = Math.Max(StartButtonMinWidth, (int)Math.Floor(availableForButtons * startRatio));
                    galleryWidth = Math.Max(GalleryButtonMinWidth, availableForButtons - startWidth);
                }
            }

            StartButton.Width = startWidth;
            GalleryButton.Width = galleryWidth;
            GalleryButton.Left = progressBar.Left - MainActionButtonGap - GalleryButton.Width;
            StartButton.Left = GalleryButton.Left - MainActionButtonGap - StartButton.Width;
            if (StartButton.Left < buttonAreaLeft) {
                int moveRight = buttonAreaLeft - StartButton.Left;
                StartButton.Left += moveRight;
                GalleryButton.Left += moveRight;
            }
            GalleryButton.Top = StartButton.Top;
        }

        private static int GetPreferredButtonWidth(Button button, int minWidth) {
            if (button == null) { return minWidth; }
            var textSize = TextRenderer.MeasureText(button.Text ?? string.Empty, button.Font);
            return Math.Max(minWidth, textSize.Width + MainActionButtonHorizontalPadding);
        }

        private static void ApplyResourcesRecursively(Control control, ComponentResourceManager resources) {
            resources.ApplyResources(control, control.Name);
            foreach (Control child in control.Controls) {
                ApplyResourcesRecursively(child, resources);
            }
        }

        private static void ApplyToolStripResources(ToolStripItemCollection items, ComponentResourceManager resources) {
            foreach (ToolStripItem item in items) {
                resources.ApplyResources(item, item.Name);
                if (item is ToolStripMenuItem menuItem && menuItem.DropDownItems.Count > 0) {
                    ApplyToolStripResources(menuItem.DropDownItems, resources);
                }
            }
        }

        private void StartTopMostAnimation() {
            topMostAnimationTick = 0;
            topMostAnimationTimer.Stop();
            topMostAnimationTimer.Start();
        }

        private void TopMostAnimationTimer_Tick(object sender, EventArgs e) {
            topMostAnimationTick++;
            float progress = (float)Math.Sin(Math.PI * topMostAnimationTick / TopMostAnimationFrames);
            UpdateTopMostMenuStyle(progress);
            if (topMostAnimationTick >= TopMostAnimationFrames) {
                topMostAnimationTimer.Stop();
                UpdateTopMostMenuStyle();
            }
        }

        private static Color BlendColor(Color from, Color to, float factor) {
            factor = Math.Max(0f, Math.Min(1f, factor));
            int r = from.R + (int)((to.R - from.R) * factor);
            int g = from.G + (int)((to.G - from.G) * factor);
            int b = from.B + (int)((to.B - from.B) * factor);
            return Color.FromArgb(r, g, b);
        }

        private void UpdateTopMostMenuStyle(float animationProgress = 0f) {
            toolStripMenuTopMost.Text = toolStripMenuTopMost.Checked
                ? Extra.ApplyResource(typeof(Extra), "strTopMostOn")
                : Extra.ApplyResource(typeof(Extra), "strTopMostOff");

            if (toolStripMenuTopMost.Checked) {
                var baseColor = BlendColor(Color.SteelBlue, Color.DodgerBlue, animationProgress);
                toolStripMenuTopMost.BackColor = baseColor;
                toolStripMenuTopMost.ForeColor = Color.White;
                return;
            }

            toolStripMenuTopMost.BackColor = BlendColor(SystemColors.Control, Color.Gainsboro, animationProgress * 0.5f);
            toolStripMenuTopMost.ForeColor = SystemColors.ControlText;
        }

        private sealed class PdfQualityOption {
            public PdfQualityOption(string text, int value) {
                Text = text;
                Value = value;
            }

            public string Text { get; }
            public int Value { get; }

            public override string ToString() {
                return Text;
            }
        }
    }
}
