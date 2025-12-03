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
using System.IO;
using System.Xml.Serialization;
using DevExpress.XtraTreeList;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraLayout.Utils;
using DevExpress.XtraRichEdit.API.Native;
using DevExpress.XtraRichEdit.Services;

// [요청하신 별칭 추가]
using Match = System.Text.RegularExpressions.Match;
using Padding = System.Windows.Forms.Padding;
using TextBox = System.Windows.Forms.TextBox;

namespace DevTools.UI.Control
{
    [Browsable(true)]
    public partial class SqlTuningApp : UserControl
    {
        // -----------------------------------------------------------
        // [필드] 설정 및 변수 관리
        // -----------------------------------------------------------
        private Timer _executionTimer;
        private Stopwatch _queryStopwatch;

        private readonly string _bindVarFilePath = Path.Combine(Application.StartupPath, "bind_vars.xml");
        private Dictionary<string, string> _bindVarCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 실행 계획 강조 임계값
        private decimal _totalPlanCost = 0;

        private readonly Dictionary<string, string> _operationAdvice = new Dictionary<string, string>
        {
            { "FILTER", "데이터를 걸러내는 작업입니다. 비효율적인 서브쿼리 반복을 확인하세요." },
            { "REMOTE", "DB Link 원격 액세스입니다. 네트워크 부하를 주의하세요." },
            { "VIEW", "View 병합 실패 가능성이 있습니다. 'MERGE' 힌트를 고려하세요." },
            { "WINDOW", "분석 함수 사용 중입니다. 대량 정렬 부하에 주의하세요." },
            { "BUFFER SORT", "메모리 소트 작업입니다. 반복 액세스를 줄이세요." },
            { "INDEX SKIP SCAN", "선행 컬럼이 조건에 없습니다. 인덱스 재구성을 고려하세요." },
            { "MAT_VIEW", "MView 액세스입니다. 데이터 최신성을 확인하세요." },
            { "BITMAP", "비트맵 변환 중입니다. 락 경합에 주의하세요." },
            { "COUNT STOPKEY", "ROWNUM 제한으로 성능에 유리한 작업입니다." }
        };

        string CONNECTION_STRING
        {
            get { return SystemInfoContext.Current == null ? null : SystemInfoContext.Current["CONSTR"]; }
        }

        public SqlTuningApp()
        {
            InitializeComponent();

            // [중요] Load 이벤트 수동 연결
            this.Load += SqlExecutorControl_Load;

            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime) return;

            // 실행 시간 타이머 초기화
            _executionTimer = new Timer { Interval = 1000 };
            _executionTimer.Tick += ExecutionTimer_Tick;

            try { LoadBindVariables(); } catch { }
        }

        private void SqlExecutorControl_Load(object sender, EventArgs e)
        {
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime) return;

            if (memoSql != null)
            {
                memoSql.ActiveViewType = DevExpress.XtraRichEdit.RichEditViewType.Simple;
                memoSql.Document.DefaultCharacterProperties.FontName = "Consolas";
                memoSql.Document.DefaultCharacterProperties.FontSize = 10;
                
                // [핵심] 고성능 구문 강조 서비스 등록 (기존 타이머 방식 대체)
                memoSql.RemoveService(typeof(ISyntaxHighlightService));
                memoSql.AddService(typeof(ISyntaxHighlightService), new SqlSyntaxHighlightService(memoSql.Document));
            }

            if (treeListPlan != null)
            {
                treeListPlan.NodeCellStyle += TreeListPlan_NodeCellStyle;
            }

