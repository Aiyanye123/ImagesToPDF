using ImgsToPDF.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WebPWrapper;

namespace ImgsToPDF
{
    public partial class GalleryForm : Form
    {
        private readonly string directoryPath;
        private readonly List<PageEdit> initialPages;
        private readonly List<GalleryItem> items = new List<GalleryItem>();
        private readonly VirtualGalleryView galleryView = new VirtualGalleryView();
        private readonly Panel galleryHeaderPanel = new Panel();
        private readonly Label headerSelectionLabel = new Label();
        private readonly Button sortButton = new Button();
        private readonly Button menuButton = new Button();
        private readonly ModernPanel extensionCard = new ModernPanel();
        private readonly ModernPanel selectionCard = new ModernPanel();
        private readonly ModernPanel viewCard = new ModernPanel();
        private readonly ModernPanel outputCard = new ModernPanel();
        private readonly SelectionRing selectionRing = new SelectionRing();
        private readonly ZoomSlider zoomTrackBar = new ZoomSlider();
        private readonly Label zoomValueLabel = new Label();
        private readonly Button zoomOutButton = new Button();
        private readonly Button zoomInButton = new Button();
        private readonly Label pathStatusLabel = new Label();
        private readonly Label totalStatusLabel = new Label();
        private readonly Button resetAllButton = new Button();
        private readonly Button pageViewButton = new Button();
        private readonly Button pdfPreviewButton = new Button();
        private readonly RadioButton singleLayoutRadio = new RadioButton();
        private readonly RadioButton leftToRightRadio = new RadioButton();
        private readonly RadioButton rightToLeftRadio = new RadioButton();
        private readonly RadioButton verticalPreviewRadio = new RadioButton();
        private readonly RadioButton horizontalPreviewRadio = new RadioButton();
        private readonly Panel previewFlowPanel = new Panel();
        private readonly Panel readingDirectionPanel = new Panel();
        private readonly ContextMenuStrip pageMenu = new ContextMenuStrip();
        private int selectedLayout;
        private string selectionSummaryFormat = "Selected {0} / Total {1}";
        private string confirmPromptFormat = "Generate {0} images?";
        private bool isBulkExtensionChanging;
        private bool isUpdatingZoom;

        private static readonly Color SurfaceColor = Color.FromArgb(246, 248, 252);
        private static readonly Color PanelColor = Color.White;
        private static readonly Color BorderColor = Color.FromArgb(218, 224, 234);
        private static readonly Color AccentColor = Color.FromArgb(41, 98, 255);
        private static readonly Color WarningColor = Color.FromArgb(214, 72, 72);

        public GalleryForm(string directoryPath, IEnumerable<PageEdit> existingPages = null, int layout = 0) {
            this.directoryPath = directoryPath;
            initialPages = existingPages?.Select(page => page.Clone()).ToList() ?? new List<PageEdit>();
            selectedLayout = Math.Max(0, Math.Min(2, layout));
            InitializeComponent();
            InitializeModernLayout();
        }

        public List<PageEdit> SelectedPages {
            get { return items.Select(ToPageEdit).ToList(); }
        }

        public int SelectedLayout => selectedLayout;

        protected override void OnFormClosed(FormClosedEventArgs e) {
            foreach (var item in items) {
                item.Thumbnail?.Dispose();
            }
            pageMenu.Dispose();
            base.OnFormClosed(e);
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
            LoadImageList();
            InitializeExtensionList();
            ApplyFilter();
        }

        private void InitializeModernLayout() {
            SuspendLayout();

            BackColor = SurfaceColor;
            DoubleBuffered = true;

            Controls.Remove(thumbFlowPanel);
            thumbFlowPanel.Dispose();

            filterPanel.Dock = DockStyle.Left;
            filterPanel.Width = 292;
            filterPanel.Padding = new Padding(16);
            filterPanel.BackColor = SurfaceColor;

            bottomPanel.Dock = DockStyle.Bottom;
            bottomPanel.Height = 58;
            bottomPanel.Padding = new Padding(0, 10, 14, 10);
            bottomPanel.BackColor = SurfaceColor;
            bottomPanel.Controls.Add(pathStatusLabel);
            bottomPanel.Controls.Add(totalStatusLabel);

            galleryView.Dock = DockStyle.None;
            galleryView.BackColor = SurfaceColor;
            galleryView.SelectionChanged += (s, e) => UpdateSelectionStatus();
            galleryView.OpenRequested += (s, path) => OpenInViewer(path);
            galleryView.ZoomChanged += (s, e) => UpdateZoomControls();
            galleryView.ContextRequested += GalleryView_ContextRequested;
            galleryView.ReorderRequested += GalleryView_ReorderRequested;
            galleryHeaderPanel.BackColor = SurfaceColor;
            Controls.Add(galleryHeaderPanel);
            Controls.Add(galleryView);
            bottomPanel.BringToFront();
            filterPanel.BringToFront();

            LayoutSidebar();
            LayoutHeader();
            LayoutGallery();
            zoomTrackBar.ValueChanged += zoomTrackBar_ValueChanged;
            zoomOutButton.Click += (s, e) => galleryView.AdjustZoom(-10);
            zoomInButton.Click += (s, e) => galleryView.AdjustZoom(10);
            StyleButton(extensionsSelectAllButton);
            StyleButton(extensionsClearButton);
            StyleButton(selectAllButton);
            StyleButton(selectNoneButton);
            StyleButton(resetZoomButton);
            StyleButton(zoomOutButton);
            StyleButton(zoomInButton);
            StyleButton(sortButton);
            StyleButton(menuButton);
            StyleButton(resetAllButton);
            StyleButton(pageViewButton, true);
            StyleButton(pdfPreviewButton);
            StyleButton(cancelButton);
            StyleButton(confirmButton, true);
            ConfigurePageMenu();

            ResumeLayout(true);
        }

