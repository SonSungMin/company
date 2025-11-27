using DevTools.Util;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Match = System.Text.RegularExpressions.Match;

namespace DevTools.UI.Control
{
    [Browsable(true)]
    public partial class SqlTuningApp : UserControl
    {
        private readonly Regex _keywordsRegex;
        private readonly Timer _parsingTimer;

        private readonly Color _defaultForeColor = Color.Gainsboro;
        private readonly Color _keywordColor = Color.CornflowerBlue;
        private readonly Color _stringColor = Color.Salmon;
        private readonly Color _commentColor = Color.LightGreen;

        string CONNECTION_STRING
        {
            get
            {
                return SystemInfoContext.Current == null ? null : SystemInfoContext.Current["CONSTR"];
            }
        }

        public SqlTuningApp()
        {
            InitializeComponent();

            if (DesignMode)
                return;

            string[] keywords = {
                "SELECT", "FROM", "WHERE", "JOIN", "LEFT", "RIGHT", "INNER", "OUTER", "ON", "GROUP", "BY", "ORDER",
                "HAVING", "AS", "INSERT", "INTO", "VALUES", "UPDATE", "SET", "DELETE", "TRUNCATE", "TABLE",
                "CREATE", "ALTER", "DROP", "INDEX", "VIEW", "PROCEDURE", "FUNCTION", "TRIGGER", "UNION", "ALL",
                "AND", "OR", "NOT", "NULL", "IS", "LIKE", "IN", "BETWEEN", "EXISTS", "CASE", "WHEN", "THEN", "ELSE", "END",
                "COUNT", "SUM", "AVG", "MAX", "MIN", "DISTINCT", "TOP", "ROWNUM"
            };

            _keywordsRegex = new Regex(@"\b(" + string.Join("|", keywords) + @")\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            _parsingTimer = new Timer { Interval = 500 };
            _parsingTimer.Tick += ParsingTimer_Tick;
        }

        private void SqlExecutorControl_Load(object sender, EventArgs e)
        {
            memoSql.Document.DefaultCharacterProperties.FontName = "Consolas";
            memoSql.Document.DefaultCharacterProperties.FontSize = 10;
            memoSql.ActiveView.BackColor = Color.FromArgb(45, 45, 48);
            memoSql.TextChanged += MemoSql_TextChanged;
        }

        private void MemoSql_TextChanged(object sender, EventArgs e)
        {
            _parsingTimer.Stop();
            _parsingTimer.Start();
        }

        private void ParsingTimer_Tick(object sender, EventArgs e)
        {
            _parsingTimer.Stop();
            ApplySyntaxHighlighting();
        }

        private void ApplySyntaxHighlighting()
        {
            var doc = memoSql.Document;
            memoSql.TextChanged -= MemoSql_TextChanged;
            doc.BeginUpdate();
            try
            {
                // 1. 전체 텍스트의 서식을 기본으로 초기화
                var allRange = doc.Range;
                var cp = doc.BeginUpdateCharacters(allRange);
                cp.ForeColor = _defaultForeColor;
                doc.EndUpdateCharacters(cp);

                // 2. 주석 찾아서 서식 적용 (한줄/여러줄)
                var commentRegex1 = new Regex(@"--.*");
                var commentRanges1 = doc.FindAll(commentRegex1);
                foreach (var range in commentRanges1)
                {
                    var commentCp = doc.BeginUpdateCharacters(range);
                    
                    commentCp.ForeColor = _commentColor;
                    doc.EndUpdateCharacters(commentCp);
                }

                var commentRegex2 = new Regex(@"/\*.*?\*/", RegexOptions.Singleline);
                var commentRanges2 = doc.FindAll(commentRegex2);
                foreach (var range in commentRanges2)
                {
                    var commentCp = doc.BeginUpdateCharacters(range);
                    commentCp.ForeColor = _commentColor;
                    doc.EndUpdateCharacters(commentCp);
                }

                // 3. 문자열 찾아서 서식 적용
                var stringRegex = new Regex(@"'[^']*'");
                var stringRanges = doc.FindAll(stringRegex);
                foreach (var range in stringRanges)
                {
                    var stringCp = doc.BeginUpdateCharacters(range);
                    stringCp.ForeColor = _stringColor;
                    doc.EndUpdateCharacters(stringCp);
                }

                // 4. 키워드 찾아서 서식 적용
                var keywordRanges = doc.FindAll(_keywordsRegex);
                foreach (var range in keywordRanges)
                {
                    var keywordCp = doc.BeginUpdateCharacters(range);
                    keywordCp.ForeColor = _keywordColor;
                    doc.EndUpdateCharacters(keywordCp);
                }
            }
            finally
            {
                doc.EndUpdate();
                memoSql.TextChanged += MemoSql_TextChanged;
            }
        }
        #region Previous Code (No Changes)

        private void memoSql_KeyDown(object sender, KeyEventArgs e) 
        { 
            if (e.KeyCode == Keys.F5 && btnExecute.Enabled) 
            {
                btnExecute_Click(sender, e); 
                e.SuppressKeyPress = true; 
            } 
        }

        private async void btnExecute_Click(object sender, EventArgs e)
        {
            string sql = memoSql.Document.GetText(memoSql.Document.Selection);

            if (string.IsNullOrWhiteSpace(sql)) 
                sql = memoSql.Document.Text;

            if (string.IsNullOrWhiteSpace(sql))
            {
                WriteMessage("실행할 쿼리를 입력하세요."); 
                return;
            }

            // 초기화
            ClearResults();

            layoutControlItemResults.Visibility = DevExpress.XtraLayout.Utils.LayoutVisibility.Always;
            xtraTabControl1.SelectedTabPage = xtraTabPageResults;
            btnExecute.Enabled = false;
            lblStatus.Text = "쿼리를 실행중입니다...";
            var stopwatch = Stopwatch.StartNew();

            try 
            { 
                await Task.Run(() => { ExecuteAllTasks(sql); }); 
            }
            catch (Exception ex) 
            { 
                WriteMessage($"오류 발생 : {ex.Message}"); 
            }
            finally
            {
                stopwatch.Stop();
                btnExecute.Enabled = true;
                long rowCount = gridViewResults.RowCount;
                lblStatus.Text = $"실행 완료. {rowCount} rows returned in {stopwatch.Elapsed.TotalSeconds:F2} seconds.";
            }

        }
        private void ExecuteAllTasks(string sql)
        {
            try
            {
                var resultTable = ExecuteQuery(sql);

                this.Invoke((MethodInvoker)delegate 
                {
                    if(gridViewResults.Columns != null && gridViewResults.Columns.Count > 0)
                        gridViewResults.Columns.Clear();

                    gridControlResults.DataSource = resultTable; 
                    gridViewResults.BestFitColumns(); 
                });
                var usedTables = ParseTableNames(sql);

                if (usedTables.Any())
                {
                    var tableCounts = GetTableCounts(usedTables);
                    this.Invoke((MethodInvoker)delegate { gridControlTables.DataSource = tableCounts; gridViewTables.BestFitColumns(); });
                }
                var suggestions = AnalyzeSql(sql);
                this.Invoke((MethodInvoker)delegate { memoAnalysis.Text = string.Join(Environment.NewLine, suggestions); });
                var planTable = GetExecutionPlan(sql);
                this.Invoke((MethodInvoker)delegate { gridControlPlan.DataSource = planTable; gridViewPlan.BestFitColumns(); });
                WriteMessage("Query executed successfully.");
            }
            catch (OracleException oex) { WriteMessage($"Oracle Error: {oex.Message}"); }
            catch (Exception ex) { WriteMessage($"General Error: {ex.Message}"); }
        }

        private DataTable ExecuteQuery(string sql)
        {
            var dataTable = new DataTable();
            DataSet dataSet = new DataSet();
            using (OracleConnection oracleConnection = new OracleConnection(CONNECTION_STRING))
            {
                oracleConnection.Open();

                OracleDataAdapter da;
                OracleCommand selectCommand = new OracleCommand();
                selectCommand.Connection = oracleConnection;

                selectCommand.CommandType = CommandType.Text;
                selectCommand.CommandText = sql;

                try
                {
                    object result = selectCommand.ExecuteScalar();
                    da = new OracleDataAdapter(selectCommand);
                    da.Fill(dataSet, "mytable");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{ex.Message}\nDBMS 정보를 확인해보세요.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                return dataSet.Tables[0];
            }
        }

        private List<string> ParseTableNames(string sql)
        {
            var regex = new Regex(@"(?:FROM|JOIN)\s+([a-zA-Z0-9_]+(?:\.[a-zA-Z0-9_]+)?)\b", RegexOptions.IgnoreCase);
            var matches = regex.Matches(sql);
            return matches.Cast<Match>().Select(m => m.Groups[1].Value.ToUpper()).Distinct().ToList();
        }

        private List<TableInfo> GetTableCounts(List<string> tableNames)
        {
            var results = new List<TableInfo>();
            using (OracleConnection oracleConnection = new OracleConnection(CONNECTION_STRING))
            {
                oracleConnection.Open();
                foreach (var tableName in tableNames)
                {
                    try
                    {
                        using (var cmd = new OracleCommand($"SELECT COUNT(*) FROM {tableName}", oracleConnection))
                        {
                            long count = Convert.ToInt64(cmd.ExecuteScalar());
                            results.Add(new TableInfo { TableName = tableName, RowCount = count });
                        }
                    }
                    catch (OracleException) 
                    { 
                        results.Add(new TableInfo { TableName = tableName, RowCount = -1 }); 
                    }
                }
            }

            return results;
        }
        private DataTable GetExecutionPlan(string sql)
        {
            using (OracleConnection oracleConnection = new OracleConnection(CONNECTION_STRING))
            {
                oracleConnection.Open();

                using (var cmd = new OracleCommand($"EXPLAIN PLAN FOR {sql}", oracleConnection))
                {
                    cmd.ExecuteNonQuery();
                }
            }

            var planTable = new DataTable();
            using (OracleConnection oracleConnection = new OracleConnection(CONNECTION_STRING))
            {
                oracleConnection.Open();

                using (var cmd = new OracleCommand("SELECT * FROM TABLE(DBMS_XPLAN.DISPLAY)", oracleConnection))
                using (var adapter = new OracleDataAdapter(cmd))
                {
                    adapter.Fill(planTable);
                }
            }
            return planTable;
        }
        private List<string> AnalyzeSql(string sql)
        {
            var suggestions = new List<string>();
            var upperSql = sql.ToUpper();

            if (upperSql.Contains("SELECT *")) 
            { 
                suggestions.Add("💡 [Suggestion] 'SELECT *' is used. Consider specifying the exact columns needed."); 
            }
            if ((upperSql.StartsWith("UPDATE ") || upperSql.StartsWith("DELETE ")) && !upperSql.Contains(" WHERE ")) 
            { 
                suggestions.Add("⚠️ [Warning] UPDATE or DELETE without a WHERE clause."); 
            }
            if (upperSql.Contains("LIKE '%")) 
            { 
                suggestions.Add("💡 [Suggestion] LIKE with a leading wildcard may prevent index usage."); 
            }
            if (!suggestions.Any()) 
            { 
                suggestions.Add("✅ No basic issues found."); 
            }

            return suggestions;
        }
        private void ClearResults()
        {
            if (gridViewResults.Columns != null && gridViewResults.Columns.Count > 0)
                gridViewResults.Columns.Clear();

            gridControlResults.DataSource = null;
            gridControlTables.DataSource = null;
            gridControlPlan.DataSource = null;
            memoAnalysis.Text = string.Empty;
            memoMessages.Text = string.Empty;
        }
        private void WriteMessage(string message)
        {
            if (memoMessages.InvokeRequired) 
            { 
                memoMessages.Invoke((MethodInvoker)delegate 
                { 
                    memoMessages.Text = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}{memoMessages.Text}"; 
                }); 
            }
            else 
            { 
                memoMessages.Text = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}{memoMessages.Text}"; 
            }
        }
        #endregion
    }

    public class TableInfo
    {
        public string TableName { get; set; }
        public long RowCount { get; set; }
    }
}