            if (layoutControlItemProgress != null)
                layoutControlItemProgress.Visibility = LayoutVisibility.Never;
        }

        // -----------------------------------------------------------
        // [이벤트] 타이머 및 UI 업데이트
        // -----------------------------------------------------------
        private void ExecutionTimer_Tick(object sender, EventArgs e)
        {
            if (_queryStopwatch != null && _queryStopwatch.IsRunning)
            {
                lblStatus.Text = $"쿼리 실행 중... ({_queryStopwatch.Elapsed.TotalSeconds:N0}초)";
            }
        }

        private void TreeListPlan_NodeCellStyle(object sender, GetCustomNodeCellStyleEventArgs e)
        {
            var node = e.Node;
            if (node == null) return;

            int id = Convert.ToInt32(node.GetValue("Id"));
            string advice = node.GetValue("Advice")?.ToString();
            object costObj = node.GetValue("Cost");
            decimal cost = (costObj != null && costObj != DBNull.Value) ? Convert.ToDecimal(costObj) : 0;
            string operation = node.GetValue("Operation")?.ToString() ?? "";
            string options = node.GetValue("Options")?.ToString() ?? "";

            // 1. 시작 노드 (ID=0) - 진한 하늘색 배경
            if (id == 0)
            {
                e.Appearance.BackColor = Color.FromArgb(180, 220, 255); 
                e.Appearance.ForeColor = Color.Black;
                e.Appearance.Font = new Font(e.Appearance.Font, FontStyle.Bold);
                return;
            }

            // 2. 튜닝 제안이 있는 노드 - 연한 주황 배경
            if (!string.IsNullOrEmpty(advice))
            {
                e.Appearance.BackColor = Color.FromArgb(255, 245, 210);
                e.Appearance.Font = new Font(e.Appearance.Font, FontStyle.Bold);
            }

            // 3. 병목 지점 - 붉은 글자 (배경색이 없으면 회색 추가)
            bool isExpensive = (_totalPlanCost > 0 && cost > (_totalPlanCost * 0.3m));
            bool isFullScan = (operation.Contains("TABLE ACCESS") && options.Contains("FULL"));

            if (isExpensive || isFullScan)
            {
                e.Appearance.ForeColor = Color.Red;
                e.Appearance.Font = new Font(e.Appearance.Font, FontStyle.Bold);
                if (e.Appearance.BackColor == Color.Empty) e.Appearance.BackColor = Color.FromArgb(245, 245, 245);
            }
        }

        private void memoSql_KeyDown(object sender, KeyEventArgs e) 
        { 
            if ((e.KeyCode == Keys.F5 || (e.Control && e.KeyCode == Keys.Enter)) && btnExecute.Enabled) 
            {
                btnExecute_Click(sender, e); 
                e.SuppressKeyPress = true; 
            } 
        }

        private async void btnExecute_Click(object sender, EventArgs e)
        {
            string rawSql = memoSql.Document.GetText(memoSql.Document.Selection);
            if (string.IsNullOrWhiteSpace(rawSql)) rawSql = memoSql.Document.Text;
            rawSql = rawSql.Trim().TrimEnd(';');

            if (string.IsNullOrWhiteSpace(rawSql))
            {
                WriteMessage("실행할 쿼리를 입력하세요.");
                return;
            }

            string finalSql;
            try
            {
                finalSql = ProcessBindVariables(rawSql);
            }
            catch (OperationCanceledException)
            {
                WriteMessage("실행이 취소되었습니다.");
                return;
            }

            ClearResults();
            layoutControlItemResults.Visibility = LayoutVisibility.Always;
            xtraTabControl1.SelectedTabPage = xtraTabPageResults;
            
            btnExecute.Enabled = false;
            
            if (layoutControlItemProgress != null)
                layoutControlItemProgress.Visibility = LayoutVisibility.Always;
            
            progressBarQuery.Properties.Stopped = false;
            lblStatus.Text = "쿼리 실행 중... (0초)";
            
            _queryStopwatch = Stopwatch.StartNew();
            if (_executionTimer != null) _executionTimer.Start();

            try 
            { 
                await Task.Run(() => { ExecuteAllTasks(finalSql); }); 
            }
            catch (Exception ex) 
            { 
                WriteMessage($"오류 발생 : {ex.Message}"); 
            }
            finally
            {
                if (_executionTimer != null) _executionTimer.Stop();
                if (_queryStopwatch != null) _queryStopwatch.Stop();
                
                btnExecute.Enabled = true;
                progressBarQuery.Properties.Stopped = true;
                
                if (layoutControlItemProgress != null)
                    layoutControlItemProgress.Visibility = LayoutVisibility.Never;

                long rowCount = gridViewResults.RowCount;
                lblStatus.Text = $"실행 완료. (상위 {rowCount}건) - {_queryStopwatch.Elapsed.TotalSeconds:F2}초 소요";
            }
        }

        private void ExecuteAllTasks(string sql)
        {
            try
            {
                string pagedSql = ConvertToPagedQuery(sql, 100);
                var resultTable = ExecuteQuery(pagedSql);

                this.Invoke((MethodInvoker)delegate 
                {
                    if(gridViewResults.Columns != null) gridViewResults.Columns.Clear();
                    gridControlResults.DataSource = resultTable; 
                    gridViewResults.BestFitColumns(); 
                });

                var usedTables = ParseTableNames(sql);
                if (usedTables.Any())
                {
                    var tableInfos = GetTableInfos(usedTables);
                    this.Invoke((MethodInvoker)delegate 
                    { 
                        gridControlTables.DataSource = tableInfos; 
                        gridViewTables.BestFitColumns(); 
                    });
                }

                RunExplainPlan(sql);
                var detailedPlan = GetExecutionPlanDetail();
                _totalPlanCost = detailedPlan.FirstOrDefault(p => p.Id == 0)?.Cost ?? 0;

                var textAnalysis = AnalyzeSql(sql); 
                var planAnalysis = AnalyzeExecutionPlanAndMapToTree(sql, detailedPlan);
                
                var finalSuggestions = new List<string>();
                finalSuggestions.Add("💡 [알림] 결과는 상위 100건만 조회되었습니다. (분석은 전체 기준)");
                finalSuggestions.Add("");
                finalSuggestions.AddRange(textAnalysis);
                if (finalSuggestions.Count > 0 && planAnalysis.Count > 0)
                    finalSuggestions.Add("\r\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\r\n"); 
                finalSuggestions.AddRange(planAnalysis);

                this.Invoke((MethodInvoker)delegate 
                { 
                    memoAnalysis.Text = string.Join(Environment.NewLine + Environment.NewLine, finalSuggestions);
                    treeListPlan.DataSource = detailedPlan; 
                    treeListPlan.ExpandAll();
                    treeListPlan.BestFitColumns(); 
                });

                WriteMessage("실행 완료.");
            }
            catch (OracleException oex) { WriteMessage($"Oracle Error: {oex.Message}"); }
            catch (Exception ex) { WriteMessage($"Error: {ex.Message}"); }
        }

        // -----------------------------------------------------------
        // [로직] 실행 계획 분석 및 트리 매핑
        // -----------------------------------------------------------
        private List<string> AnalyzeExecutionPlanAndMapToTree(string originalSql, List<PlanInfo> plans)
        {
            var suggestions = new List<string>();
            if (plans == null || plans.Count == 0) return suggestions;

            decimal totalCost = plans.FirstOrDefault(p => p.Id == 0)?.Cost ?? 0;
            suggestions.Add($"📊 [요약] 전체 예상 비용 (Cost): {totalCost:N0}");

            // 1. Full Table Scan
            var fullScans = plans.Where(p => p.Operation == "TABLE ACCESS" && p.Options == "FULL").ToList();
            foreach (var scan in fullScans)
            {
                if (scan.Cost > 50) 
                {
                    string snippet = ExtractProblematicQuery(originalSql, scan);
                    string advice = "💥 [치명적] Full Table Scan\r\n👉 조건절 인덱스 추가 필요";
                    if (!string.IsNullOrEmpty(snippet)) advice += $"\r\n(관련 쿼리: {snippet.Trim()})";
                    
                    scan.Advice = advice; 
                    suggestions.Add($"💥 [치명적] Full Table Scan 발생 (테이블: '{scan.ObjectName}')");
                    if(!string.IsNullOrEmpty(snippet)) suggestions.Add($"   -> 관련 쿼리: \"{snippet}\"");
                    suggestions.Add($"   -> Cost: {scan.Cost:N0}");
                }
            }

            // 2. Cartesian Product
            var cartesians = plans.Where(p => p.Operation.Contains("MERGE JOIN") && p.Options.Contains("CARTESIAN")).ToList();
            foreach(var cart in cartesians)
            {
                // 자식 노드 추적
                var childNodes = plans.Where(p => p.ParentId == cart.Id).ToList();
                List<string> involvedTables = new List<string>();
                foreach (var child in childNodes)
                {
                    if (!string.IsNullOrEmpty(child.ObjectName)) involvedTables.Add(child.ObjectName);
                    else if (child.Operation.Contains("SORT") || child.Operation.Contains("VIEW"))
                    {
                        var grandChild = plans.FirstOrDefault(p => p.ParentId == child.Id);
                        if (grandChild != null && !string.IsNullOrEmpty(grandChild.ObjectName)) involvedTables.Add(grandChild.ObjectName);
                    }
                }
                string targets = involvedTables.Count > 0 ? string.Join(" ✖ ", involvedTables) : "Unknown Tables";
                string snippet = ExtractProblematicQuery(originalSql, cart);

                string advice = $"⚠️ [경고] 카테시안 곱 발생!\r\n👉 대상: {targets}\r\n👉 조인 조건(ON/WHERE) 누락 확인";
                if (!string.IsNullOrEmpty(snippet)) advice += $"\r\n(관련 쿼리: {snippet.Trim()})";

                cart.Advice = advice;
                suggestions.Add($"⚠️ [경고] 카테시안 곱(Cartesian Product) 발생");
                suggestions.Add($"   -> 대상 테이블: {targets}");
            }

            // 3. Sort Group By
            var sortGroups = plans.Where(p => p.Operation == "SORT" && p.Options == "GROUP BY").ToList();
            foreach (var sort in sortGroups)
            {
                if (totalCost > 0 && (sort.Cost / totalCost) > 0.1m)
                {
                    bool hasOrderBy = originalSql.IndexOf("ORDER BY", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (hasOrderBy)
                        sort.Advice = "💡 [팁] Sort Group By\r\n👉 Group By 컬럼 인덱스 생성 권장";
                    else
                        sort.Advice = "💡 [팁] 불필요한 정렬 가능성\r\n👉 'HASH GROUP BY' 힌트 고려";

                    suggestions.Add($"💡 [튜닝 팁] 'SORT GROUP BY' 부하 감지 (Cost: {sort.Cost:N0})");
                }
            }

            // 4. Top Cost
            var expensiveStep = plans.Where(p => p.Id > 0).OrderByDescending(p => p.Cost).FirstOrDefault();
            if (expensiveStep != null && expensiveStep.Cost > 0)
            {
                string existingAdvice = expensiveStep.Advice ?? "";
                if(string.IsNullOrEmpty(existingAdvice))
                {
                    string autoDiag = "";
                    foreach (var kvp in _operationAdvice)
                    {
                        if (expensiveStep.Operation.Contains(kvp.Key) || expensiveStep.Options.Contains(kvp.Key))
                        {
                            autoDiag = $"\r\n👉 {kvp.Value}";
                            break;
                        }
                    }
                    if(string.IsNullOrEmpty(autoDiag))
                    {
                         if (expensiveStep.Operation == "NESTED LOOPS") autoDiag = "\r\n👉 연결고리 인덱스 확인 / Hash Join 고려";
                         else if (expensiveStep.Operation == "HASH JOIN") autoDiag = "\r\n👉 조인 순서(Leading) 조정 고려";
                    }

                    string snippet = ExtractProblematicQuery(originalSql, expensiveStep);
                    string snippetText = string.IsNullOrEmpty(snippet) ? "" : $"\r\n(쿼리: {snippet.Trim()})";
                    
                    expensiveStep.Advice = $"🔥 [병목] 최고 비용 발생{snippetText}{autoDiag}";
                }
                
                double percentage = totalCost > 0 ? (double)(expensiveStep.Cost / totalCost * 100) : 0;
                suggestions.Add($"🔥 [병목 지점] 가장 비용이 높은 작업 (전체의 {percentage:F1}%) : {expensiveStep.Operation} {expensiveStep.Options}");
            }

            return suggestions;
        }

        // -----------------------------------------------------------
        // [헬퍼] 기타 유틸 메서드
        // -----------------------------------------------------------
        private List<TableInfo> GetTableInfos(List<string> tableNames)
        {
            var results = new List<TableInfo>();
            using (OracleConnection conn = new OracleConnection(CONNECTION_STRING))
            {
                conn.Open();
                foreach (var fullTableName in tableNames)
                {
                    string owner = "";
                    string tableName = fullTableName;
                    if (fullTableName.Contains("."))
                    {
                        var parts = fullTableName.Split('.');
                        owner = parts[0];
                        tableName = parts[1];
                    }

                    try
                    {
                        long count = -1;
                        using (var cmd = new OracleCommand($"SELECT COUNT(*) FROM {fullTableName}", conn))
                        {
                            object val = cmd.ExecuteScalar();
                            count = (val != null) ? Convert.ToInt64(val) : 0;
                        }

                        string tableDesc = "";
                        string qryComment = "SELECT COMMENTS FROM ALL_TAB_COMMENTS WHERE TABLE_NAME = :tname";
                        if (!string.IsNullOrEmpty(owner)) qryComment += " AND OWNER = :own";

                        using (var cmd = new OracleCommand(qryComment, conn))
                        {
                            cmd.Parameters.Add("tname", tableName);
                            if (!string.IsNullOrEmpty(owner)) cmd.Parameters.Add("own", owner);
                            
                            object val = cmd.ExecuteScalar();
                            tableDesc = val?.ToString() ?? "";
                        }

                        results.Add(new TableInfo { 
                            Owner = string.IsNullOrEmpty(owner) ? "(Current)" : owner,
                            TableName = tableName,
                            TableDesc = tableDesc,
                            RowCount = count 
                        });
                    }
                    catch 
                    { 
                        results.Add(new TableInfo { Owner = owner, TableName = tableName, TableDesc = "Error/Not Found", RowCount = -1 }); 
                    }
                }
            }
            return results.OrderBy(t => t.Owner).ThenBy(t => t.TableName).ToList();
        }

        private string ConvertToPagedQuery(string originalSql, int limit)
        {
            return $"SELECT * FROM ({originalSql}) WHERE ROWNUM <= {limit}";
        }

        private List<PlanInfo> GetExecutionPlanDetail()
        {
            var list = new List<PlanInfo>();
            // Step 계산을 위한 POSITION 컬럼 포함
            string query = @"
                SELECT ID, PARENT_ID, OPERATION, OPTIONS, OBJECT_NAME, COST, CARDINALITY, POSITION
                FROM PLAN_TABLE
                START WITH ID = 0
                CONNECT BY PRIOR ID = PARENT_ID
                ORDER BY ID";

            using (OracleConnection conn = new OracleConnection(CONNECTION_STRING))
            {
                conn.Open();
                using (OracleCommand cmd = new OracleCommand(query, conn))
                using (OracleDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new PlanInfo
                        {
                            Id = Convert.ToInt32(reader["ID"]),
                            ParentId = reader["PARENT_ID"] == DBNull.Value ? -1 : Convert.ToInt32(reader["PARENT_ID"]),
                            Operation = reader["OPERATION"].ToString(),
                            Options = reader["OPTIONS"].ToString(),
                            ObjectName = reader["OBJECT_NAME"].ToString(),
                            Cost = reader["COST"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["COST"]),
                            Cardinality = reader["CARDINALITY"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["CARDINALITY"]),
                            Position = reader["POSITION"] == DBNull.Value ? 0 : Convert.ToInt32(reader["POSITION"])
                        });
                    }
                }
            }
            CalculateExecutionOrder(list); // 실행 순서 계산
            return list;
        }

        // 실행 순서(Step) 계산
        private void CalculateExecutionOrder(List<PlanInfo> plans)
        {
            int currentStep = 1;
            void Traverse(int parentId)
            {
                var children = plans.Where(p => p.ParentId == parentId).OrderBy(p => p.Position).ToList();
                foreach (var child in children) Traverse(child.Id);
                var node = plans.FirstOrDefault(p => p.Id == parentId);
                if (node != null) node.ExecutionOrder = currentStep++;
            }
            var root = plans.FirstOrDefault(p => p.ParentId == -1);
            if (root != null) Traverse(root.Id);
        }

        private void LoadBindVariables()
        {
            try {
                if (File.Exists(_bindVarFilePath)) {
                    XmlSerializer serializer = new XmlSerializer(typeof(List<BindVarItem>));
                    using (StreamReader reader = new StreamReader(_bindVarFilePath)) {
                        var list = (List<BindVarItem>)serializer.Deserialize(reader);
                        if (list != null)
                            _bindVarCache = list.ToDictionary(k => k.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);
                    }
                }
            } catch { }
        }
        private void SaveBindVariables()
        {
            try {
                var list = _bindVarCache.Select(kv => new BindVarItem { Key = kv.Key, Value = kv.Value }).ToList();
                XmlSerializer serializer = new XmlSerializer(typeof(List<BindVarItem>));
                using (StreamWriter writer = new StreamWriter(_bindVarFilePath)) {
                    serializer.Serialize(writer, list);
                }
            } catch { }
        }
        public class BindVarItem { public string Key { get; set; } public string Value { get; set; } }

        private string ProcessBindVariables(string sql) 
        {
            var regex = new Regex(@"&([a-zA-Z0-9_]+)");
            var matches = regex.Matches(sql);
            var variables = new HashSet<string>();
            foreach (Match match in matches) variables.Add(match.Groups[1].Value);
            if (variables.Count == 0) return sql;

            var variableValues = ShowMultiInputDialog(variables, _bindVarCache);
            if (variableValues == null) throw new OperationCanceledException();

            foreach (var kvp in variableValues)
            {
                if (_bindVarCache.ContainsKey(kvp.Key)) _bindVarCache[kvp.Key] = kvp.Value;
                else _bindVarCache.Add(kvp.Key, kvp.Value);
            }
            SaveBindVariables();

            string processedSql = sql;
            foreach (var kvp in variableValues) processedSql = Regex.Replace(processedSql, $"&{kvp.Key}\\b", kvp.Value);
            return processedSql;
        }

        private Dictionary<string, string> ShowMultiInputDialog(HashSet<string> variables, Dictionary<string, string> defaults)
        {
            Form promptForm = new Form() { Width = 400, Height = Math.Min(600, 150 + (variables.Count * 40)), FormBorderStyle = FormBorderStyle.FixedDialog, Text = "바인드 변수 입력", StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false, AutoScroll = true };
            Panel mainPanel = new Panel() { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(10) };
            promptForm.Controls.Add(mainPanel);
            Panel buttonPanel = new Panel() { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(10) };
            promptForm.Controls.Add(buttonPanel);
            Button btnCancel = new Button() { Text = "취소", Width = 80, Dock = DockStyle.Right, DialogResult = DialogResult.Cancel };
            Button btnOk = new Button() { Text = "확인", Width = 80, Dock = DockStyle.Right, DialogResult = DialogResult.OK };
            buttonPanel.Controls.Add(btnOk); buttonPanel.Controls.Add(new Panel() { Width = 10, Dock = DockStyle.Right }); buttonPanel.Controls.Add(btnCancel);
            promptForm.AcceptButton = btnOk; promptForm.CancelButton = btnCancel;

            var inputControls = new Dictionary<string, TextBox>();
            int topOffset = 10;
            foreach (var varName in variables) {
                mainPanel.Controls.Add(new Label() { Text = varName, Left = 10, Top = topOffset + 3, Width = 100, TextAlign = ContentAlignment.MiddleRight });
                var txt = new TextBox() { Left = 120, Top = topOffset, Width = 240 };
                if (defaults != null && defaults.ContainsKey(varName)) txt.Text = defaults[varName];
                mainPanel.Controls.Add(txt);
                inputControls.Add(varName, txt);
                topOffset += 40;
            }
            if (inputControls.Count > 0) promptForm.ActiveControl = inputControls.First().Value;
            if (promptForm.ShowDialog() == DialogResult.OK) return inputControls.ToDictionary(k => k.Key, v => v.Value.Text);
            return null;
        }

        private List<string> ParseTableNames(string sql) { return new Regex(@"(?:FROM|JOIN)\s+([a-zA-Z0-9_]+(?:\.[a-zA-Z0-9_]+)?)\b", RegexOptions.IgnoreCase).Matches(sql).Cast<Match>().Select(m => m.Groups[1].Value.ToUpper()).Distinct().ToList(); }
        private DataTable ExecuteQuery(string sql) { var dt = new DataTable(); var ds = new DataSet(); using (var conn = new OracleConnection(CONNECTION_STRING)) { conn.Open(); try { new OracleDataAdapter(sql, conn).Fill(ds, "t"); } catch (Exception ex) { MessageBox.Show(ex.Message); return dt; } return ds.Tables.Count > 0 ? ds.Tables[0] : dt; } }
        private void ClearResults() { gridControlResults.DataSource = null; gridControlTables.DataSource = null; treeListPlan.DataSource = null; memoAnalysis.Text = ""; memoMessages.Text = ""; }
        private void WriteMessage(string msg) { string log = $"[{DateTime.Now:HH:mm:ss}] {msg}\r\n"; if (memoMessages.InvokeRequired) memoMessages.Invoke((MethodInvoker)(() => memoMessages.Text = log + memoMessages.Text)); else memoMessages.Text = log + memoMessages.Text; }
        private List<string> AnalyzeSql(string sql) { var s = new List<string>(); if(sql.ToUpper().Contains("SELECT *")) s.Add("💡 [제안] 'SELECT *' 사용 지양"); return s; }
        
        private string ExtractProblematicQuery(string sql, PlanInfo plan)
        {
            if (string.IsNullOrWhiteSpace(sql)) return null;
            var lines = sql.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (!string.IsNullOrWhiteSpace(plan.ObjectName))
            {
                foreach (var line in lines) if (line.IndexOf(plan.ObjectName, StringComparison.OrdinalIgnoreCase) >= 0) return line.Trim();
            }
            string keyword = "";
            if (plan.Operation.Contains("SORT"))
            {
                if (plan.Options.Contains("GROUP")) keyword = "GROUP BY";
                else if (plan.Options.Contains("UNIQUE")) keyword = "DISTINCT";
                else if (plan.Options.Contains("ORDER")) keyword = "ORDER BY";
            }
            else if (plan.Operation.Contains("HASH JOIN") || plan.Operation.Contains("NESTED LOOPS")) keyword = "JOIN";
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                foreach (var line in lines) if (line.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) return line.Trim();
                if (keyword == "JOIN") { foreach (var line in lines) if (line.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase) >= 0) return line.Trim(); }
            }
            return null;
        }

        private void RunExplainPlan(string sql) { using (var conn = new OracleConnection(CONNECTION_STRING)) { conn.Open(); try { new OracleCommand("DELETE FROM PLAN_TABLE", conn).ExecuteNonQuery(); } catch { } new OracleCommand($"EXPLAIN PLAN FOR {sql}", conn).ExecuteNonQuery(); } }
        
        // -----------------------------------------------------------
        // [신규] 고성능 구문 강조 서비스 (ISyntaxHighlightService 구현)
        // -----------------------------------------------------------
        public class SqlSyntaxHighlightService : ISyntaxHighlightService
        {
            private readonly DevExpress.XtraRichEdit.API.Native.Document _document;
            private readonly SyntaxHighlightProperties _defaultSettings = new SyntaxHighlightProperties() { ForeColor = Color.Black };
            private readonly SyntaxHighlightProperties _keywordSettings = new SyntaxHighlightProperties() { ForeColor = Color.Blue };
            private readonly SyntaxHighlightProperties _stringSettings = new SyntaxHighlightProperties() { ForeColor = Color.Red };
            private readonly SyntaxHighlightProperties _commentSettings = new SyntaxHighlightProperties() { ForeColor = Color.Green };

            private readonly Regex _keywords;
            private readonly Regex _quotedString = new Regex(@"'[^']*'", RegexOptions.Compiled);
            
            // [수정] 정규식 옵션 수정 (Singleline 제거, 블록 주석 처리 강화)
            private readonly Regex _comment = new Regex(@"--.*|/\*[\s\S]*?\*/", RegexOptions.Compiled);

            public SqlSyntaxHighlightService(DevExpress.XtraRichEdit.API.Native.Document document)
            {
                _document = document;
                string[] keywords = { "SELECT", "FROM", "WHERE", "JOIN", "LEFT", "RIGHT", "INNER", "OUTER", "ON", "GROUP", "BY", "ORDER", "HAVING", "AS", "INSERT", "INTO", "VALUES", "UPDATE", "SET", "DELETE", "TRUNCATE", "TABLE", "CREATE", "ALTER", "DROP", "INDEX", "VIEW", "PROCEDURE", "FUNCTION", "TRIGGER", "UNION", "ALL", "AND", "OR", "NOT", "NULL", "IS", "LIKE", "IN", "BETWEEN", "EXISTS", "CASE", "WHEN", "THEN", "ELSE", "END", "COUNT", "SUM", "AVG", "MAX", "MIN", "DISTINCT", "TOP", "ROWNUM" };
                _keywords = new Regex(@"\b(" + string.Join("|", keywords) + @")\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }

            public void ForceExecute() => Execute();

            public void Execute()
            {
                string text = _document.Text;
                if (string.IsNullOrEmpty(text)) return;

                var tokens = new List<SyntaxHighlightToken>();
                var occupiedRanges = new List<Tuple<int, int>>(); 

                // 1. Comments (최우선)
                foreach (Match m in _comment.Matches(text))
                {
                    tokens.Add(new SyntaxHighlightToken(m.Index, m.Length, _commentSettings));
                    occupiedRanges.Add(Tuple.Create(m.Index, m.Index + m.Length));
                }

                // 2. Strings
                foreach (Match m in _quotedString.Matches(text))
                {
                    if (!IsRangeOccupied(occupiedRanges, m.Index, m.Length))
                    {
                        tokens.Add(new SyntaxHighlightToken(m.Index, m.Length, _stringSettings));
                        occupiedRanges.Add(Tuple.Create(m.Index, m.Index + m.Length));
                    }
                }

                // 3. Keywords
                foreach (Match m in _keywords.Matches(text))
                {
                    if (!IsRangeOccupied(occupiedRanges, m.Index, m.Length))
                    {
                        tokens.Add(new SyntaxHighlightToken(m.Index, m.Length, _keywordSettings));
                    }
                }

                if (tokens.Count > 0)
                {
                    tokens.Sort((t1, t2) => t1.Start.CompareTo(t2.Start));
                    _document.ApplySyntaxHighlight(tokens);
                }
            }

            private bool IsRangeOccupied(List<Tuple<int, int>> occupied, int start, int length)
            {
                int end = start + length;
                foreach (var range in occupied)
                {
                    if (Math.Max(start, range.Item1) < Math.Min(end, range.Item2))
                        return true;
                }
                return false;
            }
        }
    }

    public class TableInfo
    {
        public string Owner { get; set; }
        public string TableName { get; set; }
        public string TableDesc { get; set; }
        public long RowCount { get; set; }
    }

    public class PlanInfo
    {
        public string Operation { get; set; }
        public string Options { get; set; }
        public string ObjectName { get; set; }
        public decimal Cost { get; set; }
        public decimal Cardinality { get; set; }
        public int Id { get; set; }
        public int ParentId { get; set; }
        
        // 순서 및 제안 정보
        public int Position { get; set; }
        public int ExecutionOrder { get; set; }
        public string Advice { get; set; }
    }
}
