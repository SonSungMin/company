using DevExpress.XtraRichEdit;
using System.Drawing;
using System.Windows.Forms;

namespace DevTools.UI.Control
{
    partial class AIChat
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AIChat));
            this.layoutControl1 = new DevExpress.XtraLayout.LayoutControl();
            this.prgGpuUsage = new DevExpress.XtraEditors.ProgressBarControl();
            this.mproAiThink = new DevExpress.XtraEditors.MarqueeProgressBarControl();
            this.cboModelList = new DevExpress.XtraEditors.ImageComboBoxEdit();
            this.chkUsePrompt = new DevExpress.XtraEditors.CheckEdit();
            this.txtAttFile = new DevExpress.XtraRichEdit.RichEditControl();
            this.btnRemoveFile = new DevExpress.XtraEditors.SimpleButton();
            this.btnOpenFile = new DevExpress.XtraEditors.SimpleButton();
            this.txtSystemPrompt = new DevExpress.XtraRichEdit.RichEditControl();
            this.lstFiles = new DevExpress.XtraEditors.ListBoxControl();
            this.lblStatus = new DevExpress.XtraEditors.LabelControl();
            this.btnAnalyze = new DevExpress.XtraEditors.SimpleButton();
            this.txtResult = new DevExpress.XtraRichEdit.RichEditControl();
            this.txtQuest = new DevExpress.XtraRichEdit.RichEditControl();
            this.Root = new DevExpress.XtraLayout.LayoutControlGroup();
            this.splitterItem1 = new DevExpress.XtraLayout.SplitterItem();
            this.layoutControlItem2 = new DevExpress.XtraLayout.LayoutControlItem();
            this.emptySpaceItem1 = new DevExpress.XtraLayout.EmptySpaceItem();
            this.layoutControlItem3 = new DevExpress.XtraLayout.LayoutControlItem();
            this.layoutControlGroup1 = new DevExpress.XtraLayout.LayoutControlGroup();
            this.layoutControlItem6 = new DevExpress.XtraLayout.LayoutControlItem();
            this.layoutControlItem5 = new DevExpress.XtraLayout.LayoutControlItem();
            this.layoutControlItem8 = new DevExpress.XtraLayout.LayoutControlItem();
            this.emptySpaceItem3 = new DevExpress.XtraLayout.EmptySpaceItem();
            this.layoutControlItem9 = new DevExpress.XtraLayout.LayoutControlItem();
            this.layoutControlGroup2 = new DevExpress.XtraLayout.LayoutControlGroup();
            this.layoutControlItem1 = new DevExpress.XtraLayout.LayoutControlItem();
            this.layoutControlItem11 = new DevExpress.XtraLayout.LayoutControlItem();
            this.emptySpaceItem2 = new DevExpress.XtraLayout.EmptySpaceItem();
            this.layAiThink = new DevExpress.XtraLayout.LayoutControlItem();
            this.layoutControlItem13 = new DevExpress.XtraLayout.LayoutControlItem();
            this.splitterItem2 = new DevExpress.XtraLayout.SplitterItem();
            this.layoutControlGroup3 = new DevExpress.XtraLayout.LayoutControlGroup();
            this.layoutControlItem4 = new DevExpress.XtraLayout.LayoutControlItem();
            this.layoutControlItem7 = new DevExpress.XtraLayout.LayoutControlItem();
            this.splitterItem3 = new DevExpress.XtraLayout.SplitterItem();
            this.layoutControlItem10 = new DevExpress.XtraLayout.LayoutControlItem();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControl1)).BeginInit();
            this.layoutControl1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.prgGpuUsage.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.mproAiThink.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.cboModelList.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.chkUsePrompt.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.lstFiles)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.Root)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitterItem1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItem2)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.emptySpaceItem1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItem3)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlGroup1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItem6)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItem5)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItem8)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.emptySpaceItem3)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItem9)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlGroup2)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItem1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItem11)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.emptySpaceItem2)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.layAiThink)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItem13)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitterItem2)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlGroup3)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItem4)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItem7)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitterItem3)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItem10)).BeginInit();
            this.SuspendLayout();
            // 
            // layoutControl1
            // 
            this.layoutControl1.Controls.Add(this.prgGpuUsage);
            this.layoutControl1.Controls.Add(this.mproAiThink);
            this.layoutControl1.Controls.Add(this.cboModelList);
            this.layoutControl1.Controls.Add(this.chkUsePrompt);
            this.layoutControl1.Controls.Add(this.txtAttFile);
            this.layoutControl1.Controls.Add(this.btnRemoveFile);
            this.layoutControl1.Controls.Add(this.btnOpenFile);
            this.layoutControl1.Controls.Add(this.txtSystemPrompt);
            this.layoutControl1.Controls.Add(this.lstFiles);
            this.layoutControl1.Controls.Add(this.lblStatus);
            this.layoutControl1.Controls.Add(this.btnAnalyze);
            this.layoutControl1.Controls.Add(this.txtResult);
            this.layoutControl1.Controls.Add(this.txtQuest);
            this.layoutControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.layoutControl1.Location = new System.Drawing.Point(0, 0);
            this.layoutControl1.Name = "layoutControl1";
            this.layoutControl1.OptionsCustomizationForm.DesignTimeCustomizationFormPositionAndSize = new System.Drawing.Rectangle(1010, 342, 650, 400);
            this.layoutControl1.Root = this.Root;
            this.layoutControl1.Size = new System.Drawing.Size(1382, 633);
            this.layoutControl1.TabIndex = 0;
            this.layoutControl1.Text = "layoutControl1";
            // 
            // prgGpuUsage
            // 
            this.prgGpuUsage.Location = new System.Drawing.Point(797, 24);
            this.prgGpuUsage.MaximumSize = new System.Drawing.Size(50, 0);
            this.prgGpuUsage.MinimumSize = new System.Drawing.Size(50, 0);
            this.prgGpuUsage.Name = "prgGpuUsage";
            this.prgGpuUsage.Properties.PercentView = false;
            this.prgGpuUsage.Properties.ShowTitle = true;
            this.prgGpuUsage.Size = new System.Drawing.Size(50, 20);
            this.prgGpuUsage.StyleController = this.layoutControl1;
            this.prgGpuUsage.TabIndex = 20;
            // 
            // mproAiThink
            // 
            this.mproAiThink.EditValue = 0;
            this.mproAiThink.Location = new System.Drawing.Point(851, 25);
            this.mproAiThink.MaximumSize = new System.Drawing.Size(100, 0);
            this.mproAiThink.MinimumSize = new System.Drawing.Size(100, 0);
            this.mproAiThink.Name = "mproAiThink";
            this.mproAiThink.Properties.MarqueeWidth = 25;
            this.mproAiThink.Size = new System.Drawing.Size(100, 18);
            this.mproAiThink.StyleController = this.layoutControl1;
            this.mproAiThink.TabIndex = 19;
            // 
            // cboModelList
            // 
            this.cboModelList.Location = new System.Drawing.Point(104, 24);
            this.cboModelList.MaximumSize = new System.Drawing.Size(200, 0);
            this.cboModelList.MinimumSize = new System.Drawing.Size(200, 0);
            this.cboModelList.Name = "cboModelList";
            this.cboModelList.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.cboModelList.Size = new System.Drawing.Size(200, 18);
            this.cboModelList.StyleController = this.layoutControl1;
            this.cboModelList.TabIndex = 18;
            this.cboModelList.EditValueChanged += new System.EventHandler(this.cboModelList_EditValueChanged);
            // 
            // chkUsePrompt
            // 
            this.chkUsePrompt.Location = new System.Drawing.Point(980, 413);
            this.chkUsePrompt.Name = "chkUsePrompt";
            this.chkUsePrompt.Properties.Caption = "프롬프트 사용 여부";
            this.chkUsePrompt.Size = new System.Drawing.Size(378, 20);
            this.chkUsePrompt.StyleController = this.layoutControl1;
            this.chkUsePrompt.TabIndex = 17;
            // 
            // txtAttFile
            // 
            this.txtAttFile.ActiveViewType = DevExpress.XtraRichEdit.RichEditViewType.Simple;
            this.txtAttFile.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.txtAttFile.EnableToolTips = false;
            this.txtAttFile.LayoutUnit = DevExpress.XtraRichEdit.DocumentLayoutUnit.Pixel;
            this.txtAttFile.Location = new System.Drawing.Point(989, 176);
            this.txtAttFile.Name = "txtAttFile";
            this.txtAttFile.Options.DocumentSaveOptions.CurrentFormat = DevExpress.XtraRichEdit.DocumentFormat.PlainText;
            this.txtAttFile.Options.HorizontalRuler.Visibility = DevExpress.XtraRichEdit.RichEditRulerVisibility.Hidden;
            this.txtAttFile.Options.VerticalRuler.Visibility = DevExpress.XtraRichEdit.RichEditRulerVisibility.Hidden;
            this.txtAttFile.Size = new System.Drawing.Size(369, 199);
            this.txtAttFile.TabIndex = 16;
            // 
            // btnRemoveFile
            // 
            this.btnRemoveFile.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("btnRemoveFile.ImageOptions.Image")));
            this.btnRemoveFile.Location = new System.Drawing.Point(1078, 24);
            this.btnRemoveFile.MaximumSize = new System.Drawing.Size(85, 0);
            this.btnRemoveFile.MinimumSize = new System.Drawing.Size(85, 0);
            this.btnRemoveFile.Name = "btnRemoveFile";
            this.btnRemoveFile.Size = new System.Drawing.Size(85, 22);
            this.btnRemoveFile.StyleController = this.layoutControl1;
            this.btnRemoveFile.TabIndex = 15;
            this.btnRemoveFile.Text = "파일 삭제";
            this.btnRemoveFile.Click += new System.EventHandler(this.btnRemoveFile_Click);
            // 
            // btnOpenFile
            // 
            this.btnOpenFile.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("btnOpenFile.ImageOptions.Image")));
            this.btnOpenFile.Location = new System.Drawing.Point(989, 24);
            this.btnOpenFile.MaximumSize = new System.Drawing.Size(85, 0);
            this.btnOpenFile.MinimumSize = new System.Drawing.Size(85, 0);
            this.btnOpenFile.Name = "btnOpenFile";
            this.btnOpenFile.Size = new System.Drawing.Size(85, 22);
            this.btnOpenFile.StyleController = this.layoutControl1;
            this.btnOpenFile.TabIndex = 14;
            this.btnOpenFile.Text = "파일 첨부";
            this.btnOpenFile.Click += new System.EventHandler(this.btnOpenFile_Click);
            // 
            // txtSystemPrompt
            // 
            this.txtSystemPrompt.ActiveViewType = DevExpress.XtraRichEdit.RichEditViewType.Simple;
            this.txtSystemPrompt.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.txtSystemPrompt.EnableToolTips = false;
            this.txtSystemPrompt.LayoutUnit = DevExpress.XtraRichEdit.DocumentLayoutUnit.Pixel;
            this.txtSystemPrompt.Location = new System.Drawing.Point(980, 456);
            this.txtSystemPrompt.Name = "txtSystemPrompt";
            this.txtSystemPrompt.Options.DocumentSaveOptions.CurrentFormat = DevExpress.XtraRichEdit.DocumentFormat.PlainText;
            this.txtSystemPrompt.Options.HorizontalRuler.Visibility = DevExpress.XtraRichEdit.RichEditRulerVisibility.Hidden;

            this.txtSystemPrompt.Options.VerticalRuler.Visibility = DevExpress.XtraRichEdit.RichEditRulerVisibility.Hidden;
            this.txtSystemPrompt.Size = new System.Drawing.Size(378, 127);
            this.txtSystemPrompt.TabIndex = 13;
            this.txtSystemPrompt.Text = resources.GetString("txtSystemPrompt.Text");
            // 
            // lstFiles
            // 
            this.lstFiles.Location = new System.Drawing.Point(989, 50);
            this.lstFiles.Name = "lstFiles";
            this.lstFiles.Size = new System.Drawing.Size(369, 122);
            this.lstFiles.StyleController = this.layoutControl1;
            this.lstFiles.TabIndex = 12;
            // 
            // lblStatus
            // 
            this.lblStatus.Location = new System.Drawing.Point(35, 599);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(4, 20);
            this.lblStatus.StyleController = this.layoutControl1;
            this.lblStatus.TabIndex = 10;
            this.lblStatus.Text = " ";
            // 
            // btnAnalyze
            // 
            this.btnAnalyze.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("btnAnalyze.ImageOptions.Image")));
            this.btnAnalyze.ImageOptions.Location = DevExpress.XtraEditors.ImageLocation.MiddleCenter;
            this.btnAnalyze.Location = new System.Drawing.Point(1340, 599);
            this.btnAnalyze.MaximumSize = new System.Drawing.Size(30, 0);
            this.btnAnalyze.MinimumSize = new System.Drawing.Size(30, 0);
            this.btnAnalyze.Name = "btnAnalyze";
            this.btnAnalyze.Size = new System.Drawing.Size(30, 22);
            this.btnAnalyze.StyleController = this.layoutControl1;
            this.btnAnalyze.TabIndex = 9;
            this.btnAnalyze.Text = " ";
            // 
            // txtResult
            // 
            this.txtResult.ActiveViewType = DevExpress.XtraRichEdit.RichEditViewType.Simple;
            this.txtResult.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.txtResult.EnableToolTips = false;
            this.txtResult.LayoutUnit = DevExpress.XtraRichEdit.DocumentLayoutUnit.Pixel;
            this.txtResult.Location = new System.Drawing.Point(24, 48);
            this.txtResult.Name = "txtResult";
            this.txtResult.Options.DocumentSaveOptions.CurrentFormat = DevExpress.XtraRichEdit.DocumentFormat.PlainText;
            this.txtResult.Options.HorizontalRuler.Visibility = DevExpress.XtraRichEdit.RichEditRulerVisibility.Hidden;
            this.txtResult.Options.VerticalRuler.Visibility = DevExpress.XtraRichEdit.RichEditRulerVisibility.Hidden;
            this.txtResult.ReadOnly = true;
            this.txtResult.Size = new System.Drawing.Size(927, 327);
            this.txtResult.TabIndex = 8;
            // 
            // txtQuest
            // 
            this.txtQuest.ActiveViewType = DevExpress.XtraRichEdit.RichEditViewType.Simple;
            this.txtQuest.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.txtQuest.EnableToolTips = false;
            this.txtQuest.LayoutUnit = DevExpress.XtraRichEdit.DocumentLayoutUnit.Pixel;
            this.txtQuest.Location = new System.Drawing.Point(24, 432);
            this.txtQuest.Name = "txtQuest";
            this.txtQuest.Options.DocumentSaveOptions.CurrentFormat = DevExpress.XtraRichEdit.DocumentFormat.PlainText;
            this.txtQuest.Options.HorizontalRuler.Visibility = DevExpress.XtraRichEdit.RichEditRulerVisibility.Hidden;
            this.txtQuest.Options.VerticalRuler.Visibility = DevExpress.XtraRichEdit.RichEditRulerVisibility.Hidden;
            this.txtQuest.Size = new System.Drawing.Size(942, 151);
            this.txtQuest.TabIndex = 7;
            // 
            // Root
            // 
            this.Root.EnableIndentsWithoutBorders = DevExpress.Utils.DefaultBoolean.True;
            this.Root.GroupBordersVisible = false;
            this.Root.Items.AddRange(new DevExpress.XtraLayout.BaseLayoutItem[] {
            this.splitterItem1,
            this.layoutControlItem2,
            this.emptySpaceItem1,
            this.layoutControlItem3,
            this.layoutControlGroup1,
            this.layoutControlGroup2,
            this.splitterItem2,
            this.layoutControlGroup3});
            this.Root.Name = "Root";
            this.Root.Size = new System.Drawing.Size(1382, 633);
            this.Root.TextVisible = false;
            // 
            // splitterItem1
            // 
            this.splitterItem1.AllowHotTrack = true;
            this.splitterItem1.Location = new System.Drawing.Point(0, 379);
            this.splitterItem1.Name = "splitterItem1";
            this.splitterItem1.Size = new System.Drawing.Size(1362, 10);
            // 
            // layoutControlItem2
            // 
            this.layoutControlItem2.Control = this.btnAnalyze;
            this.layoutControlItem2.Location = new System.Drawing.Point(1328, 587);
            this.layoutControlItem2.Name = "layoutControlItem2";
            this.layoutControlItem2.Size = new System.Drawing.Size(34, 26);
            this.layoutControlItem2.TextSize = new System.Drawing.Size(0, 0);
            this.layoutControlItem2.TextVisible = false;
            // 
            // emptySpaceItem1
            // 
            this.emptySpaceItem1.AllowHotTrack = false;
            this.emptySpaceItem1.Location = new System.Drawing.Point(31, 587);
            this.emptySpaceItem1.Name = "emptySpaceItem1";
            this.emptySpaceItem1.Size = new System.Drawing.Size(1297, 26);
            this.emptySpaceItem1.TextSize = new System.Drawing.Size(0, 0);
            // 
            // layoutControlItem3
            // 
            this.layoutControlItem3.Control = this.lblStatus;
            this.layoutControlItem3.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("layoutControlItem3.ImageOptions.Image")));
            this.layoutControlItem3.Location = new System.Drawing.Point(0, 587);
            this.layoutControlItem3.Name = "layoutControlItem3";
            this.layoutControlItem3.Size = new System.Drawing.Size(31, 26);
            this.layoutControlItem3.Text = " ";
            this.layoutControlItem3.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
            this.layoutControlItem3.TextSize = new System.Drawing.Size(18, 20);
            this.layoutControlItem3.TextToControlDistance = 5;
            // 
            // layoutControlGroup1
            // 
            this.layoutControlGroup1.Items.AddRange(new DevExpress.XtraLayout.BaseLayoutItem[] {
            this.layoutControlItem6,
            this.layoutControlItem5,
            this.layoutControlItem8,
            this.emptySpaceItem3,
            this.layoutControlItem9});
            this.layoutControlGroup1.Location = new System.Drawing.Point(965, 0);
            this.layoutControlGroup1.Name = "layoutControlGroup1";
            this.layoutControlGroup1.Size = new System.Drawing.Size(397, 379);
            this.layoutControlGroup1.TextVisible = false;
            // 
            // layoutControlItem6
            // 
            this.layoutControlItem6.Control = this.lstFiles;
            this.layoutControlItem6.Location = new System.Drawing.Point(0, 26);
            this.layoutControlItem6.Name = "layoutControlItem6";
            this.layoutControlItem6.Size = new System.Drawing.Size(373, 126);
            this.layoutControlItem6.Text = "더블 클릭으로 파일 ";
            this.layoutControlItem6.TextLocation = DevExpress.Utils.Locations.Top;
            this.layoutControlItem6.TextSize = new System.Drawing.Size(0, 0);
            this.layoutControlItem6.TextVisible = false;
            // 
            // layoutControlItem5
            // 
            this.layoutControlItem5.Control = this.btnOpenFile;
            this.layoutControlItem5.Location = new System.Drawing.Point(0, 0);
            this.layoutControlItem5.Name = "layoutControlItem5";
            this.layoutControlItem5.Size = new System.Drawing.Size(89, 26);
            this.layoutControlItem5.TextSize = new System.Drawing.Size(0, 0);
            this.layoutControlItem5.TextVisible = false;
            // 
            // layoutControlItem8
            // 
            this.layoutControlItem8.Control = this.btnRemoveFile;
            this.layoutControlItem8.Location = new System.Drawing.Point(89, 0);
            this.layoutControlItem8.Name = "layoutControlItem8";
            this.layoutControlItem8.Size = new System.Drawing.Size(89, 26);
            this.layoutControlItem8.TextSize = new System.Drawing.Size(0, 0);
            this.layoutControlItem8.TextVisible = false;
            // 
            // emptySpaceItem3
            // 
            this.emptySpaceItem3.AllowHotTrack = false;
            this.emptySpaceItem3.Location = new System.Drawing.Point(178, 0);
            this.emptySpaceItem3.Name = "emptySpaceItem3";
            this.emptySpaceItem3.Size = new System.Drawing.Size(195, 26);
            this.emptySpaceItem3.TextSize = new System.Drawing.Size(0, 0);
            // 
            // layoutControlItem9
            // 
            this.layoutControlItem9.Control = this.txtAttFile;
            this.layoutControlItem9.Location = new System.Drawing.Point(0, 152);
            this.layoutControlItem9.Name = "layoutControlItem9";
            this.layoutControlItem9.Size = new System.Drawing.Size(373, 203);
            this.layoutControlItem9.TextSize = new System.Drawing.Size(0, 0);
            this.layoutControlItem9.TextVisible = false;
            // 
            // layoutControlGroup2
            // 
            this.layoutControlGroup2.Items.AddRange(new DevExpress.XtraLayout.BaseLayoutItem[] {
            this.layoutControlItem1,
            this.layoutControlItem11,
            this.emptySpaceItem2,
            this.layAiThink,
            this.layoutControlItem13});
            this.layoutControlGroup2.Location = new System.Drawing.Point(0, 0);
            this.layoutControlGroup2.Name = "layoutControlGroup2";
            this.layoutControlGroup2.Size = new System.Drawing.Size(955, 379);
            this.layoutControlGroup2.TextVisible = false;
            // 
            // layoutControlItem1
            // 
            this.layoutControlItem1.Control = this.txtResult;
            this.layoutControlItem1.Location = new System.Drawing.Point(0, 24);
            this.layoutControlItem1.Name = "layoutControlItem1";
            this.layoutControlItem1.Size = new System.Drawing.Size(931, 331);
            this.layoutControlItem1.TextSize = new System.Drawing.Size(0, 0);
            this.layoutControlItem1.TextVisible = false;
            // 
            // layoutControlItem11
            // 
            this.layoutControlItem11.Control = this.cboModelList;
            this.layoutControlItem11.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("layoutControlItem11.ImageOptions.Image")));
            this.layoutControlItem11.Location = new System.Drawing.Point(0, 0);
            this.layoutControlItem11.Name = "layoutControlItem11";
            this.layoutControlItem11.Size = new System.Drawing.Size(284, 24);
            this.layoutControlItem11.Text = "AI Model";
            this.layoutControlItem11.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
            this.layoutControlItem11.TextSize = new System.Drawing.Size(75, 20);
            this.layoutControlItem11.TextToControlDistance = 5;
            // 
            // emptySpaceItem2
            // 
            this.emptySpaceItem2.AllowHotTrack = false;
            this.emptySpaceItem2.Location = new System.Drawing.Point(284, 0);
            this.emptySpaceItem2.Name = "emptySpaceItem2";
            this.emptySpaceItem2.Size = new System.Drawing.Size(434, 24);
            this.emptySpaceItem2.TextSize = new System.Drawing.Size(0, 0);
            // 
            // layAiThink
            // 
            this.layAiThink.Control = this.mproAiThink;
            this.layAiThink.Location = new System.Drawing.Point(827, 0);
            this.layAiThink.Name = "layAiThink";
            this.layAiThink.Size = new System.Drawing.Size(104, 24);
            this.layAiThink.Spacing = new DevExpress.XtraLayout.Utils.Padding(0, 0, 1, 0);
            this.layAiThink.TextSize = new System.Drawing.Size(0, 0);
            this.layAiThink.TextVisible = false;
            // 
            // layoutControlItem13
            // 
            this.layoutControlItem13.AppearanceItemCaption.Font = new System.Drawing.Font("굴림", 9F, System.Drawing.FontStyle.Bold);
            this.layoutControlItem13.AppearanceItemCaption.Options.UseFont = true;
            this.layoutControlItem13.Control = this.prgGpuUsage;
            this.layoutControlItem13.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("layoutControlItem13.ImageOptions.Image")));
            this.layoutControlItem13.Location = new System.Drawing.Point(718, 0);
            this.layoutControlItem13.Name = "layoutControlItem13";
            this.layoutControlItem13.Size = new System.Drawing.Size(109, 24);
            this.layoutControlItem13.Text = "GPU";
            this.layoutControlItem13.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
            this.layoutControlItem13.TextSize = new System.Drawing.Size(50, 20);
            this.layoutControlItem13.TextToControlDistance = 5;
            // 
            // splitterItem2
            // 
            this.splitterItem2.AllowHotTrack = true;
            this.splitterItem2.Location = new System.Drawing.Point(955, 0);
            this.splitterItem2.Name = "splitterItem2";
            this.splitterItem2.Size = new System.Drawing.Size(10, 379);
            // 
            // layoutControlGroup3
            // 
            this.layoutControlGroup3.Items.AddRange(new DevExpress.XtraLayout.BaseLayoutItem[] {
            this.layoutControlItem4,
            this.layoutControlItem7,
            this.splitterItem3,
            this.layoutControlItem10});
            this.layoutControlGroup3.Location = new System.Drawing.Point(0, 389);
            this.layoutControlGroup3.Name = "layoutControlGroup3";
            this.layoutControlGroup3.Size = new System.Drawing.Size(1362, 198);
            this.layoutControlGroup3.TextVisible = false;
            // 
            // layoutControlItem4
            // 
            this.layoutControlItem4.Control = this.txtQuest;
            this.layoutControlItem4.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("layoutControlItem4.ImageOptions.Image")));
            this.layoutControlItem4.Location = new System.Drawing.Point(0, 0);
            this.layoutControlItem4.Name = "layoutControlItem4";
            this.layoutControlItem4.Size = new System.Drawing.Size(946, 174);
            this.layoutControlItem4.Text = "질문 입력";
            this.layoutControlItem4.TextLocation = DevExpress.Utils.Locations.Top;
            this.layoutControlItem4.TextSize = new System.Drawing.Size(109, 16);
            // 
            // layoutControlItem7
            // 
            this.layoutControlItem7.Control = this.txtSystemPrompt;
            this.layoutControlItem7.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("layoutControlItem7.ImageOptions.Image")));
            this.layoutControlItem7.Location = new System.Drawing.Point(956, 24);
            this.layoutControlItem7.Name = "layoutControlItem7";
            this.layoutControlItem7.Size = new System.Drawing.Size(382, 150);
            this.layoutControlItem7.Text = "시스템 프롬프트";
            this.layoutControlItem7.TextLocation = DevExpress.Utils.Locations.Top;
            this.layoutControlItem7.TextSize = new System.Drawing.Size(109, 16);
            // 
            // splitterItem3
            // 
            this.splitterItem3.AllowHotTrack = true;
            this.splitterItem3.Location = new System.Drawing.Point(946, 0);
            this.splitterItem3.Name = "splitterItem3";
            this.splitterItem3.Size = new System.Drawing.Size(10, 174);
            // 
            // layoutControlItem10
            // 
            this.layoutControlItem10.Control = this.chkUsePrompt;
            this.layoutControlItem10.Location = new System.Drawing.Point(956, 0);
            this.layoutControlItem10.Name = "layoutControlItem10";
            this.layoutControlItem10.Size = new System.Drawing.Size(382, 24);
            this.layoutControlItem10.TextSize = new System.Drawing.Size(0, 0);
            this.layoutControlItem10.TextVisible = false;
            // 
            // AIChat
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.layoutControl1);
            this.Name = "AIChat";
            this.Size = new System.Drawing.Size(1382, 633);
            ((System.ComponentModel.ISupportInitialize)(this.layoutControl1)).EndInit();
            this.layoutControl1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.prgGpuUsage.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.mproAiThink.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.cboModelList.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.chkUsePrompt.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.lstFiles)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.Root)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitterItem1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItem2)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.emptySpaceItem1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItem3)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlGroup1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItem6)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItem5)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItem8)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.emptySpaceItem3)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItem9)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlGroup2)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItem1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItem11)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.emptySpaceItem2)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.layAiThink)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItem13)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitterItem2)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlGroup3)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItem4)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItem7)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitterItem3)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItem10)).EndInit();
            this.ResumeLayout(false);

        }

        private DevExpress.XtraLayout.LayoutControl layoutControl1;
        private DevExpress.XtraLayout.LayoutControlGroup Root;
        private DevExpress.XtraLayout.SplitterItem splitterItem1;
        private RichEditControl txtQuest;
        private DevExpress.XtraLayout.LayoutControlItem layoutControlItem4;
        private RichEditControl txtResult;
        private DevExpress.XtraLayout.LayoutControlItem layoutControlItem1;
        private DevExpress.XtraEditors.SimpleButton btnAnalyze;
        private DevExpress.XtraLayout.LayoutControlItem layoutControlItem2;
        private DevExpress.XtraLayout.EmptySpaceItem emptySpaceItem1;
        private DevExpress.XtraEditors.LabelControl lblStatus;
        private DevExpress.XtraLayout.LayoutControlItem layoutControlItem3;
        private RichEditControl txtSystemPrompt;
        private DevExpress.XtraEditors.ListBoxControl lstFiles;
        private DevExpress.XtraLayout.LayoutControlGroup layoutControlGroup1;
        private DevExpress.XtraLayout.LayoutControlItem layoutControlItem6;
        private DevExpress.XtraLayout.LayoutControlGroup layoutControlGroup2;
        private DevExpress.XtraLayout.SplitterItem splitterItem2;
        private DevExpress.XtraLayout.LayoutControlGroup layoutControlGroup3;
        private DevExpress.XtraLayout.LayoutControlItem layoutControlItem7;
        private DevExpress.XtraLayout.SplitterItem splitterItem3;
        private DevExpress.XtraEditors.SimpleButton btnRemoveFile;
        private DevExpress.XtraEditors.SimpleButton btnOpenFile;
        private DevExpress.XtraLayout.LayoutControlItem layoutControlItem5;
        private DevExpress.XtraLayout.LayoutControlItem layoutControlItem8;
        private DevExpress.XtraLayout.EmptySpaceItem emptySpaceItem3;
        private RichEditControl txtAttFile;
        private DevExpress.XtraLayout.LayoutControlItem layoutControlItem9;
        private DevExpress.XtraEditors.CheckEdit chkUsePrompt;
        private DevExpress.XtraLayout.LayoutControlItem layoutControlItem10;
        private DevExpress.XtraEditors.ImageComboBoxEdit cboModelList;
        private DevExpress.XtraLayout.LayoutControlItem layoutControlItem11;
        private DevExpress.XtraLayout.EmptySpaceItem emptySpaceItem2;
        private DevExpress.XtraEditors.MarqueeProgressBarControl mproAiThink;
        private DevExpress.XtraLayout.LayoutControlItem layAiThink;
        private DevExpress.XtraEditors.ProgressBarControl prgGpuUsage;
        private DevExpress.XtraLayout.LayoutControlItem layoutControlItem13;
    }
}
