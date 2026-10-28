using ImgsToPDF.Shared;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using WebPWrapper;

namespace ImgsToPDFCore {
    public enum Layout {
        Single,
        DuplexLeftToRight,
        DuplexRightToLeft
    }
    internal class PDFWrapper {
        public const int OriginalQuality = -1;
        private const int DefaultJpegQuality = 80;

        static iTextSharp.text.Image GetImageInstance(Bitmap bitmap, int jpegQuality) {
            if (jpegQuality != OriginalQuality) {
                bool hasAlpha = HasTransparency(bitmap);
                return hasAlpha
                    ? iTextSharp.text.Image.GetInstance(ToPngBytes(bitmap))
                    : iTextSharp.text.Image.GetInstance(ToJpegBytes(bitmap, jpegQuality));
            }

            if (bitmap.RawFormat.Equals(ImageFormat.MemoryBmp)) {
                return iTextSharp.text.Image.GetInstance(ToPngBytes(bitmap));
            }

            return iTextSharp.text.Image.GetInstance(bitmap, bitmap.RawFormat);
        }

        static bool HasTransparency(Bitmap bitmap) {
            if (!Image.IsAlphaPixelFormat(bitmap.PixelFormat)) {
                return false;
            }

            if (bitmap.PixelFormat == PixelFormat.Format32bppArgb
                || bitmap.PixelFormat == PixelFormat.Format32bppPArgb) {
                var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
                var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, bitmap.PixelFormat);
                try {
                    var pixels = new byte[bitmap.Width * 4];
                    for (int y = 0; y < bitmap.Height; y++) {
                        Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), pixels, 0, pixels.Length);
                        for (int x = 0; x < bitmap.Width; x++) {
                            if (pixels[x * 4 + 3] < 255) {
                                return true;
                            }
                        }
                    }
                    return false;
                }
                finally {
                    bitmap.UnlockBits(data);
                }
            }

            return HasTransparencySlow(bitmap);
        }

        static bool HasTransparencySlow(Bitmap bitmap) {
            for (int y = 0; y < bitmap.Height; y++) {
                for (int x = 0; x < bitmap.Width; x++) {
                    if (bitmap.GetPixel(x, y).A < 255) {
                        return true;
                    }
                }
            }
            return false;
        }

        static byte[] ToJpegBytes(Bitmap bitmap, int quality) {
            quality = Math.Max(1, Math.Min(100, quality));
            using (var stream = new MemoryStream())
            using (var parameters = new EncoderParameters(1)) {
                parameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
                var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                bitmap.Save(stream, codec, parameters);
                return stream.ToArray();
            }
        }

        static byte[] ToPngBytes(Bitmap bitmap) {
            using (var stream = new MemoryStream()) {
                bitmap.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
        }

        static Bitmap LoadBitmap(string imagePath) {
            var fileExt = Path.GetExtension(imagePath)?.ToLower();
            try {
                if (fileExt == ".webp") {
                    using (WebP webp = new WebP()) {
                        return webp.Load(imagePath);
                    }
                }
                return Bitmap.FromFile(imagePath) as Bitmap;
            }
            catch (Exception) {
                return null;
            }
        }

        static void AddPage(iTextSharp.text.Document document, Bitmap bitmap, int jpegQuality, float? uniformWidth) {
            using (bitmap) {
                AddPage(document, GetImageInstance(bitmap, jpegQuality), bitmap.Width, bitmap.Height, uniformWidth);
            }
        }

        static void AddPage(iTextSharp.text.Document document, iTextSharp.text.Image image, float width, float height, float? uniformWidth) {
            var configuredPageSize = CSGlobal.luaConfig.PageSizeToSave;
            bool useFixedPageSize = configuredPageSize != null
                && configuredPageSize.Width > 0
                && configuredPageSize.Height > 0;
            bool useUniformWidth = uniformWidth.HasValue && uniformWidth.Value > 0;
            iTextSharp.text.Rectangle pageSize;

            if (useUniformWidth) {
                var targetWidth = useFixedPageSize ? configuredPageSize.Width : uniformWidth.Value;
                var scale = targetWidth / width;
                var targetHeight = height * scale;
                pageSize = new iTextSharp.text.Rectangle(0, 0, targetWidth, targetHeight);
            }
            else {
                pageSize = useFixedPageSize
                    ? configuredPageSize
                    : new iTextSharp.text.Rectangle(0, 0, width, height);
            }

            document.SetPageSize(pageSize);

            if (useFixedPageSize && !useUniformWidth) {
                image.ScaleToFit(pageSize.Width, pageSize.Height);
                var wMargins = (pageSize.Width - image.ScaledWidth) / 2;
                var hMargins = (pageSize.Height - image.ScaledHeight) / 2;
                document.SetMargins(wMargins, wMargins, hMargins, hMargins);
            }
            else {
                document.SetMargins(0, 0, 0, 0);
                image.ScaleAbsolute(pageSize.Width, pageSize.Height);
                image.SetAbsolutePosition(0, 0);
            }

            document.NewPage();
            document.PageCount = document.PageNumber + 1;
            document.Add(image);
            ExportRuntime.PageWritten();
        }

        static bool HasFullCrop(RectangleF crop) {
            return crop.Left <= 0 && crop.Top <= 0 && crop.Right >= 1 && crop.Bottom >= 1;
        }

        static Rectangle GetCropRectangle(Size size, RectangleF crop) {
            if (HasFullCrop(crop)) { return new Rectangle(Point.Empty, size); }
            int left = Math.Max(0, Math.Min(size.Width - 1, (int)Math.Floor(crop.Left * size.Width)));
            int top = Math.Max(0, Math.Min(size.Height - 1, (int)Math.Floor(crop.Top * size.Height)));
            int right = Math.Max(left + 1, Math.Min(size.Width, (int)Math.Ceiling(crop.Right * size.Width)));
            int bottom = Math.Max(top + 1, Math.Min(size.Height, (int)Math.Ceiling(crop.Bottom * size.Height)));
            return new Rectangle(left, top, right - left, bottom - top);
        }

        static bool TryGetEditedSize(PageEdit page, out Size size) {
            size = Size.Empty;
            try {
                if (string.Equals(Path.GetExtension(page.Path), ".webp", StringComparison.OrdinalIgnoreCase)) {
                    using (var webp = new WebP()) {
                        webp.GetInfo(File.ReadAllBytes(page.Path), out int width, out int height,
                            out bool hasAlpha, out bool hasAnimation, out string format);
                        size = new Size(width, height);
                    }
                }
                else {
                    using (var stream = File.OpenRead(page.Path))
                    using (var image = Image.FromStream(stream, false, false)) {
                        size = image.Size;
                    }
                }
                if (size.Width <= 0 || size.Height <= 0) { return false; }
                if (page.Rotation == 90 || page.Rotation == 270) {
                    size = new Size(size.Height, size.Width);
                }
                size = GetCropRectangle(size, page.Crop).Size;
                return true;
            }
            catch (Exception) {
                return false;
            }
        }

        static bool TryLoadOriginalJpeg(PageEdit page, int jpegQuality, bool requireLandscape, out iTextSharp.text.Image image) {
            image = null;
            if (jpegQuality != OriginalQuality || page.Rotation != 0 || !HasFullCrop(page.Crop)) { return false; }
            try {
                using (var stream = File.OpenRead(page.Path)) {
                    if (stream.ReadByte() != 0xff || stream.ReadByte() != 0xd8 || stream.ReadByte() != 0xff) { return false; }
                    if (requireLandscape) {
                        stream.Position = 0;
                        using (var metadata = Image.FromStream(stream, false, false)) {
                            if (metadata.Height >= metadata.Width) { return false; }
                        }
                    }
                }
                image = iTextSharp.text.Image.GetInstance(page.Path);
                return true;
            }
            catch (Exception) {
                return false;
            }
        }

        static Bitmap LoadEditedBitmap(PageEdit page) {
            Bitmap bitmap = LoadBitmap(page.Path);
            if (bitmap == null) { return null; }
            try {
                switch (page.Rotation) {
                    case 90:
                        bitmap.RotateFlip(RotateFlipType.Rotate90FlipNone);
                        break;
                    case 180:
                        bitmap.RotateFlip(RotateFlipType.Rotate180FlipNone);
                        break;
                    case 270:
                        bitmap.RotateFlip(RotateFlipType.Rotate270FlipNone);
                        break;
                }

                RectangleF crop = page.Crop;
                if (HasFullCrop(crop)) { return bitmap; }
                Rectangle cropRectangle = GetCropRectangle(bitmap.Size, crop);
                var result = new Bitmap(cropRectangle.Width, cropRectangle.Height, PixelFormat.Format32bppArgb);
                try {
                    using (var graphics = Graphics.FromImage(result)) {
                        graphics.DrawImage(bitmap,
                            new Rectangle(0, 0, result.Width, result.Height),
                            cropRectangle,
                            GraphicsUnit.Pixel);
                    }
                }
                catch (Exception) {
                    result.Dispose();
                    throw;
                }
                bitmap.Dispose();
                return result;
            }
            catch (Exception) {
                bitmap.Dispose();
                throw;
            }
        }

        static float GetUniformTargetWidth(IEnumerable<PageEdit> pages, Layout layout) {
            const int duplexMargin = 10;
            float maxWidth = 0;
            if (layout != Layout.DuplexLeftToRight && layout != Layout.DuplexRightToLeft) {
                foreach (PageEdit page in pages) {
                    ExportRuntime.ThrowIfCancellationRequested();
                    if (!TryGetEditedSize(page, out Size size)) { continue; }
                    if (size.Width > maxWidth) {
                        maxWidth = size.Width;
                    }
                }
                return maxWidth;
            }

            int pendingWidth = 0;
            foreach (PageEdit page in pages) {
                ExportRuntime.ThrowIfCancellationRequested();
                if (!TryGetEditedSize(page, out Size current)) { continue; }
                if (page.IsCover || current.Height < current.Width) {
                    if (pendingWidth > maxWidth) { maxWidth = pendingWidth; }
                    pendingWidth = 0;
                    if (current.Width > maxWidth) { maxWidth = current.Width; }
                    continue;
                }
                if (pendingWidth == 0) {
                    pendingWidth = current.Width;
                    continue;
                }

                var combinedWidth = pendingWidth + current.Width + duplexMargin;
                if (combinedWidth > maxWidth) {
                    maxWidth = combinedWidth;
                }
                pendingWidth = 0;
            }
            if (pendingWidth > maxWidth) {
                maxWidth = pendingWidth;
            }
            return maxWidth;
        }

        static Bitmap CombineBitmap(Bitmap bm1, Bitmap bm2, int margin) {
            using (bm1)
            using (bm2) {
                var width = bm1.Width + bm2.Width + margin;
                var height = Math.Max(bm1.Height, bm2.Height);
                Bitmap bitmap = new Bitmap(width, height);
                try {
                    using (Graphics canvas = Graphics.FromImage(bitmap)) {
                        canvas.FillRectangle(Brushes.White, new Rectangle(0, 0, width, height));
                        canvas.DrawImage(bm1, 0, 0, bm1.Width, bm1.Height);
                        canvas.DrawImage(bm2, bm1.Width + margin, 0, bm2.Width, bm2.Height);
                    }
                    return bitmap;
                }
                catch (Exception) {
                    bitmap.Dispose();
                    throw;
                }
            }
        }

        static void WriteSinglePages(iTextSharp.text.Document document, IReadOnlyList<PageEdit> pages, int jpegQuality, float? uniformWidth) {
            foreach (PageEdit page in pages) {
                ExportRuntime.ThrowIfCancellationRequested();
                try {
                    if (TryLoadOriginalJpeg(page, jpegQuality, false, out iTextSharp.text.Image originalJpeg)) {
                        AddPage(document, originalJpeg, originalJpeg.Width, originalJpeg.Height, uniformWidth);
                    }
                    else {
                        Bitmap bitmap = LoadEditedBitmap(page);
                        if (bitmap == null) { continue; }
                        AddPage(document, bitmap, jpegQuality, uniformWidth);
                    }
                }
                finally {
                    ExportRuntime.InputProcessed();
                }
            }
        }

        static void WriteDuplexPages(iTextSharp.text.Document document, IReadOnlyList<PageEdit> pages, Layout layout, int jpegQuality, float? uniformWidth) {
            Bitmap pending = null;
            try {
                foreach (PageEdit page in pages) {
                    ExportRuntime.ThrowIfCancellationRequested();
                    Bitmap current = null;
                    try {
                        bool useOriginalJpeg = TryLoadOriginalJpeg(page, jpegQuality, !page.IsCover, out iTextSharp.text.Image originalJpeg);
                        current = useOriginalJpeg ? null : LoadEditedBitmap(page);
                        if (!useOriginalJpeg && current == null) { continue; }
                        if (useOriginalJpeg || page.IsCover || current.Height < current.Width) {
                            AddPendingPage(document, ref pending, jpegQuality, uniformWidth);
                            if (useOriginalJpeg) {
                                AddPage(document, originalJpeg, originalJpeg.Width, originalJpeg.Height, uniformWidth);
                            }
                            else {
                                Bitmap standalone = current;
                                current = null;
                                AddPage(document, standalone, jpegQuality, uniformWidth);
                            }
                            continue;
                        }
                        if (pending == null) {
                            pending = current;
                            current = null;
                            continue;
                        }
                        Bitmap picAtLeft = layout == Layout.DuplexLeftToRight ? pending : current;
                        Bitmap picAtRight = layout == Layout.DuplexLeftToRight ? current : pending;
                        pending = null;
                        current = null;
                        AddPage(document, CombineBitmap(picAtLeft, picAtRight, 10), jpegQuality, uniformWidth);
                    }
                    finally {
                        current?.Dispose();
                        ExportRuntime.InputProcessed();
                    }
                }
                ExportRuntime.ThrowIfCancellationRequested();
                if (pending != null) {
                    AddPendingPage(document, ref pending, jpegQuality, uniformWidth);
                }
            }
            finally {
                pending?.Dispose();
            }
        }

        static void AddPendingPage(iTextSharp.text.Document document, ref Bitmap pending, int jpegQuality, float? uniformWidth) {
            if (pending == null) { return; }
            Bitmap bitmap = pending;
            pending = null;
            AddPage(document, bitmap, jpegQuality, uniformWidth);
        }

        static void ImagesToPdf(IReadOnlyList<PageEdit> pages, Layout layout = Layout.Single, int jpegQuality = DefaultJpegQuality) {
            ExportRuntime.ThrowIfCancellationRequested();
            ExportRuntime.BeginProgress(pages.Count);
            string pathToSave = ExportRuntime.GetOutputPath(CSGlobal.luaConfig.PathToSave());
            string directory = Path.GetDirectoryName(pathToSave);
            Directory.CreateDirectory(directory);
            string stagePath = Path.Combine(directory, "." + Path.GetFileNameWithoutExtension(pathToSave)
                + "." + Guid.NewGuid().ToString("N") + ".stage");
            try {
                using (var fs = new FileStream(CommonUtils.ToLongPath(stagePath), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                using (var document = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 0, 0, 0, 0)) {
                    iTextSharp.text.pdf.PdfWriter.GetInstance(document, fs).SetFullCompression();
                    document.Open();
                    try {
                        float? uniformWidth = null;
                        if (CSGlobal.UniformWidthScale) {
                            uniformWidth = GetUniformTargetWidth(pages, layout);
                        }
                        if (layout == Layout.DuplexLeftToRight || layout == Layout.DuplexRightToLeft) {
                            WriteDuplexPages(document, pages, layout, jpegQuality, uniformWidth);
                        }
                        else {
                            WriteSinglePages(document, pages, jpegQuality, uniformWidth);
                        }
                    }
                    finally {
                        try {
                            if (ExportRuntime.WrittenPages == 0) {
                                document.NewPage();
                                document.Add(iTextSharp.text.Chunk.NEWLINE);
                            }
                            document.Close();
                        }
                        catch (Exception) {
                            ExportRuntime.CleanupFailed = true;
                            throw;
                        }
                    }
                }
                ExportRuntime.ThrowIfCancellationRequested();
                if (ExportRuntime.WrittenPages > 0) { ExportRuntime.CommitOutput(stagePath, pathToSave); }
            }
            finally {
                if (File.Exists(CommonUtils.ToLongPath(stagePath))) { ExportRuntime.RecycleFile(stagePath); }
            }
        }

        public static void ImagesToPDF(string[] imagePaths, Layout layout = Layout.Single, int jpegQuality = DefaultJpegQuality) {
            if (imagePaths == null) { return; }
            var orderedPaths = imagePaths
                .Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p))
                .OrderBy(p => p, new StringLenComparer())
                .ToList();
            if (!orderedPaths.Any()) { return; }
            ImagesToPdf(orderedPaths.Select((path, index) => new PageEdit { Path = path, OriginalIndex = index }).ToList(), layout, jpegQuality);
        }

        public static void ImagesToPDF(string directoryPath, Layout layout = Layout.Single, int jpegQuality = DefaultJpegQuality) {
            if (!Directory.Exists(directoryPath)) { return; }   // 如果不是文件夹，直接结束执行
            var imagepaths = Directory.EnumerateFiles(directoryPath)
                .Where(ImageFileExtensions.IsSupported)
                .OrderBy(p => p, new StringLenComparer())
                .ToList();
            if (!imagepaths.Any()) { return; }
            ImagesToPdf(imagepaths.Select((path, index) => new PageEdit { Path = path, OriginalIndex = index }).ToList(), layout, jpegQuality);
        }

        public static void ImagesToPDFManifest(string manifestPath, Layout layout = Layout.Single, int jpegQuality = DefaultJpegQuality) {
            List<PageEdit> pages = PageManifest.Read(manifestPath);
            ImagesToPdf(pages, layout, jpegQuality);
        }

        /// <summary>
        /// 文件名排序：默认使用 lua 配置中的自定义方法
        /// </summary>
        class StringLenComparer : IComparer<string> {
            int IComparer<string>.Compare(string x, string y) {
                return CSGlobal.luaConfig.FilePathComparer(x, y);
            }
        }
    }
}
