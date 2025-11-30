using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using WebPWrapper;

namespace ImgsToPDF
{
    public partial class GalleryForm : Form
    {
        private readonly string directoryPath;
        private readonly List<GalleryItem> items = new List<GalleryItem>();
        private readonly HashSet<string> allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            ".png", ".apng", ".jpg", ".jpeg", ".jfif", ".pjpeg", ".pjp", ".bmp", ".tif", ".tiff", ".gif", ".webp"
        };
        private const int BasePanelWidth = 200;
        private const int BasePanelHeight = 270;
        private const int BaseThumbSize = 170;
        private const int BaseLabelHeight = 38;
        private const float BaseFontSize = 9f;
        private float currentScale = 1.0f;
        private readonly Dictionary<float, Font> fontCache = new Dictionary<float, Font>();
        private readonly Font baseLabelFont;
        private string selectionSummaryFormat = "Selected {0} / Total {1}";
        private string confirmPromptFormat = "Generate {0} images?";

        public GalleryForm(string directoryPath) {
            this.directoryPath = directoryPath;
            InitializeComponent();
            baseLabelFont = new Font(this.Font.FontFamily, BaseFontSize, FontStyle.Regular);
            thumbFlowPanel.MouseWheel += ThumbFlowPanel_MouseWheel;
            thumbFlowPanel.MouseEnter += (s, e) => thumbFlowPanel.Focus();
        }

        public List<string> SelectedFiles {
            get { return items.Where(i => i.IsSelected).Select(i => i.Path).ToList(); }
        }

