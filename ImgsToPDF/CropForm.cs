using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using WebPWrapper;

namespace ImgsToPDF {
    internal sealed class CropForm : Form {
        private readonly CropCanvas canvas;

        public RectangleF Crop => canvas.Crop;

        public CropForm(string imagePath, int rotation, RectangleF crop) {
            Text = IsChinese ? "裁边" : "Crop";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(720, 560);
            Size = new Size(980, 760);
            BackColor = Color.FromArgb(36, 40, 48);

            canvas = new CropCanvas(LoadImage(imagePath), rotation, crop) { Dock = DockStyle.Fill };
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 58, BackColor = Color.White };
            var reset = CreateButton(IsChinese ? "重置" : "Reset");
            var cancel = CreateButton(IsChinese ? "取消" : "Cancel");
            var apply = CreateButton(IsChinese ? "应用" : "Apply", true);
            reset.SetBounds(14, 12, 90, 34);
            apply.SetBounds(footer.Width - 214, 12, 90, 34);
            cancel.SetBounds(footer.Width - 114, 12, 90, 34);
            apply.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            cancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            reset.Click += (s, e) => canvas.Crop = new RectangleF(0, 0, 1, 1);
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            apply.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
            footer.Controls.Add(reset);
            footer.Controls.Add(apply);
            footer.Controls.Add(cancel);
            Controls.Add(canvas);
            Controls.Add(footer);
            AcceptButton = apply;
            CancelButton = cancel;
        }

        private static bool IsChinese => System.Threading.Thread.CurrentThread.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

        private static Button CreateButton(string text, bool primary = false) {
            return new Button {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = primary ? Color.FromArgb(41, 98, 255) : Color.White,
                ForeColor = primary ? Color.White : Color.FromArgb(35, 42, 52),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
        }

        private static Bitmap LoadImage(string path) {
            Bitmap image;
            if (string.Equals(Path.GetExtension(path), ".webp", StringComparison.OrdinalIgnoreCase)) {
                using (var webp = new WebP()) { image = webp.Load(path); }
            }
            else {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var source = Image.FromStream(stream, false, false)) { image = new Bitmap(source); }
            }
            return image;
        }

        private sealed class CropCanvas : Control {
            private const float MinimumCrop = 0.02f;
            private readonly Bitmap image;
            private RectangleF crop;
            private DragHandle dragHandle;
            private Point dragStart;
            private RectangleF cropAtDragStart;

            public RectangleF Crop {
                get { return crop; }
                set { crop = Clamp(value); Invalidate(); }
            }

            public CropCanvas(Bitmap image, int rotation, RectangleF crop) {
                this.image = image ?? throw new ArgumentNullException(nameof(image));
                if (rotation == 90) { image.RotateFlip(RotateFlipType.Rotate90FlipNone); }
                else if (rotation == 180) { image.RotateFlip(RotateFlipType.Rotate180FlipNone); }
                else if (rotation == 270) { image.RotateFlip(RotateFlipType.Rotate270FlipNone); }
                this.crop = Clamp(crop);
                DoubleBuffered = true;
            }

            protected override void Dispose(bool disposing) {
                if (disposing) { image.Dispose(); }
                base.Dispose(disposing);
            }

            protected override void OnPaint(PaintEventArgs e) {
                base.OnPaint(e);
                e.Graphics.Clear(BackColor);
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                RectangleF imageRect = GetImageRect();
                e.Graphics.DrawImage(image, imageRect);
                RectangleF cropRect = ToClient(crop, imageRect);
                using (var outside = new Region(imageRect))
                using (var cropPath = new GraphicsPath())
                using (var overlay = new SolidBrush(Color.FromArgb(145, 0, 0, 0)))
                using (var border = new Pen(Color.White, 2)) {
                    cropPath.AddRectangle(cropRect);
                    outside.Exclude(cropPath);
                    e.Graphics.FillRegion(overlay, outside);
                    e.Graphics.DrawRectangle(border, cropRect.X, cropRect.Y, cropRect.Width, cropRect.Height);
                }
                foreach (PointF point in GetHandlePoints(cropRect)) {
                    e.Graphics.FillRectangle(Brushes.White, point.X - 5, point.Y - 5, 10, 10);
                }
            }

            protected override void OnMouseDown(MouseEventArgs e) {
                if (e.Button != MouseButtons.Left) { return; }
                dragHandle = HitTest(e.Location);
                dragStart = e.Location;
                cropAtDragStart = crop;
                Capture = dragHandle != DragHandle.None;
            }

