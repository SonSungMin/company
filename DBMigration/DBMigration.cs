using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections;
using System.IO;
//using System.Data.SQLite;
using DevTools.Util;
using DevExpress.Utils;
using DevTools.Util.DataBase;
using DevExpress.XtraGrid.Views.Grid;
using DbType = DevTools.Util.DataBase.DbType;

namespace DevTools.UI.Control
{
    [Browsable(true)]
    [ToolboxItem(true)]
    public partial class DBMigration : UserControlBase
    {
        string OBJECT = "";
        Keys PRE_KEY = Keys.None;
        string DBMS
        {
            get
            {
                if (grvDbmsMigration == null || grvDbmsMigration.FocusedRowHandle < 0)
                    return "";

                return grvDbmsMigration.GetFocusedRowCellValue("DBMS").ToString();
            }
        }

        string DB
        {
            get
            {
                if (grvDbmsMigration == null || grvDbmsMigration.FocusedRowHandle < 0)
                    return "";

                return grvDbmsMigration.GetFocusedRowCellValue("DB").ToString();
            }
        }

        string USER
        {
            get
            {
                if (grvDbmsMigration == null || grvDbmsMigration.FocusedRowHandle < 0)
                    return "";

                return grvDbmsMigration.GetFocusedRowCellValue("USER").ToString();
            }
        }

        string CONSTR
        {
            get
            {
                if (grvDbmsMigration == null || grvDbmsMigration.FocusedRowHandle < 0)
                    return "";

                return grvDbmsMigration.GetFocusedRowCellValue("CONSTR").ToString();
            }
        }

        string TO_DBMS
        {
            get
            {
                if (grvDbmsMigTarget == null || grvDbmsMigTarget.FocusedRowHandle < 0)
                    return "";

                return grvDbmsMigTarget.GetFocusedRowCellValue("DBMS").ToString();
            }
        }

        string TO_DB
        {
            get
            {
                if (grvDbmsMigTarget == null || grvDbmsMigTarget.FocusedRowHandle < 0)
                    return "";

                return grvDbmsMigTarget.GetFocusedRowCellValue("DB").ToString();
            }
        }

        string TO_USER
        {
            get
            {
                if (grvDbmsMigTarget == null || grvDbmsMigTarget.FocusedRowHandle < 0)
                    return "";

                return grvDbmsMigTarget.GetFocusedRowCellValue("USER").ToString();
            }
        }

        string TO_CONSTR
        {
            get
            {
                if (grvDbmsMigTarget == null || grvDbmsMigTarget.FocusedRowHandle < 0)
                    return "";

                return grvDbmsMigTarget.GetFocusedRowCellValue("CONSTR").ToString();
            }
        }

        /// <summary>
        /// 생성자
        /// </summary>
        public DBMigration()
        {
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            InitLoad();
        }

        void InitLoad()
        {
            grdDbmsMigration.DataSource = CommonFunction.GetConnectionList;
            grdDbmsMigTarget.DataSource = CommonFunction.GetConnectionList;
        }

        /// <summary>
        /// Object 조회
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSearchMig_Click(object sender, EventArgs e)
        {
            if (grvDbmsMigration.FocusedRowHandle < 0)
            {
                MessageBox.Show("접속할 DBMS 정보를 선택하세요.");
                grdDbmsMigration.Focus();
                return;
            }

            string pre_connection_string = SystemInfoContext.Current["CONSTR"];
            
            SystemInfoContext.Current["CONSTR"] = CONSTR;

            DataTable dt = null;

            string gubun = cboMigObject.EditValue.ToString();

            this.ShowProgress();

            if (gubun.StartsWithInvariantCultureIgnoreCase("table"))
            {
                //columnExtra.Visible = false;
                //if (dbms.Equals("MY SQL"))
                //    columnExtra.Visible = true;

                grdColMigration.FieldName = "TABLE_NAME";

                dt = DBUtil.GetTableList(DBMS, DB, USER, "Table");
            }
            else if (gubun.StartsWithInvariantCultureIgnoreCase("package"))
            {
                grdColMigration.FieldName = "PKG_NAME";
                dt = DBUtil.GetPackageList(DBMS, SystemInfoContext.Current["DB"]);
            }

            if (dt != null && !dt.Columns.Contains("CHK"))
                dt.Columns.Add("CHK");

            this.CloseProgress();

            grdObjectMig.DataSource = dt;

            SystemInfoContext.Current["CONSTR"] = pre_connection_string;
        }

        /// <summary>
        /// Object 변경
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cboMigObject_EnabledChanged(object sender, EventArgs e)
        {
            if ("Table".Equals(cboMigObject.Text))
            {
                layMigrationTableOption.Visibility = DevExpress.XtraLayout.Utils.LayoutVisibility.Always;
            }
            else if ("Package".Equals(cboMigObject.Text))
            {
                layMigrationTableOption.Visibility = DevExpress.XtraLayout.Utils.LayoutVisibility.Never;
            }
            else if ("Procedure".Equals(cboMigObject.Text))
            {
                layMigrationTableOption.Visibility = DevExpress.XtraLayout.Utils.LayoutVisibility.Never;
            }

            if (grdObjectMig.DataSource != null)
                (grdObjectMig.DataSource as DataTable).Rows.Clear();

            if (grdMigDataList.DataSource != null)
                (grdMigDataList.DataSource as DataTable).Rows.Clear();

            mmoMigScript.Text = "";
            mmoMigrationScript.Text = "";
        }