        private void LayoutSidebar() {
            filterPanel.Controls.Clear();
            filterPanel.AutoScroll = true;
            ConfigureCard(extensionCard, 16, 16, 260, 198);
            ConfigureCard(selectionCard, 16, 228, 260, 344);
            ConfigureCard(viewCard, 16, 586, 260, 190);
            ConfigureCard(outputCard, 16, 790, 260, 278);

            extensionLabel.AutoSize = false;
            extensionLabel.TextAlign = ContentAlignment.MiddleLeft;
            extensionLabel.ForeColor = Color.FromArgb(35, 42, 52);
            extensionLabel.Font = new Font(Font, FontStyle.Bold);
            extensionLabel.SetBounds(18, 14, 224, 24);

            extensionCheckedList.DrawMode = DrawMode.OwnerDrawFixed;
            extensionCheckedList.ItemHeight = 26;
            extensionCheckedList.BackColor = PanelColor;
            extensionCheckedList.BorderStyle = BorderStyle.None;
            extensionCheckedList.CheckOnClick = true;
            extensionCheckedList.DrawItem -= extensionCheckedList_DrawItem;
            extensionCheckedList.DrawItem += extensionCheckedList_DrawItem;
            extensionCheckedList.SetBounds(18, 48, 224, 104);

            extensionsSelectAllButton.SetBounds(18, 160, 105, 28);
            extensionsClearButton.SetBounds(137, 160, 105, 28);

            selectionStatusLabel.AutoSize = false;
            selectionStatusLabel.SetBounds(18, 14, 224, 26);
            selectionStatusLabel.Font = new Font(Font, FontStyle.Bold);
            selectionRing.SetBounds(54, 52, 150, 150);
            selectAllButton.SetBounds(18, 214, 224, 30);
            selectNoneButton.SetBounds(18, 248, 224, 30);

            resetAllButton.Text = IsChinese ? "一键全部重置" : "Reset All";
            resetAllButton.SetBounds(18, 282, 224, 30);
            resetAllButton.Click -= resetAllButton_Click;
            resetAllButton.Click += resetAllButton_Click;

            noSelectionLabel.AutoSize = false;
            noSelectionLabel.SetBounds(18, 316, 224, 18);
            noSelectionLabel.ForeColor = WarningColor;
            noSelectionLabel.Visible = false;

            loadingLabel.AutoSize = false;
            loadingLabel.SetBounds(18, 166, 224, 18);
            loadingLabel.ForeColor = AccentColor;
            loadingLabel.Visible = false;

            zoomOutButton.Text = "-";
            zoomInButton.Text = "+";
            zoomValueLabel.TextAlign = ContentAlignment.MiddleCenter;
            zoomTrackBar.Minimum = 65;
            zoomTrackBar.Maximum = 180;
            zoomTrackBar.SetBounds(54, 58, 132, 38);
            zoomOutButton.SetBounds(18, 60, 30, 28);
            zoomInButton.SetBounds(192, 60, 30, 28);
            zoomValueLabel.SetBounds(224, 60, 36, 28);
            resetZoomButton.SetBounds(18, 134, 224, 32);

            var zoomTitle = new Label {
                Text = IsChinese ? "视图与缩放" : "View and Zoom",
                Font = new Font(Font, FontStyle.Bold),
                Location = new Point(18, 16),
                Size = new Size(224, 24)
            };

            extensionCard.Controls.Add(extensionLabel);
            extensionCard.Controls.Add(extensionCheckedList);
            extensionCard.Controls.Add(extensionsSelectAllButton);
            extensionCard.Controls.Add(extensionsClearButton);
            selectionCard.Controls.Add(selectionStatusLabel);
            selectionCard.Controls.Add(selectionRing);
            selectionCard.Controls.Add(selectAllButton);
            selectionCard.Controls.Add(selectNoneButton);
            selectionCard.Controls.Add(resetAllButton);
            selectionCard.Controls.Add(noSelectionLabel);
            viewCard.Controls.Add(zoomTitle);
            viewCard.Controls.Add(zoomOutButton);
            viewCard.Controls.Add(zoomTrackBar);
            viewCard.Controls.Add(zoomInButton);
            viewCard.Controls.Add(zoomValueLabel);
            viewCard.Controls.Add(resetZoomButton);
            viewCard.Controls.Add(loadingLabel);

            var outputTitle = new Label {
                Text = IsChinese ? "输出预览" : "Output Preview",
                Font = new Font(Font, FontStyle.Bold),
                Location = new Point(18, 14),
                Size = new Size(224, 24)
            };
            pageViewButton.Text = IsChinese ? "页面视图" : "Pages";
            pdfPreviewButton.Text = IsChinese ? "PDF 预览" : "PDF Preview";
            pageViewButton.SetBounds(18, 48, 105, 30);
            pdfPreviewButton.SetBounds(137, 48, 105, 30);
            pageViewButton.Click -= pageViewButton_Click;
            pageViewButton.Click += pageViewButton_Click;
            pdfPreviewButton.Click -= pdfPreviewButton_Click;
            pdfPreviewButton.Click += pdfPreviewButton_Click;
            var previewFlowTitle = new Label {
                Text = IsChinese ? "预览滚动" : "Preview Flow",
                Location = new Point(18, 92),
                Size = new Size(224, 22)
            };
            verticalPreviewRadio.Text = IsChinese ? "纵向" : "Vertical";
            horizontalPreviewRadio.Text = IsChinese ? "横向" : "Horizontal";
            previewFlowPanel.SetBounds(18, 114, 224, 28);
            verticalPreviewRadio.SetBounds(0, 0, 100, 24);
            horizontalPreviewRadio.SetBounds(112, 0, 112, 24);
            verticalPreviewRadio.Checked = true;
            verticalPreviewRadio.CheckedChanged += previewFlowRadio_CheckedChanged;
            horizontalPreviewRadio.CheckedChanged += previewFlowRadio_CheckedChanged;
            previewFlowPanel.Controls.Add(verticalPreviewRadio);
            previewFlowPanel.Controls.Add(horizontalPreviewRadio);
            var directionTitle = new Label {
                Text = IsChinese ? "阅读方向" : "Reading Direction",
                Location = new Point(18, 150),
                Size = new Size(224, 22)
            };
            readingDirectionPanel.SetBounds(18, 172, 224, 78);
            ConfigureLayoutRadio(singleLayoutRadio, IsChinese ? "单页" : "Single", 0, 0, 206, 24, 0);
            ConfigureLayoutRadio(leftToRightRadio, IsChinese ? "左 → 右" : "Left → Right", 0, 26, 206, 24, 1);
            ConfigureLayoutRadio(rightToLeftRadio, IsChinese ? "右 → 左" : "Right → Left", 0, 52, 206, 24, 2);
            if (selectedLayout == 0) { singleLayoutRadio.Checked = true; }
            else if (selectedLayout == 1) { leftToRightRadio.Checked = true; }
            else { rightToLeftRadio.Checked = true; }
            outputCard.Controls.Add(outputTitle);
            outputCard.Controls.Add(pageViewButton);
            outputCard.Controls.Add(pdfPreviewButton);
            outputCard.Controls.Add(previewFlowTitle);
            outputCard.Controls.Add(previewFlowPanel);
            outputCard.Controls.Add(directionTitle);
            readingDirectionPanel.Controls.Add(singleLayoutRadio);
            readingDirectionPanel.Controls.Add(leftToRightRadio);
            readingDirectionPanel.Controls.Add(rightToLeftRadio);
            outputCard.Controls.Add(readingDirectionPanel);
            filterPanel.Controls.Add(extensionCard);
            filterPanel.Controls.Add(selectionCard);
            filterPanel.Controls.Add(viewCard);
            filterPanel.Controls.Add(outputCard);
            UpdateZoomControls();

            cancelButton.Size = new Size(90, 34);
            confirmButton.Size = new Size(110, 34);
            cancelButton.Location = new Point(bottomPanel.Width - cancelButton.Width - 14, 12);
            confirmButton.Location = new Point(cancelButton.Left - confirmButton.Width - 10, 12);
            cancelButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            confirmButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            pathStatusLabel.AutoSize = false;
            pathStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            pathStatusLabel.ForeColor = Color.FromArgb(74, 84, 102);
            pathStatusLabel.SetBounds(18, 12, 420, 34);
            pathStatusLabel.Anchor = AnchorStyles.Left | AnchorStyles.Top;

            totalStatusLabel.AutoSize = false;
            totalStatusLabel.TextAlign = ContentAlignment.MiddleCenter;
            totalStatusLabel.ForeColor = Color.FromArgb(74, 84, 102);
            totalStatusLabel.SetBounds(440, 12, 180, 34);
            totalStatusLabel.Anchor = AnchorStyles.Top;
        }

        private static void ConfigureCard(ModernPanel panel, int x, int y, int width, int height) {
            panel.Controls.Clear();
            panel.SetBounds(x, y, width, height);
            panel.BackColor = PanelColor;
            panel.BorderColor = BorderColor;
        }

        private static bool IsChinese => Thread.CurrentThread.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

        private void ConfigureLayoutRadio(RadioButton radio, string text, int x, int y, int width, int height, int layout) {
            radio.Text = text;
            radio.SetBounds(x, y, width, height);
            radio.Tag = layout;
            radio.CheckedChanged -= layoutRadio_CheckedChanged;
            radio.CheckedChanged += layoutRadio_CheckedChanged;
        }

        private void layoutRadio_CheckedChanged(object sender, EventArgs e) {
            var radio = sender as RadioButton;
            if (radio == null || !radio.Checked) { return; }
            selectedLayout = (int)radio.Tag;
            galleryView.SetLayout(selectedLayout);
        }

        private void previewFlowRadio_CheckedChanged(object sender, EventArgs e) {
            if (!verticalPreviewRadio.Checked && !horizontalPreviewRadio.Checked) { return; }
            galleryView.SetPreviewFlow(horizontalPreviewRadio.Checked);
        }

        private void pageViewButton_Click(object sender, EventArgs e) {
            galleryView.SetPdfPreview(false);
            SetPreviewButtonStyles(false);
        }

        private void pdfPreviewButton_Click(object sender, EventArgs e) {
            galleryView.SetPdfPreview(true);
            SetPreviewButtonStyles(true);
        }

        private void SetPreviewButtonStyles(bool pdfPreview) {
            StyleButton(pageViewButton, !pdfPreview);
            StyleButton(pdfPreviewButton, pdfPreview);
        }

        private void resetAllButton_Click(object sender, EventArgs e) {
            string message = IsChinese ? "恢复自然排序并清除全部旋转、裁边和封面设置？" : "Restore natural order and clear all rotation, crop, and cover settings?";
            if (MessageBox.Show(this, message, Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) { return; }
            foreach (GalleryItem item in items) {
                item.Rotation = 0;
                item.Crop = new RectangleF(0, 0, 1, 1);
                item.IsCover = false;
                InvalidateThumbnail(item);
            }
            items.Sort((left, right) => left.OriginalIndex.CompareTo(right.OriginalIndex));
            RefreshGalleryItems();
        }

        private void ConfigurePageMenu() {
            pageMenu.Items.Clear();
            pageMenu.Items.Add(IsChinese ? "左旋 90°" : "Rotate Left 90°", null, (s, e) => RotateContextItem(false));
            pageMenu.Items.Add(IsChinese ? "右旋 90°" : "Rotate Right 90°", null, (s, e) => RotateContextItem(true));
            pageMenu.Items.Add(IsChinese ? "裁边…" : "Crop…", null, (s, e) => CropContextItem());
            pageMenu.Items.Add(IsChinese ? "设为封面" : "Set as Cover", null, (s, e) => SetContextItemAsCover());
            pageMenu.Items.Add(new ToolStripSeparator());
            pageMenu.Items.Add(IsChinese ? "恢复此页" : "Restore This Page", null, (s, e) => RestoreContextItem());
        }

        private GalleryItem contextItem;

        private void GalleryView_ContextRequested(object sender, GalleryItemEventArgs e) {
            contextItem = e.Item;
            pageMenu.Items[5].Enabled = IsEdited(contextItem) || items.IndexOf(contextItem) != contextItem.OriginalIndex;
            pageMenu.Show(galleryView, e.Location);
        }

        private void GalleryView_ReorderRequested(object sender, GalleryReorderEventArgs e) {
            int sourceIndex = items.IndexOf(e.Source);
            int targetIndex = items.IndexOf(e.Target);
            if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex) { return; }
            items.RemoveAt(sourceIndex);
            if (sourceIndex < targetIndex) { targetIndex--; }
            items.Insert(targetIndex, e.Source);
            RefreshGalleryItems();
        }

