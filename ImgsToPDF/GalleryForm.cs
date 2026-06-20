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
        private readonly List<GalleryItem> items = new List<GalleryItem>();
        private readonly VirtualGalleryView galleryView = new VirtualGalleryView();
        private readonly Panel galleryHeaderPanel = new Panel();
        private readonly Label headerSelectionLabel = new Label();
        private readonly Button sortButton = new Button();
        private readonly Button menuButton = new Button();
        private readonly ModernPanel extensionCard = new ModernPanel();
        private readonly ModernPanel selectionCard = new ModernPanel();
        private readonly ModernPanel viewCard = new ModernPanel();
        private readonly SelectionRing selectionRing = new SelectionRing();
        private readonly ZoomSlider zoomTrackBar = new ZoomSlider();
        private readonly Label zoomValueLabel = new Label();
        private readonly Button zoomOutButton = new Button();
        private readonly Button zoomInButton = new Button();
        private readonly Label pathStatusLabel = new Label();
        private readonly Label totalStatusLabel = new Label();
        private string selectionSummaryFormat = "Selected {0} / Total {1}";
        private string confirmPromptFormat = "Generate {0} images?";
        private bool isBulkExtensionChanging;
        private bool isUpdatingZoom;

        private static readonly Color SurfaceColor = Color.FromArgb(246, 248, 252);
        private static readonly Color PanelColor = Color.White;
        private static readonly Color BorderColor = Color.FromArgb(218, 224, 234);
        private static readonly Color AccentColor = Color.FromArgb(41, 98, 255);
        private static readonly Color WarningColor = Color.FromArgb(214, 72, 72);

        public GalleryForm(string directoryPath) {
            this.directoryPath = directoryPath;
            InitializeComponent();
            InitializeModernLayout();
        }

        public List<string> SelectedFiles {
            get { return items.Where(i => i.IsSelected).Select(i => i.Path).ToList(); }
        }

        protected override void OnFormClosed(FormClosedEventArgs e) {
            foreach (var item in items) {
                item.Thumbnail?.Dispose();
            }
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
            StyleButton(cancelButton);
            StyleButton(confirmButton, true);

            ResumeLayout(true);
        }

        private void LayoutSidebar() {
            filterPanel.Controls.Clear();
            ConfigureCard(extensionCard, 16, 16, 260, 198);
            ConfigureCard(selectionCard, 16, 228, 260, 300);
            ConfigureCard(viewCard, 16, 542, 260, 190);

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

            noSelectionLabel.AutoSize = false;
            noSelectionLabel.SetBounds(18, 282, 224, 18);
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
                Text = "视图与缩放",
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
            selectionCard.Controls.Add(noSelectionLabel);
            viewCard.Controls.Add(zoomTitle);
            viewCard.Controls.Add(zoomOutButton);
            viewCard.Controls.Add(zoomTrackBar);
            viewCard.Controls.Add(zoomInButton);
            viewCard.Controls.Add(zoomValueLabel);
            viewCard.Controls.Add(resetZoomButton);
            viewCard.Controls.Add(loadingLabel);
            filterPanel.Controls.Add(extensionCard);
            filterPanel.Controls.Add(selectionCard);
            filterPanel.Controls.Add(viewCard);
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
            foreach (var file in Directory.EnumerateFiles(directoryPath)
                .Where(ImageFileExtensions.IsSupported)
                .OrderBy(file => file, NaturalPathComparer.Instance)) {
                items.Add(new GalleryItem {
                    Path = file,
                    Extension = Path.GetExtension(file)?.ToLower() ?? string.Empty,
                    IsSelected = true
                });
            }
            pathStatusLabel.Text = directoryPath;
            totalStatusLabel.Text = $"{items.Count} 个文件";
            galleryView.SetItems(items);
        }

        private void ApplyFilter() {
            var selectedExts = new HashSet<string>(
                extensionCheckedList.CheckedItems.Cast<ExtensionOption>().Select(i => i.Extension),
                StringComparer.OrdinalIgnoreCase);
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
            private static readonly SemaphoreSlim ThumbnailGate = new SemaphoreSlim(8);
            private readonly List<GalleryItem> items = new List<GalleryItem>();
            private readonly List<GalleryItem> visibleItems = new List<GalleryItem>();
            private readonly Font nameFont = new Font("Segoe UI", 9f);
            private float scale = 1f;

            public event EventHandler SelectionChanged;
            public event EventHandler<string> OpenRequested;
            public event EventHandler ZoomChanged;

            public int VisibleCount => visibleItems.Count;
            public int ZoomPercent => (int)Math.Round(scale * 100);

            public VirtualGalleryView() {
                AutoScroll = true;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
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
                UpdateScrollSize();
                Invalidate();
            }

            public void SetFilter(HashSet<string> extensions) {
                visibleItems.Clear();
                visibleItems.AddRange(items.Where(i => extensions.Count > 0 && extensions.Contains(i.Extension)));
                AutoScrollPosition = Point.Empty;
                UpdateScrollSize();
                Invalidate();
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
                base.OnMouseWheel(e);
            }

            protected override void OnMouseClick(MouseEventArgs e) {
                var hit = HitTest(e.Location);
                if (hit == null) { return; }
                hit.IsSelected = !hit.IsSelected;
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }

            protected override void OnMouseDoubleClick(MouseEventArgs e) {
                var hit = HitTest(e.Location);
                if (hit != null) {
                    OpenRequested?.Invoke(this, hit.Path);
                }
            }

            protected override void OnPaint(PaintEventArgs e) {
                base.OnPaint(e);
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
            }

            private GalleryItem HitTest(Point point) {
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

                string sizeText = item.Size.IsEmpty ? "加载中..." : $"{item.Size.Width}x{item.Size.Height}";
                var sizeRect = new Rectangle(rect.Left + 8, rect.Bottom - 34, rect.Width - 16, 18);
                var nameRect = new Rectangle(rect.Left + 8, sizeRect.Top - 22, rect.Width - 16, 20);
                int thumbSize = Math.Min((int)(BaseThumbSize * scale), nameRect.Top - rect.Top - 40);
                var checkRect = new Rectangle(rect.Left + 10, rect.Top + 10, 18, 18);
                var thumbRect = new Rectangle(rect.Left + (rect.Width - thumbSize) / 2, rect.Top + 28, thumbSize, thumbSize);
                if (item.Thumbnail != null) {
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
                if (visibleItems.Count == 0) { return new VisibleRange(0, -1); }
                int cardWidth = (int)(BaseCardWidth * scale);
                int cardHeight = (int)(BaseCardHeight * scale);
                int columns = GetColumnCount(cardWidth);
                int rowHeight = cardHeight + Gap;
                int firstRow = Math.Max(0, (visibleBounds.Top - PaddingSize) / rowHeight - 1);
                int lastRow = Math.Max(firstRow, (visibleBounds.Bottom - PaddingSize) / rowHeight + 1);
                int start = Math.Min(visibleItems.Count - 1, firstRow * columns);
                int end = Math.Min(visibleItems.Count - 1, ((lastRow + 1) * columns) - 1);
                return new VisibleRange(start, end);
            }

            private int GetColumnCount(int cardWidth) {
                return Math.Max(1, (ClientSize.Width - PaddingSize * 2 + Gap) / (cardWidth + Gap));
            }

            private void UpdateScrollSize() {
                int cardWidth = (int)(BaseCardWidth * scale);
                int cardHeight = (int)(BaseCardHeight * scale);
                int columns = GetColumnCount(cardWidth);
                int rows = (int)Math.Ceiling(visibleItems.Count / (double)columns);
                AutoScrollMinSize = new Size(0, PaddingSize * 2 + rows * (cardHeight + Gap));
            }

            private void EnsureThumbnail(GalleryItem item) {
                if (item.Thumbnail != null || item.IsLoading) { return; }
                item.IsLoading = true;
                Task.Run(() => {
                    ThumbnailGate.Wait();
                    try {
                        return LoadThumbnail(item.Path);
                    }
                    finally {
                        ThumbnailGate.Release();
                    }
                }).ContinueWith(task => {
                    if (IsDisposed || !IsHandleCreated) { return; }
                    try {
                        BeginInvoke(new Action(() => {
                            if (IsDisposed) { return; }
                            var thumbnail = task.Status == TaskStatus.RanToCompletion ? task.Result : null;
                            item.IsLoading = false;
                            if (thumbnail != null) {
                                item.Thumbnail = thumbnail.Thumbnail;
                                item.Size = thumbnail.Size;
                            }
                            Invalidate();
                        }));
                    }
                    catch (InvalidOperationException) {
                        item.IsLoading = false;
                    }
                });
            }

            private static ThumbnailData LoadThumbnail(string path) {
                try {
                    using (var image = LoadBitmapSafe(path)) {
                        if (image == null) { return null; }
                        return new ThumbnailData {
                            Size = image.Size,
                            Thumbnail = CreateThumbnail(image, BaseThumbSize)
                        };
                    }
                }
                catch (Exception) {
                    return null;
                }
            }

            private static Image LoadBitmapSafe(string path) {
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

        private sealed class GalleryItem
        {
            public string Path { get; set; }
            public string Extension { get; set; }
            public Size Size { get; set; }
            public bool IsSelected { get; set; }
            public bool IsLoading { get; set; }
            public Image Thumbnail { get; set; }
        }

        private sealed class ThumbnailData
        {
            public Size Size { get; set; }
            public Image Thumbnail { get; set; }
        }
    }
}
