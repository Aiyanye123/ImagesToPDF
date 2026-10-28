using ImgsToPDF.Lang;
using ImgsToPDF.Shared;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ImgsToPDF
{
    public partial class ImgsToPDF
    {
        private bool isExporting;
        private bool closeAfterExport;
        private EventWaitHandle exportCancellation;
        private int exportSession;

        private async void StartButton_Click(object sender, EventArgs e) {
            if (isExporting) {
                RequestExportStop();
                return;
            }
            if (queueIndex < 0 || queueIndex >= pathQueue.Count) { return; }

            bool isChinese = Thread.CurrentThread.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            string cancelEventName = @"Local\ImgsToPDF_" + Guid.NewGuid().ToString("N");
            var errors = new List<string>();
            bool cancelled = false;
            int session = ++exportSession;
            try {
                exportCancellation = new EventWaitHandle(false, EventResetMode.ManualReset, cancelEventName);
                SetExporting(true);
                var progress = new Progress<ExportProgress>(update => {
                    if (isExporting && exportSession == session && !exportCancellation.WaitOne(0)) {
                        ShowExportProgress(update, isChinese);
                    }
                });
                while (queueIndex >= 0 && queueIndex < pathQueue.Count && !exportCancellation.WaitOne(0)) {
                    var request = new ExportRequest {
                        Path = pathQueue[queueIndex],
                        Layout = generateModeBox.SelectedIndex,
                        Quality = GetSelectedQualityValue(),
                        UniformWidth = UniformWidth.Checked,
                        Recursive = Recursive.Checked,
                        OutputDirectory = ConfiguredOutputDirectory,
                        CancelEventName = cancelEventName,
                        Cancellation = exportCancellation,
                        Pages = organizedPages.Count == 0 ? null : organizedPages
                            .Where(page => page.IsSelected).Select(page => page.Clone()).ToList()
                    };
                    progressBar.Style = ProgressBarStyle.Marquee;
                    MsgLabel.Text = Extra.ApplyResource(typeof(Extra), "strPDFIsGenerating");
                    List<CoreExportResult> results = await Task.Run(() => ExportPath(request, progress));
                    errors.AddRange(results.Where(result => result.ExitCode == 1)
                        .Select(result => result.SourcePath + Environment.NewLine + result.Error));
                    cancelled = results.Any(result => result.ExitCode == 2)
                        || (results.Count == 0 && exportCancellation.WaitOne(0));
                    if (cancelled || results.Any(result => result.ExitCode != 0)) { break; }

                    pathQueue.RemoveAt(queueIndex);
                    if (queueIndex >= pathQueue.Count) { queueIndex = pathQueue.Count - 1; }
                    if (pathQueue.Count > 0) { DisplayCurrentQueueItem(); }
                }
                cancelled = cancelled || exportCancellation.WaitOne(0);
            }
            catch (Exception ex) {
                errors.Add(ex.Message);
            }
            finally {
                SetExporting(false);
                exportCancellation?.Dispose();
                exportCancellation = null;
                progressBar.Style = ProgressBarStyle.Blocks;
                progressBar.Maximum = 100;
                progressBar.Value = errors.Count == 0 && !cancelled ? 100 : 0;
                if (pathQueue.Count == 0) {
                    queueIndex = -1;
                    ClearCurrentView();
                }
                UpdateQueueControls();
            }

            if (errors.Count > 0) {
                MsgLabel.Text = isChinese ? "导出失败，任务已保留" : "Export failed; task retained";
                MessageBox.Show(this, string.Join(Environment.NewLine + Environment.NewLine, errors),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else if (cancelled) {
                MsgLabel.Text = isChinese ? "已停止，未完成任务已保留" : "Stopped; unfinished tasks retained";
            }
            else {
                MsgLabel.Text = Extra.ApplyResource(typeof(Extra), "strPDFGenerationSuccess");
            }
            if (closeAfterExport) { Close(); }
        }

        private void SetExporting(bool exporting) {
            isExporting = exporting;
            bool isChinese = Thread.CurrentThread.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            if (exporting) {
                StartButton.Text = isChinese ? "停止" : "Stop";
            }
            else {
                StartButton.Text = Extra.ApplyResource(typeof(ImgsToPDF), "StartButton.Text");
            }
            previousQueueButton.Enabled = !exporting;
            nextQueueButton.Enabled = !exporting;
            clearCurrentButton.Enabled = !exporting;
            generateModeBox.Enabled = !exporting;
            qualityBox.Enabled = !exporting;
            UniformWidth.Enabled = !exporting;
            Recursive.Enabled = !exporting;
            menuStripMain.Enabled = !exporting;
            SetExportSettingsEnabled(!exporting);
            progressBar.Visible = true;
            UpdateQueueControls();
            AdjustMainActionButtonsLayout();
            RepositionExportSettings();
        }

        private void RequestExportStop() {
            if (!isExporting) { return; }
            exportCancellation.Set();
            StartButton.Enabled = false;
            bool isChinese = Thread.CurrentThread.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            MsgLabel.Text = isChinese ? "正在停止…" : "Stopping...";
        }

        private void ShowExportProgress(ExportProgress update, bool isChinese) {
            progressBar.Style = ProgressBarStyle.Blocks;
            progressBar.Value = 0;
            progressBar.Maximum = Math.Max(1, update.Total);
            progressBar.Value = Math.Min(update.Processed, progressBar.Maximum);
            string source = System.IO.Path.GetFileName(update.SourcePath.TrimEnd('\\', '/'));
            MsgLabel.Text = isChinese
                ? $"{source} · 图片 {update.Processed}/{update.Total} · PDF {update.Written} 页"
                : $"{source} · Images {update.Processed}/{update.Total} · PDF {update.Written} pages";
        }

        private static List<CoreExportResult> ExportPath(ExportRequest request, IProgress<ExportProgress> progress) {
            string manifestPath = null;
            try {
                if (request.Pages != null) {
                    if (request.Pages.Count == 0) { throw new InvalidOperationException("No pages are selected."); }
                    PageEdit missing = request.Pages.FirstOrDefault(page => !File.Exists(page.Path));
                    if (missing != null) { throw new FileNotFoundException("Source image was not found.", missing.Path); }
                    manifestPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ImgsToPDF_pages_{Guid.NewGuid():N}.txt");
                    PageManifest.Write(manifestPath, request.Pages);
                    return new List<CoreExportResult> { RunCoreExport(request, request.Path, manifestPath, progress) };
                }
                if (request.Recursive && Directory.Exists(request.Path)) {
                    var directories = new List<string>();
                    CollectExportDirectories(request.Path, directories, request.Cancellation);
                    return directories.AsParallel().WithDegreeOfParallelism(4)
                        .Select(path => RunCoreExport(request, path, null, progress)).ToList();
                }
                return new List<CoreExportResult> { RunCoreExport(request, request.Path, null, progress) };
            }
            finally {
                if (manifestPath != null && File.Exists(manifestPath)) {
                    FileSystem.DeleteFile(manifestPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }
            }
        }

        private static void CollectExportDirectories(string path, List<string> directories, EventWaitHandle cancellation) {
            if (cancellation.WaitOne(0)) { return; }
            directories.Add(path);
            foreach (string child in Directory.EnumerateDirectories(path)) {
                CollectExportDirectories(child, directories, cancellation);
            }
        }

        private static CoreExportResult RunCoreExport(ExportRequest request, string path,
            string manifestPath, IProgress<ExportProgress> progress) {
            if (request.Cancellation.WaitOne(0)) {
                return new CoreExportResult { SourcePath = path, ExitCode = 2 };
            }
            var args = new List<string> {
                "-d", path,
                "-l", request.Layout.ToString(CultureInfo.InvariantCulture),
                "--quality", request.Quality.ToString(CultureInfo.InvariantCulture),
                "--progress", "--cancel-event", request.CancelEventName
            };
            if (manifestPath != null) {
                args.Add("--page-manifest");
                args.Add(manifestPath);
            }
            if (request.OutputDirectory.Length > 0) {
                args.Add("--output-directory");
                args.Add(request.OutputDirectory);
            }
            if (request.UniformWidth) { args.Add("--uniform-width"); }
            string corePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Core", "ImgsToPDFCore.exe");
            try {
                return RunProcess(corePath, args, path, progress);
            }
            catch (Exception ex) {
                return new CoreExportResult { SourcePath = path, ExitCode = 1, Error = ex.Message };
            }
        }

        private static CoreExportResult RunProcess(string fileName, List<string> args,
            string sourcePath, IProgress<ExportProgress> progress) {
            var result = new CoreExportResult { SourcePath = sourcePath };
            using (var process = new Process()) {
                process.StartInfo.FileName = fileName;
                process.StartInfo.Arguments = string.Join(" ", args.Select(QuoteProcessArgument));
                process.StartInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(fileName);
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                Task<string> errorRead = process.StandardError.ReadToEndAsync();
                string line;
                while ((line = process.StandardOutput.ReadLine()) != null) {
                    if (line.StartsWith("OUTPUT|", StringComparison.Ordinal)) {
                        result.OutputPath = line.Substring(7);
                        continue;
                    }
                    string[] fields = line.Split('|');
                    if (fields.Length == 4 && fields[0] == "PROGRESS"
                        && int.TryParse(fields[1], out int processed)
                        && int.TryParse(fields[2], out int total)
                        && int.TryParse(fields[3], out int written)
                        && processed >= 0 && total >= processed && written >= 0) {
                        progress.Report(new ExportProgress {
                            SourcePath = sourcePath, Processed = processed, Total = total, Written = written
                        });
                    }
                }
                process.WaitForExit();
                result.Error = errorRead.GetAwaiter().GetResult();
                result.ExitCode = process.ExitCode;
                if (result.ExitCode != 0 && result.ExitCode != 2 && result.Error.Length == 0) {
                    result.Error = "Core exited with code " + result.ExitCode.ToString(CultureInfo.InvariantCulture);
                }
                if (result.ExitCode != 2 && result.Error.Length > 0) { result.ExitCode = 1; }
                if (result.ExitCode != 0 && result.ExitCode != 2) { result.ExitCode = 1; }
                return result;
            }
        }

        private static string QuoteProcessArgument(string argument) {
            var quoted = new StringBuilder("\"");
            int backslashes = 0;
            foreach (char character in argument) {
                if (character == '\\') {
                    backslashes++;
                    continue;
                }
                if (character == '"') {
                    quoted.Append('\\', backslashes * 2 + 1);
                }
                else {
                    quoted.Append('\\', backslashes);
                }
                quoted.Append(character);
                backslashes = 0;
            }
            quoted.Append('\\', backslashes * 2);
            return quoted.Append('"').ToString();
        }

        private sealed class ExportRequest {
            public string Path;
            public int Layout;
            public int Quality;
            public bool UniformWidth;
            public bool Recursive;
            public string OutputDirectory;
            public string CancelEventName;
            public EventWaitHandle Cancellation;
            public List<PageEdit> Pages;
        }

        private sealed class ExportProgress {
            public string SourcePath;
            public int Processed;
            public int Total;
            public int Written;
        }

        private sealed class CoreExportResult {
            public string SourcePath;
            public string OutputPath;
            public string Error;
            public int ExitCode;
        }
    }
}
