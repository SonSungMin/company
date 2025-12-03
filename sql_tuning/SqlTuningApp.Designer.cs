namespace DevTools.UI.Control
{
    partial class SqlTuningApp
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        private void InitializeComponent()
        {
            this.layoutControl1 = new DevExpress.XtraLayout.LayoutControl();
            this.lblStatus = new DevExpress.XtraEditors.LabelControl();
            this.progressBarQuery = new DevExpress.XtraEditors.MarqueeProgressBarControl();
            this.xtraTabControl1 = new DevExpress.XtraTab.XtraTabControl();
            this.xtraTabPageResults = new DevExpress.XtraTab.XtraTabPage();
            this.gridControlResults = new DevExpress.XtraGrid.GridControl();
            this.gridViewResults = new DevExpress.XtraGrid.Views.Grid.GridView();
            this.xtraTabPageTables = new DevExpress.XtraTab.XtraTabPage();
            this.gridControlTables = new DevExpress.XtraGrid.GridControl();
            this.gridViewTables = new DevExpress.XtraGrid.Views.Grid.GridView();
            this.colOwner = new DevExpress.XtraGrid.Columns.GridColumn();
            this.colTableName = new DevExpress.XtraGrid.Columns.GridColumn();
            this.colTableDesc = new DevExpress.XtraGrid.Columns.GridColumn();
            this.colRowCount = new DevExpress.XtraGrid.Columns.GridColumn();
            this.xtraTabPageAnalysis = new DevExpress.XtraTab.XtraTabPage();
            this.memoAnalysis = new DevExpress.XtraEditors.MemoEdit();
            this.xtraTabPagePlan = new DevExpress.XtraTab.XtraTabPage();
            this.treeListPlan = new DevExpress.XtraTreeList.TreeList();
            this.colExecutionOrder = new DevExpress.XtraTreeList.Columns.TreeListColumn();
            this.colOperation = new DevExpress.XtraTreeList.Columns.TreeListColumn();
            this.colOptions = new DevExpress.XtraTreeList.Columns.TreeListColumn();
            this.colObject = new DevExpress.XtraTreeList.Columns.TreeListColumn();
            this.colCost = new DevExpress.XtraTreeList.Columns.TreeListColumn();
            this.colCardinality = new DevExpress.XtraTreeList.Columns.TreeListColumn();
            this.colId = new DevExpress.XtraTreeList.Columns.TreeListColumn();
            this.colAdvice = new DevExpress.XtraTreeList.Columns.TreeListColumn();
            this.repoMemoAdvice = new DevExpress.XtraEditors.Repository.RepositoryItemMemoEdit();
            this.xtraTabPageMessages = new DevExpress.XtraTab.XtraTabPage();
            this.memoMessages = new DevExpress.XtraEditors.MemoEdit();
            this.btnExecute = new DevExpress.XtraEditors.SimpleButton();
            this.memoSql = new DevExpress.XtraRichEdit.RichEditControl();
            this.Root = new DevExpress.XtraLayout.LayoutControlGroup();
            this.layoutControlGroupQuery = new DevExpress.XtraLayout.LayoutControlGroup();
            this.layoutItemSqlMemo = new DevExpress.XtraLayout.LayoutControlItem();
            this.layoutItemExecuteButton = new DevExpress.XtraLayout.LayoutControlItem();
            this.splitterItem1 = new DevExpress.XtraLayout.SplitterItem();
            this.layoutControlItemResults = new DevExpress.XtraLayout.LayoutControlItem();
            this.layoutControlItemStatus = new DevExpress.XtraLayout.LayoutControlItem();
            this.layoutControlItemProgress = new DevExpress.XtraLayout.LayoutControlItem();
            this.emptySpaceItem1 = new DevExpress.XtraLayout.EmptySpaceItem();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControl1)).BeginInit();
            this.layoutControl1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.progressBarQuery.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.xtraTabControl1)).BeginInit();
            this.xtraTabControl1.SuspendLayout();
            this.xtraTabPageResults.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridControlResults)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewResults)).BeginInit();
            this.xtraTabPageTables.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridControlTables)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewTables)).BeginInit();
            this.xtraTabPageAnalysis.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.memoAnalysis.Properties)).BeginInit();
            this.xtraTabPagePlan.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.treeListPlan)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.repoMemoAdvice)).BeginInit();
            this.xtraTabPageMessages.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.memoMessages.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.Root)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlGroupQuery)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutItemSqlMemo)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutItemExecuteButton)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitterItem1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItemResults)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItemStatus)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItemProgress)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.emptySpaceItem1)).BeginInit();
            this.SuspendLayout();
            // 
            // layoutControl1
            // 
            this.layoutControl1.Controls.Add(this.lblStatus);
            this.layoutControl1.Controls.Add(this.progressBarQuery);
            this.layoutControl1.Controls.Add(this.xtraTabControl1);
            this.layoutControl1.Controls.Add(this.btnExecute);
            this.layoutControl1.Controls.Add(this.memoSql);
            this.layoutControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.layoutControl1.Location = new System.Drawing.Point(0, 0);
            this.layoutControl1.Name = "layoutControl1";
            this.layoutControl1.Root = this.Root;
            this.layoutControl1.Size = new System.Drawing.Size(1202, 726);
            this.layoutControl1.TabIndex = 0;
            this.layoutControl1.Text = "layoutControl1";
            // 
            // lblStatus
            // 
            this.lblStatus.Location = new System.Drawing.Point(12, 700);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(992, 14);
            this.lblStatus.StyleController = this.layoutControl1;
            this.lblStatus.TabIndex = 11;
            this.lblStatus.Text = "상태: 대기 중";
            // 
            // progressBarQuery
            // 
            this.progressBarQuery.EditValue = 0;
            this.progressBarQuery.Location = new System.Drawing.Point(1008, 698);
            this.progressBarQuery.Name = "progressBarQuery";
            this.progressBarQuery.Properties.MarqueeAnimationSpeed = 50;
            this.progressBarQuery.Size = new System.Drawing.Size(182, 18);
            this.progressBarQuery.StyleController = this.layoutControl1;
            this.progressBarQuery.TabIndex = 12;
            this.progressBarQuery.Visible = false;
            // 
            // xtraTabControl1
            // 
            this.xtraTabControl1.Location = new System.Drawing.Point(12, 496);
            this.xtraTabControl1.Name = "xtraTabControl1";
            this.xtraTabControl1.SelectedTabPage = this.xtraTabPageResults;
            this.xtraTabControl1.Size = new System.Drawing.Size(1178, 200);
            this.xtraTabControl1.TabIndex = 10;
            this.xtraTabControl1.TabPages.AddRange(new DevExpress.XtraTab.XtraTabPage[] {
            this.xtraTabPageResults,
            this.xtraTabPageTables,
            this.xtraTabPageAnalysis,
            this.xtraTabPagePlan,
            this.xtraTabPageMessages});
            // 
            // xtraTabPageResults
            // 
            this.xtraTabPageResults.Controls.Add(this.gridControlResults);
            this.xtraTabPageResults.Name = "xtraTabPageResults";
            this.xtraTabPageResults.Size = new System.Drawing.Size(1172, 171);
            this.xtraTabPageResults.Text = "실행 결과 (Grid)";
            // 
            // gridControlResults
            // 
            this.gridControlResults.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridControlResults.Location = new System.Drawing.Point(0, 0);
            this.gridControlResults.MainView = this.gridViewResults;
            this.gridControlResults.Name = "gridControlResults";
            this.gridControlResults.Size = new System.Drawing.Size(1172, 171);
            this.gridControlResults.TabIndex = 0;
            this.gridControlResults.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.gridViewResults});
            // 
            // gridViewResults
            // 
            this.gridViewResults.DetailHeight = 323;
            this.gridViewResults.GridControl = this.gridControlResults;
            this.gridViewResults.Name = "gridViewResults";
            this.gridViewResults.OptionsBehavior.Editable = false;
            this.gridViewResults.OptionsView.ColumnAutoWidth = false;
            this.gridViewResults.OptionsView.ShowAutoFilterRow = true;
            this.gridViewResults.OptionsView.ShowGroupPanel = false;
            // 
            // xtraTabPageTables
            // 
            this.xtraTabPageTables.Controls.Add(this.gridControlTables);
            this.xtraTabPageTables.Name = "xtraTabPageTables";
            this.xtraTabPageTables.Size = new System.Drawing.Size(1172, 171);
            this.xtraTabPageTables.Text = "테이블 정보";
            // 
            // gridControlTables
            // 
            this.gridControlTables.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridControlTables.Location = new System.Drawing.Point(0, 0);
            this.gridControlTables.MainView = this.gridViewTables;
            this.gridControlTables.Name = "gridControlTables";
            this.gridControlTables.Size = new System.Drawing.Size(1172, 171);
            this.gridControlTables.TabIndex = 0;
            this.gridControlTables.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.gridViewTables});
            // 
            // gridViewTables
            // 
            this.gridViewTables.Columns.AddRange(new DevExpress.XtraGrid.Columns.GridColumn[] {
            this.colOwner,
            this.colTableName,
            this.colTableDesc,
            this.colRowCount});
            this.gridViewTables.DetailHeight = 323;
            this.gridViewTables.GridControl = this.gridControlTables;
            this.gridViewTables.Name = "gridViewTables";
            this.gridViewTables.OptionsBehavior.Editable = false;
            this.gridViewTables.OptionsView.ColumnAutoWidth = false;
            this.gridViewTables.OptionsView.ShowAutoFilterRow = true;
            this.gridViewTables.OptionsView.ShowGroupPanel = false;
            // 
            // colOwner
            // 
            this.colOwner.Caption = "Owner";
            this.colOwner.FieldName = "Owner";
            this.colOwner.Name = "colOwner";
            this.colOwner.Visible = true;
            this.colOwner.VisibleIndex = 0;
            this.colOwner.Width = 100;
            // 
            // colTableName
            // 
            this.colTableName.Caption = "Table Name";
            this.colTableName.FieldName = "TableName";
            this.colTableName.Name = "colTableName";
            this.colTableName.Visible = true;
            this.colTableName.VisibleIndex = 1;
            this.colTableName.Width = 150;
            // 
            // colTableDesc
            // 
            this.colTableDesc.Caption = "Table Desc";
            this.colTableDesc.FieldName = "TableDesc";
            this.colTableDesc.Name = "colTableDesc";
            this.colTableDesc.Visible = true;
            this.colTableDesc.VisibleIndex = 2;
            this.colTableDesc.Width = 200;
            // 
            // colRowCount
            // 
            this.colRowCount.Caption = "Rows";
            this.colRowCount.DisplayFormat.FormatString = "N0";
            this.colRowCount.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
            this.colRowCount.FieldName = "RowCount";
            this.colRowCount.Name = "colRowCount";
            this.colRowCount.Visible = true;
            this.colRowCount.VisibleIndex = 3;
            this.colRowCount.Width = 100;
            // 
            // xtraTabPageAnalysis
            // 
            this.xtraTabPageAnalysis.Controls.Add(this.memoAnalysis);
            this.xtraTabPageAnalysis.Name = "xtraTabPageAnalysis";
            this.xtraTabPageAnalysis.Size = new System.Drawing.Size(1172, 171);
            this.xtraTabPageAnalysis.Text = "튜닝 분석 리포트";
            // 
            // memoAnalysis
            // 
            this.memoAnalysis.Dock = System.Windows.Forms.DockStyle.Fill;
            this.memoAnalysis.Location = new System.Drawing.Point(0, 0);
            this.memoAnalysis.Name = "memoAnalysis";
            this.memoAnalysis.Properties.ReadOnly = true;
            this.memoAnalysis.Size = new System.Drawing.Size(1172, 171);
            this.memoAnalysis.TabIndex = 0;
            // 
            // xtraTabPagePlan
            // 
            this.xtraTabPagePlan.Controls.Add(this.treeListPlan);
            this.xtraTabPagePlan.Name = "xtraTabPagePlan";
            this.xtraTabPagePlan.Size = new System.Drawing.Size(1172, 171);
            this.xtraTabPagePlan.Text = "실행 계획 (Plan)";
            // 
            // treeListPlan
            // 
            this.treeListPlan.Columns.AddRange(new DevExpress.XtraTreeList.Columns.TreeListColumn[] {
            this.colExecutionOrder,
            this.colOperation,
            this.colOptions,
            this.colObject,
            this.colCost,
            this.colCardinality,
            this.colId,
            this.colAdvice});
            this.treeListPlan.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeListPlan.KeyFieldName = "Id";
            this.treeListPlan.Location = new System.Drawing.Point(0, 0);
            this.treeListPlan.Name = "treeListPlan";
            this.treeListPlan.OptionsBehavior.AutoNodeHeight = true;
            this.treeListPlan.OptionsBehavior.Editable = false;
            this.treeListPlan.OptionsView.AutoWidth = false;
            this.treeListPlan.OptionsView.ShowAutoFilterRow = true;
            this.treeListPlan.OptionsView.ShowIndicator = false;
            this.treeListPlan.ParentFieldName = "ParentId";
            this.treeListPlan.RepositoryItems.AddRange(new DevExpress.XtraEditors.Repository.RepositoryItem[] {
            this.repoMemoAdvice});
            this.treeListPlan.RootValue = -1;
            this.treeListPlan.Size = new System.Drawing.Size(1172, 171);
            this.treeListPlan.TabIndex = 0;
            // 
            // colExecutionOrder
            // 
            this.colExecutionOrder.Caption = "Step";
            this.colExecutionOrder.FieldName = "ExecutionOrder";
            this.colExecutionOrder.Name = "colExecutionOrder";
            this.colExecutionOrder.Visible = true;
            this.colExecutionOrder.VisibleIndex = 0;
            this.colExecutionOrder.Width = 50;
            this.colExecutionOrder.AppearanceCell.Options.UseTextOptions = true;
            this.colExecutionOrder.AppearanceCell.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            // 
            // colOperation
            // 
            this.colOperation.Caption = "Operation";
            this.colOperation.FieldName = "Operation";
            this.colOperation.Name = "colOperation";
            this.colOperation.Visible = true;
            this.colOperation.VisibleIndex = 1;
            this.colOperation.Width = 220;
            // 
            // colOptions
            // 
            this.colOptions.Caption = "Options";
            this.colOptions.FieldName = "Options";
            this.colOptions.Name = "colOptions";
            this.colOptions.Visible = true;
            this.colOptions.VisibleIndex = 2;
            this.colOptions.Width = 150;
            // 
            // colObject
            // 
            this.colObject.Caption = "Object Name";
            this.colObject.FieldName = "ObjectName";
            this.colObject.Name = "colObject";
            this.colObject.Visible = true;
            this.colObject.VisibleIndex = 3;
            this.colObject.Width = 150;
            // 
            // colCost
            // 
            this.colCost.Caption = "Cost";
            this.colCost.FieldName = "Cost";
            this.colCost.Format.FormatString = "N0";
            this.colCost.Format.FormatType = DevExpress.Utils.FormatType.Numeric;
            this.colCost.Name = "colCost";
            this.colCost.Visible = true;
            this.colCost.VisibleIndex = 4;
            this.colCost.Width = 80;
            // 
            // colCardinality
            // 
            this.colCardinality.Caption = "Rows";
            this.colCardinality.FieldName = "Cardinality";
            this.colCardinality.Format.FormatString = "N0";
            this.colCardinality.Format.FormatType = DevExpress.Utils.FormatType.Numeric;
            this.colCardinality.Name = "colCardinality";
            this.colCardinality.Visible = true;
            this.colCardinality.VisibleIndex = 5;
            this.colCardinality.Width = 80;
            // 
            // colId
            // 
            this.colId.Caption = "ID";
            this.colId.FieldName = "Id";
            this.colId.Name = "colId";
            this.colId.Visible = true;
            this.colId.VisibleIndex = 6;
            this.colId.Width = 40;
            // 
            // colAdvice
            // 
            this.colAdvice.Caption = "튜닝 제안 & 관련 쿼리";
            this.colAdvice.ColumnEdit = this.repoMemoAdvice;
            this.colAdvice.FieldName = "Advice";
            this.colAdvice.Name = "colAdvice";
            this.colAdvice.Visible = true;
            this.colAdvice.VisibleIndex = 7;
            this.colAdvice.Width = 300;
            // 
            // repoMemoAdvice
            // 
            this.repoMemoAdvice.Name = "repoMemoAdvice";
            // 
            // xtraTabPageMessages
            // 
            this.xtraTabPageMessages.Controls.Add(this.memoMessages);
            this.xtraTabPageMessages.Name = "xtraTabPageMessages";
            this.xtraTabPageMessages.Size = new System.Drawing.Size(1172, 171);
            this.xtraTabPageMessages.Text = "실행 로그";
            // 
            // memoMessages
            // 
            this.memoMessages.Dock = System.Windows.Forms.DockStyle.Fill;
            this.memoMessages.Location = new System.Drawing.Point(0, 0);
            this.memoMessages.Name = "memoMessages";
            this.memoMessages.Properties.ReadOnly = true;
            this.memoMessages.Size = new System.Drawing.Size(1172, 171);
            this.memoMessages.TabIndex = 0;
            // 
            // btnExecute
            // 
            this.btnExecute.Location = new System.Drawing.Point(24, 24);
            this.btnExecute.MaximumSize = new System.Drawing.Size(130, 0);
            this.btnExecute.MinimumSize = new System.Drawing.Size(130, 0);
            this.btnExecute.Name = "btnExecute";
            this.btnExecute.Size = new System.Drawing.Size(130, 22);
            this.btnExecute.StyleController = this.layoutControl1;
            this.btnExecute.TabIndex = 9;
            this.btnExecute.Text = "실행 (F5)";
            this.btnExecute.Click += new System.EventHandler(this.btnExecute_Click);
            // 
            // memoSql
            // 
            this.memoSql.ActiveViewType = DevExpress.XtraRichEdit.RichEditViewType.Simple;
            this.memoSql.LayoutUnit = DevExpress.XtraRichEdit.DocumentLayoutUnit.Pixel;
            this.memoSql.Location = new System.Drawing.Point(24, 50);
            this.memoSql.Name = "memoSql";
            this.memoSql.Size = new System.Drawing.Size(1154, 425);
            this.memoSql.TabIndex = 8;
            this.memoSql.KeyDown += new System.Windows.Forms.KeyEventHandler(this.memoSql_KeyDown);
            // 
            // Root
            // 
            this.Root.EnableIndentsWithoutBorders = DevExpress.Utils.DefaultBoolean.True;
            this.Root.GroupBordersVisible = false;
            this.Root.Items.AddRange(new DevExpress.XtraLayout.BaseLayoutItem[] {
            this.layoutControlGroupQuery,
            this.splitterItem1,
            this.layoutControlItemResults,
            this.layoutControlItemStatus,
            this.layoutControlItemProgress});
            this.Root.Name = "Root";
            this.Root.Size = new System.Drawing.Size(1202, 726);
            this.Root.TextVisible = false;
            // 
            // layoutControlGroupQuery
            // 
            this.layoutControlGroupQuery.Items.AddRange(new DevExpress.XtraLayout.BaseLayoutItem[] {
            this.layoutItemSqlMemo,
            this.layoutItemExecuteButton,
            this.emptySpaceItem1});
            this.layoutControlGroupQuery.Location = new System.Drawing.Point(0, 0);
            this.layoutControlGroupQuery.Name = "layoutControlGroupQuery";
            this.layoutControlGroupQuery.Size = new System.Drawing.Size(1182, 479);
            this.layoutControlGroupQuery.Text = "SQL 편집기";
            this.layoutControlGroupQuery.TextVisible = false;
            // 
            // layoutItemSqlMemo
            // 
            this.layoutItemSqlMemo.Control = this.memoSql;
            this.layoutItemSqlMemo.Location = new System.Drawing.Point(0, 26);
            this.layoutItemSqlMemo.Name = "layoutItemSqlMemo";
            this.layoutItemSqlMemo.Size = new System.Drawing.Size(1158, 429);
            this.layoutItemSqlMemo.TextSize = new System.Drawing.Size(0, 0);
            this.layoutItemSqlMemo.TextVisible = false;
            // 
            // layoutItemExecuteButton
            // 
            this.layoutItemExecuteButton.Control = this.btnExecute;
            this.layoutItemExecuteButton.ControlAlignment = System.Drawing.ContentAlignment.MiddleRight;
            this.layoutItemExecuteButton.Location = new System.Drawing.Point(0, 0);
            this.layoutItemExecuteButton.MaxSize = new System.Drawing.Size(0, 26);
            this.layoutItemExecuteButton.MinSize = new System.Drawing.Size(92, 26);
            this.layoutItemExecuteButton.Name = "layoutItemExecuteButton";
            this.layoutItemExecuteButton.Size = new System.Drawing.Size(133, 26);
            this.layoutItemExecuteButton.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
            this.layoutItemExecuteButton.TextSize = new System.Drawing.Size(0, 0);
            this.layoutItemExecuteButton.TextVisible = false;
            // 
            // splitterItem1
            // 
            this.splitterItem1.AllowHotTrack = true;
            this.splitterItem1.Location = new System.Drawing.Point(0, 479);
            this.splitterItem1.Name = "splitterItem1";
            this.splitterItem1.Size = new System.Drawing.Size(1182, 5);
            // 
            // layoutControlItemResults
            // 
            this.layoutControlItemResults.Control = this.xtraTabControl1;
            this.layoutControlItemResults.Location = new System.Drawing.Point(0, 484);
            this.layoutControlItemResults.Name = "layoutControlItemResults";
            this.layoutControlItemResults.Size = new System.Drawing.Size(1182, 204);
            this.layoutControlItemResults.TextSize = new System.Drawing.Size(0, 0);
            this.layoutControlItemResults.TextVisible = false;
            // 
            // layoutControlItemStatus
            // 
            this.layoutControlItemStatus.Control = this.lblStatus;
            this.layoutControlItemStatus.Location = new System.Drawing.Point(0, 688);
            this.layoutControlItemStatus.Name = "layoutControlItemStatus";
            this.layoutControlItemStatus.Size = new System.Drawing.Size(996, 22);
            this.layoutControlItemStatus.TextSize = new System.Drawing.Size(0, 0);
            this.layoutControlItemStatus.TextVisible = false;
            // 
            // layoutControlItemProgress
            // 
            this.layoutControlItemProgress.Control = this.progressBarQuery;
            this.layoutControlItemProgress.Location = new System.Drawing.Point(996, 688);
            this.layoutControlItemProgress.MaxSize = new System.Drawing.Size(200, 22);
            this.layoutControlItemProgress.MinSize = new System.Drawing.Size(100, 22);
            this.layoutControlItemProgress.Name = "layoutControlItemProgress";
            this.layoutControlItemProgress.Size = new System.Drawing.Size(186, 22);
            this.layoutControlItemProgress.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
            this.layoutControlItemProgress.TextSize = new System.Drawing.Size(0, 0);
            this.layoutControlItemProgress.TextVisible = false;
            // 
            // emptySpaceItem1
            // 
            this.emptySpaceItem1.AllowHotTrack = false;
            this.emptySpaceItem1.Location = new System.Drawing.Point(133, 0);
            this.emptySpaceItem1.Name = "emptySpaceItem1";
            this.emptySpaceItem1.Size = new System.Drawing.Size(1025, 26);
            this.emptySpaceItem1.TextSize = new System.Drawing.Size(0, 0);
            // 
            // SqlTuningApp
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.layoutControl1);
            this.Name = "SqlTuningApp";
            this.Size = new System.Drawing.Size(1202, 726);
            ((System.ComponentModel.ISupportInitialize)(this.layoutControl1)).EndInit();
            this.layoutControl1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.progressBarQuery.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.xtraTabControl1)).EndInit();
            this.xtraTabControl1.ResumeLayout(false);
            this.xtraTabPageResults.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridControlResults)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewResults)).EndInit();
            this.xtraTabPageTables.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridControlTables)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewTables)).EndInit();
            this.xtraTabPageAnalysis.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.memoAnalysis.Properties)).EndInit();
            this.xtraTabPagePlan.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.treeListPlan)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.repoMemoAdvice)).EndInit();
            this.xtraTabPageMessages.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.memoMessages.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.Root)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlGroupQuery)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutItemSqlMemo)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutItemExecuteButton)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitterItem1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItemResults)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItemStatus)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItemProgress)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.emptySpaceItem1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private DevExpress.XtraLayout.LayoutControl layoutControl1;
        private DevExpress.XtraLayout.LayoutControlGroup Root;
        private DevExpress.XtraLayout.LayoutControlGroup layoutControlGroupQuery;
        private DevExpress.XtraEditors.SimpleButton btnExecute;
        private DevExpress.XtraRichEdit.RichEditControl memoSql;
        private DevExpress.XtraLayout.LayoutControlItem layoutItemSqlMemo;
        private DevExpress.XtraLayout.LayoutControlItem layoutItemExecuteButton;
        private DevExpress.XtraTab.XtraTabControl xtraTabControl1;
        private DevExpress.XtraTab.XtraTabPage xtraTabPageResults;
        private DevExpress.XtraGrid.GridControl gridControlResults;
        private DevExpress.XtraGrid.Views.Grid.GridView gridViewResults;
        private DevExpress.XtraTab.XtraTabPage xtraTabPageTables;
        private DevExpress.XtraGrid.GridControl gridControlTables;
        private DevExpress.XtraGrid.Views.Grid.GridView gridViewTables;
        private DevExpress.XtraTab.XtraTabPage xtraTabPageAnalysis;
        private DevExpress.XtraEditors.MemoEdit memoAnalysis;
        private DevExpress.XtraTab.XtraTabPage xtraTabPagePlan;
        
        private DevExpress.XtraTreeList.TreeList treeListPlan;
        private DevExpress.XtraTreeList.Columns.TreeListColumn colExecutionOrder;
        private DevExpress.XtraTreeList.Columns.TreeListColumn colOperation;
        private DevExpress.XtraTreeList.Columns.TreeListColumn colOptions;
        private DevExpress.XtraTreeList.Columns.TreeListColumn colObject;
        private DevExpress.XtraTreeList.Columns.TreeListColumn colCost;
        private DevExpress.XtraTreeList.Columns.TreeListColumn colCardinality;
        private DevExpress.XtraTreeList.Columns.TreeListColumn colId;
        private DevExpress.XtraTreeList.Columns.TreeListColumn colAdvice;
        private DevExpress.XtraEditors.Repository.RepositoryItemMemoEdit repoMemoAdvice;

        private DevExpress.XtraGrid.Columns.GridColumn colOwner;
        private DevExpress.XtraGrid.Columns.GridColumn colTableName;
        private DevExpress.XtraGrid.Columns.GridColumn colTableDesc;
        private DevExpress.XtraGrid.Columns.GridColumn colRowCount;

        private DevExpress.XtraTab.XtraTabPage xtraTabPageMessages;
        private DevExpress.XtraEditors.MemoEdit memoMessages;
        private DevExpress.XtraLayout.LayoutControlItem layoutControlItemResults;
        private DevExpress.XtraLayout.SplitterItem splitterItem1;
        
        private DevExpress.XtraEditors.LabelControl lblStatus;
        private DevExpress.XtraLayout.LayoutControlItem layoutControlItemStatus;
        private DevExpress.XtraEditors.MarqueeProgressBarControl progressBarQuery;
        private DevExpress.XtraLayout.LayoutControlItem layoutControlItemProgress;
        private DevExpress.XtraLayout.EmptySpaceItem emptySpaceItem1;
    }
}
