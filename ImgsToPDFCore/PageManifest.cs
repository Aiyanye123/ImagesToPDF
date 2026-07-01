using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ImgsToPDF.Shared {
    public sealed class PageEdit {
        public string Path { get; set; }
        public int OriginalIndex { get; set; }
        public int Rotation { get; set; }
        public RectangleF Crop { get; set; } = new RectangleF(0, 0, 1, 1);
        public bool IsSelected { get; set; } = true;
        public bool IsCover { get; set; }

        public PageEdit Clone() {
            return new PageEdit {
                Path = Path,
                OriginalIndex = OriginalIndex,
                Rotation = Rotation,
                Crop = Crop,
                IsSelected = IsSelected,
                IsCover = IsCover
            };
        }
    }

    public static class PageManifest {
        private const string Header = "ImagesToPDF.PageManifest/1";

        public static void Write(string manifestPath, IEnumerable<PageEdit> pages) {
            if (string.IsNullOrWhiteSpace(manifestPath)) { throw new ArgumentException("Manifest path is required.", nameof(manifestPath)); }
            if (pages == null) { throw new ArgumentNullException(nameof(pages)); }

            var lines = new List<string> { Header };
            foreach (PageEdit page in pages) {
                Validate(page, false);
                lines.Add(string.Join("|",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(page.Path)),
                    page.Rotation.ToString(CultureInfo.InvariantCulture),
                    page.Crop.Left.ToString("R", CultureInfo.InvariantCulture),
                    page.Crop.Top.ToString("R", CultureInfo.InvariantCulture),
                    page.Crop.Width.ToString("R", CultureInfo.InvariantCulture),
                    page.Crop.Height.ToString("R", CultureInfo.InvariantCulture),
                    page.IsCover ? "1" : "0"));
            }
            File.WriteAllLines(manifestPath, lines, new UTF8Encoding(false));
        }

        public static List<PageEdit> Read(string manifestPath) {
            if (!File.Exists(manifestPath)) { throw new FileNotFoundException("Page manifest was not found.", manifestPath); }
            string[] lines = File.ReadAllLines(manifestPath, Encoding.UTF8);
            if (lines.Length == 0 || lines[0] != Header) { throw new InvalidDataException("Unsupported page manifest version."); }

            var pages = new List<PageEdit>();
            for (int index = 1; index < lines.Length; index++) {
                if (string.IsNullOrWhiteSpace(lines[index])) { continue; }
                string[] fields = lines[index].Split('|');
                if (fields.Length != 7) { throw new InvalidDataException($"Invalid page manifest line {index + 1}."); }
                try {
                    var page = new PageEdit {
                        Path = Encoding.UTF8.GetString(Convert.FromBase64String(fields[0])),
                        OriginalIndex = pages.Count,
                        Rotation = int.Parse(fields[1], CultureInfo.InvariantCulture),
                        Crop = new RectangleF(
                            float.Parse(fields[2], CultureInfo.InvariantCulture),
                            float.Parse(fields[3], CultureInfo.InvariantCulture),
                            float.Parse(fields[4], CultureInfo.InvariantCulture),
                            float.Parse(fields[5], CultureInfo.InvariantCulture)),
                        IsCover = fields[6] == "1"
                    };
                    if (fields[6] != "0" && fields[6] != "1") { throw new InvalidDataException("Invalid cover flag."); }
                    Validate(page, true);
                    pages.Add(page);
                }
                catch (Exception ex) when (!(ex is InvalidDataException)) {
                    throw new InvalidDataException($"Invalid page manifest line {index + 1}.", ex);
                }
            }
            if (pages.Count == 0) { throw new InvalidDataException("Page manifest contains no pages."); }
            if (pages.Count(page => page.IsCover) > 1) { throw new InvalidDataException("Page manifest contains more than one cover."); }
            return pages;
        }

        public static void RunSelfCheck() {
            string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ImgsToPDF_清单自检_" + Guid.NewGuid().ToString("N"));
            string imagePath = System.IO.Path.Combine(directory, "页面 01.jpg");
            string manifestPath = System.IO.Path.Combine(directory, "pages.txt");
            Directory.CreateDirectory(directory);
            try {
                File.WriteAllBytes(imagePath, new byte[] { 0 });
                var expected = new PageEdit {
                    Path = imagePath,
                    Rotation = 90,
                    Crop = new RectangleF(0.1f, 0.2f, 0.7f, 0.6f),
                    IsCover = true
                };
                Write(manifestPath, new[] { expected });
                PageEdit actual = Read(manifestPath).Single();
                if (actual.Path != expected.Path || actual.Rotation != 90 || actual.Crop != expected.Crop || !actual.IsCover) {
                    throw new InvalidOperationException("Page manifest self-check failed.");
                }
            }
            finally {
                if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
            }
        }

        private static void Validate(PageEdit page, bool requireExistingFile) {
            if (page == null || string.IsNullOrWhiteSpace(page.Path)) { throw new InvalidDataException("A page path is required."); }
            if (requireExistingFile && !File.Exists(page.Path)) { throw new FileNotFoundException("Source image was not found.", page.Path); }
            if (page.Rotation != 0 && page.Rotation != 90 && page.Rotation != 180 && page.Rotation != 270) {
                throw new InvalidDataException("Rotation must be 0, 90, 180, or 270 degrees.");
            }
            RectangleF crop = page.Crop;
            if (!IsFinite(crop.Left) || !IsFinite(crop.Top) || !IsFinite(crop.Width) || !IsFinite(crop.Height)
                || crop.Left < 0 || crop.Top < 0 || crop.Width <= 0 || crop.Height <= 0
                || crop.Right > 1.0001f || crop.Bottom > 1.0001f) {
                throw new InvalidDataException("Crop rectangle must be inside the image.");
            }
        }

        private static bool IsFinite(float value) {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
