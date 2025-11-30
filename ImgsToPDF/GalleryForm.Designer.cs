namespace ImgsToPDF
{
    partial class GalleryForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GalleryForm));
            this.filterPanel = new System.Windows.Forms.Panel();
            this.loadingLabel = new System.Windows.Forms.Label();
            this.noSelectionLabel = new System.Windows.Forms.Label();
            this.selectionStatusLabel = new System.Windows.Forms.Label();
            this.extensionsSelectAllButton = new System.Windows.Forms.Button();
            this.extensionsClearButton = new System.Windows.Forms.Button();
            this.extensionCheckedList = new System.Windows.Forms.CheckedListBox();
            this.extensionLabel = new System.Windows.Forms.Label();
            this.selectNoneButton = new System.Windows.Forms.Button();
            this.selectAllButton = new System.Windows.Forms.Button();
            this.resetZoomButton = new System.Windows.Forms.Button();
            this.thumbFlowPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.bottomPanel = new System.Windows.Forms.Panel();
            this.cancelButton = new System.Windows.Forms.Button();
            this.confirmButton = new System.Windows.Forms.Button();
            this.filterPanel.SuspendLayout();
            this.bottomPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // filterPanel
            // 
            this.filterPanel.Controls.Add(this.loadingLabel);
            this.filterPanel.Controls.Add(this.noSelectionLabel);
            this.filterPanel.Controls.Add(this.selectionStatusLabel);
            this.filterPanel.Controls.Add(this.extensionsSelectAllButton);
            this.filterPanel.Controls.Add(this.extensionsClearButton);
            this.filterPanel.Controls.Add(this.extensionCheckedList);
            this.filterPanel.Controls.Add(this.extensionLabel);
            this.filterPanel.Controls.Add(this.selectNoneButton);
            this.filterPanel.Controls.Add(this.selectAllButton);
            this.filterPanel.Controls.Add(this.resetZoomButton);
            this.filterPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.filterPanel.Location = new System.Drawing.Point(0, 0);
            this.filterPanel.Margin = new System.Windows.Forms.Padding(4);
            this.filterPanel.Name = "filterPanel";
            this.filterPanel.Size = new System.Drawing.Size(1056, 160);
            this.filterPanel.TabIndex = 0;
            // 
            // loadingLabel
            // 
            this.loadingLabel.AutoSize = true;
            this.loadingLabel.ForeColor = System.Drawing.Color.RoyalBlue;
            this.loadingLabel.Location = new System.Drawing.Point(14, 110);
            this.loadingLabel.Name = "loadingLabel";
            this.loadingLabel.Size = new System.Drawing.Size(70, 18);
            this.loadingLabel.TabIndex = 17;
            this.loadingLabel.Text = resources.GetString("loadingLabel.Text");
            this.loadingLabel.Visible = false;
            // 
            // noSelectionLabel
            // 
            this.noSelectionLabel.AutoSize = true;
            this.noSelectionLabel.Location = new System.Drawing.Point(720, 82);
            this.noSelectionLabel.Name = "noSelectionLabel";
            this.noSelectionLabel.Size = new System.Drawing.Size(156, 18);
            this.noSelectionLabel.TabIndex = 16;
            this.noSelectionLabel.Text = resources.GetString("noSelectionLabel.Text");
            this.noSelectionLabel.Visible = false;
            // 
            // selectionStatusLabel
            // 
            this.selectionStatusLabel.AutoSize = true;
            this.selectionStatusLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold);
            this.selectionStatusLabel.Location = new System.Drawing.Point(720, 12);
            this.selectionStatusLabel.Name = "selectionStatusLabel";
            this.selectionStatusLabel.Size = new System.Drawing.Size(208, 22);
            this.selectionStatusLabel.TabIndex = 15;
            this.selectionStatusLabel.Text = resources.GetString("selectionStatusLabel.Text");
            // 
            // extensionsSelectAllButton
            // 
            this.extensionsSelectAllButton.Location = new System.Drawing.Point(360, 40);
            this.extensionsSelectAllButton.Name = "extensionsSelectAllButton";
            this.extensionsSelectAllButton.Size = new System.Drawing.Size(110, 32);
            this.extensionsSelectAllButton.TabIndex = 2;
            this.extensionsSelectAllButton.Text = resources.GetString("extensionsSelectAllButton.Text");
            this.extensionsSelectAllButton.UseVisualStyleBackColor = true;
            this.extensionsSelectAllButton.Click += new System.EventHandler(this.extensionsSelectAllButton_Click);
            // 
            // extensionsClearButton
            // 
            this.extensionsClearButton.Location = new System.Drawing.Point(360, 78);
            this.extensionsClearButton.Name = "extensionsClearButton";
            this.extensionsClearButton.Size = new System.Drawing.Size(110, 32);
            this.extensionsClearButton.TabIndex = 3;
            this.extensionsClearButton.Text = resources.GetString("extensionsClearButton.Text");
            this.extensionsClearButton.UseVisualStyleBackColor = true;
            this.extensionsClearButton.Click += new System.EventHandler(this.extensionsClearButton_Click);
            // 
            // extensionCheckedList
            // 
            this.extensionCheckedList.CheckOnClick = true;
            this.extensionCheckedList.FormattingEnabled = true;
            this.extensionCheckedList.Location = new System.Drawing.Point(14, 40);
            this.extensionCheckedList.MultiColumn = false;
            this.extensionCheckedList.Name = "extensionCheckedList";
            this.extensionCheckedList.HorizontalScrollbar = false;
            this.extensionCheckedList.IntegralHeight = false;
            this.extensionCheckedList.Size = new System.Drawing.Size(340, 120);
            this.extensionCheckedList.TabIndex = 1;
            this.extensionCheckedList.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.extensionCheckedList_ItemCheck);
            // 
            // extensionLabel
            // 
            this.extensionLabel.AutoSize = true;
            this.extensionLabel.Location = new System.Drawing.Point(11, 14);
            this.extensionLabel.Name = "extensionLabel";
            this.extensionLabel.Size = new System.Drawing.Size(134, 18);
            this.extensionLabel.TabIndex = 0;
            this.extensionLabel.Text = resources.GetString("extensionLabel.Text");
            // 
            // selectNoneButton
            // 
            this.selectNoneButton.Location = new System.Drawing.Point(500, 78);
            this.selectNoneButton.Name = "selectNoneButton";
            this.selectNoneButton.Size = new System.Drawing.Size(100, 32);
            this.selectNoneButton.TabIndex = 11;
            this.selectNoneButton.Text = resources.GetString("selectNoneButton.Text");
            this.selectNoneButton.UseVisualStyleBackColor = true;
            this.selectNoneButton.Click += new System.EventHandler(this.selectNoneButton_Click);
            // 
            // selectAllButton
            // 
            this.selectAllButton.Location = new System.Drawing.Point(500, 38);
            this.selectAllButton.Name = "selectAllButton";
            this.selectAllButton.Size = new System.Drawing.Size(110, 32);
            this.selectAllButton.TabIndex = 10;
            this.selectAllButton.Text = resources.GetString("selectAllButton.Text");
            this.selectAllButton.UseVisualStyleBackColor = true;
            this.selectAllButton.Click += new System.EventHandler(this.selectAllButton_Click);
            // 
            // resetZoomButton
            // 
            this.resetZoomButton.Location = new System.Drawing.Point(620, 40);
            this.resetZoomButton.Name = "resetZoomButton";
            this.resetZoomButton.Size = new System.Drawing.Size(110, 32);
            this.resetZoomButton.TabIndex = 17;
            this.resetZoomButton.Text = resources.GetString("resetZoomButton.Text");
            this.resetZoomButton.UseVisualStyleBackColor = true;
            this.resetZoomButton.Click += new System.EventHandler(this.resetZoomButton_Click);
            // 
            // thumbFlowPanel
            // 
            this.thumbFlowPanel.AutoScroll = true;
            this.thumbFlowPanel.WrapContents = true;
            this.thumbFlowPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.thumbFlowPanel.Location = new System.Drawing.Point(0, 160);
            this.thumbFlowPanel.Name = "thumbFlowPanel";
            this.thumbFlowPanel.Padding = new System.Windows.Forms.Padding(40, 10, 40, 10);
            this.thumbFlowPanel.Size = new System.Drawing.Size(1056, 480);
            this.thumbFlowPanel.TabIndex = 1;
            // 
            // bottomPanel
            // 
            this.bottomPanel.Controls.Add(this.cancelButton);
            this.bottomPanel.Controls.Add(this.confirmButton);
            this.bottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.bottomPanel.Location = new System.Drawing.Point(0, 640);
            this.bottomPanel.Name = "bottomPanel";
            this.bottomPanel.Size = new System.Drawing.Size(1056, 60);
            this.bottomPanel.TabIndex = 2;
            // 
            // cancelButton
            // 
            this.cancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cancelButton.Location = new System.Drawing.Point(950, 14);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(90, 32);
            this.cancelButton.TabIndex = 1;
            this.cancelButton.Text = resources.GetString("cancelButton.Text");
            this.cancelButton.UseVisualStyleBackColor = true;
            this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
            // 
            // confirmButton
            // 
            this.confirmButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.confirmButton.Location = new System.Drawing.Point(840, 14);
            this.confirmButton.Name = "confirmButton";
            this.confirmButton.Size = new System.Drawing.Size(100, 32);
            this.confirmButton.TabIndex = 0;
            this.confirmButton.Tag = resources.GetString("confirmButton.Tag");
            this.confirmButton.Text = resources.GetString("confirmButton.Text");
            this.confirmButton.UseVisualStyleBackColor = true;
            this.confirmButton.Click += new System.EventHandler(this.confirmButton_Click);
            // 
            // GalleryForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1056, 700);
            this.Controls.Add(this.thumbFlowPanel);
            this.Controls.Add(this.bottomPanel);
            this.Controls.Add(this.filterPanel);
            this.AcceptButton = this.confirmButton;
            this.CancelButton = this.cancelButton;
            this.MinimumSize = new System.Drawing.Size(640, 480);
            this.Name = "GalleryForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = resources.GetString("$this.Text");
            this.Load += new System.EventHandler(this.GalleryForm_Load);
            this.filterPanel.ResumeLayout(false);
            this.filterPanel.PerformLayout();
            this.bottomPanel.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel filterPanel;
        private System.Windows.Forms.FlowLayoutPanel thumbFlowPanel;
        private System.Windows.Forms.Button selectNoneButton;
        private System.Windows.Forms.Button selectAllButton;
        private System.Windows.Forms.CheckedListBox extensionCheckedList;
        private System.Windows.Forms.Label extensionLabel;
        private System.Windows.Forms.Button extensionsSelectAllButton;
        private System.Windows.Forms.Button extensionsClearButton;
        private System.Windows.Forms.Label selectionStatusLabel;
        private System.Windows.Forms.Panel bottomPanel;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Button confirmButton;
        private System.Windows.Forms.Label noSelectionLabel;
        private System.Windows.Forms.Label loadingLabel;
        private System.Windows.Forms.Button resetZoomButton;
    }
}