        private void RotateContextItem(bool clockwise) {
            if (contextItem == null) { return; }
            RectangleF crop = contextItem.Crop;
            contextItem.Crop = clockwise
                ? new RectangleF(1f - crop.Bottom, crop.Left, crop.Height, crop.Width)
                : new RectangleF(crop.Top, 1f - crop.Right, crop.Height, crop.Width);
            contextItem.Rotation = (contextItem.Rotation + (clockwise ? 90 : 270)) % 360;
            InvalidateThumbnail(contextItem);
            RefreshGalleryItems();
        }

        private void CropContextItem() {
            if (contextItem == null) { return; }
            using (var cropForm = new CropForm(contextItem.Path, contextItem.Rotation, contextItem.Crop)) {
                if (cropForm.ShowDialog(this) != DialogResult.OK) { return; }
                contextItem.Crop = cropForm.Crop;
                InvalidateThumbnail(contextItem);
                RefreshGalleryItems();
            }
        }

        private void SetContextItemAsCover() {
            if (contextItem == null) { return; }
            foreach (GalleryItem item in items) { item.IsCover = false; }
            contextItem.IsCover = true;
            items.Remove(contextItem);
            items.Insert(0, contextItem);
            RefreshGalleryItems();
        }

        private void RestoreContextItem() {
            if (contextItem == null) { return; }
            contextItem.Rotation = 0;
            contextItem.Crop = new RectangleF(0, 0, 1, 1);
            contextItem.IsCover = false;
            items.Remove(contextItem);
            int insertIndex = items.FindIndex(item => item.OriginalIndex > contextItem.OriginalIndex);
            items.Insert(insertIndex < 0 ? items.Count : insertIndex, contextItem);
            InvalidateThumbnail(contextItem);
            RefreshGalleryItems();
        }

        private void RefreshGalleryItems() {
            var selectedExtensions = GetSelectedExtensions();
            galleryView.SetItems(items);
            galleryView.SetFilter(selectedExtensions, false);
            galleryView.SetLayout(selectedLayout);
            UpdateSelectionStatus();
        }

