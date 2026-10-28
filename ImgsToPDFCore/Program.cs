using CommandLine;
using ImgsToPDF.Shared;
using System;
using System.Linq;
using System.Text;
using XLua;

namespace ImgsToPDFCore {
    internal class Program {
        /// <summary>
        /// Command-line options.
        /// </summary>
        class Options {
            [Option('d', "dir-path", Required = false, HelpText = "图片所在的文件夹路径")]
            public string DirectoryPath { get; set; }

            [Option('l', "layout", Required = false, HelpText = "页面布局：0 单页，1 双页从左到右，2 双页从右到左")]
            public Layout Layout { get; set; }

            [Option('q', "quality", Required = false, Default = 85, HelpText = "JPEG质量：75/85/90，0 表示原图无损")]
            public int Quality { get; set; }

            [Option("file-list", Required = false, HelpText = "选中的图片列表文件路径")]
            public string FileList { get; set; }

            [Option("page-manifest", Required = false, HelpText = "专业整理页面清单路径")]
            public string PageManifest { get; set; }

            [Option('u', "uniform-width", Required = false, HelpText = "统一宽度缩放（等比缩放，不裁剪）")]
            public bool UniformWidthScale { get; set; }

            [Option("output-directory", Required = false, HelpText = "输出PDF的文件夹")]
            public string OutputDirectory { get; set; }

            [Option("progress", Required = false, HelpText = "输出导出进度事件")]
            public bool Progress { get; set; }

            [Option("cancel-event", Required = false, HelpText = "协作停止导出的命名事件")]
            public string CancelEvent { get; set; }
        }
        static int Main(string[] args) {
            if (args.Contains("--self-check-page-manifest")) {
                PageManifest.RunSelfCheck();
                Console.WriteLine("Page manifest self-check passed.");
                return 0;
            }
            int exitCode = 1;
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(option => exitCode = Run(option))
                .WithNotParsed(errors => {
                    if (errors.All(error => error.Tag == ErrorType.HelpRequestedError || error.Tag == ErrorType.VersionRequestedError)) {
                        exitCode = 0;
                    }
                });
            return exitCode;
        }
        /// <summary>
        /// Runs with parsed command-line options.
        /// </summary>
        /// <param name="option">Parsed options.</param>
        static int Run(Options option) {
            if (string.IsNullOrWhiteSpace(option.DirectoryPath) && string.IsNullOrWhiteSpace(option.FileList)) {
                Console.Error.WriteLine("No directory or file list provided.");
                return 1;
            }
            if (!string.IsNullOrWhiteSpace(option.PageManifest) && string.IsNullOrWhiteSpace(option.DirectoryPath)) {
                Console.Error.WriteLine("A source directory is required with a page manifest.");
                return 1;
            }
            Exception failure = null;
            bool luaInitialized = false;
            try {
                if (option.Progress) { Console.OutputEncoding = new UTF8Encoding(false); }
                ExportRuntime.Initialize(option.OutputDirectory, option.Progress, option.CancelEvent);
                var luaEnv = CSGlobal.luaEnv;
                luaInitialized = true;
                luaEnv.AddBuildin("ffi", XLua.LuaDLL.Lua.LoadFFI);
                luaEnv.AddBuildin("lfs", XLua.LuaDLL.Lua.LoadLFS);
                luaEnv.DoString(@"config = require 'config';");
                CSGlobal.luaConfig = luaEnv.Global.Get<IConfig>("config");
                CSGlobal.UniformWidthScale = option.UniformWidthScale;
                int quality = option.Quality;
                if (quality != 0 && quality != 75 && quality != 85 && quality != 90) {
                    quality = 85;
                }
                if (quality == 0) {
                    quality = PDFWrapper.OriginalQuality;
                }
                CSGlobal.luaConfig.PreProcess(option.DirectoryPath, option.Layout, quality, option.FileList, option.PageManifest);
                ExportRuntime.ThrowIfCancellationRequested();
            }
            catch (Exception ex) {
                failure = ex;
            }
            finally {
                try {
                    if (luaInitialized) { CSGlobal.luaConfig?.PostProcess(); }
                }
                catch (Exception ex) {
                    ExportRuntime.CleanupFailed = true;
                    failure = failure == null ? ex : new AggregateException(failure, ex);
                }
                finally {
                    try {
                        if (luaInitialized) { CSGlobal.luaEnv.Dispose(); }
                    }
                    catch (Exception ex) {
                        ExportRuntime.CleanupFailed = true;
                        failure = failure == null ? ex : new AggregateException(failure, ex);
                    }
                    ExportRuntime.Dispose();
                }
            }
            if (failure == null) { return 0; }
            if (ExportRuntime.CancellationObserved && !ExportRuntime.CleanupFailed) { return 2; }
            Console.Error.WriteLine(failure);
            return 1;
        }
    }
}
