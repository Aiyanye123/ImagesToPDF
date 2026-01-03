using ImgsToPDF.Lang;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ImgsToPDF
{
    public partial class ImgsToPDF : Form
    {
        private List<string> selectedImages = new List<string>();

        public ImgsToPDF() {
            string language = Properties.Settings.Default.DefaultLanguage != "" ? Properties.Settings.Default.DefaultLanguage : System.Globalization.CultureInfo.CurrentCulture.Name;
            System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(language);

            this.StartPosition = FormStartPosition.CenterScreen; // ���ھ���
            CheckForIllegalCrossThreadCalls = false; // UI����Ҫ�����̼߳����

            InitializeComponent();
        }

        private void ImgsToPDF_Load(object sender, EventArgs e) {
            if (System.Threading.Thread.CurrentThread.CurrentUICulture.Name.StartsWith("zh")) {
                chineseToolStripMenuItem.Checked = true;
                chineseToolStripMenuItem.Enabled = false;
            }
            else {
                englishToolStripMenuItem.Checked = true;
                englishToolStripMenuItem.Enabled = false;
            }
            //FolderImg.SizeMode = PictureBoxSizeMode.Zoom;
            //PicInFolder.SizeMode = PictureBoxSizeMode.Zoom;
            MsgLabel.ForeColor = Color.Blue;
            generateModeBox.Items.AddRange(new string[] {
                Extra.ApplyResource(typeof(Extra), "strSingle"),
                Extra.ApplyResource(typeof(Extra), "strDuplex"),
                Extra.ApplyResource(typeof(Extra), "strDuplexRightToLeft")
            });
            generateModeBox.SelectedIndex = 0;
            toolStripMenuTopMost.Checked = this.TopMost;
            UpdateTopMostMenuStyle();
        }
        readonly string[] compressExtensions = { ".zip", ".rar", ".7z" };
        private void ImgsToPDF_DragEnter(object sender, DragEventArgs e) {
            string filePath = ((System.Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();
            if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                (Directory.Exists(filePath) || compressExtensions.Contains(Path.GetExtension(filePath)?.ToLower()))
                ) {
                e.Effect = DragDropEffects.All;
            }
            else {
                e.Effect = DragDropEffects.None;
            }
        }
        private void ChooseFileAction(string directoryPath) {
            // ��ʱ�ͷ�Bitmap����
            PicInFolder.Image?.Dispose();

            PathLabel.Text = directoryPath;
            selectedImages.Clear();
            GalleryButton.Enabled = false;

            // ���·���Ƿ���Ч
            if (Directory.Exists(directoryPath)) {
                PicInFolder.Image = Properties.Resources.no_photo;
                FolderImg.Image = Properties.Resources.folder;
                List<string> imageExtensions = new List<string> { ".png", ".apng", ".jpg", ".jpeg", ".jfif", ".pjpeg", ".pjp", ".bmp", ".tif", ".tiff", ".gif", ".webp" };
                IEnumerable<string> imagepaths = Directory.EnumerateFiles(directoryPath)
                    .Where(p => imageExtensions.Any(e => Path.GetExtension(p)?.ToLower() == e));
                foreach (var imagepath in imagepaths) {
                    try {
                        using (var srcImage = new Bitmap(imagepath)) {
                            PicInFolder.Image = Bitmap.FromFile(imagepath) as Bitmap;
                            break;
                        }
                    }
                    catch (Exception) {
                        // ����ļ�����һ�źϷ���ͼƬ����ֱ������
                        continue;
                    }
                }
            }
            else if (compressExtensions.Contains(Path.GetExtension(directoryPath)?.ToLower())) {
                PicInFolder.Image = Properties.Resources.compressedFile;
                FolderImg.Image = null;
            }
            else {
                PicInFolder.Image = Properties.Resources.no_photo;
                FolderImg.Image = null;
                MsgLabel.Text = "Invalid directory path";
                return;
            }

            StartButton.Enabled = true;
            GalleryButton.Enabled = Directory.Exists(directoryPath);
            MsgLabel.Text = Extra.ApplyResource(typeof(Extra), "strClickToStart");
        }
        private void ImgsToPDF_DragDrop(object sender, DragEventArgs e) {
            string directoryPath = ((System.Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();       //���·��
            ChooseFileAction(directoryPath);
        }
        private async void StartButton_Click(object sender, EventArgs e) {
            //Thread ButtonClickThread = new Thread(ButtonClickAction);
            //ButtonClickThread.Start();
            MsgLabel.Text = Extra.ApplyResource(typeof(Extra), "strPDFIsGenerating");
            progressBar.Visible = true;
            progressBar.Maximum = 100;
            progressBar.Value = 50;
            StartButton.Enabled = false;
            await Task.Run(() => ButtonClickAction());  // ����ġ�await�������ں�̨�߳�����LoadData����
            // LoadData������ɺ󣬻ص����̸߳���UI
            progressBar.Value = 100;
            StartButton.Enabled = true;
            MsgLabel.Text = Extra.ApplyResource(typeof(Extra), "strPDFGenerationSuccess");
        }
        static List<string> RecursiveFolder(string path, List<string> dirs) {
            dirs.Add(path);
            var TheFolder = new DirectoryInfo(path);
            foreach (var childFolder in TheFolder.GetDirectories()) {
                RecursiveFolder(childFolder.FullName, dirs);
            }
            return dirs;
        }
        private void ButtonClickAction() {
            var fileName = AppDomain.CurrentDomain.BaseDirectory + @"\Core\ImgsToPDFCore.exe";
            string tempFileListPath = null;
            try {
                if (selectedImages.Any()) {
                    var validSelections = selectedImages
                        .Where(File.Exists)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    if (validSelections.Count == 0) {
                        MessageBox.Show(Extra.ApplyResource(typeof(Extra), "strNoValidSelection"));
                        return;
                    }
                    tempFileListPath = Path.Combine(Path.GetTempPath(), $"ImgsToPDF_selected_{Guid.NewGuid():N}.txt");
                    File.WriteAllLines(tempFileListPath, validSelections);
                    List<string> argsList = new List<string> {
                        "--file-list", tempFileListPath,
                        "-l", generateModeBox.SelectedIndex.ToString()
                    };
                    if (FastMode.Checked) { argsList.Add("--fast"); }
                    if (UniformWidth.Checked) { argsList.Add("--uniform-width"); }
                    string[] args = argsList.ToArray();
                    var (_, stderr) = RunProcess(fileName, args);
                    if (stderr.Length > 0) {
                        MessageBox.Show(stderr);
                    }
                    return;
                }

                if (Recursive.Checked && Directory.Exists(PathLabel.Text)) {
                    RecursiveFolder(PathLabel.Text, new List<string> { }).AsParallel().WithDegreeOfParallelism(4).ForAll(dirPath => {
                        List<string> argsList = new List<string> {
                            "-d", dirPath,
                            "-l", generateModeBox.SelectedIndex.ToString()
                        };
                        if (FastMode.Checked) { argsList.Add("--fast"); }
                        if (UniformWidth.Checked) { argsList.Add("--uniform-width"); }
                        string[] args = argsList.ToArray();
                        var (_, stderr) = RunProcess(fileName, args);
                        if (stderr.Length > 0) {
                            MessageBox.Show(stderr);
                        }
                    });
                }
                else {
                    List<string> argsList = new List<string> {
                        "-d", PathLabel.Text,
                        "-l", generateModeBox.SelectedIndex.ToString()
                    };
                    if (FastMode.Checked) { argsList.Add("--fast"); }
                    if (UniformWidth.Checked) { argsList.Add("--uniform-width"); }
                    string[] args = argsList.ToArray();
                    var (_, stderr) = RunProcess(fileName, args);
                    if (stderr.Length > 0) {
                        MessageBox.Show(stderr);
                    }
                }
            }
            finally {
                if (tempFileListPath != null && File.Exists(tempFileListPath)) {
                    try {
                        File.Delete(tempFileListPath);
                    }
                    catch (Exception) {
                        // ignore cleanup errors
                    }
                }
            }
        }
        /// <summary>
        /// ���и�����������صõ��ı�׼�������׼����
        /// </summary>
        /// <param name="command">��Ҫ���е�ָ��</param>
        /// <returns>Ԫ�飺(stdout:��׼���, stderr:��׼����)</returns>
        private static (string stdout, string stderr) RunProcess(string fileName, string[] args) {
            for (int i = 0; i < args.Length; i++) {
                if (args[i].EndsWith(@"\")) {
                    //���������Ϊ��\\�����ᱻת��ɡ�\����Ȼ����ת�����
                    args[i] += @"\";
                }
                args[i] = string.Format("\"{0}\"", args[i]);
            }
            // ��Process
            Process p = new Process();
            p.StartInfo.FileName = fileName;
            p.StartInfo.Arguments = string.Join(" ", args);
            p.StartInfo.UseShellExecute = false;        // Shell��ʹ��
            p.StartInfo.RedirectStandardInput = true;   // �ض�������
            p.StartInfo.RedirectStandardOutput = true;  // �ض������
            p.StartInfo.RedirectStandardError = true;   // �ض����������
            p.StartInfo.CreateNoWindow = true;          // �����ò���ʾʾ����
            p.Start();
            return (p.StandardOutput.ReadToEnd(), p.StandardError.ReadToEnd()); // �������ȡ�������н��
        }

        private void GalleryButton_Click(object sender, EventArgs e) {
            if (!Directory.Exists(PathLabel.Text)) {
                return;
            }
            using (var galleryForm = new GalleryForm(PathLabel.Text)) {
                var result = galleryForm.ShowDialog(this);
                if (result == DialogResult.OK) {
                    selectedImages = galleryForm.SelectedFiles;
                    MsgLabel.Text = string.Format(Extra.ApplyResource(typeof(Extra), "strGallerySelected"), selectedImages.Count);
                }
            }
        }
        private void toolStripMenuExit_Click(object sender, EventArgs e) {
            this.Close();
        }
        private void toolStripMenuConfigFile_Click(object sender, EventArgs e) {
            Process.Start(AppDomain.CurrentDomain.BaseDirectory + "/Core/Config.lua");
        }
        private void toolStripMenuAbout_Click(object sender, EventArgs e) {
            MessageBox.Show(
                "ImagesToPDF v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version + "\nCopyright (c) 2022-2024 Sinryou. At MIT License.",
                "About",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1,
                0,
                "https://github.com/Sinryou/ImagesToPDF"
            );
        }
        private void toolStripMenuOpenFolder_Click(object sender, EventArgs e) {
            FolderBrowserDialog dialog = new FolderBrowserDialog {
                Description = Extra.ApplyResource(typeof(Extra), "strSelectIMGFolder")
            };
            if (dialog.ShowDialog() == DialogResult.Cancel) {
                return;
            }
            string directoryPath = dialog.SelectedPath.Trim();
            ChooseFileAction(directoryPath);
        }
        private void toolStripMenuClearChosen_Click(object sender, EventArgs e) {
            PicInFolder.Image?.Dispose();
            PicInFolder.Image = Properties.Resources.folder;
            FolderImg.Image = null;
            PathLabel.Text = null;
            selectedImages.Clear();
            GalleryButton.Enabled = false;
            StartButton.Enabled = false;
            MsgLabel.Text = Extra.ApplyResource(this.GetType(), "MsgLabel.Text");
        }

        private void englishToolStripMenuItem_Click(object sender, EventArgs e) {
            Properties.Settings.Default.DefaultLanguage = "en-US";
            Properties.Settings.Default.Save();
            MessageBox.Show(
                "Application will restart immediately to take effect your language setting.",
                "Notice",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            this.Close();
            Application.Restart();
        }

        private void chineseToolStripMenuItem_Click(object sender, EventArgs e) {
            Properties.Settings.Default.DefaultLanguage = "zh-CN";
            Properties.Settings.Default.Save();
            MessageBox.Show(
                "����������������Ч����������á�",
                "ע��",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            this.Close();
            Application.Restart();
        }

        private void toolStripMenuTopMost_Click(object sender, EventArgs e) {
            this.TopMost = toolStripMenuTopMost.Checked;
            UpdateTopMostMenuStyle();
        }

        private void UpdateTopMostMenuStyle() {
            // 深色/正常色，用于显示置顶状态
            toolStripMenuTopMost.BackColor = toolStripMenuTopMost.Checked ? Color.DimGray : SystemColors.Control;
            toolStripMenuTopMost.ForeColor = toolStripMenuTopMost.Checked ? Color.White : SystemColors.ControlText;
        }
    }
}
