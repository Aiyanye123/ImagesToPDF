using CommandLine;
using System;
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

            [Option('f', "fast", Required = false, HelpText = "是否压缩图片以加快读取速度")]
            public bool FastFlag { get; set; }

            [Option('q', "quality", Required = false, Default = 85, HelpText = "JPEG质量：75/85/90，0 表示原图无损")]
            public int Quality { get; set; }

            [Option("file-list", Required = false, HelpText = "选中的图片列表文件路径")]
            public string FileList { get; set; }

            [Option('u', "uniform-width", Required = false, HelpText = "统一宽度缩放（等比缩放，不裁剪）")]
            public bool UniformWidthScale { get; set; }
        }
        static void Main(string[] args) {
            Parser.Default.ParseArguments<Options>(args).WithParsed(Run);
        }
        /// <summary>
        /// Runs with parsed command-line options.
        /// </summary>
        /// <param name="option">Parsed options.</param>
        static void Run(Options option) {
            if (string.IsNullOrWhiteSpace(option.DirectoryPath) && string.IsNullOrWhiteSpace(option.FileList)) {
                Console.Error.WriteLine("No directory or file list provided.");
                return;
            }
            CSGlobal.luaEnv.AddBuildin("ffi", XLua.LuaDLL.Lua.LoadFFI);
            CSGlobal.luaEnv.AddBuildin("lfs", XLua.LuaDLL.Lua.LoadLFS);

            CSGlobal.luaEnv.DoString(@"config = require 'config';");

            CSGlobal.luaConfig = CSGlobal.luaEnv.Global.Get<IConfig>("config");
            CSGlobal.UniformWidthScale = option.UniformWidthScale;
            try {
                int quality = option.FastFlag ? 75 : option.Quality;
                if (quality != 0 && quality != 75 && quality != 85 && quality != 90) {
                    quality = 85;
                }
                if (quality == 0) {
                    quality = PDFWrapper.OriginalQuality;
                }
                CSGlobal.luaConfig.PreProcess(option.DirectoryPath, option.Layout, quality, option.FileList);
            }
            catch (Exception ex) {
                Console.Error.WriteLine(ex);
            }
            finally {
                // Ensure cleanup even when generation throws.
                try {
                    CSGlobal.luaConfig?.PostProcess();
                }
                finally {
                    CSGlobal.luaEnv?.Dispose();
                }
            }
        }
    }
}
