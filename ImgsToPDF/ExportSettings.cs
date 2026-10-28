using System;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ImgsToPDF
{
    public partial class ImgsToPDF
    {
        private readonly Label outputDirectoryLabel = new Label();
        private readonly TextBox outputDirectoryBox = new TextBox();
        private readonly Button chooseOutputDirectoryButton = new Button();
        private readonly Button resetOutputDirectoryButton = new Button();
        private ToolTip outputDirectoryToolTip;

        private string ConfiguredOutputDirectory {
            get { return (Properties.Settings.Default.OutputDirectory ?? string.Empty).Trim(); }
        }

        private void InitializeExportSettings() {
            outputDirectoryLabel.Name = "outputDirectoryLabel";
            outputDirectoryLabel.TextAlign = ContentAlignment.MiddleLeft;
            outputDirectoryBox.Name = "outputDirectoryBox";
            outputDirectoryBox.ReadOnly = true;
            outputDirectoryBox.BackColor = SystemColors.Window;
            outputDirectoryBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            outputDirectoryBox.TabIndex = 17;
            chooseOutputDirectoryButton.Name = "chooseOutputDirectoryButton";
            chooseOutputDirectoryButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            chooseOutputDirectoryButton.TabIndex = 18;
            resetOutputDirectoryButton.Name = "resetOutputDirectoryButton";
            resetOutputDirectoryButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            resetOutputDirectoryButton.TabIndex = 19;
            StyleQueueButton(chooseOutputDirectoryButton);
            StyleQueueButton(resetOutputDirectoryButton);
            chooseOutputDirectoryButton.Click += ChooseOutputDirectoryButton_Click;
            resetOutputDirectoryButton.Click += (sender, e) => SaveOutputDirectory(string.Empty);

            if (components == null) {
                components = new System.ComponentModel.Container();
            }
            outputDirectoryToolTip = new ToolTip(components);
            Controls.Add(outputDirectoryLabel);
            Controls.Add(outputDirectoryBox);
            Controls.Add(chooseOutputDirectoryButton);
            Controls.Add(resetOutputDirectoryButton);
            ReloadExportSettingsLanguage();
        }

        private void ReloadExportSettingsLanguage() {
            bool isChinese = System.Threading.Thread.CurrentThread.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            outputDirectoryLabel.Text = isChinese ? "输出目录:" : "Output folder:";
            outputDirectoryBox.AccessibleName = outputDirectoryLabel.Text.TrimEnd(':');
            chooseOutputDirectoryButton.Text = isChinese ? "选择目录" : "Browse...";
            resetOutputDirectoryButton.Text = isChinese ? "恢复默认" : "Reset";
            outputDirectoryToolTip.SetToolTip(outputDirectoryLabel, isChinese
                ? "默认沿用源位置 / Lua 规则；同名 PDF 自动编号。"
                : "Default: source location / Lua rules. Existing PDF names are numbered automatically.");
            UpdateOutputDirectoryText();
            RepositionExportSettings();
        }

        private void RepositionExportSettings() {
            int existingControlsBottom = Math.Max(StartButton.Bottom, GalleryButton.Bottom);
            existingControlsBottom = Math.Max(existingControlsBottom, progressBar.Bottom);
            existingControlsBottom = Math.Max(existingControlsBottom, generateModeBox.Bottom);
            existingControlsBottom = Math.Max(existingControlsBottom, qualityBox.Bottom);
            existingControlsBottom = Math.Max(existingControlsBottom, UniformWidth.Bottom);
            existingControlsBottom = Math.Max(existingControlsBottom, Recursive.Bottom);
            existingControlsBottom = Math.Max(existingControlsBottom, MsgLabel.Bottom);
            int rowTop = existingControlsBottom + 12;
            int rowHeight = Math.Max(28, outputDirectoryBox.PreferredHeight);
            int labelWidth = TextRenderer.MeasureText(outputDirectoryLabel.Text, outputDirectoryLabel.Font).Width + 8;
            int chooseWidth = GetPreferredButtonWidth(chooseOutputDirectoryButton, 90);
            int resetWidth = GetPreferredButtonWidth(resetOutputDirectoryButton, 90);
            int resetLeft = ClientSize.Width - 20 - resetWidth;
            int chooseLeft = resetLeft - 8 - chooseWidth;
            int pathLeft = 20 + labelWidth;

            outputDirectoryLabel.SetBounds(20, rowTop, labelWidth, rowHeight);
            outputDirectoryBox.SetBounds(pathLeft, rowTop + (rowHeight - outputDirectoryBox.PreferredHeight) / 2,
                Math.Max(80, chooseLeft - 8 - pathLeft), outputDirectoryBox.PreferredHeight);
            chooseOutputDirectoryButton.SetBounds(chooseLeft, rowTop, chooseWidth, rowHeight);
            resetOutputDirectoryButton.SetBounds(resetLeft, rowTop, resetWidth, rowHeight);
            ClientSize = new Size(ClientSize.Width, Math.Max(ClientSize.Height, rowTop + rowHeight + 18));
        }

        private void SetExportSettingsEnabled(bool enabled) {
            chooseOutputDirectoryButton.Enabled = enabled;
            resetOutputDirectoryButton.Enabled = enabled;
        }

        private void ChooseOutputDirectoryButton_Click(object sender, EventArgs e) {
            bool isChinese = System.Threading.Thread.CurrentThread.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            using (var dialog = new FolderBrowserDialog()) {
                dialog.Description = isChinese ? "选择 PDF 输出目录" : "Choose PDF output folder";
                if (Directory.Exists(ConfiguredOutputDirectory)) {
                    dialog.SelectedPath = ConfiguredOutputDirectory;
                }
                if (dialog.ShowDialog(this) == DialogResult.OK) {
                    SaveOutputDirectory(dialog.SelectedPath);
                }
            }
        }

        private void SaveOutputDirectory(string directory) {
            Properties.Settings.Default.OutputDirectory = directory.Trim();
            UpdateOutputDirectoryText();
            try {
                Properties.Settings.Default.Save();
            }
            catch (ConfigurationErrorsException ex) {
                bool isChinese = System.Threading.Thread.CurrentThread.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
                MessageBox.Show(this, (isChinese ? "无法保存输出目录设置。\n" : "Could not save output folder settings.\n") + ex.Message,
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateOutputDirectoryText() {
            string directory = ConfiguredOutputDirectory;
            if (directory.Length == 0) {
                bool isChinese = System.Threading.Thread.CurrentThread.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
                outputDirectoryBox.Text = isChinese ? "源位置 / Lua 规则" : "Source location / Lua rules";
            }
            else {
                outputDirectoryBox.Text = directory;
            }
            outputDirectoryBox.SelectionStart = 0;
            outputDirectoryBox.SelectionLength = 0;
            outputDirectoryToolTip.SetToolTip(outputDirectoryBox, directory.Length == 0
                ? outputDirectoryToolTip.GetToolTip(outputDirectoryLabel)
                : directory);
        }
    }
}