        /// <summary>
        /// Migration에서 Object를 선택하면
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void grvObjectMig_RowCellClick(object sender, DevExpress.XtraGrid.Views.Grid.RowCellClickEventArgs e)
        {
            if (e.RowHandle < 0) return;

            this.ShowProgress();

            string tableSpace = "";

            string pre_connection_string = SystemInfoContext.Current["CONSTR"];
            
            SystemInfoContext.Current["CONSTR"] = CONSTR;

            if ("Table".Equals(cboMigObject.Text))
            {
                string tableName = grvObjectMig.GetFocusedRowCellValue("TABLE_NAME").ToString();

                DataSet dsTableInfo = DBUtil.GetTableInfo(DBMS, DB, tableName);

                tableSpace = dsTableInfo.Tables[1].Rows[0]["TABLESPACE_NAME"].ToString();

                if (chkCpySchema.Checked)
                    mmoMigScript.Text = $"DROP TABLE {tableName};" + Environment.NewLine + Environment.NewLine;
                else
                    mmoMigScript.Text = "";

                mmoMigScript.Text += DBUtil.GetTableScript(DBMS, DB, tableSpace, tableName, USER, DbType.Oracle);

                mmoMigrationScript.Text = $@"
SELECT *
  FROM {tableName}
 WHERE ROWNUM <= 500
";

                ExecuteMigData();
            }
            else if ("Package".Equals(cboMigObject.Text))
            {
                mmoMigScript.Text = DBUtil.GetPackageInfo(DBMS, DB, grvObjectMig.GetFocusedRowCellValue("PKG_NAME").ToString(), "");
            }

            SystemInfoContext.Current["CONSTR"] = pre_connection_string;

            this.CloseProgress();
        }

        private void grvDbmsMigTarget_RowCellClick(object sender, DevExpress.XtraGrid.Views.Grid.RowCellClickEventArgs e)
        {
            //DataTable dt = (grdObjectMig.DataSource as DataTable);
            //if (dt != null)
            //    dt.Rows.Clear();
        }

        void ExecuteMigData()
        {
            string sql = mmoMigrationScript.Text;

            DataSet ds = DBMS.Equals("ORACLE") ? DBMng.ExecuteSelect(sql, CommonFunction.SQLTYPE.쿼리) : DBMS.Equals("MY SQL") ? DBMng.ExecuteSelectMysql(sql, CommonFunction.SQLTYPE.쿼리) : DBMng.ExecuteSelectMSsql(sql, CommonFunction.SQLTYPE.쿼리);

            if (ds != null && ds.Tables.Count > 0)
            {
                grvMigDataList.Columns.Clear();
                grdMigDataList.DataSource = ds.Tables[0];
            }
        }

        private void mmoMigrationScript_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return && PRE_KEY == Keys.ControlKey)
            {
                e.Handled = true;

                ExecuteMigData();
            }
            PRE_KEY = e.KeyCode;
        }

        private void grvDbmsMigration_CustomDrawCell(object sender, DevExpress.XtraGrid.Views.Base.RowCellCustomDrawEventArgs e)
        {
            if (e.RowHandle == grvDbmsMigration.FocusedRowHandle)
                e.Appearance.BackColor = Color.Yellow;
        }

        private void grvDbmsMigTarget_CustomDrawCell(object sender, DevExpress.XtraGrid.Views.Base.RowCellCustomDrawEventArgs e)
        {
            if (e.RowHandle == grvDbmsMigTarget.FocusedRowHandle)
                e.Appearance.BackColor = Color.Yellow;
        }

        /// <summary>
        /// 실행 클릭
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCopy_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show($"Migration 작업을 진행합니까?\n(설정한 조건을 잘 확인하세요.)\n\nFrom. {DBMS} {DB} {USER} → To. {TO_DBMS} {TO_DB} {TO_USER}", "알림", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.OK)
                return;

            if(DBMS == TO_DBMS && DB == TO_DB && USER == TO_USER)
            {
                MessageBox.Show("작업 대상 From, To DB 정보가 동일합니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if("Table".Equals(cboMigObject.Text))
            {
                // 조회된 항목만 복사
                if(rdoMigData.EditValue.ToString().Equals("S"))
                {
                    DataTable dt = grdMigDataList.DataSource as DataTable;

                    if(dt == null || dt.Rows.Count == 0)
                    {
                        MessageBox.Show("조회된 데이터가 없습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    foreach(DataRow row in dt.Rows)
                    {

                    }
                }
                // 모든 항목 복사
                else
                {

                }
            }
            else if ("Package".Equals(cboMigObject.Text))
            {

            }
            else if ("Procedure".Equals(cboMigObject.Text))
            {

            }
        }
    }
}










        