        private void GalleryForm_Load(object sender, EventArgs e) {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath)) {
                MessageBox.Show(this, "Directory is invalid.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }

            selectionSummaryFormat = selectionStatusLabel.Text;
            confirmPromptFormat = confirmButton.Tag?.ToString() ?? confirmPromptFormat;
            InitializeExtensionList();
            UpdateSelectionStatus();
            _ = LoadImagesAsync();
        }

        private void InitializeExtensionList() {
            var extensions = allowedExtensions.OrderBy(ext => ext).ToArray();
            extensionCheckedList.Items.AddRange(extensions);
            for (int i = 0; i < extensionCheckedList.Items.Count; i++) {
                extensionCheckedList.SetItemChecked(i, true);
            }
        }

        private async Task LoadImagesAsync() {
            SetLoadingState(true);
            var files = Directory.EnumerateFiles(directoryPath)
                .Where(p => allowedExtensions.Contains(Path.GetExtension(p) ?? string.Empty));

            await Task.Run(() => {
                foreach (var file in files) {
                    try {
                        using (var image = LoadBitmapSafe(file)) {
                            if (image == null) { continue; }
                            using (var thumb = CreateThumbnail(image, 160)) {
                                var aspectRatio = image.Height == 0 ? 0 : (double)image.Width / image.Height;
                                var item = new GalleryItem {
                                    Path = file,
                                    Extension = Path.GetExtension(file)?.ToLower() ?? string.Empty,
                                    Size = image.Size,
                                    AspectRatio = aspectRatio,
                                    IsSelected = true
                                };
                                AddItemOnUiThread(item, (Image)thumb.Clone());
                            }
                        }
                    }
                    catch (Exception) {
                        // Skip invalid images
                        continue;
                    }
                }
            });

            SetLoadingState(false);
            ApplyFilter();
        }

        private static Image CreateThumbnail(Image original, int maxEdge) {
            if (original.Width == 0 || original.Height == 0) {
                return new Bitmap(1, 1);
            }
            double scale = original.Width > original.Height
                ? (double)maxEdge / original.Width
                : (double)maxEdge / original.Height;
            int width = Math.Max(1, (int)Math.Round(original.Width * scale));
            int height = Math.Max(1, (int)Math.Round(original.Height * scale));
            var thumbnail = new Bitmap(width, height);
            using (var graphics = Graphics.FromImage(thumbnail)) {
                graphics.Clear(Color.Transparent);
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(original, 0, 0, width, height);
            }
            return thumbnail;
        }

        private Image LoadBitmapSafe(string path) {
            var extension = Path.GetExtension(path)?.ToLower();
            try {
                if (extension == ".webp") {
                    using (var webp = new WebP()) {
                        return webp.Load(path);
                    }
                }
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var img = Image.FromStream(fs, false, false)) {
                    return new Bitmap(img);
                }
            }
            catch (Exception) {
                return null;
            }
        }

        private void AddItemOnUiThread(GalleryItem item, Image thumbnail) {
            if (IsDisposed) { thumbnail.Dispose(); return; }
            BeginInvoke(new Action(() => {
                if (IsDisposed) { thumbnail.Dispose(); return; }
                var panel = new Panel {
                    Width = BasePanelWidth,
                    Height = BasePanelHeight,
                    Margin = new Padding(8)
                };

                var pictureBox = new PictureBox {
                    Image = thumbnail,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Width = BaseThumbSize,
                    Height = BaseThumbSize,
                    Cursor = Cursors.Hand,
                    Left = 10,
                    Top = 8,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };
                pictureBox.DoubleClick += (s, e) => OpenInViewer(item.Path);

                var nameLabel = new Label {
                    AutoSize = false,
                    Width = BaseThumbSize,
                    Height = BaseLabelHeight,
                    Left = 10,
                    Top = pictureBox.Bottom + 4,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Text = $"{Path.GetFileName(item.Path)}\n{item.Size.Width}x{item.Size.Height}"
                };

                var checkBox = new CheckBox {
                    AutoSize = true,
                    Checked = true,
                    Left = (panel.Width / 2) - 10,
                    Top = nameLabel.Bottom + 4
                };
                checkBox.CheckedChanged += (s, e) => {
                    item.IsSelected = checkBox.Checked;
                    UpdateSelectionStatus();
                };

                panel.Controls.Add(pictureBox);
                panel.Controls.Add(nameLabel);
                panel.Controls.Add(checkBox);
                thumbFlowPanel.Controls.Add(panel);

                item.Container = panel;
                item.SelectBox = checkBox;
                item.PictureBox = pictureBox;
                item.NameLabel = nameLabel;
                items.Add(item);
                ApplyScaleToItem(item);
                UpdateSelectionStatus();
            }));
        }

        private void OpenInViewer(string imagePath) {
            try {
                Process.Start(new ProcessStartInfo {
                    FileName = imagePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex) {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetLoadingState(bool isLoading) {
            BeginInvoke(new Action(() => {
                loadingLabel.Visible = isLoading;
                selectAllButton.Enabled = !isLoading;
                selectNoneButton.Enabled = !isLoading;
                extensionsSelectAllButton.Enabled = !isLoading;
                extensionsClearButton.Enabled = !isLoading;
                confirmButton.Enabled = !isLoading;
                resetZoomButton.Enabled = !isLoading;
            }));
        }

        private void ApplyFilter() {
            var selectedExts = extensionCheckedList.CheckedItems
                .Cast<string>()
                .Select(ext => ext.ToLower())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var item in items) {
                bool matchExt = selectedExts.Count > 0 && selectedExts.Contains(item.Extension);
                bool visible = matchExt;
                item.Container.Visible = visible;
            }
            UpdateSelectionStatus();
        }

        private void selectAllButton_Click(object sender, EventArgs e) {
            SetSelectionForVisible(true);
        }

        private void selectNoneButton_Click(object sender, EventArgs e) {
            SetSelectionForVisible(false);
        }

        private void SetSelectionForVisible(bool isChecked) {
            foreach (var item in items.Where(i => i.Container.Visible)) {
                item.SelectBox.Checked = isChecked;
                item.IsSelected = isChecked;
            }
            UpdateSelectionStatus();
        }

        private void extensionsSelectAllButton_Click(object sender, EventArgs e) {
            for (int i = 0; i < extensionCheckedList.Items.Count; i++) {
                extensionCheckedList.SetItemChecked(i, true);
            }
            ApplyFilter();
        }

        private void extensionsClearButton_Click(object sender, EventArgs e) {
            for (int i = 0; i < extensionCheckedList.Items.Count; i++) {
                extensionCheckedList.SetItemChecked(i, false);
            }
            ApplyFilter();
        }

        private void UpdateSelectionStatus() {
            int selectedCount = items.Count(i => i.IsSelected);
            int totalCount = items.Count;
            selectionStatusLabel.Text = string.Format(selectionSummaryFormat, selectedCount, totalCount);
        }

        private void confirmButton_Click(object sender, EventArgs e) {
            int selectedCount = items.Count(i => i.IsSelected);
            if (selectedCount == 0) {
                MessageBox.Show(this, noSelectionLabel.Text, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string message = string.Format(confirmPromptFormat, selectedCount);
            var result = MessageBox.Show(this, message, Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (result == DialogResult.OK) {
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private void cancelButton_Click(object sender, EventArgs e) {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void extensionCheckedList_ItemCheck(object sender, ItemCheckEventArgs e) {
            BeginInvoke(new Action(ApplyFilter));
        }

        private void resetZoomButton_Click(object sender, EventArgs e) {
            currentScale = 1.0f;
            ApplyScaleToAllItems();
        }

        private void ThumbFlowPanel_MouseWheel(object sender, MouseEventArgs e) {
            if ((ModifierKeys & Keys.Control) != Keys.Control) { return; }
            float delta = e.Delta > 0 ? 0.1f : -0.1f;
            float newScale = Math.Max(0.5f, Math.Min(3.0f, currentScale + delta));
            if (Math.Abs(newScale - currentScale) < 0.001f) { return; }
            currentScale = newScale;
            ApplyScaleToAllItems();
            if (e is HandledMouseEventArgs h) {
                h.Handled = true;
            }
        }

        private void ApplyScaleToAllItems() {
            thumbFlowPanel.SuspendLayout();
            foreach (var item in items) {
                ApplyScaleToItem(item);
            }
            thumbFlowPanel.ResumeLayout(true);
        }

        private void ApplyScaleToItem(GalleryItem item) {
            if (item.Container == null || item.PictureBox == null || item.NameLabel == null || item.SelectBox == null) {
                return;
            }
            int panelWidth = (int)(BasePanelWidth * currentScale);
            int thumbSize = (int)(BaseThumbSize * currentScale);
            int labelHeight = (int)(BaseLabelHeight * currentScale);
            item.Container.Width = panelWidth;
            item.PictureBox.Width = thumbSize;
            item.PictureBox.Height = thumbSize;
            item.PictureBox.Left = (panelWidth - thumbSize) / 2;
            item.NameLabel.Width = thumbSize;
            item.NameLabel.Height = labelHeight;
            item.NameLabel.Left = item.PictureBox.Left;
            item.NameLabel.Top = item.PictureBox.Bottom + 4;
            var fontSize = Math.Max(7f, BaseFontSize * currentScale);
            item.NameLabel.Font = GetScaledFont(fontSize);
            item.SelectBox.Top = item.NameLabel.Bottom + 6;
            item.SelectBox.Left = (panelWidth - item.SelectBox.Width) / 2;
            item.Container.Height = item.SelectBox.Bottom + 8;
        }

        private Font GetScaledFont(float size) {
            size = (float)Math.Round(size, 1);
            if (fontCache.TryGetValue(size, out var cached)) {
                return cached;
            }
            var font = new Font(baseLabelFont.FontFamily, size, baseLabelFont.Style);
            fontCache[size] = font;
            return font;
        }

        private class GalleryItem
        {
            public string Path { get; set; }
            public string Extension { get; set; }
            public Size Size { get; set; }
            public double AspectRatio { get; set; }
            public bool IsSelected { get; set; }
            public Panel Container { get; set; }
            public CheckBox SelectBox { get; set; }
            public PictureBox PictureBox { get; set; }
            public Label NameLabel { get; set; }
        }
    }
}
