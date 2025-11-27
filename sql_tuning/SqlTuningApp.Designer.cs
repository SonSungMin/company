using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraRichEdit;
using DevExpress.XtraRichEdit.API.Native;
using DevExpress.XtraRichEdit.Services;
using Oracle.ManagedDataAccess.Client;

namespace DevTools.UI.Control
{
    partial class SqlTuningApp
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.layoutControl1 = new DevExpress.XtraLayout.LayoutControl();
            this.lblStatus = new DevExpress.XtraEditors.LabelControl();
            this.xtraTabControl1 = new DevExpress.XtraTab.XtraTabControl();
            this.xtraTabPageResults = new DevExpress.XtraTab.XtraTabPage();
            this.gridControlResults = new DevExpress.XtraGrid.GridControl();
            this.gridViewResults = new DevExpress.XtraGrid.Views.Grid.GridView();
            this.xtraTabPageTables = new DevExpress.XtraTab.XtraTabPage();
            this.gridControlTables = new DevExpress.XtraGrid.GridControl();
            this.gridViewTables = new DevExpress.XtraGrid.Views.Grid.GridView();
            this.xtraTabPageAnalysis = new DevExpress.XtraTab.XtraTabPage();
            this.memoAnalysis = new DevExpress.XtraEditors.MemoEdit();
            this.xtraTabPagePlan = new DevExpress.XtraTab.XtraTabPage();
            this.gridControlPlan = new DevExpress.XtraGrid.GridControl();
            this.gridViewPlan = new DevExpress.XtraGrid.Views.Grid.GridView();
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
            this.emptySpaceItem1 = new DevExpress.XtraLayout.EmptySpaceItem();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControl1)).BeginInit();
            this.layoutControl1.SuspendLayout();
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
            ((System.ComponentModel.ISupportInitialize)(this.gridControlPlan)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewPlan)).BeginInit();
            this.xtraTabPageMessages.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.memoMessages.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.Root)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlGroupQuery)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutItemSqlMemo)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutItemExecuteButton)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitterItem1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItemResults)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItemStatus)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.emptySpaceItem1)).BeginInit();
            this.SuspendLayout();
            // 
            // layoutControl1
            // 
            this.layoutControl1.Controls.Add(this.lblStatus);
            this.layoutControl1.Controls.Add(this.xtraTabControl1);
            this.layoutControl1.Controls.Add(this.btnExecute);
            this.layoutControl1.Controls.Add(this.memoSql);
            this.layoutControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.layoutControl1.Location = new System.Drawing.Point(0, 0);
            this.layoutControl1.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.layoutControl1.Name = "layoutControl1";
            this.layoutControl1.Root = this.Root;
            this.layoutControl1.Size = new System.Drawing.Size(1202, 726);
            this.layoutControl1.TabIndex = 0;
            this.layoutControl1.Text = "layoutControl1";
            // 
            // lblStatus
            // 
            this.lblStatus.Location = new System.Drawing.Point(12, 700);
            this.lblStatus.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(117, 14);
            this.lblStatus.StyleController = this.layoutControl1;
            this.lblStatus.TabIndex = 11;
            this.lblStatus.Text = "Status: Disconnected";
            // 
            // xtraTabControl1
            // 
            this.xtraTabControl1.Location = new System.Drawing.Point(12, 496);
            this.xtraTabControl1.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
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
            this.xtraTabPageResults.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.xtraTabPageResults.Name = "xtraTabPageResults";
            this.xtraTabPageResults.Size = new System.Drawing.Size(1172, 171);
            this.xtraTabPageResults.Text = "Results";
            // 
            // gridControlResults
            // 
            this.gridControlResults.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridControlResults.EmbeddedNavigator.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.gridControlResults.Location = new System.Drawing.Point(0, 0);
            this.gridControlResults.MainView = this.gridViewResults;
            this.gridControlResults.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
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
            this.gridViewResults.OptionsView.ShowGroupPanel = false;
            // 
            // xtraTabPageTables
            // 
            this.xtraTabPageTables.Controls.Add(this.gridControlTables);
            this.xtraTabPageTables.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.xtraTabPageTables.Name = "xtraTabPageTables";
            this.xtraTabPageTables.Size = new System.Drawing.Size(1172, 171);
            this.xtraTabPageTables.Text = "Used Tables";
            // 
            // gridControlTables
            // 
            this.gridControlTables.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridControlTables.EmbeddedNavigator.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.gridControlTables.Location = new System.Drawing.Point(0, 0);
            this.gridControlTables.MainView = this.gridViewTables;
            this.gridControlTables.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.gridControlTables.Name = "gridControlTables";
            this.gridControlTables.Size = new System.Drawing.Size(1172, 171);
            this.gridControlTables.TabIndex = 0;
            this.gridControlTables.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.gridViewTables});
            // 
            // gridViewTables
            // 
            this.gridViewTables.DetailHeight = 323;
            this.gridViewTables.GridControl = this.gridControlTables;
            this.gridViewTables.Name = "gridViewTables";
            this.gridViewTables.OptionsBehavior.Editable = false;
            this.gridViewTables.OptionsView.ShowGroupPanel = false;
            // 
            // xtraTabPageAnalysis
            // 
            this.xtraTabPageAnalysis.Controls.Add(this.memoAnalysis);
            this.xtraTabPageAnalysis.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.xtraTabPageAnalysis.Name = "xtraTabPageAnalysis";
            this.xtraTabPageAnalysis.Size = new System.Drawing.Size(1172, 171);
            this.xtraTabPageAnalysis.Text = "Improvement Suggestions";
            // 
            // memoAnalysis
            // 
            this.memoAnalysis.Dock = System.Windows.Forms.DockStyle.Fill;
            this.memoAnalysis.Location = new System.Drawing.Point(0, 0);
            this.memoAnalysis.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.memoAnalysis.Name = "memoAnalysis";
            this.memoAnalysis.Properties.ReadOnly = true;
            this.memoAnalysis.Size = new System.Drawing.Size(1172, 171);
            this.memoAnalysis.TabIndex = 0;
            // 
            // xtraTabPagePlan
            // 
            this.xtraTabPagePlan.Controls.Add(this.gridControlPlan);
            this.xtraTabPagePlan.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.xtraTabPagePlan.Name = "xtraTabPagePlan";
            this.xtraTabPagePlan.Size = new System.Drawing.Size(1172, 171);
            this.xtraTabPagePlan.Text = "Execution Plan";
            // 
            // gridControlPlan
            // 
            this.gridControlPlan.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridControlPlan.EmbeddedNavigator.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.gridControlPlan.Location = new System.Drawing.Point(0, 0);
            this.gridControlPlan.MainView = this.gridViewPlan;
            this.gridControlPlan.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.gridControlPlan.Name = "gridControlPlan";
            this.gridControlPlan.Size = new System.Drawing.Size(1172, 171);
            this.gridControlPlan.TabIndex = 0;
            this.gridControlPlan.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.gridViewPlan});
            // 
            // gridViewPlan
            // 
            this.gridViewPlan.DetailHeight = 323;
            this.gridViewPlan.GridControl = this.gridControlPlan;
            this.gridViewPlan.Name = "gridViewPlan";
            this.gridViewPlan.OptionsBehavior.Editable = false;
            this.gridViewPlan.OptionsView.ColumnAutoWidth = false;
            this.gridViewPlan.OptionsView.ShowGroupPanel = false;
            // 
            // xtraTabPageMessages
            // 
            this.xtraTabPageMessages.Controls.Add(this.memoMessages);
            this.xtraTabPageMessages.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.xtraTabPageMessages.Name = "xtraTabPageMessages";
            this.xtraTabPageMessages.Size = new System.Drawing.Size(1172, 171);
            this.xtraTabPageMessages.Text = "Messages";
            // 
            // memoMessages
            // 
            this.memoMessages.Dock = System.Windows.Forms.DockStyle.Fill;
            this.memoMessages.Location = new System.Drawing.Point(0, 0);
            this.memoMessages.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.memoMessages.Name = "memoMessages";
            this.memoMessages.Properties.ReadOnly = true;
            this.memoMessages.Size = new System.Drawing.Size(1172, 171);
            this.memoMessages.TabIndex = 0;
            // 
            // btnExecute
            // 
            this.btnExecute.Location = new System.Drawing.Point(24, 24);
            this.btnExecute.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.btnExecute.MaximumSize = new System.Drawing.Size(130, 0);
            this.btnExecute.MinimumSize = new System.Drawing.Size(130, 0);
            this.btnExecute.Name = "btnExecute";
            this.btnExecute.Size = new System.Drawing.Size(130, 20);
            this.btnExecute.StyleController = this.layoutControl1;
            this.btnExecute.TabIndex = 9;
            this.btnExecute.Text = "Execute (F5)";
            this.btnExecute.Click += new System.EventHandler(this.btnExecute_Click);
            // 
            // memoSql
            // 
            this.memoSql.ActiveViewType = DevExpress.XtraRichEdit.RichEditViewType.Simple;
            this.memoSql.LayoutUnit = DevExpress.XtraRichEdit.DocumentLayoutUnit.Pixel;
            this.memoSql.Location = new System.Drawing.Point(24, 48);
            this.memoSql.Name = "memoSql";
            this.memoSql.Size = new System.Drawing.Size(1154, 427);
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
            this.layoutControlItemStatus});
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
            this.layoutControlGroupQuery.Text = "SQL Editor";
            this.layoutControlGroupQuery.TextVisible = false;
            // 
            // layoutItemSqlMemo
            // 
            this.layoutItemSqlMemo.Control = this.memoSql;
            this.layoutItemSqlMemo.Location = new System.Drawing.Point(0, 24);
            this.layoutItemSqlMemo.Name = "layoutItemSqlMemo";
            this.layoutItemSqlMemo.Size = new System.Drawing.Size(1158, 431);
            this.layoutItemSqlMemo.TextSize = new System.Drawing.Size(0, 0);
            this.layoutItemSqlMemo.TextVisible = false;
            // 
            // layoutItemExecuteButton
            // 
            this.layoutItemExecuteButton.Control = this.btnExecute;
            this.layoutItemExecuteButton.ControlAlignment = System.Drawing.ContentAlignment.MiddleRight;
            this.layoutItemExecuteButton.Location = new System.Drawing.Point(0, 0);
            this.layoutItemExecuteButton.MaxSize = new System.Drawing.Size(0, 24);
            this.layoutItemExecuteButton.MinSize = new System.Drawing.Size(92, 24);
            this.layoutItemExecuteButton.Name = "layoutItemExecuteButton";
            this.layoutItemExecuteButton.Size = new System.Drawing.Size(133, 24);
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
            this.layoutControlItemStatus.Size = new System.Drawing.Size(1182, 18);
            this.layoutControlItemStatus.TextSize = new System.Drawing.Size(0, 0);
            this.layoutControlItemStatus.TextVisible = false;
            // 
            // emptySpaceItem1
            // 
            this.emptySpaceItem1.AllowHotTrack = false;
            this.emptySpaceItem1.Location = new System.Drawing.Point(133, 0);
            this.emptySpaceItem1.Name = "emptySpaceItem1";
            this.emptySpaceItem1.Size = new System.Drawing.Size(1025, 24);
            this.emptySpaceItem1.TextSize = new System.Drawing.Size(0, 0);
            // 
            // SqlTuningApp
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.layoutControl1);
            this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.Name = "SqlTuningApp";
            this.Size = new System.Drawing.Size(1202, 726);
            ((System.ComponentModel.ISupportInitialize)(this.layoutControl1)).EndInit();
            this.layoutControl1.ResumeLayout(false);
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
            ((System.ComponentModel.ISupportInitialize)(this.gridControlPlan)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewPlan)).EndInit();
            this.xtraTabPageMessages.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.memoMessages.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.Root)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlGroupQuery)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutItemSqlMemo)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutItemExecuteButton)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitterItem1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItemResults)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlItemStatus)).EndInit();
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
        private DevExpress.XtraGrid.GridControl gridControlPlan;
        private DevExpress.XtraGrid.Views.Grid.GridView gridViewPlan;
        private DevExpress.XtraTab.XtraTabPage xtraTabPageMessages;
        private DevExpress.XtraEditors.MemoEdit memoMessages;
        private DevExpress.XtraLayout.LayoutControlItem layoutControlItemResults;
        private DevExpress.XtraLayout.SplitterItem splitterItem1;
        private DevExpress.XtraEditors.LabelControl lblStatus;
        private DevExpress.XtraLayout.LayoutControlItem layoutControlItemStatus;
        private DevExpress.XtraLayout.EmptySpaceItem emptySpaceItem1;
    }
}

          

      
