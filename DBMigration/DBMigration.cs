using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Data.Common;
using System.Data.SqlClient; // MSSQL 사용 시
// using Oracle.ManagedDataAccess.Client; // Oracle 사용 시 (참조 필요)
// using MySql.Data.MySqlClient; // MySQL 사용 시 (참조 필요)
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

        #region Properties (Source & Target DB Info)
        string DBMS
        {
            get
            {
                if (grvDbmsMigration == null || grvDbmsMigration.FocusedRowHandle < 0) return "";
                return grvDbmsMigration.GetFocusedRowCellValue("DBMS")?.ToString() ?? "";
            }
        }
        string DB
        {
            get
            {
                if (grvDbmsMigration == null || grvDbmsMigration.FocusedRowHandle < 0) return "";
                return grvDbmsMigration.GetFocusedRowCellValue("DB")?.ToString() ?? "";
            }
        }
        string USER
        {
            get
            {
                if (grvDbmsMigration == null || grvDbmsMigration.FocusedRowHandle < 0) return "";
                return grvDbmsMigration.GetFocusedRowCellValue("USER")?.ToString() ?? "";
            }
        }
        string CONSTR
        {
            get
            {
                if (grvDbmsMigration == null || grvDbmsMigration.FocusedRowHandle < 0) return "";
                return grvDbmsMigration.GetFocusedRowCellValue("CONSTR")?.ToString() ?? "";
            }
        }

        string TO_DBMS
        {
            get
            {
                if (grvDbmsMigTarget == null || grvDbmsMigTarget.FocusedRowHandle < 0) return "";
                return grvDbmsMigTarget.GetFocusedRowCellValue("DBMS")?.ToString() ?? "";
            }
        }
        string TO_DB
        {
            get
            {
                if (grvDbmsMigTarget == null || grvDbmsMigTarget.FocusedRowHandle < 0) return "";
                return grvDbmsMigTarget.GetFocusedRowCellValue("DB")?.ToString() ?? "";
            }
        }
        string TO_USER
        {
            get
            {
                if (grvDbmsMigTarget == null || grvDbmsMigTarget.FocusedRowHandle < 0) return "";
                return grvDbmsMigTarget.GetFocusedRowCellValue("USER")?.ToString() ?? "";
            }
        }
        string TO_CONSTR
        {
            get
            {
                if (grvDbmsMigTarget == null || grvDbmsMigTarget.FocusedRowHandle < 0) return "";
                return grvDbmsMigTarget.GetFocusedRowCellValue("CONSTR")?.ToString() ?? "";
            }
        }
        #endregion

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
        /// Object 조회 (Source DB)
        /// </summary>
        private void btnSearchMig_Click(object sender, EventArgs e)
        {
            if (grvDbmsMigration.FocusedRowHandle < 0)
            {
                MessageBox.Show("접속할 Source DBMS 정보를 선택하세요.");
                grdDbmsMigration.Focus();
                return;
            }

            string pre_connection_string = SystemInfoContext.Current["CONSTR"];
            SystemInfoContext.Current["CONSTR"] = CONSTR;

            DataTable dt = null;
            string gubun = cboMigObject.EditValue.ToString();

            this.ShowProgress();
            try
            {
                if (gubun.StartsWithInvariantCultureIgnoreCase("table"))
                {
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
            }
            finally
            {
                this.CloseProgress();
                SystemInfoContext.Current["CONSTR"] = pre_connection_string;
            }

            grdObjectMig.DataSource = dt;
        }

        /// <summary>
        /// Object 변경 시 UI 초기화
        /// </summary>
        private void cboMigObject_EnabledChanged(object sender, EventArgs e)
        {
            bool isTable = "Table".Equals(cboMigObject.Text);
            layMigrationTableOption.Visibility = isTable ? DevExpress.XtraLayout.Utils.LayoutVisibility.Always : DevExpress.XtraLayout.Utils.LayoutVisibility.Never;

            if (grdObjectMig.DataSource != null) (grdObjectMig.DataSource as DataTable).Rows.Clear();
            if (grdMigDataList.DataSource != null) (grdMigDataList.DataSource as DataTable).Rows.Clear();

            mmoMigScript.Text = "";
            mmoMigrationScript.Text = "";
        }

        /// <summary>
        /// Object 선택 시 스크립트 생성 및 데이터 조회
        /// </summary>
        private void grvObjectMig_RowCellClick(object sender, DevExpress.XtraGrid.Views.Grid.RowCellClickEventArgs e)
        {
            if (e.RowHandle < 0) return;

            this.ShowProgress();
            string pre_connection_string = SystemInfoContext.Current["CONSTR"];
            SystemInfoContext.Current["CONSTR"] = CONSTR;

            try
            {
                if ("Table".Equals(cboMigObject.Text))
                {
                    string tableName = grvObjectMig.GetFocusedRowCellValue("TABLE_NAME").ToString();
                    DataSet dsTableInfo = DBUtil.GetTableInfo(DBMS, DB, tableName);
                    string tableSpace = dsTableInfo.Tables[1].Rows[0]["TABLESPACE_NAME"].ToString();

                    mmoMigScript.Text = chkCpySchema.Checked ? $"DROP TABLE {tableName};" + Environment.NewLine + Environment.NewLine : "";
                    mmoMigScript.Text += DBUtil.GetTableScript(DBMS, DB, tableSpace, tableName, USER, DbType.Oracle);

                    mmoMigrationScript.Text = $@"SELECT * FROM {tableName} WHERE ROWNUM <= 500";
                    ExecuteMigData();
                }
                else if ("Package".Equals(cboMigObject.Text))
                {
                    mmoMigScript.Text = DBUtil.GetPackageInfo(DBMS, DB, grvObjectMig.GetFocusedRowCellValue("PKG_NAME").ToString(), "");
                }
            }
            finally
            {
                SystemInfoContext.Current["CONSTR"] = pre_connection_string;
                this.CloseProgress();
            }
        }

        private void grvDbmsMigTarget_RowCellClick(object sender, DevExpress.XtraGrid.Views.Grid.RowCellClickEventArgs e) { }

        void ExecuteMigData()
        {
            string sql = mmoMigrationScript.Text;
            DataSet ds = DBMS.Equals("ORACLE") ? DBMng.ExecuteSelect(sql, CommonFunction.SQLTYPE.쿼리) :
                         DBMS.Equals("MY SQL") ? DBMng.ExecuteSelectMysql(sql, CommonFunction.SQLTYPE.쿼리) :
                         DBMng.ExecuteSelectMSsql(sql, CommonFunction.SQLTYPE.쿼리);

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
            if (e.RowHandle == grvDbmsMigration.FocusedRowHandle) e.Appearance.BackColor = Color.Yellow;
        }

        private void grvDbmsMigTarget_CustomDrawCell(object sender, DevExpress.XtraGrid.Views.Base.RowCellCustomDrawEventArgs e)
        {
            if (e.RowHandle == grvDbmsMigTarget.FocusedRowHandle) e.Appearance.BackColor = Color.Yellow;
        }

        // =========================================================
        // [실행 (복사)] 클릭 이벤트
        // =========================================================
        private void btnCopy_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show($"Migration 작업을 진행합니까?\n(From: {DBMS} -> To: {TO_DBMS})", "알림", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.OK) return;

            if ("Table".Equals(cboMigObject.Text))
            {
                string targetTableName = GetTargetTableName();
                if (string.IsNullOrEmpty(targetTableName)) return;

                // Source Grid에서 데이터 수집
                DataTable dtSource = grdMigDataList.DataSource as DataTable;
                if (dtSource == null || dtSource.Rows.Count == 0)
                {
                    MessageBox.Show("복사할 데이터가 없습니다.");
                    return;
                }

                List<DataRow> rowsToCopy = GetRowsToCopy(dtSource);
                if (rowsToCopy.Count == 0) return;

                // 마이그레이션 실행
                ExecuteMigrationWithUI(rowsToCopy, targetTableName, chkCpySchema.Checked);
            }
            else
            {
                MessageBox.Show("현재 Table 복사 기능만 지원합니다.");
            }
        }

        // =========================================================
        // [복원] 클릭 이벤트 (추가 기능)
        // =========================================================
        private void btnRestore_Click(object sender, EventArgs e)
        {
            // 1. 타겟 테이블 확인
            string targetTableName = GetTargetTableName();
            if (string.IsNullOrEmpty(targetTableName)) return;

            // 2. 파일 선택 다이얼로그
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "XML Backup Files (*.xml)|*.xml|All Files (*.*)|*.*";
            ofd.Title = $"[{targetTableName}] 테이블 복원 파일 선택";
            
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    // 3. XML 파일 로드
                    DataTable dtRestore = new DataTable();
                    dtRestore.ReadXml(ofd.FileName); // 스키마 포함 로드

                    // 4. 검증: 테이블 이름 일치 여부 확인
                    // XML 저장 시 DataTable.TableName이 저장됨.
                    if (!string.Equals(dtRestore.TableName, targetTableName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (MessageBox.Show($"선택한 파일의 테이블 정보({dtRestore.TableName})가\n대상 테이블({targetTableName})과 다릅니다.\n\n그래도 계속 진행하시겠습니까?", 
                            "검증 경고", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                        {
                            return;
                        }
                    }

                    // 5. 검증: 컬럼 매칭 (간단 체크)
                    // 현재 DB 컬럼과 파일 컬럼이 맞는지 확인하는 로직을 추가할 수도 있음.

                    // 6. 복원 모드 질문 (초기화 후 복원 vs 병합)
                    bool isFullRestore = true;
                    if (MessageBox.Show("기존 데이터를 모두 삭제하고 복원하시겠습니까?\n(No 선택 시 데이터 병합 시도)", "복원 옵션", 
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
                    {
                        isFullRestore = false;
                    }

                    // 7. 복원 실행 (기존 Migration 로직 재사용)
                    List<DataRow> rowsToRestore = dtRestore.AsEnumerable().ToList();
                    ExecuteMigrationWithUI(rowsToRestore, targetTableName, isFullRestore);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"파일 읽기 실패: {ex.Message}", "오류");
                }
            }
        }

        /// <summary>
        /// 공통 마이그레이션 실행 래퍼 (Progress bar 및 예외 처리)
        /// </summary>
        private void ExecuteMigrationWithUI(List<DataRow> rows, string tableName, bool isFullReload)
        {
            this.ShowProgress();
            try
            {
                int result = ProcessTableMigration(rows, tableName, TO_DBMS, TO_CONSTR, isFullReload);
                MessageBox.Show($"작업 완료.\n처리 건수: {result}건", "성공", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"오류 발생:\n{ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.CloseProgress();
            }
        }

        /// <summary>
        /// 실제 데이터 처리 로직 (백업 -> (옵션:삭제) -> 입력)
        /// </summary>
        private int ProcessTableMigration(List<DataRow> sourceRows, string tableName, string targetDbms, string targetConnStr, bool isFullReload)
        {
            int count = 0;
            if (sourceRows == null || sourceRows.Count == 0) return 0;

            using (IDbConnection conn = CreateTargetConnection(targetDbms, targetConnStr))
            {
                if (conn == null) throw new Exception("DB 연결 객체를 생성할 수 없습니다. (지원되지 않는 DBMS)");
                conn.Open();

                // 1. 자동 백업 수행 (Target 테이블 데이터)
                BackupTargetTable(conn, tableName, targetDbms);

                // 2. 컬럼 동기화 (Source 데이터 기준, Target에 없는 컬럼 추가)
                if (isFullReload)
                {
                    SyncTableColumns(conn, tableName, sourceRows[0].Table, targetDbms);
                }

                // 3. 트랜잭션 처리
                using (IDbTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        if (isFullReload)
                        {
                            // 전체 삭제 후 Insert
                            using (IDbCommand cmdDel = conn.CreateCommand())
                            {
                                cmdDel.Transaction = trans;
                                cmdDel.CommandText = $"DELETE FROM {tableName}";
                                cmdDel.ExecuteNonQuery();
                            }

                            foreach (DataRow row in sourceRows)
                            {
                                ExecuteInsert(conn, trans, tableName, row, targetDbms);
                                count++;
                            }
                        }
                        else
                        {
                            // Upsert (PK 기준 Update, 없으면 Insert)
                            List<string> pkCols = GetPrimaryKeyColumns(conn, tableName, targetDbms);

                            foreach (DataRow row in sourceRows)
                            {
                                if (pkCols.Count > 0 && CheckRecordExists(conn, trans, tableName, row, pkCols, targetDbms))
                                {
                                    ExecuteUpdate(conn, trans, tableName, row, pkCols, targetDbms);
                                }
                                else
                                {
                                    ExecuteInsert(conn, trans, tableName, row, targetDbms);
                                }
                                count++;
                            }
                        }
                        trans.Commit();
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
            return count;
        }

        #region Helper Methods (Backup, Sync, Query Build)

        private string GetTargetTableName()
        {
            if (grvObjectMig.FocusedRowHandle < 0)
            {
                MessageBox.Show("대상 테이블을 선택해주세요.");
                return null;
            }
            return grvObjectMig.GetFocusedRowCellValue("TABLE_NAME")?.ToString();
        }

        private List<DataRow> GetRowsToCopy(DataTable dt)
        {
            List<DataRow> rows = new List<DataRow>();
            if (rdoMigData.EditValue != null && rdoMigData.EditValue.ToString().Equals("S"))
            {
                int[] selectedRows = grvMigDataList.GetSelectedRows();
                if (selectedRows == null || selectedRows.Length == 0)
                {
                    MessageBox.Show("선택된 행이 없습니다.");
                    return rows;
                }
                foreach (int h in selectedRows)
                    if (h >= 0) rows.Add(grvMigDataList.GetDataRow(h));
            }
            else
            {
                rows = dt.AsEnumerable().ToList();
            }
            return rows;
        }

        private void BackupTargetTable(IDbConnection conn, string tableName, string dbms)
        {
            try
            {
                DataTable dtBackup = new DataTable(tableName);
                using (IDbCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"SELECT * FROM {tableName}";
                    using (IDataReader reader = cmd.ExecuteReader())
                    {
                        dtBackup.Load(reader);
                    }
                }

                string backupDir = Path.Combine(Application.StartupPath, "Backup");
                if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);

                string fileName = $"{tableName}_{DateTime.Now:yyyyMMdd_HHmmss}.xml";
                string fullPath = Path.Combine(backupDir, fileName);
                dtBackup.WriteXml(fullPath, XmlWriteMode.WriteSchema);
            }
            catch (Exception ex)
            {
                // 백업 실패는 로그만 남기고 진행하거나 예외 던짐 (여기서는 진행)
                Debug.WriteLine($"백업 실패: {ex.Message}");
            }
        }

        private void SyncTableColumns(IDbConnection conn, string tableName, DataTable sourceSchema, string dbms)
        {
            try
            {
                List<string> targetCols = new List<string>();
                using (IDbCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"SELECT * FROM {tableName} WHERE 1=0";
                    using (IDataReader reader = cmd.ExecuteReader())
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                            targetCols.Add(reader.GetName(i).ToUpper());
                    }
                }

                foreach (DataColumn srcCol in sourceSchema.Columns)
                {
                    if (srcCol.ColumnName.Equals("CHK", StringComparison.OrdinalIgnoreCase)) continue;

                    if (!targetCols.Contains(srcCol.ColumnName.ToUpper()))
                    {
                        using (IDbCommand cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = GenerateAlterAddSql(tableName, srcCol, dbms);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch { /* 컬럼 추가 실패 시 무시 (권한 문제 등) */ }
        }

        private string GenerateAlterAddSql(string tableName, DataColumn col, string dbms)
        {
            string type = "VARCHAR(255)";
            if (col.DataType == typeof(int) || col.DataType == typeof(long) || col.DataType == typeof(decimal))
                type = "NUMBER"; // Oracle
            else if (col.DataType == typeof(DateTime))
                type = "DATE";

            if (dbms.ToUpper().Contains("MSSQL") || dbms.ToUpper().Contains("MS SQL"))
            {
                if (col.DataType == typeof(int)) type = "INT";
                else if (col.DataType == typeof(DateTime)) type = "DATETIME";
            }
            
            return $"ALTER TABLE {tableName} ADD {col.ColumnName} {type}";
        }

        private bool CheckRecordExists(IDbConnection conn, IDbTransaction trans, string tableName, DataRow row, List<string> pkCols, string dbms)
        {
            using (IDbCommand cmd = conn.CreateCommand())
            {
                cmd.Transaction = trans;
                StringBuilder sbWhere = new StringBuilder();
                string prefix = GetParamPrefix(dbms);

                for (int i = 0; i < pkCols.Count; i++)
                {
                    string col = pkCols[i];
                    if (i > 0) sbWhere.Append(" AND ");
                    sbWhere.Append($"{col} = {prefix}W_{col}");

                    IDbDataParameter param = cmd.CreateParameter();
                    param.ParameterName = $"{prefix}W_{col}";
                    param.Value = row[col] ?? DBNull.Value;
                    cmd.Parameters.Add(param);
                }

                cmd.CommandText = $"SELECT COUNT(1) FROM {tableName} WHERE {sbWhere}";
                object res = cmd.ExecuteScalar();
                return Convert.ToInt32(res) > 0;
            }
        }

        private void ExecuteInsert(IDbConnection conn, IDbTransaction trans, string tableName, DataRow row, string dbms)
        {
            using (IDbCommand cmd = conn.CreateCommand())
            {
                cmd.Transaction = trans;
                StringBuilder sbCols = new StringBuilder();
                StringBuilder sbVals = new StringBuilder();
                string prefix = GetParamPrefix(dbms);
                bool first = true;

                foreach (DataColumn col in row.Table.Columns)
                {
                    if (col.ColumnName.Equals("CHK", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!first) { sbCols.Append(", "); sbVals.Append(", "); }

                    sbCols.Append(col.ColumnName);
                    sbVals.Append($"{prefix}{col.ColumnName}");

                    IDbDataParameter param = cmd.CreateParameter();
                    param.ParameterName = $"{prefix}{col.ColumnName}";
                    param.Value = row[col.ColumnName] ?? DBNull.Value;
                    cmd.Parameters.Add(param);
                    first = false;
                }
                cmd.CommandText = $"INSERT INTO {tableName} ({sbCols}) VALUES ({sbVals})";
                cmd.ExecuteNonQuery();
            }
        }

        private void ExecuteUpdate(IDbConnection conn, IDbTransaction trans, string tableName, DataRow row, List<string> pkCols, string dbms)
        {
            using (IDbCommand cmd = conn.CreateCommand())
            {
                cmd.Transaction = trans;
                StringBuilder sbSet = new StringBuilder();
                StringBuilder sbWhere = new StringBuilder();
                string prefix = GetParamPrefix(dbms);
                bool first = true;

                foreach (DataColumn col in row.Table.Columns)
                {
                    if (col.ColumnName.Equals("CHK", StringComparison.OrdinalIgnoreCase)) continue;
                    if (pkCols.Contains(col.ColumnName)) continue; // PK 제외

                    if (!first) sbSet.Append(", ");
                    sbSet.Append($"{col.ColumnName} = {prefix}{col.ColumnName}");

                    IDbDataParameter param = cmd.CreateParameter();
                    param.ParameterName = $"{prefix}{col.ColumnName}";
                    param.Value = row[col.ColumnName] ?? DBNull.Value;
                    cmd.Parameters.Add(param);
                    first = false;
                }

                // Where 절
                for (int i = 0; i < pkCols.Count; i++)
                {
                    string col = pkCols[i];
                    if (i > 0) sbWhere.Append(" AND ");
                    sbWhere.Append($"{col} = {prefix}W_{col}");

                    IDbDataParameter param = cmd.CreateParameter();
                    param.ParameterName = $"{prefix}W_{col}";
                    param.Value = row[col] ?? DBNull.Value;
                    cmd.Parameters.Add(param);
                }

                cmd.CommandText = $"UPDATE {tableName} SET {sbSet} WHERE {sbWhere}";
                cmd.ExecuteNonQuery();
            }
        }

        private List<string> GetPrimaryKeyColumns(IDbConnection conn, string tableName, string dbms)
        {
            List<string> pks = new List<string>();
            try
            {
                string sql = "";
                if (dbms.ToUpper().Contains("ORACLE"))
                {
                    sql = $@"SELECT COLUMN_NAME FROM ALL_CONS_COLUMNS A JOIN ALL_CONSTRAINTS C ON A.CONSTRAINT_NAME = C.CONSTRAINT_NAME WHERE C.CONSTRAINT_TYPE = 'P' AND A.TABLE_NAME = '{tableName}' AND A.OWNER = '{TO_USER}'";
                }
                else if (dbms.ToUpper().Contains("MS SQL") || dbms.ToUpper().Contains("MSSQL"))
                {
                    sql = $@"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE WHERE OBJECTPROPERTY(OBJECT_ID(CONSTRAINT_SCHEMA + '.' + QUOTENAME(CONSTRAINT_NAME)), 'IsPrimaryKey') = 1 AND TABLE_NAME = '{tableName}'";
                }

                if (!string.IsNullOrEmpty(sql))
                {
                    using (IDbCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        using (IDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read()) pks.Add(reader[0].ToString());
                        }
                    }
                }
            }
            catch { }
            return pks;
        }

        private string GetParamPrefix(string dbms)
        {
            return (dbms != null && dbms.ToUpper().Contains("ORACLE")) ? ":" : "@";
        }

        private IDbConnection CreateTargetConnection(string dbms, string connStr)
        {
            if (string.IsNullOrEmpty(dbms)) return null;
            if (dbms.ToUpper().Contains("MSSQL") || dbms.ToUpper().Contains("MS SQL"))
                return new SqlConnection(connStr);
            // else if (dbms.Contains("ORACLE")) return new OracleConnection(connStr);
            // else if (dbms.Contains("MYSQL")) return new MySqlConnection(connStr);
            
            return null; // 라이브러리 참조에 맞게 수정 필요
        }
        #endregion
    }
}
