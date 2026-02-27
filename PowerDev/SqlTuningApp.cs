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

using Match = System.Text.RegularExpressions.Match;
using TextBox = System.Windows.Forms.TextBox;
using Padding = System.Windows.Forms.Padding;

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

        private object _commandLock = new object();
        private OracleCommand _activeCommand; // 현재 실행 중인 커맨드 추적
        private bool _isExecuting = false;    // 실행 상태 플래그

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

            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime) return;

            this.Load += SqlExecutorControl_Load;

            layoutControlItemProgress.Visibility = LayoutVisibility.Never;

            // [수정] 충돌을 일으키던 _parsingTimer 제거함
            _executionTimer = new Timer { Interval = 1000 };
            _executionTimer.Tick += ExecutionTimer_Tick;

            try { LoadBindVariables(); } catch { }
        }

        private void SqlExecutorControl_Load(object sender, EventArgs e)
        {
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime) return;

            if (memoSql != null)
            {
                // RichEdit 기본 설정
                memoSql.ActiveViewType = DevExpress.XtraRichEdit.RichEditViewType.Simple;
                memoSql.Document.DefaultCharacterProperties.FontName = "Consolas";
                memoSql.Document.DefaultCharacterProperties.FontSize = 10;

                // [핵심] 고성능 구문 강조 서비스 등록
                // 기존 타이머 방식 대신 이 서비스가 자동으로 컬러링을 처리합니다.
                memoSql.RemoveService(typeof(ISyntaxHighlightService));
                memoSql.AddService(typeof(ISyntaxHighlightService), new SyntaxHighlightServiceSQL(memoSql.Document));
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

            // 1. 시작 노드 (ID=0) - 하늘색 배경
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

            // 3. 병목 지점 - 붉은 글자
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
            // 1. 이미 실행 중이라면 '취소' 동작 수행
            if (_isExecuting)
            {
                if (MessageBox.Show("쿼리 실행을 중단하시겠습니까?", "확인",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    CancelCurrentCommand();
                    lblStatus.Text = "취소 요청 중...";
                }
                return;
            }

            // 2. 실행 준비
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

            // UI 초기화
            ClearResults();
            layoutControlItemResults.Visibility = LayoutVisibility.Always;
            xtraTabControl1.SelectedTabPage = xtraTabPageResults;

            // [변경] 버튼 상태를 '중지' 모드로 변경
            _isExecuting = true;
            btnExecute.Text = "중지";
            // btnExecute.Enabled = false; // <-- 비활성화 로직 제거 (클릭 가능해야 함)

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
                // 정리 작업
                if (_executionTimer != null) _executionTimer.Stop();
                if (_queryStopwatch != null) _queryStopwatch.Stop();

                _isExecuting = false;
                btnExecute.Text = "실행"; // 버튼 텍스트 복구
                btnExecute.Enabled = true;
                progressBarQuery.Properties.Stopped = true;

                if (layoutControlItemProgress != null)
                    layoutControlItemProgress.Visibility = LayoutVisibility.Never;

                // 취소된 경우 데이터가 없을 수 있으므로 체크
                long rowCount = gridViewResults.RowCount;
                lblStatus.Text = $"작업 종료. ({rowCount}건) - {_queryStopwatch.Elapsed.TotalSeconds:F2}초";
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // [중요] 컨트롤이 사라질 때 실행 중인 쿼리 강제 취소
                CancelCurrentCommand();

                if (_executionTimer != null)
                {
                    _executionTimer.Stop();
                    _executionTimer.Dispose();
                }
                if (components != null)
                {
                    components.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        private void ExecuteAllTasks(string sql)
        {
            try
            {
                string pagedSql = ConvertToPagedQuery(sql, 100);
                var resultTable = ExecuteQuery(pagedSql);

                this.Invoke((MethodInvoker)delegate
                {
                    if (gridViewResults.Columns != null) gridViewResults.Columns.Clear();
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
                    if (!string.IsNullOrEmpty(snippet)) suggestions.Add($"   -> 관련 쿼리: \"{snippet}\"");
                    suggestions.Add($"   -> Cost: {scan.Cost:N0}");
                }
            }

            // 2. Cartesian Product
            var cartesians = plans.Where(p => p.Operation.Contains("MERGE JOIN") && p.Options.Contains("CARTESIAN")).ToList();
            foreach (var cart in cartesians)
            {
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
                if (string.IsNullOrEmpty(existingAdvice))
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
                    if (string.IsNullOrEmpty(autoDiag))
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

                        results.Add(new TableInfo
                        {
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
            // [수정] POSITION 컬럼 포함
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
            CalculateExecutionOrder(list); // 순서 계산
            return list;
        }

        // 실행 순서 계산
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
            try
            {
                if (File.Exists(_bindVarFilePath))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(List<BindVarItem>));
                    using (StreamReader reader = new StreamReader(_bindVarFilePath))
                    {
                        var list = (List<BindVarItem>)serializer.Deserialize(reader);
                        if (list != null)
                            _bindVarCache = list.ToDictionary(k => k.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);
                    }
                }
            }
            catch { }
        }
        private void SaveBindVariables()
        {
            try
            {
                var list = _bindVarCache.Select(kv => new BindVarItem { Key = kv.Key, Value = kv.Value }).ToList();
                XmlSerializer serializer = new XmlSerializer(typeof(List<BindVarItem>));
                using (StreamWriter writer = new StreamWriter(_bindVarFilePath))
                {
                    serializer.Serialize(writer, list);
                }
            }
            catch { }
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
            foreach (var varName in variables)
            {
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
        private DataTable ExecuteQuery(string sql)
        {
            var dt = new DataTable();
            var ds = new DataSet();

            // using 블록을 사용하여 예외 발생 시에도 Connection이 닫히도록 보장
            using (var conn = new OracleConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (var cmd = new OracleCommand(sql, conn))
                {
                    // [핵심] 현재 실행 중인 커맨드를 전역 변수에 등록 (스레드 안전하게)
                    lock (_commandLock)
                    {
                        _activeCommand = cmd;
                    }

                    try
                    {
                        // OracleDataAdapter는 내부적으로 cmd를 실행함
                        using (var adapter = new OracleDataAdapter(cmd))
                        {
                            adapter.Fill(ds, "t");
                        }
                    }
                    catch (OracleException ex)
                    {
                        // ORA-01013: 사용자가 작업을 취소함
                        if (ex.Number == 1013)
                        {
                            WriteMessage("사용자에 의해 쿼리 실행이 취소되었습니다.");
                            return null; // 취소 시 빈 결과 반환 처리
                        }
                        throw; // 다른 에러는 상위로 전파
                    }
                    finally
                    {
                        // [핵심] 실행 완료 후 커맨드 해제
                        lock (_commandLock)
                        {
                            _activeCommand = null;
                        }
                    }
                }
            }
            return ds.Tables.Count > 0 ? ds.Tables[0] : dt;
        }
        private void ClearResults() { gridControlResults.DataSource = null; gridControlTables.DataSource = null; treeListPlan.DataSource = null; memoAnalysis.Text = ""; memoMessages.Text = ""; }
        private void WriteMessage(string msg) { string log = $"[{DateTime.Now:HH:mm:ss}] {msg}\r\n"; if (memoMessages.InvokeRequired) memoMessages.Invoke((MethodInvoker)(() => memoMessages.Text = log + memoMessages.Text)); else memoMessages.Text = log + memoMessages.Text; }
        private List<string> AnalyzeSql(string sql) { var s = new List<string>(); if (sql.ToUpper().Contains("SELECT *")) s.Add("💡 [제안] 'SELECT *' 사용 지양"); return s; }

        private void CancelCurrentCommand()
        {
            lock (_commandLock)
            {
                if (_activeCommand != null)
                {
                    try
                    {
                        _activeCommand.Cancel(); // 오라클 세션에 Cancel 신호 전송
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Command Cancel Error: {ex.Message}");
                    }
                }
            }
        }

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

        private void RunExplainPlan(string sql) 
        { 
            using (var conn = new OracleConnection(CONNECTION_STRING)) 
            { 
                conn.Open(); 
                try 
                { 
                    new OracleCommand("DELETE FROM PLAN_TABLE", conn).ExecuteNonQuery(); 
                } 
                catch { } 

                new OracleCommand($"EXPLAIN PLAN FOR {sql}", conn).ExecuteNonQuery(); 
            } 
        }


        //public class SyntaxHighlightServiceSQL : ISyntaxHighlightService
        //{
        //    private readonly Document _document;
        //    private readonly SyntaxHighlightProperties _defaultSettings = new SyntaxHighlightProperties() { ForeColor = Color.Black };
        //    private readonly SyntaxHighlightProperties _keywordSettings = new SyntaxHighlightProperties() { ForeColor = Color.Blue };
        //    private readonly SyntaxHighlightProperties _stringSettings = new SyntaxHighlightProperties() { ForeColor = Color.Red };
        //    private readonly SyntaxHighlightProperties _commentSettings = new SyntaxHighlightProperties() { ForeColor = Color.Green };

        //    private readonly Regex _keywords;
        //    // [개선 1] 문자열 내부의 이스케이프된 따옴표('') 처리 추가
        //    private readonly Regex _quotedString = new Regex(@"'(''|[^'])*'", RegexOptions.Compiled);
        //    private readonly Regex _comment = new Regex(@"--.*|/\*[\s\S]*?\*/", RegexOptions.Compiled);

        //    public SyntaxHighlightServiceSQL(Document document)
        //    {
        //        _document = document;
        //        string[] keywords = 
        //        {
        //            "SELECT", "FROM", "WHERE", "JOIN", "LEFT", "RIGHT", "INNER", "OUTER", "ON",
        //            "GROUP", "BY", "ORDER", "HAVING", "AS", "INSERT", "INTO", "VALUES", "UPDATE",
        //            "SET", "DELETE", "TRUNCATE", "TABLE", "CREATE", "ALTER", "DROP", "INDEX",
        //            "VIEW", "PROCEDURE", "FUNCTION", "TRIGGER", "UNION", "ALL", "AND", "OR",
        //            "NOT", "NULL", "IS", "LIKE", "IN", "BETWEEN", "EXISTS", "CASE", "WHEN",
        //            "THEN", "ELSE", "END", "COUNT", "SUM", "AVG", "MAX", "MIN", "DISTINCT",
        //            "TOP", "ROWNUM", "BEGIN", "END", "DECLARE"
        //        };

        //        // [참고] 키워드 정규식은 단어 경계(\b)를 사용하여 부분 일치를 방지합니다.
        //        _keywords = new Regex(@"\b(" + string.Join("|", keywords) + @")\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        //    }

        //    public void ForceExecute() => Execute();

        //    public void Execute()
        //    {
        //        string text = _document.Text;
        //        if (string.IsNullOrEmpty(text)) return;

        //        // 1. 모든 토큰 후보를 하나의 리스트에 수집합니다.
        //        var candidates = new List<SyntaxHighlightToken>();

        //        // 문자열 (String)
        //        foreach (Match m in _quotedString.Matches(text))
        //        {
        //            candidates.Add(new SyntaxHighlightToken(m.Index, m.Length, _stringSettings));
        //        }

        //        // 주석 (Comment)
        //        foreach (Match m in _comment.Matches(text))
        //        {
        //            candidates.Add(new SyntaxHighlightToken(m.Index, m.Length, _commentSettings));
        //        }

        //        // 키워드 (Keyword)
        //        foreach (Match m in _keywords.Matches(text))
        //        {
        //            candidates.Add(new SyntaxHighlightToken(m.Index, m.Length, _keywordSettings));
        //        }

        //        // 2. [핵심] 시작 위치 순서대로 정렬합니다.
        //        // 위치가 같다면 길이가 긴 것(예: 문자열/주석)이 키워드보다 우선하도록 정렬합니다.
        //        candidates.Sort((t1, t2) =>
        //        {
        //            int cmp = t1.Start.CompareTo(t2.Start);
        //            if (cmp == 0) return t2.Length.CompareTo(t1.Length); // 길이가 긴 것이 우선
        //            return cmp;
        //        });

        //        // 3. [핵심] 겹치는 토큰(Overlap) 제거 로직
        //        // 이미 처리된 영역(lastEnd) 내에 시작점이 있는 토큰은 무시합니다.
        //        var tokens = new List<SyntaxHighlightToken>();
        //        int lastEnd = 0;

        //        foreach (var token in candidates)
        //        {
        //            // 현재 토큰의 시작점이 이전 토큰의 끝점보다 같거나 뒤에 있어야 겹치지 않음
        //            if (token.Start >= lastEnd)
        //            {
        //                tokens.Add(token);
        //                lastEnd = token.Start + token.Length;
        //            }
        //        }

        //        // 4. 최종 토큰 적용
        //        _document.ApplySyntaxHighlight(tokens);
        //    }
        //}
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

        // 순서 계산용
        public int Position { get; set; }
        public int ExecutionOrder { get; set; }
        public string Advice { get; set; }
    }
}
