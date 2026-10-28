using Microsoft.VisualBasic.FileIO;
using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace ImgsToPDFCore {
    internal static class ExportRuntime {
        static EventWaitHandle cancelEvent;
        static string outputDirectory;
        static bool reportProgress;
        static int processed;
        static int total;
        static int written;

        internal static bool CancellationObserved { get; private set; }
        internal static bool CleanupFailed { get; set; }
        internal static int WrittenPages { get { return written; } }

        internal static void Initialize(string directory, bool progress, string cancelEventName) {
            outputDirectory = string.IsNullOrWhiteSpace(directory) ? null : Path.GetFullPath(directory);
            reportProgress = progress;
            CancellationObserved = false;
            CleanupFailed = false;
            if (!string.IsNullOrWhiteSpace(cancelEventName)) {
                cancelEvent = EventWaitHandle.OpenExisting(cancelEventName);
            }
            ThrowIfCancellationRequested();
        }

        internal static void Dispose() {
            cancelEvent?.Dispose();
            cancelEvent = null;
        }

        internal static void ThrowIfCancellationRequested() {
            if (cancelEvent != null && cancelEvent.WaitOne(0)) {
                CancellationObserved = true;
                throw new OperationCanceledException("Export canceled.");
            }
        }

        internal static string GetOutputPath(string configuredPath) {
            string path = outputDirectory == null
                ? configuredPath
                : Path.Combine(outputDirectory, Path.GetFileName(configuredPath));
            return Path.GetFullPath(path);
        }

        internal static void BeginProgress(int inputCount) {
            processed = 0;
            written = 0;
            total = inputCount;
            WriteProgress();
        }

        internal static void InputProcessed() {
            processed++;
            WriteProgress();
        }

        internal static void PageWritten() {
            written++;
            WriteProgress();
        }

        static void WriteProgress() {
            if (!reportProgress) { return; }
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "PROGRESS|{0}|{1}|{2}", processed, total, written));
            Console.Out.Flush();
        }

        internal static string CommitOutput(string stagePath, string requestedPath) {
            string directory = Path.GetDirectoryName(requestedPath);
            string name = Path.GetFileNameWithoutExtension(requestedPath);
            string extension = Path.GetExtension(requestedPath);
            for (int number = 0; ; number++) {
                ThrowIfCancellationRequested();
                string candidate = number == 0
                    ? requestedPath
                    : Path.Combine(directory, name + " (" + number.ToString(CultureInfo.InvariantCulture) + ")" + extension);
                try {
                    File.Move(CommonUtils.ToLongPath(stagePath), CommonUtils.ToLongPath(candidate));
                }
                catch (IOException) {
                    if (File.Exists(CommonUtils.ToLongPath(candidate))) { continue; }
                    throw;
                }
                if (reportProgress) {
                    Console.WriteLine("OUTPUT|" + candidate);
                    Console.Out.Flush();
                }
                return candidate;
            }
        }

        internal static void RecycleFile(string path) {
            try {
                FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }
            catch (Exception) {
                CleanupFailed = true;
                throw;
            }
        }
    }
}
