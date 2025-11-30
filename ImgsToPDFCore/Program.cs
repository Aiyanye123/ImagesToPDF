using CommandLine;
using System;
using XLua;

namespace ImgsToPDFCore {
    internal class Program {
        /// <summary>
        /// ��������������в�������
        /// </summary>
        class Options {
            [Option('d', "dir-path", Required = false, HelpText = "ͼƬ���ڵ��ļ���·����")]
            public string DirectoryPath { get; set; }

            [Option('l', "layout", Required = false, HelpText = "ҳ�沼�֣�0Ϊ��ҳ�����1Ϊ˫ҳ�����ң�2Ϊ˫ҳ������")]
            public Layout Layout { get; set; }

            [Option('f', "fast", Required = false, HelpText = "�Ƿ�������ͼƬ������ȡ�����ٶȡ�")]
            public bool FastFlag { get; set; }

            [Option("file-list", Required = false, HelpText = "ѡ�е�ͼƬ�б��ļ���·��")]
            public string FileList { get; set; }
        }
        static void Main(string[] args) {
            //for (int i = 0; i < args.Length; i++) {
            //    Console.WriteLine(i + " " + args[i]);
            //}
            Parser.Default.ParseArguments<Options>(args).WithParsed(Run);
        }
        /// <summary>
        /// ʹ�ý�����������в������в�����
        /// </summary>
        /// <param name="option">������Ĳ���</param>
        static void Run(Options option) {
            if (string.IsNullOrWhiteSpace(option.DirectoryPath) && string.IsNullOrWhiteSpace(option.FileList)) {
                Console.Error.WriteLine("No directory or file list provided.");
                return;
            }
            CSGlobal.luaEnv.AddBuildin("ffi", XLua.LuaDLL.Lua.LoadFFI);
            CSGlobal.luaEnv.AddBuildin("lfs", XLua.LuaDLL.Lua.LoadLFS);

            CSGlobal.luaEnv.DoString(@"config = require 'config';"); // ��ȡlua�ڵķ���

            CSGlobal.luaConfig = CSGlobal.luaEnv.Global.Get<IConfig>("config");
            try {
                CSGlobal.luaConfig.PreProcess(option.DirectoryPath, option.Layout, option.FastFlag, option.FileList);
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
