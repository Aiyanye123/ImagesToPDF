using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualBasic.FileIO;

namespace ImgsToPDFCore
{
    internal class CommonUtils
    {
        /// <summary>
        /// Normalize to full path and add long-path prefix to avoid MAX_PATH errors.
        /// </summary>
        internal static string ToLongPath(string path) {
            if (string.IsNullOrWhiteSpace(path)) { return path; }
            var fullPath = Path.GetFullPath(path);
            if (fullPath.StartsWith(@"\\?\")) { return fullPath; }
            if (fullPath.StartsWith(@"\\", StringComparison.Ordinal)) {
                return @"\\?\UNC\" + fullPath.Substring(2);
            }
            return @"\\?\" + fullPath;
        }

        private static bool ExtraArchive(IArchive archive, string outFileDirectory) {
            ExportRuntime.ThrowIfCancellationRequested();
            if (!archive.Entries.Any()) { return false; }
            Directory.CreateDirectory(outFileDirectory);
            bool result = true;
            foreach (var entry in archive.Entries) {
                ExportRuntime.ThrowIfCancellationRequested();
                if (!entry.IsDirectory && entry.Size > 0) {
                    try {
                        entry.WriteToDirectory(outFileDirectory, new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
                    }
                    catch (Exception) {
                        result = false;
                        break;
                    }
                    ExportRuntime.ThrowIfCancellationRequested();
                }
            }
            ExportRuntime.ThrowIfCancellationRequested();
            return result;
        }

        public static void RecycleDirectory(string path) {
            try {
                FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }
            catch (Exception) {
                ExportRuntime.CleanupFailed = true;
                throw;
            }
        }
        /// <summary>
        /// 解压缩(支持rar，zip)
        /// </summary>
        /// <param name="fromFilePath">待解压文件全路径</param>
        /// <param name="outFileDirectory">解压文件后目录</param>
        public static bool Decompress(string fromFilePath, string outFileDirectory) {
            ExportRuntime.ThrowIfCancellationRequested();
            using (var archive = ArchiveFactory.Open(fromFilePath)) {
                if (archive.Entries.Where(p => !p.IsDirectory).First().IsEncrypted) {
                    return false;
                }
                return ExtraArchive(archive, outFileDirectory);
            }
        }
        /// <summary>
        /// 解压缩加密的包(不支持rar，支持zip)
        /// </summary>
        /// <param name="fromFilePath">待解压文件全路径</param>
        /// <param name="outFileDirectory">解压文件后目录</param>
        /// <param name="password">密码</param>
        public static bool Decompress(string fromFilePath, string outFileDirectory, string password) {
            ExportRuntime.ThrowIfCancellationRequested();
            using (var archive = ArchiveFactory.Open(fromFilePath, new ReaderOptions { Password = password })) {
                return ExtraArchive(archive, outFileDirectory);
            }
        }
    }
}
