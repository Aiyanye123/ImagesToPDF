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
            bool hasAlpha = HasTransparency(bitmap);
            if (jpegQuality != OriginalQuality) {
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
                    if (data.Stride < 0) {
                        return HasTransparencySlow(bitmap);
                    }
                    int stride = data.Stride;
                    var pixels = new byte[stride * bitmap.Height];
                    Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                    for (int y = 0; y < bitmap.Height; y++) {
                        int row = y * stride;
                        for (int x = 0; x < bitmap.Width; x++) {
                            if (pixels[row + x * 4 + 3] < 255) {
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
            var configuredPageSize = CSGlobal.luaConfig.PageSizeToSave;
            bool useFixedPageSize = configuredPageSize != null
                && configuredPageSize.Width > 0
                && configuredPageSize.Height > 0;

            if (uniformWidth.HasValue && uniformWidth.Value > 0) {
                var targetWidth = useFixedPageSize ? configuredPageSize.Width : uniformWidth.Value;
                var scale = targetWidth / bitmap.Width;
                var targetHeight = bitmap.Height * scale;
                var uniformPageSize = new iTextSharp.text.Rectangle(0, 0, targetWidth, targetHeight);
                document.SetPageSize(uniformPageSize);
                document.SetMargins(0, 0, 0, 0);
                var image = GetImageInstance(bitmap, jpegQuality);
                image.ScaleAbsolute(uniformPageSize.Width, uniformPageSize.Height);
                image.SetAbsolutePosition(0, 0);
                document.NewPage();
                document.PageCount = document.PageNumber + 1;
                document.Add(image);
                bitmap.Dispose();
                return;
            }

            iTextSharp.text.Rectangle pageSize = useFixedPageSize
                ? configuredPageSize
                : new iTextSharp.text.Rectangle(0, 0, bitmap.Width, bitmap.Height);

            document.SetPageSize(pageSize);
            var resultImage = GetImageInstance(bitmap, jpegQuality);

            if (useFixedPageSize) {
                resultImage.ScaleToFit(pageSize.Width, pageSize.Height);
                var wMargins = (pageSize.Width - resultImage.ScaledWidth) / 2;
                var hMargins = (pageSize.Height - resultImage.ScaledHeight) / 2;
                document.SetMargins(wMargins, wMargins, hMargins, hMargins);
            }
            else {
                document.SetMargins(0, 0, 0, 0);
                resultImage.ScaleAbsolute(pageSize.Width, pageSize.Height);
                resultImage.SetAbsolutePosition(0, 0);
            }

            document.NewPage();
            document.PageCount = document.PageNumber + 1;
            document.Add(resultImage);
            bitmap.Dispose();   // 释放位图占用的资源
        }

        static float GetUniformTargetWidth(IEnumerable<string> imagePaths, Layout layout) {
            const int duplexMargin = 10;
            float maxWidth = 0;
            if (layout != Layout.DuplexLeftToRight && layout != Layout.DuplexRightToLeft) {
                foreach (var imagePath in imagePaths) {
                    using (var imagePic = LoadBitmap(imagePath)) {
                        if (imagePic == null) { continue; }
                        if (imagePic.Width > maxWidth) {
                            maxWidth = imagePic.Width;
                        }
                    }
                }
                return maxWidth;
            }

            int pendingWidth = 0;
            int pendingHeight = 0;
            foreach (var imagePath in imagePaths) {
                using (var current = LoadBitmap(imagePath)) {
                    if (current == null) { continue; }
                    if (pendingWidth == 0) {
                        pendingWidth = current.Width;
                        pendingHeight = current.Height;
                        continue;
                    }

                    bool pendingPortrait = pendingHeight >= pendingWidth;
                    bool currentPortrait = current.Height >= current.Width;
                    if (pendingPortrait && currentPortrait) {
                        var combinedWidth = pendingWidth + current.Width + duplexMargin;
                        if (combinedWidth > maxWidth) {
                            maxWidth = combinedWidth;
                        }
                    }
                    else {
                        if (pendingWidth > maxWidth) {
                            maxWidth = pendingWidth;
                        }
                        if (current.Width > maxWidth) {
                            maxWidth = current.Width;
                        }
                    }
                    pendingWidth = 0;
                    pendingHeight = 0;
                }
            }
            if (pendingWidth > maxWidth) {
                maxWidth = pendingWidth;
            }
            return maxWidth;
        }

        static Bitmap CombineBitmap(Bitmap bm1, Bitmap bm2, int margin) {
            var width = bm1.Width + bm2.Width + margin;
            var height = Math.Max(bm1.Height, bm2.Height);
            Bitmap bitMap = new Bitmap(width, height);
            Graphics canavas = Graphics.FromImage(bitMap);
            canavas.FillRectangle(Brushes.White, new System.Drawing.Rectangle(0, 0, width, height));
            canavas.DrawImage(bm1, 0, 0, bm1.Width, bm1.Height);
            canavas.DrawImage(bm2, bm1.Width + margin, 0, bm2.Width, bm2.Height);
            bm1.Dispose();
            bm2.Dispose();
            return bitMap;
        }

        static void ImagesToPdf(IEnumerable<string> imagePaths, Layout layout = Layout.Single, int jpegQuality = DefaultJpegQuality) {
            string pathToSave = CommonUtils.ToLongPath(CSGlobal.luaConfig.PathToSave()); // 从lua里读设置的保存路径
            bool shouldDeleteOutput = false;
            using (var fs = new FileStream(pathToSave, FileMode.Create, FileAccess.Write, FileShare.Read)) {
                var document = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 0, 0, 0, 0);
                iTextSharp.text.pdf.PdfWriter.GetInstance(document, fs).SetFullCompression();
                document.Open();
                bool hasImagePage = false;

                float? uniformWidth = null;
                if (CSGlobal.UniformWidthScale) {
                    uniformWidth = GetUniformTargetWidth(imagePaths, layout);
                }

                if (layout != Layout.DuplexLeftToRight && layout != Layout.DuplexRightToLeft) {
                    foreach (var imagePath in imagePaths) {
                        Bitmap imagePic = LoadBitmap(imagePath);
                        if (imagePic == null) { continue; }
                        AddPage(document, imagePic, jpegQuality, uniformWidth);
                        hasImagePage = true;
                    }
                }
                else {
                    Bitmap pending = null;
                    foreach (var imagePath in imagePaths) {
                        Bitmap current = LoadBitmap(imagePath);
                        if (current == null) { continue; }
                        if (pending == null) {
                            pending = current;
                            continue;
                        }
                        if (pending.Height >= pending.Width && current.Height >= current.Width) {
                            Bitmap picAtLeft = layout == Layout.DuplexLeftToRight ? pending : current;
                            Bitmap picAtRight = layout == Layout.DuplexLeftToRight ? current : pending;
                            using (var combinedBitmap = CombineBitmap(picAtLeft, picAtRight, 10)) {
                                AddPage(document, combinedBitmap, jpegQuality, uniformWidth);
                                hasImagePage = true;
                            }
                            pending = null;
                        }
                        else {
                            AddPage(document, pending, jpegQuality, uniformWidth);
                            AddPage(document, current, jpegQuality, uniformWidth);
                            hasImagePage = true;
                            pending = null;
                        }
                    }
                    if (pending != null) {
                        AddPage(document, pending, jpegQuality, uniformWidth);
                        hasImagePage = true;
                        pending = null;
                    }
                }

                if (!hasImagePage) {
                    document.NewPage();
                    document.Add(iTextSharp.text.Chunk.NEWLINE);
                    shouldDeleteOutput = true;
                }
                document.Close();
            }
            if (shouldDeleteOutput && File.Exists(pathToSave)) {
                try {
                    File.Delete(pathToSave);
                }
                catch (Exception) {
                    // Keep silent: empty PDF cleanup failure should not break main flow.
                }
            }
        }

        public static void ImagesToPDF(string[] imagePaths, Layout layout = Layout.Single, int jpegQuality = DefaultJpegQuality) {
            if (imagePaths == null) { return; }
            var orderedPaths = imagePaths
                .Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p))
                .OrderBy(p => p, new StringLenComparer())
                .ToList();
            if (!orderedPaths.Any()) { return; }
            ImagesToPdf(orderedPaths, layout, jpegQuality);
        }

        public static void ImagesToPDF(string directoryPath, Layout layout = Layout.Single, int jpegQuality = DefaultJpegQuality) {
            if (!Directory.Exists(directoryPath)) { return; }   // 如果不是文件夹，直接结束执行
            var imagepaths = Directory.EnumerateFiles(directoryPath)
                .Where(ImageFileExtensions.IsSupported)
                .OrderBy(p => p, new StringLenComparer())
                .ToList();
            if (!imagepaths.Any()) { return; }
            ImagesToPdf(imagepaths, layout, jpegQuality);
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
