
namespace ImageCropTool
{
    partial class MainForm
    {
        /// <summary>
        /// 필수 디자이너 변수입니다.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 사용 중인 모든 리소스를 정리합니다.
        /// </summary>
        /// <param name="disposing">관리되는 리소스를 삭제해야 하면 true이고, 그렇지 않으면 false입니다.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposePyramidCaches();
                if (components != null)
                    components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form 디자이너에서 생성한 코드

        /// <summary>
        /// 디자이너 지원에 필요한 메서드입니다. 
        /// 이 메서드의 내용을 코드 편집기로 수정하지 마세요.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.pictureBoxImage = new System.Windows.Forms.PictureBox();
            this.splitContainerMain = new System.Windows.Forms.SplitContainer();
            this.panelRight = new System.Windows.Forms.Panel();
            this.label5 = new System.Windows.Forms.Label();
            this.pictureBoxMiniMap = new System.Windows.Forms.PictureBox();
            this.groupInfo = new System.Windows.Forms.GroupBox();
            this.lblLineIndex = new System.Windows.Forms.Label();
            this.lblCropSize = new System.Windows.Forms.Label();
            this.lblCropCount = new System.Windows.Forms.Label();
            this.lblLineLength = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.groupCrop = new System.Windows.Forms.GroupBox();
            this.numCropSize = new System.Windows.Forms.NumericUpDown();
            this.label2 = new System.Windows.Forms.Label();
            this.btnReset = new System.Windows.Forms.Button();
            this.btnCropSave = new System.Windows.Forms.Button();
            this.btnLoadImage = new System.Windows.Forms.Button();
            this.listViewMain = new System.Windows.Forms.ListView();
            this.pictureBoxPreview = new System.Windows.Forms.PictureBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.ZoomFit = new System.Windows.Forms.ToolStripMenuItem();
            this.LineDelete = new System.Windows.Forms.ToolStripMenuItem();
            this.ResetLine = new System.Windows.Forms.ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxImage)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).BeginInit();
            this.splitContainerMain.Panel1.SuspendLayout();
            this.splitContainerMain.Panel2.SuspendLayout();
            this.splitContainerMain.SuspendLayout();
            this.panelRight.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxMiniMap)).BeginInit();
            this.groupInfo.SuspendLayout();
            this.groupCrop.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numCropSize)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxPreview)).BeginInit();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // pictureBoxImage
            // 
            this.pictureBoxImage.BackColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.pictureBoxImage.ContextMenuStrip = this.contextMenuStrip1;
            this.pictureBoxImage.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pictureBoxImage.Location = new System.Drawing.Point(0, 0);
            this.pictureBoxImage.Name = "pictureBoxImage";
            this.pictureBoxImage.Size = new System.Drawing.Size(707, 843);
            this.pictureBoxImage.TabIndex = 0;
            this.pictureBoxImage.TabStop = false;
            // 
            // splitContainerMain
            // 
            this.splitContainerMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerMain.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainerMain.Location = new System.Drawing.Point(0, 0);
            this.splitContainerMain.Name = "splitContainerMain";
            // 
            // splitContainerMain.Panel1
            // 
            this.splitContainerMain.Panel1.Controls.Add(this.pictureBoxImage);
            // 
            // splitContainerMain.Panel2
            // 
            this.splitContainerMain.Panel2.AccessibleName = "";
            this.splitContainerMain.Panel2.Controls.Add(this.panelRight);
            this.splitContainerMain.Size = new System.Drawing.Size(965, 843);
            this.splitContainerMain.SplitterDistance = 707;
            this.splitContainerMain.SplitterWidth = 6;
            this.splitContainerMain.TabIndex = 2;
            // 
            // panelRight
            // 
            this.panelRight.Controls.Add(this.label5);
            this.panelRight.Controls.Add(this.pictureBoxMiniMap);
            this.panelRight.Controls.Add(this.groupInfo);
            this.panelRight.Controls.Add(this.label3);
            this.panelRight.Controls.Add(this.groupCrop);
            this.panelRight.Controls.Add(this.btnReset);
            this.panelRight.Controls.Add(this.btnCropSave);
            this.panelRight.Controls.Add(this.btnLoadImage);
            this.panelRight.Controls.Add(this.listViewMain);
            this.panelRight.Controls.Add(this.pictureBoxPreview);
            this.panelRight.Controls.Add(this.label4);
            this.panelRight.Controls.Add(this.label1);
            this.panelRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelRight.Location = new System.Drawing.Point(0, 0);
            this.panelRight.Name = "panelRight";
            this.panelRight.Size = new System.Drawing.Size(252, 843);
            this.panelRight.TabIndex = 0;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(95, 541);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(61, 15);
            this.label5.TabIndex = 19;
            this.label5.Text = "MiniMap";
            // 
            // pictureBoxMiniMap
            // 
            this.pictureBoxMiniMap.BackColor = System.Drawing.SystemColors.ActiveBorder;
            this.pictureBoxMiniMap.Location = new System.Drawing.Point(27, 559);
            this.pictureBoxMiniMap.Name = "pictureBoxMiniMap";
            this.pictureBoxMiniMap.Size = new System.Drawing.Size(210, 204);
            this.pictureBoxMiniMap.TabIndex = 10;
            this.pictureBoxMiniMap.TabStop = false;
            // 
            // groupInfo
            // 
            this.groupInfo.Controls.Add(this.lblLineIndex);
            this.groupInfo.Controls.Add(this.lblCropSize);
            this.groupInfo.Controls.Add(this.lblCropCount);
            this.groupInfo.Controls.Add(this.lblLineLength);
            this.groupInfo.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupInfo.Location = new System.Drawing.Point(0, 95);
            this.groupInfo.Name = "groupInfo";
            this.groupInfo.Size = new System.Drawing.Size(252, 68);
            this.groupInfo.TabIndex = 18;
            this.groupInfo.TabStop = false;
            this.groupInfo.Text = "Line Info";
            // 
            // lblLineIndex
            // 
            this.lblLineIndex.AutoSize = true;
            this.lblLineIndex.Location = new System.Drawing.Point(10, 21);
            this.lblLineIndex.Name = "lblLineIndex";
            this.lblLineIndex.Size = new System.Drawing.Size(61, 15);
            this.lblLineIndex.TabIndex = 3;
            this.lblLineIndex.Text = "Index: -";
            // 
            // lblCropSize
            // 
            this.lblCropSize.AutoSize = true;
            this.lblCropSize.Location = new System.Drawing.Point(133, 21);
            this.lblCropSize.Name = "lblCropSize";
            this.lblCropSize.Size = new System.Drawing.Size(90, 15);
            this.lblCropSize.TabIndex = 2;
            this.lblCropSize.Text = "Crop Size: -";
            // 
            // lblCropCount
            // 
            this.lblCropCount.AutoSize = true;
            this.lblCropCount.Location = new System.Drawing.Point(134, 41);
            this.lblCropCount.Name = "lblCropCount";
            this.lblCropCount.Size = new System.Drawing.Size(100, 15);
            this.lblCropCount.TabIndex = 1;
            this.lblCropCount.Text = "Crop Count: -";
            // 
            // lblLineLength
            // 
            this.lblLineLength.AutoSize = true;
            this.lblLineLength.Location = new System.Drawing.Point(10, 41);
            this.lblLineLength.Name = "lblLineLength";
            this.lblLineLength.Size = new System.Drawing.Size(69, 15);
            this.lblLineLength.TabIndex = 0;
            this.lblLineLength.Text = "Length: -";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Dock = System.Windows.Forms.DockStyle.Top;
            this.label3.Location = new System.Drawing.Point(0, 80);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(317, 15);
            this.label3.TabIndex = 17;
            this.label3.Text = "                                                              ";
            // 
            // groupCrop
            // 
            this.groupCrop.Controls.Add(this.numCropSize);
            this.groupCrop.Controls.Add(this.label2);
            this.groupCrop.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupCrop.Location = new System.Drawing.Point(0, 15);
            this.groupCrop.Name = "groupCrop";
            this.groupCrop.Size = new System.Drawing.Size(252, 65);
            this.groupCrop.TabIndex = 16;
            this.groupCrop.TabStop = false;
            this.groupCrop.Text = "Crop 설정";
            // 
            // numCropSize
            // 
            this.numCropSize.Increment = new decimal(new int[] {
            64,
            0,
            0,
            0});
            this.numCropSize.Location = new System.Drawing.Point(136, 24);
            this.numCropSize.Maximum = new decimal(new int[] {
            2560,
            0,
            0,
            0});
            this.numCropSize.Minimum = new decimal(new int[] {
            256,
            0,
            0,
            0});
            this.numCropSize.Name = "numCropSize";
            this.numCropSize.Size = new System.Drawing.Size(120, 25);
            this.numCropSize.TabIndex = 1;
            this.numCropSize.Value = new decimal(new int[] {
            512,
            0,
            0,
            0});
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(10, 30);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(106, 15);
            this.label2.TabIndex = 0;
            this.label2.Text = "Crop Size (px)";
            // 
            // btnReset
            // 
            this.btnReset.Location = new System.Drawing.Point(181, 778);
            this.btnReset.Name = "btnReset";
            this.btnReset.Size = new System.Drawing.Size(79, 49);
            this.btnReset.TabIndex = 15;
            this.btnReset.Text = "초기화";
            this.btnReset.UseVisualStyleBackColor = true;
            this.btnReset.Click += new System.EventHandler(this.BtnReset_Click);
            // 
            // btnCropSave
            // 
            this.btnCropSave.Location = new System.Drawing.Point(98, 778);
            this.btnCropSave.Name = "btnCropSave";
            this.btnCropSave.Size = new System.Drawing.Size(79, 49);
            this.btnCropSave.TabIndex = 14;
            this.btnCropSave.Text = "저장하기";
            this.btnCropSave.UseVisualStyleBackColor = true;
            this.btnCropSave.Click += new System.EventHandler(this.BtnCropSave_Click);
            // 
            // btnLoadImage
            // 
            this.btnLoadImage.Location = new System.Drawing.Point(12, 778);
            this.btnLoadImage.Name = "btnLoadImage";
            this.btnLoadImage.Size = new System.Drawing.Size(84, 49);
            this.btnLoadImage.TabIndex = 13;
            this.btnLoadImage.Text = "불러오기";
            this.btnLoadImage.UseVisualStyleBackColor = true;
            this.btnLoadImage.Click += new System.EventHandler(this.BtnLoadImage_Click);
            // 
            // listViewMain
            // 
            this.listViewMain.HideSelection = false;
            this.listViewMain.Location = new System.Drawing.Point(27, 190);
            this.listViewMain.Name = "listViewMain";
            this.listViewMain.Size = new System.Drawing.Size(210, 122);
            this.listViewMain.TabIndex = 12;
            this.listViewMain.UseCompatibleStateImageBehavior = false;
            this.listViewMain.View = System.Windows.Forms.View.List;
            // 
            // pictureBoxPreview
            // 
            this.pictureBoxPreview.BackColor = System.Drawing.SystemColors.ActiveBorder;
            this.pictureBoxPreview.Location = new System.Drawing.Point(27, 333);
            this.pictureBoxPreview.Name = "pictureBoxPreview";
            this.pictureBoxPreview.Size = new System.Drawing.Size(210, 204);
            this.pictureBoxPreview.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBoxPreview.TabIndex = 8;
            this.pictureBoxPreview.TabStop = false;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(95, 315);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(58, 15);
            this.label4.TabIndex = 9;
            this.label4.Text = "Preview";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Dock = System.Windows.Forms.DockStyle.Top;
            this.label1.Location = new System.Drawing.Point(0, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(417, 15);
            this.label1.TabIndex = 4;
            this.label1.Text = "                                                                                 " +
    " ";
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ZoomFit,
            this.LineDelete,
            this.ResetLine});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(211, 104);
            // 
            // ZoomFit
            // 
            this.ZoomFit.Name = "ZoomFit";
            this.ZoomFit.Size = new System.Drawing.Size(210, 24);
            this.ZoomFit.Text = "줌핏";
            this.ZoomFit.Click += new System.EventHandler(this.ZoomFit_Click);
            // 
            // LineDelete
            // 
            this.LineDelete.Name = "LineDelete";
            this.LineDelete.Size = new System.Drawing.Size(210, 24);
            this.LineDelete.Text = "라인 삭제";
            this.LineDelete.Click += new System.EventHandler(this.LineDelete_Click);
            // 
            // ResetLine
            // 
            this.ResetLine.Name = "ResetLine";
            this.ResetLine.Size = new System.Drawing.Size(210, 24);
            this.ResetLine.Text = "라인 초기화";
            this.ResetLine.Click += new System.EventHandler(this.ResetLine_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(965, 843);
            this.Controls.Add(this.splitContainerMain);
            this.DoubleBuffered = true;
            this.Name = "MainForm";
            this.Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxImage)).EndInit();
            this.splitContainerMain.Panel1.ResumeLayout(false);
            this.splitContainerMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).EndInit();
            this.splitContainerMain.ResumeLayout(false);
            this.panelRight.ResumeLayout(false);
            this.panelRight.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxMiniMap)).EndInit();
            this.groupInfo.ResumeLayout(false);
            this.groupInfo.PerformLayout();
            this.groupCrop.ResumeLayout(false);
            this.groupCrop.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numCropSize)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxPreview)).EndInit();
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox pictureBoxImage;
        private System.Windows.Forms.SplitContainer splitContainerMain;
        private System.Windows.Forms.Panel panelRight;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.PictureBox pictureBoxPreview;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.PictureBox pictureBoxMiniMap;
        private System.Windows.Forms.ListView listViewMain;
        private System.Windows.Forms.Button btnReset;
        private System.Windows.Forms.Button btnCropSave;
        private System.Windows.Forms.Button btnLoadImage;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.GroupBox groupInfo;
        private System.Windows.Forms.Label lblLineIndex;
        private System.Windows.Forms.Label lblCropSize;
        private System.Windows.Forms.Label lblCropCount;
        private System.Windows.Forms.Label lblLineLength;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.GroupBox groupCrop;
        private System.Windows.Forms.NumericUpDown numCropSize;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ToolStripMenuItem ZoomFit;
        private System.Windows.Forms.ToolStripMenuItem LineDelete;
        private System.Windows.Forms.ToolStripMenuItem ResetLine;
    }
}

