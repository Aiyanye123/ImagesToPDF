using System;
using System.Collections.Generic;
using System.IO;

namespace ImgsToPDF.Shared {
    internal static class ImageFileExtensions {
        internal static readonly string[] All = {
            ".png", ".apng", ".jpg", ".jpeg", ".jfif", ".pjpeg", ".pjp", ".bmp", ".tif", ".tiff", ".gif", ".webp"
        };

        private static readonly HashSet<string> Lookup = new HashSet<string>(All, StringComparer.OrdinalIgnoreCase);

        internal static bool IsSupported(string path) {
            return Lookup.Contains(Path.GetExtension(path) ?? string.Empty);
        }
    }
}