        private HashSet<string> GetSelectedExtensions() {
            return new HashSet<string>(extensionCheckedList.CheckedItems.Cast<ExtensionOption>().Select(item => item.Extension), StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsEdited(GalleryItem item) {
            return item.Rotation != 0 || item.IsCover || IsCropped(item.Crop);
        }

        private static bool IsCropped(RectangleF crop) {
            return crop.Left > 0.0001f || crop.Top > 0.0001f || crop.Right < 0.9999f || crop.Bottom < 0.9999f;
        }

        private static void InvalidateThumbnail(GalleryItem item) {
            item.ThumbnailVersion++;
            item.Thumbnail?.Dispose();
            item.Thumbnail = null;
            item.Size = Size.Empty;
            item.IsLoading = false;
        }

        private void LayoutHeader() {
            galleryHeaderPanel.Controls.Clear();
            headerSelectionLabel.Font = new Font(Font.FontFamily, 11f, FontStyle.Bold);
            headerSelectionLabel.ForeColor = Color.FromArgb(35, 42, 52);
            headerSelectionLabel.SetBounds(24, 18, 260, 28);
            sortButton.Text = "↕";
            sortButton.SetBounds(0, 14, 40, 34);
            menuButton.Text = "☰";
            menuButton.SetBounds(0, 14, 40, 34);
            galleryHeaderPanel.Controls.Add(headerSelectionLabel);
            galleryHeaderPanel.Controls.Add(sortButton);
            galleryHeaderPanel.Controls.Add(menuButton);
        }

        protected override void OnResize(EventArgs e) {
            base.OnResize(e);
            LayoutGallery();
        }

        private void LayoutGallery() {
            if (galleryView == null || bottomPanel == null || filterPanel == null) { return; }
            int left = filterPanel.Width;
            int width = Math.Max(0, ClientSize.Width - left);
            int height = Math.Max(0, ClientSize.Height - bottomPanel.Height);
            galleryHeaderPanel.SetBounds(left, 0, width, 64);
            sortButton.Left = Math.Max(24, width - 104);
            menuButton.Left = Math.Max(24, width - 54);
            galleryView.SetBounds(left, 64, width, Math.Max(0, height - 64));
        }

        private static void StyleButton(Button button, bool primary = false) {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = primary ? AccentColor : BorderColor;
            button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(66, 120, 255) : Color.FromArgb(246, 248, 252);
            button.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(30, 82, 220) : Color.FromArgb(238, 242, 248);
            button.BackColor = primary ? AccentColor : Color.White;
            button.ForeColor = primary ? Color.White : Color.FromArgb(35, 42, 52);
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
        }

        private void UpdateZoomControls() {
            isUpdatingZoom = true;
            zoomTrackBar.Value = galleryView.ZoomPercent;
            zoomValueLabel.Text = $"{galleryView.ZoomPercent}%";
            isUpdatingZoom = false;
        }

        private void zoomTrackBar_ValueChanged(object sender, EventArgs e) {
            if (isUpdatingZoom) { return; }
            galleryView.SetZoomPercent(zoomTrackBar.Value);
        }

        private void extensionCheckedList_DrawItem(object sender, DrawItemEventArgs e) {
            if (e.Index < 0) { return; }
            var option = (ExtensionOption)extensionCheckedList.Items[e.Index];
            bool isChecked = extensionCheckedList.GetItemChecked(e.Index);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var background = new SolidBrush(PanelColor)) {
                e.Graphics.FillRectangle(background, e.Bounds);
            }

            var box = new Rectangle(e.Bounds.Left + 2, e.Bounds.Top + 5, 16, 16);
            using (var brush = new SolidBrush(isChecked ? AccentColor : Color.White))
            using (var pen = new Pen(isChecked ? AccentColor : BorderColor)) {
                e.Graphics.FillRectangle(brush, box);
                e.Graphics.DrawRectangle(pen, box);
            }
            if (isChecked) {
                TextRenderer.DrawText(e.Graphics, "✓", Font, box, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            TextRenderer.DrawText(e.Graphics, option.Extension, Font, new Rectangle(e.Bounds.Left + 26, e.Bounds.Top + 4, 110, 20), Color.FromArgb(35, 42, 52), TextFormatFlags.Left);
            TextRenderer.DrawText(e.Graphics, option.Count.ToString(), Font, new Rectangle(e.Bounds.Right - 54, e.Bounds.Top + 4, 44, 20), Color.FromArgb(74, 84, 102), TextFormatFlags.Right);
        }

        private void InitializeExtensionList() {
            var counts = items.GroupBy(i => i.Extension, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
            extensionCheckedList.Items.Clear();
            extensionCheckedList.Items.AddRange(ImageFileExtensions.All
                .Where(counts.ContainsKey)
                .OrderBy(ext => ext)
                .Select(ext => new ExtensionOption(ext, counts[ext]))
                .Cast<object>()
                .ToArray());
            for (int i = 0; i < extensionCheckedList.Items.Count; i++) {
                extensionCheckedList.SetItemChecked(i, true);
            }
        }

        private void LoadImageList() {
            items.Clear();
            List<string> naturalPaths = Directory.EnumerateFiles(directoryPath)
                .Where(ImageFileExtensions.IsSupported)
                .OrderBy(file => file, NaturalPathComparer.Instance)
                .ToList();
            var naturalIndexes = naturalPaths.Select((path, index) => new { path, index })
                .ToDictionary(value => value.path, value => value.index, StringComparer.OrdinalIgnoreCase);
            var existing = initialPages.Where(page => naturalIndexes.ContainsKey(page.Path))
                .ToDictionary(page => page.Path, page => page, StringComparer.OrdinalIgnoreCase);
            IEnumerable<string> orderedPaths = initialPages.Count == 0
                ? naturalPaths
                : initialPages.Select(page => page.Path).Where(naturalIndexes.ContainsKey)
                    .Concat(naturalPaths.Where(path => !existing.ContainsKey(path)));
            foreach (string file in orderedPaths.Distinct(StringComparer.OrdinalIgnoreCase)) {
                PageEdit page = existing.TryGetValue(file, out PageEdit value) ? value : null;
                items.Add(new GalleryItem {
                    Path = file,
                    Extension = Path.GetExtension(file)?.ToLower() ?? string.Empty,
                    OriginalIndex = naturalIndexes[file],
                    Rotation = page?.Rotation ?? 0,
                    Crop = page?.Crop ?? new RectangleF(0, 0, 1, 1),
                    IsCover = page?.IsCover ?? false,
                    IsSelected = page?.IsSelected ?? true
                });
            }
            pathStatusLabel.Text = directoryPath;
            totalStatusLabel.Text = $"{items.Count} 个文件";
            galleryView.SetItems(items);
        }

        private static PageEdit ToPageEdit(GalleryItem item) {
            return new PageEdit {
                Path = item.Path,
                OriginalIndex = item.OriginalIndex,
                Rotation = item.Rotation,
                Crop = item.Crop,
                IsCover = item.IsCover,
                IsSelected = item.IsSelected
            };
        }

        private void ApplyFilter() {
            var selectedExts = GetSelectedExtensions();
            galleryView.SetFilter(selectedExts);
            UpdateSelectionStatus();
        }

        private void SetLoadingState(bool isLoading) {
            loadingLabel.Visible = isLoading;
            selectAllButton.Enabled = !isLoading;
            selectNoneButton.Enabled = !isLoading;
            extensionsSelectAllButton.Enabled = !isLoading;
            extensionsClearButton.Enabled = !isLoading;
            confirmButton.Enabled = !isLoading;
            resetZoomButton.Enabled = !isLoading;
        }

        private void selectAllButton_Click(object sender, EventArgs e) {
            galleryView.SetSelectionForVisible(true);
        }

        private void selectNoneButton_Click(object sender, EventArgs e) {
            galleryView.SetSelectionForVisible(false);
        }

        private void extensionsSelectAllButton_Click(object sender, EventArgs e) {
            isBulkExtensionChanging = true;
            extensionCheckedList.BeginUpdate();
            try {
                for (int i = 0; i < extensionCheckedList.Items.Count; i++) {
                    extensionCheckedList.SetItemChecked(i, true);
                }
            }
            finally {
                extensionCheckedList.EndUpdate();
                isBulkExtensionChanging = false;
            }
            ApplyFilter();
        }

        private void extensionsClearButton_Click(object sender, EventArgs e) {
            isBulkExtensionChanging = true;
            extensionCheckedList.BeginUpdate();
            try {
                for (int i = 0; i < extensionCheckedList.Items.Count; i++) {
                    extensionCheckedList.SetItemChecked(i, false);
                }
            }
            finally {
                extensionCheckedList.EndUpdate();
                isBulkExtensionChanging = false;
            }
            ApplyFilter();
        }

        private void extensionCheckedList_ItemCheck(object sender, ItemCheckEventArgs e) {
            if (isBulkExtensionChanging) { return; }
            BeginInvoke(new Action(ApplyFilter));
        }

        private void resetZoomButton_Click(object sender, EventArgs e) {
            galleryView.ResetZoom();
        }

        private void UpdateSelectionStatus() {
            int totalCount = items.Count;
            int selectedCount = items.Count(i => i.IsSelected);
            totalStatusLabel.Text = $"{galleryView.VisibleCount} / {totalCount} 个文件";
            headerSelectionLabel.Text = $"已选  {selectedCount}  /  总  {totalCount}";
            selectionRing.SelectedCount = selectedCount;
            selectionRing.TotalCount = totalCount;
            selectionStatusLabel.Text = string.Format(selectionSummaryFormat, selectedCount, totalCount);
            selectionStatusLabel.ForeColor = selectedCount == 0 && totalCount > 0 ? WarningColor : Color.FromArgb(35, 42, 52);
            noSelectionLabel.Visible = selectedCount == 0 && totalCount > 0;
            galleryView.RebuildPreview();
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

        private sealed class VirtualGalleryView : ScrollableControl
        {
            private const int BaseCardWidth = 150;
            private const int BaseCardHeight = 230;
            private const int BaseThumbSize = 122;
            private const int Gap = 18;
            private const int PaddingSize = 24;
            private const int PreviewMargin = 12;
            private const int PreviewGap = 12;
            private static readonly SemaphoreSlim ThumbnailGate = new SemaphoreSlim(8);
            private static readonly SemaphoreSlim PreviewGate = new SemaphoreSlim(2);
            private readonly List<GalleryItem> items = new List<GalleryItem>();
            private readonly List<GalleryItem> visibleItems = new List<GalleryItem>();
            private readonly List<PreviewPage> previewPages = new List<PreviewPage>();
            private readonly VScrollBar previewVScroll = new VScrollBar();
            private readonly HScrollBar previewHScroll = new HScrollBar();
            private readonly Font nameFont = new Font("Segoe UI", 9f);
            private float scale = 1f;
            private bool pdfPreview;
            private bool previewHorizontal;
            private int layout;
            private int currentPreviewPage;
            private GalleryItem dragItem;
            private Point dragStart;
            private bool suppressClick;
            private long thumbnailAccess;

            public event EventHandler SelectionChanged;
            public event EventHandler<string> OpenRequested;
            public event EventHandler ZoomChanged;
            public event EventHandler<GalleryItemEventArgs> ContextRequested;
            public event EventHandler<GalleryReorderEventArgs> ReorderRequested;

            public int VisibleCount => visibleItems.Count;
            public int ZoomPercent => (int)Math.Round(scale * 100);

            public VirtualGalleryView() {
                AutoScroll = true;
                AllowDrop = true;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
                previewVScroll.Visible = false;
                previewHScroll.Visible = false;
                previewVScroll.ValueChanged += PreviewScroll_ValueChanged;
                previewHScroll.ValueChanged += PreviewScroll_ValueChanged;
                Controls.Add(previewVScroll);
                Controls.Add(previewHScroll);
            }

            protected override void Dispose(bool disposing) {
                if (disposing) {
                    nameFont.Dispose();
                }
                base.Dispose(disposing);
            }

            public void SetItems(IEnumerable<GalleryItem> source) {
                items.Clear();
                items.AddRange(source);
                visibleItems.Clear();
                visibleItems.AddRange(items);
                RebuildPreview();
                UpdateScrollSize();
                Invalidate();
            }

            public void SetFilter(HashSet<string> extensions, bool resetScroll = true) {
                visibleItems.Clear();
                visibleItems.AddRange(items.Where(i => extensions.Count > 0 && extensions.Contains(i.Extension)));
                if (resetScroll) { AutoScrollPosition = Point.Empty; }
                RebuildPreview();
                UpdateScrollSize();
                Invalidate();
            }

            public void SetPdfPreview(bool value) {
                pdfPreview = value;
                AutoScroll = !value;
                RebuildPreview();
                UpdateScrollSize();
                ResetPreviewScroll();
                Invalidate();
            }

            public void SetPreviewFlow(bool horizontal) {
                previewHorizontal = horizontal;
                UpdateScrollSize();
                ResetPreviewScroll();
                Invalidate();
            }

            public void SetLayout(int value) {
                layout = Math.Max(0, Math.Min(2, value));
                RebuildPreview();
                UpdateScrollSize();
                if (pdfPreview && previewHorizontal) { ResetPreviewScroll(); }
                Invalidate();
            }

            private void ResetPreviewScroll() {
                if (!pdfPreview) { AutoScrollPosition = Point.Empty; return; }
                currentPreviewPage = 0;
                if (previewVScroll.Value != 0) { previewVScroll.Value = 0; }
                if (previewHScroll.Value != 0) { previewHScroll.Value = 0; }
                PrefetchPreviewPages();
            }

            public void RebuildPreview() {
                foreach (PreviewPage page in previewPages) { page.Preview?.Dispose(); }
                previewPages.Clear();
                GalleryItem pending = null;
                foreach (GalleryItem item in items.Where(value => value.IsSelected)) {
                    bool landscape = !item.Size.IsEmpty && item.Size.Width > item.Size.Height;
                    if (layout == 0 || item.IsCover || landscape) {
                        if (pending != null) { previewPages.Add(new PreviewPage(pending)); pending = null; }
                        previewPages.Add(new PreviewPage(item));
                    }
                    else if (pending == null) {
                        pending = item;
                    }
                    else {
                        previewPages.Add(layout == 1 ? new PreviewPage(pending, item) : new PreviewPage(item, pending));
                        pending = null;
                    }
                }
                if (pending != null) { previewPages.Add(new PreviewPage(pending)); }
                currentPreviewPage = Math.Max(0, Math.Min(currentPreviewPage, previewPages.Count - 1));
                UpdatePreviewScrollbars();
                if (pdfPreview) { Invalidate(); }
            }

            public void SetSelectionForVisible(bool isSelected) {
                foreach (var item in visibleItems) {
                    item.IsSelected = isSelected;
                }
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }

            public void ResetZoom() {
                SetZoomPercent(100);
            }

            public void AdjustZoom(int delta) {
                SetZoomPercent(ZoomPercent + delta);
            }

            public void SetZoomPercent(int value) {
                scale = Math.Max(0.65f, Math.Min(1.8f, value / 100f));
                UpdateScrollSize();
                Invalidate();
                ZoomChanged?.Invoke(this, EventArgs.Empty);
            }

            protected override void OnResize(EventArgs e) {
                base.OnResize(e);
                UpdateScrollSize();
            }

            protected override void OnMouseWheel(MouseEventArgs e) {
                if ((ModifierKeys & Keys.Control) == Keys.Control) {
                    AdjustZoom(e.Delta > 0 ? 10 : -10);
                    return;
                }
                if (pdfPreview) {
                    if (previewHorizontal) {
                        int step = e.Delta < 0 ? 1 : -1;
                        SetCurrentPreviewPage(currentPreviewPage + step);
                    }
                    else {
                        int lines = SystemInformation.MouseWheelScrollLines;
                        int delta = lines < 0
                            ? (e.Delta < 0 ? GetPreviewViewport().Height : -GetPreviewViewport().Height)
                            : -(e.Delta * Math.Max(1, lines) / 3);
                        SetVerticalPreviewOffset(previewVScroll.Value + delta);
                    }
                    return;
                }
                base.OnMouseWheel(e);
            }

            protected override void OnMouseClick(MouseEventArgs e) {
                if (suppressClick) { suppressClick = false; return; }
                if (e.Button != MouseButtons.Left) { return; }
                if (pdfPreview) {
                    if (!previewHorizontal || previewPages.Count == 0) { return; }
                    Rectangle pageRect = GetCurrentPreviewPageRect();
                    if (pageRect.Contains(e.Location)) { return; }
                    bool clickedLeft = e.X < GetPreviewViewport().Width / 2;
                    int step = clickedLeft ? -1 : 1;
                    if (layout == 2) { step = -step; }
                    SetCurrentPreviewPage(currentPreviewPage + step);
                    return;
                }
                var hit = HitTest(e.Location);
                if (hit == null) { return; }
                hit.IsSelected = !hit.IsSelected;
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }

            protected override void OnMouseDoubleClick(MouseEventArgs e) {
                if (pdfPreview || e.Button != MouseButtons.Left) { return; }
                var hit = HitTest(e.Location);
                if (hit != null) {
                    OpenRequested?.Invoke(this, hit.Path);
                }
            }

            protected override void OnMouseDown(MouseEventArgs e) {
                base.OnMouseDown(e);
                if (pdfPreview || e.Button != MouseButtons.Left) { return; }
                dragItem = HitTest(e.Location);
                dragStart = e.Location;
            }

            protected override void OnMouseMove(MouseEventArgs e) {
                base.OnMouseMove(e);
                if (pdfPreview || e.Button != MouseButtons.Left || dragItem == null) { return; }
                Size dragSize = SystemInformation.DragSize;
                if (Math.Abs(e.X - dragStart.X) < dragSize.Width / 2 && Math.Abs(e.Y - dragStart.Y) < dragSize.Height / 2) { return; }
                GalleryItem source = dragItem;
                dragItem = null;
                suppressClick = true;
                DoDragDrop(source, DragDropEffects.Move);
            }

            protected override void OnMouseUp(MouseEventArgs e) {
                base.OnMouseUp(e);
                dragItem = null;
                if (pdfPreview || e.Button != MouseButtons.Right) { return; }
                GalleryItem hit = HitTest(e.Location);
                if (hit != null) { ContextRequested?.Invoke(this, new GalleryItemEventArgs(hit, e.Location)); }
            }

            protected override void OnDragOver(DragEventArgs drgevent) {
                base.OnDragOver(drgevent);
                drgevent.Effect = !pdfPreview && drgevent.Data.GetDataPresent(typeof(GalleryItem))
                    ? DragDropEffects.Move
                    : DragDropEffects.None;
            }

            protected override void OnDragDrop(DragEventArgs drgevent) {
                base.OnDragDrop(drgevent);
                if (pdfPreview) { return; }
                var source = drgevent.Data.GetData(typeof(GalleryItem)) as GalleryItem;
                Point location = PointToClient(new Point(drgevent.X, drgevent.Y));
                GalleryItem target = HitTest(location);
                if (source != null && target != null && source != target) {
                    ReorderRequested?.Invoke(this, new GalleryReorderEventArgs(source, target));
                }
            }

            protected override void OnPaint(PaintEventArgs e) {
                base.OnPaint(e);
                if (pdfPreview) {
                    DrawPdfReader(e.Graphics);
                    return;
                }
                e.Graphics.Clear(BackColor);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                var visibleBounds = new Rectangle(-AutoScrollPosition.X, -AutoScrollPosition.Y, ClientSize.Width, ClientSize.Height);
                var range = GetVisibleRange(visibleBounds);
                for (int i = range.Start; i <= range.End; i++) {
                    var rect = GetItemRect(i);
                    rect.Offset(AutoScrollPosition);
                    DrawItem(e.Graphics, visibleItems[i], rect);
                    EnsureThumbnail(visibleItems[i]);
                }
                TrimThumbnailCache();
            }

            private GalleryItem HitTest(Point point) {
                if (pdfPreview) { return null; }
                var logical = new Point(point.X - AutoScrollPosition.X, point.Y - AutoScrollPosition.Y);
                for (int i = 0; i < visibleItems.Count; i++) {
                    if (GetItemRect(i).Contains(logical)) {
                        return visibleItems[i];
                    }
                }
                return null;
            }

            private void DrawItem(Graphics graphics, GalleryItem item, Rectangle rect) {
                using (var back = new SolidBrush(item.IsSelected ? Color.FromArgb(230, 238, 255) : Color.White))
                using (var border = new Pen(item.IsSelected ? AccentColor : BorderColor)) {
                    graphics.FillRectangle(back, rect);
                    graphics.DrawRectangle(border, rect);
                }
                if (IsEdited(item)) {
                    using (var editedBorder = new Pen(Color.DarkOrange, 3)) { graphics.DrawRectangle(editedBorder, rect); }
                }

                string sizeText = item.Size.IsEmpty ? "加载中..." : $"{item.Size.Width}x{item.Size.Height}";
                var sizeRect = new Rectangle(rect.Left + 8, rect.Bottom - 34, rect.Width - 16, 18);
                var nameRect = new Rectangle(rect.Left + 8, sizeRect.Top - 22, rect.Width - 16, 20);
                int thumbTop = IsEdited(item) ? rect.Top + 50 : rect.Top + 28;
                int thumbSize = Math.Min((int)(BaseThumbSize * scale), nameRect.Top - thumbTop - 4);
                var checkRect = new Rectangle(rect.Left + 10, rect.Top + 10, 18, 18);
                var thumbRect = new Rectangle(rect.Left + (rect.Width - thumbSize) / 2, thumbTop, thumbSize, thumbSize);
                if (item.Thumbnail != null) {
                    item.ThumbnailAccess = ++thumbnailAccess;
                    graphics.DrawImage(item.Thumbnail, FitRect(item.Thumbnail.Size, thumbRect));
                }
                else {
                    using (var brush = new SolidBrush(Color.FromArgb(242, 245, 250))) {
                        graphics.FillRectangle(brush, thumbRect);
                    }
                }

                TextRenderer.DrawText(graphics, Path.GetFileName(item.Path), nameFont, nameRect, Color.FromArgb(35, 42, 52), TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(graphics, sizeText, nameFont, sizeRect, Color.FromArgb(74, 84, 102), TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                using (var brush = new SolidBrush(item.IsSelected ? AccentColor : Color.White))
                using (var pen = new Pen(item.IsSelected ? AccentColor : BorderColor)) {
                    graphics.FillRectangle(brush, checkRect);
                    graphics.DrawRectangle(pen, checkRect);
                }
                if (item.IsSelected) {
                    TextRenderer.DrawText(graphics, "✓", nameFont, checkRect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                DrawBadges(graphics, item, rect);
            }

            private void DrawPreviewPage(Graphics graphics, PreviewPage page, Rectangle rect, int pageNumber) {
                using (var back = new SolidBrush(Color.White))
                using (var border = new Pen(BorderColor)) {
                    graphics.FillRectangle(back, rect);
                    graphics.DrawRectangle(border, rect);
                }
                var content = new Rectangle(rect.Left + 12, rect.Top + 30, rect.Width - 24, rect.Height - 62);
                int slotWidth = content.Width / page.Items.Count;
                for (int index = 0; index < page.Items.Count; index++) {
                    GalleryItem item = page.Items[index];
                    var slot = new Rectangle(content.Left + index * slotWidth, content.Top, slotWidth, content.Height);
                    if (item.Thumbnail != null) {
                        item.ThumbnailAccess = ++thumbnailAccess;
                        graphics.DrawImage(item.Thumbnail, FitRect(item.Thumbnail.Size, slot));
                    }
                    DrawBadges(graphics, item, new Rectangle(slot.Left, rect.Top + 4, slot.Width, 24));
                }
                TextRenderer.DrawText(graphics, (IsChinese ? "第 " : "Page ") + pageNumber, nameFont,
                    new Rectangle(rect.Left + 8, rect.Bottom - 26, rect.Width - 16, 20),
                    Color.FromArgb(74, 84, 102), TextFormatFlags.HorizontalCenter);
            }

            private void DrawPdfReader(Graphics graphics) {
                graphics.Clear(Color.FromArgb(28, 29, 31));
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
                if (previewPages.Count == 0) { return; }
                if (!previewHorizontal) {
                    DrawVerticalPdfReader(graphics);
                    TrimPagePreviewCache();
                    return;
                }
                currentPreviewPage = Math.Max(0, Math.Min(currentPreviewPage, previewPages.Count - 1));
                Rectangle slot = GetPreviewPageSlot();
                DrawFinishedPage(graphics, previewPages[currentPreviewPage], slot, currentPreviewPage + 1);
                PrefetchPreviewPages();
                TrimPagePreviewCache();
            }

            private void DrawVerticalPdfReader(Graphics graphics) {
                Rectangle viewport = GetPreviewViewport();
                int pageHeight = Math.Max(1, viewport.Height - PreviewMargin * 2);
                int y = PreviewMargin - previewVScroll.Value;
                int firstVisible = -1;
                for (int index = 0; index < previewPages.Count; index++) {
                    PreviewPage page = previewPages[index];
                    int pageWidth = GetVerticalPageWidth(page, pageHeight);
                    var slot = new Rectangle((viewport.Width - pageWidth) / 2, y, pageWidth, pageHeight);
                    if (slot.Bottom >= viewport.Top && slot.Top <= viewport.Bottom) {
                        if (firstVisible < 0) { firstVisible = index; }
                        DrawFinishedPage(graphics, page, slot, index + 1, true);
                    }
                    if (slot.Top > viewport.Bottom) { break; }
                    y += pageHeight + PreviewGap;
                }
                if (firstVisible >= 0) {
                    currentPreviewPage = firstVisible;
                    PrefetchPreviewPages();
                }
            }

            private void DrawFinishedPage(Graphics graphics, PreviewPage page, Rectangle slot, int pageNumber, bool compact = false) {
                Rectangle pageRect = slot;
                if (page.Preview == null) {
                    using (var placeholder = new SolidBrush(Color.FromArgb(52, 54, 57))) {
                        graphics.FillRectangle(placeholder, slot);
                    }
                    TextRenderer.DrawText(graphics, IsChinese ? "正在生成预览…" : "Rendering preview…", nameFont,
                        slot, Color.Gainsboro, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                else {
                    page.PreviewAccess = ++thumbnailAccess;
                    pageRect = FitRect(page.Preview.Size, slot);
                    graphics.FillRectangle(Brushes.White, pageRect);
                    graphics.DrawImage(page.Preview, pageRect);
                    using (var border = new Pen(Color.FromArgb(92, 94, 98))) { graphics.DrawRectangle(border, pageRect); }
                }
                string edits = string.Join("  ", page.Items.SelectMany(GetEditLabels));
                if (edits.Length > 0) {
                    Rectangle editRect = compact
                        ? new Rectangle(pageRect.Left + 8, pageRect.Top + 8, Math.Max(1, pageRect.Width - 16), 24)
                        : new Rectangle(slot.Left, slot.Top - 24, slot.Width, 20);
                    if (compact) {
                        using (var back = new SolidBrush(Color.FromArgb(190, 48, 32, 0))) { graphics.FillRectangle(back, editRect); }
                    }
                    TextRenderer.DrawText(graphics, edits, nameFont,
                        editRect, Color.Orange,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
                }
                Rectangle numberRect = compact
                    ? new Rectangle(pageRect.Left, pageRect.Bottom - 26, pageRect.Width, 22)
                    : new Rectangle(slot.Left, slot.Bottom + 12, slot.Width, 22);
                TextRenderer.DrawText(graphics, (IsChinese ? "第 " : "Page ") + pageNumber, nameFont,
                    numberRect, compact ? Color.DimGray : Color.Gainsboro, TextFormatFlags.HorizontalCenter);
                EnsurePagePreview(page);
            }

            private static IEnumerable<string> GetEditLabels(GalleryItem item) {
                if (item.IsCover) { yield return IsChinese ? "封面" : "Cover"; }
                if (item.Rotation != 0) { yield return (IsChinese ? "旋转 " : "Rotate ") + item.Rotation + "°"; }
                if (IsCropped(item.Crop)) { yield return IsChinese ? "已裁边" : "Cropped"; }
            }

            private void DrawBadges(Graphics graphics, GalleryItem item, Rectangle rect) {
                var labels = new List<string>();
                if (item.IsCover) { labels.Add(IsChinese ? "封面" : "Cover"); }
                if (item.Rotation != 0) { labels.Add("↻" + item.Rotation + "°"); }
                if (IsCropped(item.Crop)) { labels.Add(IsChinese ? "裁边" : "Crop"); }
                if (labels.Count == 0) { return; }
                string text = string.Join(" · ", labels);
                var badgeRect = new Rectangle(rect.Left + 34, rect.Top + 7, Math.Max(0, rect.Width - 42), 32);
                using (var back = new SolidBrush(Color.FromArgb(255, 239, 208)))
                using (var border = new Pen(Color.DarkOrange)) {
                    graphics.FillRectangle(back, badgeRect);
                    graphics.DrawRectangle(border, badgeRect);
                }
                TextRenderer.DrawText(graphics, text, nameFont, badgeRect, Color.FromArgb(160, 76, 0),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            }

            private static Rectangle FitRect(Size imageSize, Rectangle bounds) {
                if (imageSize.Width <= 0 || imageSize.Height <= 0) { return bounds; }
                double scale = Math.Min((double)bounds.Width / imageSize.Width, (double)bounds.Height / imageSize.Height);
                int width = Math.Max(1, (int)Math.Round(imageSize.Width * scale));
                int height = Math.Max(1, (int)Math.Round(imageSize.Height * scale));
                return new Rectangle(bounds.Left + (bounds.Width - width) / 2, bounds.Top + (bounds.Height - height) / 2, width, height);
            }

            private Rectangle GetItemRect(int visibleIndex) {
                int cardWidth = (int)(BaseCardWidth * scale);
                int cardHeight = (int)(BaseCardHeight * scale);
                int columns = GetColumnCount(cardWidth);
                int row = visibleIndex / columns;
                int col = visibleIndex % columns;
                return new Rectangle(PaddingSize + col * (cardWidth + Gap), PaddingSize + row * (cardHeight + Gap), cardWidth, cardHeight);
            }

            private VisibleRange GetVisibleRange(Rectangle visibleBounds) {
                int itemCount = pdfPreview ? previewPages.Count : visibleItems.Count;
                if (itemCount == 0) { return new VisibleRange(0, -1); }
                int cardWidth = (int)(BaseCardWidth * scale);
                int cardHeight = (int)(BaseCardHeight * scale);
                int columns = GetColumnCount(cardWidth);
                int rowHeight = cardHeight + Gap;
                int firstRow = Math.Max(0, (visibleBounds.Top - PaddingSize) / rowHeight - 1);
                int lastRow = Math.Max(firstRow, (visibleBounds.Bottom - PaddingSize) / rowHeight + 1);
                int start = Math.Min(itemCount - 1, firstRow * columns);
                int end = Math.Min(itemCount - 1, ((lastRow + 1) * columns) - 1);
                return new VisibleRange(start, end);
            }

            private int GetColumnCount(int cardWidth) {
                return Math.Max(1, (ClientSize.Width - PaddingSize * 2 + Gap) / (cardWidth + Gap));
            }

            private void UpdateScrollSize() {
                if (pdfPreview) {
                    AutoScrollMinSize = Size.Empty;
                    UpdatePreviewScrollbars();
                    return;
                }
                previewVScroll.Visible = false;
                previewHScroll.Visible = false;
                int cardWidth = (int)(BaseCardWidth * scale);
                int cardHeight = (int)(BaseCardHeight * scale);
                int columns = GetColumnCount(cardWidth);
                int rows = (int)Math.Ceiling(visibleItems.Count / (double)columns);
                AutoScrollMinSize = new Size(0, PaddingSize * 2 + rows * (cardHeight + Gap));
            }

            private void UpdatePreviewScrollbars() {
                if (!pdfPreview) {
                    previewVScroll.Visible = false;
                    previewHScroll.Visible = false;
                    return;
                }
                int verticalWidth = SystemInformation.VerticalScrollBarWidth;
                int horizontalHeight = SystemInformation.HorizontalScrollBarHeight;
                previewVScroll.SetBounds(Math.Max(0, ClientSize.Width - verticalWidth), 0,
                    verticalWidth, ClientSize.Height);
                previewHScroll.SetBounds(0, Math.Max(0, ClientSize.Height - horizontalHeight),
                    ClientSize.Width, horizontalHeight);
                previewVScroll.Visible = !previewHorizontal;
                previewHScroll.Visible = previewHorizontal;
                previewHScroll.RightToLeft = layout == 2 ? RightToLeft.Yes : RightToLeft.No;
                previewVScroll.Minimum = previewHScroll.Minimum = 0;
                Rectangle viewport = GetPreviewViewport();
                int verticalContentHeight = GetVerticalContentHeight(viewport.Height);
                previewVScroll.LargeChange = Math.Max(1, viewport.Height);
                previewVScroll.SmallChange = 48;
                previewVScroll.Maximum = Math.Max(0, verticalContentHeight - 1);
                SetVerticalPreviewOffset(previewVScroll.Value);
                int pageMaximum = Math.Max(0, previewPages.Count - 1);
                previewHScroll.LargeChange = 1;
                previewHScroll.SmallChange = 1;
                previewHScroll.Maximum = pageMaximum;
                currentPreviewPage = Math.Max(0, Math.Min(currentPreviewPage, pageMaximum));
                previewHScroll.Value = currentPreviewPage;
                previewVScroll.BringToFront();
                previewHScroll.BringToFront();
            }

            private void PreviewScroll_ValueChanged(object sender, EventArgs e) {
                if (!pdfPreview) { return; }
                if (previewHorizontal && ReferenceEquals(sender, previewHScroll)) {
                    SetCurrentPreviewPage(previewHScroll.Value);
                    return;
                }
                if (!previewHorizontal && ReferenceEquals(sender, previewVScroll)) {
                    Invalidate();
                }
            }

            private void SetCurrentPreviewPage(int pageIndex) {
                int maximum = Math.Max(0, previewPages.Count - 1);
                int target = Math.Max(0, Math.Min(pageIndex, maximum));
                if (currentPreviewPage == target && previewHScroll.Value == target) { return; }
                currentPreviewPage = target;
                if (previewHScroll.Value != target) { previewHScroll.Value = target; }
                PrefetchPreviewPages();
                Invalidate();
            }

            private void SetVerticalPreviewOffset(int offset) {
                int maximum = Math.Max(previewVScroll.Minimum,
                    previewVScroll.Maximum - previewVScroll.LargeChange + 1);
                int target = Math.Max(previewVScroll.Minimum, Math.Min(offset, maximum));
                if (previewVScroll.Value != target) { previewVScroll.Value = target; }
            }

            private Rectangle GetPreviewViewport() {
                int width = ClientSize.Width - (previewVScroll.Visible ? previewVScroll.Width : 0);
                int height = ClientSize.Height - (previewHScroll.Visible ? previewHScroll.Height : 0);
                return new Rectangle(0, 0, Math.Max(1, width), Math.Max(1, height));
            }

            private Rectangle GetPreviewPageSlot() {
                Rectangle viewport = GetPreviewViewport();
                return new Rectangle(viewport.Left + 50, viewport.Top + 42,
                    Math.Max(280, viewport.Width - 100), Math.Max(320, viewport.Height - 100));
            }

            private Rectangle GetCurrentPreviewPageRect() {
                Rectangle slot = GetPreviewPageSlot();
                if (previewPages.Count == 0) { return slot; }
                Image preview = previewPages[currentPreviewPage].Preview;
                return preview == null ? slot : FitRect(preview.Size, slot);
            }

            private int GetVerticalContentHeight(int viewportHeight) {
                int pageHeight = Math.Max(1, viewportHeight - PreviewMargin * 2);
                return PreviewMargin + previewPages.Count * (pageHeight + PreviewGap);
            }

            private static int GetVerticalPageWidth(PreviewPage page, int pageHeight) {
                Size size = GetPreviewPageSize(page);
                return Math.Max(1, (int)Math.Round(pageHeight * size.Width / (double)Math.Max(1, size.Height)));
            }

            private static Size GetPreviewPageSize(PreviewPage page) {
                if (page.Preview != null) { return page.Preview.Size; }
                int width = 0;
                int height = 0;
                foreach (GalleryItem item in page.Items) {
                    Size size = item.Size.IsEmpty ? new Size(1000, 1400) : item.Size;
                    width += size.Width;
                    height = Math.Max(height, size.Height);
                }
                if (page.Items.Count > 1) { width += 10; }
                return new Size(Math.Max(1, width), Math.Max(1, height));
            }

            private void PrefetchPreviewPages() {
                if (!pdfPreview || previewPages.Count == 0) { return; }
                EnsurePagePreview(previewPages[currentPreviewPage]);
                if (currentPreviewPage > 0) { EnsurePagePreview(previewPages[currentPreviewPage - 1]); }
                if (currentPreviewPage + 1 < previewPages.Count) { EnsurePagePreview(previewPages[currentPreviewPage + 1]); }
            }

            private void EnsureThumbnail(GalleryItem item) {
                if (item.Thumbnail != null || item.IsLoading) { return; }
                item.IsLoading = true;
                int version = item.ThumbnailVersion;
                Task.Run(() => {
                    ThumbnailGate.Wait();
                    try {
                        return LoadThumbnail(item);
                    }
                    finally {
                        ThumbnailGate.Release();
                    }
                }).ContinueWith(task => {
                    if (IsDisposed || !IsHandleCreated) {
                        if (task.Status == TaskStatus.RanToCompletion) { task.Result?.Thumbnail?.Dispose(); }
                        return;
                    }
                    try {
                        BeginInvoke(new Action(() => {
                            if (IsDisposed) { return; }
                            var thumbnail = task.Status == TaskStatus.RanToCompletion ? task.Result : null;
                            item.IsLoading = false;
                            if (thumbnail != null && item.ThumbnailVersion == version) {
                                item.Thumbnail = thumbnail.Thumbnail;
                                item.Size = thumbnail.Size;
                            }
                            else {
                                thumbnail?.Thumbnail?.Dispose();
                            }
                            RebuildPreview();
                            Invalidate();
                        }));
                    }
                    catch (InvalidOperationException) {
                        item.IsLoading = false;
                    }
                });
            }

            private static ThumbnailData LoadThumbnail(GalleryItem item) {
                try {
                    using (var source = LoadBitmapSafe(item.Path)) {
                        if (source == null) { return null; }
                        using (var image = ApplyPreviewEdits(source, item.Rotation, item.Crop)) {
                        return new ThumbnailData {
                            Size = image.Size,
                            Thumbnail = CreateThumbnail(image, BaseThumbSize)
                        };
                        }
                    }
                }
                catch (Exception) {
                    return null;
                }
            }

            private static Bitmap LoadBitmapSafe(string path) {
                if (string.Equals(Path.GetExtension(path), ".webp", StringComparison.OrdinalIgnoreCase)) {
                    using (var webp = new WebP()) {
                        return webp.Load(path);
                    }
                }
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var img = Image.FromStream(fs, false, false)) {
                    return new Bitmap(img);
                }
            }

            private static Bitmap ApplyPreviewEdits(Bitmap source, int rotation, RectangleF crop) {
                var rotated = new Bitmap(source);
                if (rotation == 90) { rotated.RotateFlip(RotateFlipType.Rotate90FlipNone); }
                else if (rotation == 180) { rotated.RotateFlip(RotateFlipType.Rotate180FlipNone); }
                else if (rotation == 270) { rotated.RotateFlip(RotateFlipType.Rotate270FlipNone); }
                if (!IsCropped(crop)) { return rotated; }
                int left = Math.Max(0, Math.Min(rotated.Width - 1, (int)Math.Floor(crop.Left * rotated.Width)));
                int top = Math.Max(0, Math.Min(rotated.Height - 1, (int)Math.Floor(crop.Top * rotated.Height)));
                int right = Math.Max(left + 1, Math.Min(rotated.Width, (int)Math.Ceiling(crop.Right * rotated.Width)));
                int bottom = Math.Max(top + 1, Math.Min(rotated.Height, (int)Math.Ceiling(crop.Bottom * rotated.Height)));
                var result = new Bitmap(right - left, bottom - top);
                using (var graphics = Graphics.FromImage(result)) {
                    graphics.DrawImage(rotated, new Rectangle(0, 0, result.Width, result.Height),
                        new Rectangle(left, top, right - left, bottom - top), GraphicsUnit.Pixel);
                }
                rotated.Dispose();
                return result;
            }

            private void EnsurePagePreview(PreviewPage page) {
                if (page.Preview != null || page.IsLoading) { return; }
                page.IsLoading = true;
                Task.Run(() => {
                    PreviewGate.Wait();
                    try { return LoadPagePreview(page); }
                    finally { PreviewGate.Release(); }
                }).ContinueWith(task => {
                    Image preview = task.Status == TaskStatus.RanToCompletion ? task.Result : null;
                    if (IsDisposed || !IsHandleCreated) { preview?.Dispose(); return; }
                    try {
                        BeginInvoke(new Action(() => {
                            page.IsLoading = false;
                            if (!previewPages.Contains(page)) { preview?.Dispose(); return; }
                            page.Preview = preview;
                            UpdatePreviewScrollbars();
                            Invalidate();
                        }));
                    }
                    catch (InvalidOperationException) {
                        preview?.Dispose();
                        page.IsLoading = false;
                    }
                });
            }

            private static Image LoadPagePreview(PreviewPage page) {
                var edited = new List<Bitmap>();
                try {
                    foreach (GalleryItem item in page.Items) {
                        using (Bitmap source = LoadBitmapSafe(item.Path)) {
                            if (source == null) { continue; }
                            edited.Add(ApplyPreviewEdits(source, item.Rotation, item.Crop));
                        }
                    }
                    if (edited.Count == 0) { return null; }
                    if (edited.Count == 1) { return ResizeToMaxEdge(edited[0], 1800); }

                    using (Bitmap left = ResizeToMaxEdge(edited[0], 1400))
                    using (Bitmap right = ResizeToMaxEdge(edited[1], 1400))
                    using (var combined = new Bitmap(left.Width + right.Width + 10, Math.Max(left.Height, right.Height))) {
                        using (Graphics graphics = Graphics.FromImage(combined)) {
                            graphics.Clear(Color.White);
                            graphics.DrawImage(left, 0, 0, left.Width, left.Height);
                            graphics.DrawImage(right, left.Width + 10, 0, right.Width, right.Height);
                        }
                        return ResizeToMaxEdge(combined, 1800);
                    }
                }
                finally {
                    foreach (Bitmap bitmap in edited) { bitmap.Dispose(); }
                }
            }

            private static Bitmap ResizeToMaxEdge(Image source, int maxEdge) {
                double ratio = Math.Min(1, maxEdge / (double)Math.Max(source.Width, source.Height));
                int width = Math.Max(1, (int)Math.Round(source.Width * ratio));
                int height = Math.Max(1, (int)Math.Round(source.Height * ratio));
                var result = new Bitmap(width, height);
                using (Graphics graphics = Graphics.FromImage(result)) {
                    graphics.Clear(Color.White);
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(source, 0, 0, width, height);
                }
                return result;
            }

            private void TrimPagePreviewCache() {
                const int limit = 4;
                foreach (PreviewPage page in previewPages.Where(value => value.Preview != null)
                    .OrderByDescending(value => value.PreviewAccess).Skip(limit)) {
                    page.Preview.Dispose();
                    page.Preview = null;
                }
            }

            private void TrimThumbnailCache() {
                const int limit = 200;
                List<GalleryItem> loaded = items.Where(item => item.Thumbnail != null).OrderByDescending(item => item.ThumbnailAccess).ToList();
                foreach (GalleryItem item in loaded.Skip(limit)) {
                    item.Thumbnail.Dispose();
                    item.Thumbnail = null;
                }
            }

            private static Image CreateThumbnail(Image original, int maxEdge) {
                double scale = original.Width > original.Height
                    ? (double)maxEdge / original.Width
                    : (double)maxEdge / original.Height;
                int width = Math.Max(1, (int)Math.Round(original.Width * scale));
                int height = Math.Max(1, (int)Math.Round(original.Height * scale));
                var thumbnail = new Bitmap(width, height);
                using (var graphics = Graphics.FromImage(thumbnail)) {
                    graphics.Clear(Color.Transparent);
                    graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    graphics.DrawImage(original, 0, 0, width, height);
                }
                return thumbnail;
            }
        }

        private sealed class ModernPanel : Panel
        {
            public Color BorderColor { get; set; } = Color.FromArgb(218, 224, 234);

            public ModernPanel() {
                DoubleBuffered = true;
            }

            protected override void OnPaint(PaintEventArgs e) {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 10))
                using (var back = new SolidBrush(BackColor))
                using (var border = new Pen(BorderColor)) {
                    e.Graphics.FillPath(back, path);
                    e.Graphics.DrawPath(border, path);
                }
            }
        }

        private sealed class SelectionRing : Control
        {
            private int selectedCount;
            private int totalCount;

            public int SelectedCount {
                get { return selectedCount; }
                set { selectedCount = value; Invalidate(); }
            }

            public int TotalCount {
                get { return totalCount; }
                set { totalCount = value; Invalidate(); }
            }

            public SelectionRing() {
                DoubleBuffered = true;
            }

            protected override void OnPaint(PaintEventArgs e) {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(14, 14, Width - 28, Height - 28);
                using (var basePen = new Pen(Color.FromArgb(224, 234, 250), 8))
                using (var accentPen = new Pen(AccentColor, 8)) {
                    e.Graphics.DrawArc(basePen, rect, 0, 360);
                    if (TotalCount > 0) {
                        e.Graphics.DrawArc(accentPen, rect, -90, 360f * SelectedCount / TotalCount);
                    }
                }
                TextRenderer.DrawText(e.Graphics, "已选", Font, new Rectangle(0, 44, Width, 20), Color.FromArgb(74, 84, 102), TextFormatFlags.HorizontalCenter);
                using (var countFont = new Font(Font.FontFamily, 18f, FontStyle.Bold)) {
                    TextRenderer.DrawText(e.Graphics, SelectedCount.ToString(), countFont, new Rectangle(0, 64, Width, 34), Color.Black, TextFormatFlags.HorizontalCenter);
                }
                TextRenderer.DrawText(e.Graphics, $"/ 总 {TotalCount}", Font, new Rectangle(0, 98, Width, 22), Color.FromArgb(74, 84, 102), TextFormatFlags.HorizontalCenter);
            }
        }

        private sealed class ZoomSlider : Control
        {
            private int value = 100;
            public int Minimum { get; set; } = 65;
            public int Maximum { get; set; } = 180;

            public int Value {
                get { return value; }
                set {
                    int next = Math.Max(Minimum, Math.Min(Maximum, value));
                    if (this.value == next) { return; }
                    this.value = next;
                    Invalidate();
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }

            public event EventHandler ValueChanged;

            public ZoomSlider() {
                DoubleBuffered = true;
                Cursor = Cursors.Hand;
            }

            protected override void OnPaint(PaintEventArgs e) {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                int y = Height / 2;
                float percent = (Value - Minimum) / (float)(Maximum - Minimum);
                int knobX = 8 + (int)Math.Round((Width - 16) * percent);
                using (var basePen = new Pen(Color.FromArgb(218, 224, 234), 4))
                using (var accentPen = new Pen(AccentColor, 4))
                using (var knob = new SolidBrush(AccentColor)) {
                    e.Graphics.DrawLine(basePen, 8, y, Width - 8, y);
                    e.Graphics.DrawLine(accentPen, 8, y, knobX, y);
                    e.Graphics.FillEllipse(knob, knobX - 6, y - 6, 12, 12);
                }
            }

            protected override void OnMouseDown(MouseEventArgs e) {
                SetFromX(e.X);
            }

            protected override void OnMouseMove(MouseEventArgs e) {
                if (e.Button == MouseButtons.Left) {
                    SetFromX(e.X);
                }
            }

            private void SetFromX(int x) {
                float percent = Math.Max(0, Math.Min(1, (x - 8) / (float)Math.Max(1, Width - 16)));
                Value = Minimum + (int)Math.Round((Maximum - Minimum) * percent);
            }
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius) {
            int diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private sealed class VisibleRange
        {
            public VisibleRange(int start, int end) {
                Start = start;
                End = end;
            }

            public int Start { get; }
            public int End { get; }
        }

        private sealed class ExtensionOption
        {
            public ExtensionOption(string extension, int count) {
                Extension = extension;
                Count = count;
            }

            public string Extension { get; }
            public int Count { get; set; }

            public override string ToString() {
                return Count > 0 ? $"{Extension}    {Count}" : Extension;
            }
        }

        private sealed class NaturalPathComparer : IComparer<string>
        {
            public static readonly NaturalPathComparer Instance = new NaturalPathComparer();

            [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
            private static extern int StrCmpLogicalW(string x, string y);

            public int Compare(string x, string y) {
                return StrCmpLogicalW(Path.GetFileName(x), Path.GetFileName(y));
            }
        }

        private sealed class GalleryItemEventArgs : EventArgs {
            public GalleryItemEventArgs(GalleryItem item, Point location) {
                Item = item;
                Location = location;
            }
            public GalleryItem Item { get; }
            public Point Location { get; }
        }

        private sealed class GalleryReorderEventArgs : EventArgs {
            public GalleryReorderEventArgs(GalleryItem source, GalleryItem target) {
                Source = source;
                Target = target;
            }
            public GalleryItem Source { get; }
            public GalleryItem Target { get; }
        }

        private sealed class PreviewPage {
            public PreviewPage(params GalleryItem[] items) { Items = items; }
            public IReadOnlyList<GalleryItem> Items { get; }
            public Image Preview { get; set; }
            public bool IsLoading { get; set; }
            public long PreviewAccess { get; set; }
        }

        private sealed class GalleryItem
        {
            public string Path { get; set; }
            public string Extension { get; set; }
            public int OriginalIndex { get; set; }
            public int Rotation { get; set; }
            public RectangleF Crop { get; set; } = new RectangleF(0, 0, 1, 1);
            public bool IsCover { get; set; }
            public Size Size { get; set; }
            public bool IsSelected { get; set; }
            public bool IsLoading { get; set; }
            public Image Thumbnail { get; set; }
            public int ThumbnailVersion { get; set; }
            public long ThumbnailAccess { get; set; }
        }

        private sealed class ThumbnailData
        {
            public Size Size { get; set; }
            public Image Thumbnail { get; set; }
        }
    }
}
