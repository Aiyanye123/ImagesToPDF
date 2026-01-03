using iTextSharp.text;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using WebPWrapper;

namespace ImgsToPDFCore {
    public enum Layout {
        Single,
        DuplexLeftToRight,
        DuplexRightToLeft
    }
    internal class PDFWrapper {
        static iTextSharp.text.Image GetImageInstance(Bitmap bitmap, bool fastFlag) {
            iTextSharp.text.Image resultImage;
            if (fastFlag) {
                resultImage = iTextSharp.text.Image.GetInstance(bitmap, System.Drawing.Imaging.ImageFormat.Jpeg);
            }
            else if (bitmap.RawFormat.Equals(System.Drawing.Imaging.ImageFormat.MemoryBmp)) {
                // webp 直接转为 bmp 写入格式，否则报错，强制按格式写
                resultImage = iTextSharp.text.Image.GetInstance(bitmap, System.Drawing.Imaging.ImageFormat.Bmp);
            }
            else {
                resultImage = iTextSharp.text.Image.GetInstance(bitmap, bitmap.RawFormat);
            }
            return resultImage;
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

        static void AddPage(Document document, Bitmap bitmap, bool fastFlag, float? uniformWidth) {
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
                var image = GetImageInstance(bitmap, fastFlag);
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
            var resultImage = GetImageInstance(bitmap, fastFlag);

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

        static void ImagesToPdf(IEnumerable<string> imagePaths, Layout layout = Layout.Single, bool fastFlag = false) {
            string pathToSave = CommonUtils.ToLongPath(CSGlobal.luaConfig.PathToSave()); // 从lua里读设置的保存路径
            using (var fs = new FileStream(pathToSave, FileMode.Create, FileAccess.Write, FileShare.Read)) {
                var document = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 0, 0, 0, 0);
                iTextSharp.text.pdf.PdfWriter.GetInstance(document, fs).SetFullCompression();
                document.Open();

                float? uniformWidth = null;
                if (CSGlobal.UniformWidthScale) {
                    uniformWidth = GetUniformTargetWidth(imagePaths, layout);
                }

                if (layout != Layout.DuplexLeftToRight && layout != Layout.DuplexRightToLeft) {
                    foreach (var imagePath in imagePaths) {
                        Bitmap imagePic = LoadBitmap(imagePath);
                        if (imagePic == null) { continue; }
                        AddPage(document, imagePic, fastFlag, uniformWidth);
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
                                AddPage(document, combinedBitmap, fastFlag, uniformWidth);
                            }
                            pending = null;
                        }
                        else {
                            AddPage(document, pending, fastFlag, uniformWidth);
                            AddPage(document, current, fastFlag, uniformWidth);
                            pending = null;
                        }
                    }
                    if (pending != null) {
                        AddPage(document, pending, fastFlag, uniformWidth);
                        pending = null;
                    }
                }

                if (document.PageNumber == 0) {
                    document.NewPage();
                    document.Add(iTextSharp.text.Chunk.NEWLINE);
                }
                document.Close();
            }
        }

        public static void ImagesToPDF(string[] imagePaths, Layout layout = Layout.Single, bool fastFlag = false) {
            if (imagePaths == null) { return; }
            var orderedPaths = imagePaths
                .Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p))
                .OrderBy(p => p, new StringLenComparer())
                .ToList();
            if (!orderedPaths.Any()) { return; }
            ImagesToPdf(orderedPaths, layout, fastFlag);
        }

        public static void ImagesToPDF(string directoryPath, Layout layout = Layout.Single, bool fastFlag = false) {
            if (!Directory.Exists(directoryPath)) { return; }   // 如果不是文件夹，直接结束执行
            List<string> imageExtensions = new List<string> { ".png", ".apng", ".jpg", ".jpeg", ".jfif", ".pjpeg", ".pjp", ".bmp", ".tif", ".tiff", ".gif", ".webp" };
            IEnumerable<string> imagepaths = Directory.EnumerateFiles(directoryPath)
                .Where(p => imageExtensions.Any(e => Path.GetExtension(p)?.ToLower() == e))
                .OrderBy(p => p, new StringLenComparer());
            ImagesToPdf(imagepaths, layout, fastFlag);
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