            protected override void OnMouseMove(MouseEventArgs e) {
                if (dragHandle == DragHandle.None || e.Button != MouseButtons.Left) {
                    Cursor = CursorFor(HitTest(e.Location));
                    return;
                }
                RectangleF imageRect = GetImageRect();
                float dx = (e.X - dragStart.X) / imageRect.Width;
                float dy = (e.Y - dragStart.Y) / imageRect.Height;
                float left = cropAtDragStart.Left;
                float top = cropAtDragStart.Top;
                float right = cropAtDragStart.Right;
                float bottom = cropAtDragStart.Bottom;
                if (HasLeft(dragHandle)) { left = Limit(left + dx, 0, right - MinimumCrop); }
                if (HasRight(dragHandle)) { right = Limit(right + dx, left + MinimumCrop, 1); }
                if (HasTop(dragHandle)) { top = Limit(top + dy, 0, bottom - MinimumCrop); }
                if (HasBottom(dragHandle)) { bottom = Limit(bottom + dy, top + MinimumCrop, 1); }
                Crop = RectangleF.FromLTRB(left, top, right, bottom);
            }

            protected override void OnMouseUp(MouseEventArgs e) {
                dragHandle = DragHandle.None;
                Capture = false;
            }

            private RectangleF GetImageRect() {
                const int padding = 24;
                float availableWidth = Math.Max(1, ClientSize.Width - padding * 2);
                float availableHeight = Math.Max(1, ClientSize.Height - padding * 2);
                float scale = Math.Min(availableWidth / image.Width, availableHeight / image.Height);
                float width = image.Width * scale;
                float height = image.Height * scale;
                return new RectangleF((ClientSize.Width - width) / 2, (ClientSize.Height - height) / 2, width, height);
            }

            private static RectangleF ToClient(RectangleF value, RectangleF imageRect) {
                return new RectangleF(
                    imageRect.Left + value.Left * imageRect.Width,
                    imageRect.Top + value.Top * imageRect.Height,
                    value.Width * imageRect.Width,
                    value.Height * imageRect.Height);
            }

            private DragHandle HitTest(Point point) {
                RectangleF rect = ToClient(crop, GetImageRect());
                PointF[] points = GetHandlePoints(rect);
                DragHandle[] handles = { DragHandle.TopLeft, DragHandle.Top, DragHandle.TopRight, DragHandle.Right, DragHandle.BottomRight, DragHandle.Bottom, DragHandle.BottomLeft, DragHandle.Left };
                for (int i = 0; i < points.Length; i++) {
                    if (Math.Abs(point.X - points[i].X) <= 9 && Math.Abs(point.Y - points[i].Y) <= 9) { return handles[i]; }
                }
                return DragHandle.None;
            }

            private static PointF[] GetHandlePoints(RectangleF rect) {
                return new[] {
                    new PointF(rect.Left, rect.Top), new PointF(rect.Left + rect.Width / 2, rect.Top), new PointF(rect.Right, rect.Top),
                    new PointF(rect.Right, rect.Top + rect.Height / 2), new PointF(rect.Right, rect.Bottom), new PointF(rect.Left + rect.Width / 2, rect.Bottom),
                    new PointF(rect.Left, rect.Bottom), new PointF(rect.Left, rect.Top + rect.Height / 2)
                };
            }

            private static RectangleF Clamp(RectangleF value) {
                float left = Limit(value.Left, 0, 1 - MinimumCrop);
                float top = Limit(value.Top, 0, 1 - MinimumCrop);
                float right = Limit(value.Right, left + MinimumCrop, 1);
                float bottom = Limit(value.Bottom, top + MinimumCrop, 1);
                return RectangleF.FromLTRB(left, top, right, bottom);
            }

            private static float Limit(float value, float minimum, float maximum) {
                return Math.Max(minimum, Math.Min(maximum, value));
            }

            private static bool HasLeft(DragHandle value) { return value == DragHandle.Left || value == DragHandle.TopLeft || value == DragHandle.BottomLeft; }
            private static bool HasRight(DragHandle value) { return value == DragHandle.Right || value == DragHandle.TopRight || value == DragHandle.BottomRight; }
            private static bool HasTop(DragHandle value) { return value == DragHandle.Top || value == DragHandle.TopLeft || value == DragHandle.TopRight; }
            private static bool HasBottom(DragHandle value) { return value == DragHandle.Bottom || value == DragHandle.BottomLeft || value == DragHandle.BottomRight; }

            private static Cursor CursorFor(DragHandle value) {
                if (value == DragHandle.Left || value == DragHandle.Right) { return Cursors.SizeWE; }
                if (value == DragHandle.Top || value == DragHandle.Bottom) { return Cursors.SizeNS; }
                if (value == DragHandle.TopLeft || value == DragHandle.BottomRight) { return Cursors.SizeNWSE; }
                if (value == DragHandle.TopRight || value == DragHandle.BottomLeft) { return Cursors.SizeNESW; }
                return Cursors.Default;
            }

            private enum DragHandle { None, Left, TopLeft, Top, TopRight, Right, BottomRight, Bottom, BottomLeft }
        }
    }
}
