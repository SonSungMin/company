using DevExpress.Charts.Model;
using DevExpress.XtraRichEdit.API.Native;
using DevTools.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite; // [필수] NuGet 패키지: System.Data.SQLite
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Web.Services.Description;
using System.Windows.Forms;

namespace DevTools.UI.Control
{
    [Browsable(true)]
    [ToolboxItem(true)]
    public partial class AIChat : UserControlBase
    {
        // ─────────────────────────────────────────────────────────────
        // 1. 설정 및 상수 정의
        // ─────────────────────────────────────────────────────────────
        private string EYE_MODEL = "qwen2-vl";
        private string BRAIN_MODEL = "deepseek-r1-8b";
        private const string OLLAMA_URL = "http://localhost:11434/api/generate";
        private const string CHAT_URL = "http://localhost:11434/api/chat";
        private const string EMBED_URL = "http://localhost:11434/api/embeddings";
        private const string TAGS_URL = "http://localhost:11434/api/tags";

        private const string EMBEDDING_MODEL = "nomic-embed-text";
        private string DB_CONNECTION_STRING = SQLiteExt.ConnString;

        // GPU 모니터링
        private Timer _gpuTimer;
        private PerformanceCounter _gpuUsageCounter;
        private PerformanceCounter _vramUsedCounter;
        private long _totalVramMB = 0;


        private List<SystemPrompt> _systemPropt = new List<SystemPrompt>();

        // [RAG] 인메모리 벡터 저장소 (DB와 동기화)
        private List<VectorData> _vectorStore = new List<VectorData>();

        private readonly Dictionary<string, int> _modelTokenLimits = new Dictionary<string, int>
        {
            { "qwen2.5", 32768 },
            { "deepseek-r1", 8192 },
            { "llama3", 8192 },
            { "mistral", 32768 },
            { "gemma", 8192 },
            { "phi", 4096 }
        };

        private static readonly HttpClient client = new HttpClient();
        private List<object> _chatHistory = new List<object>();

        // ─────────────────────────────────────────────────────────────
        // 2. 초기화 (생성자)
        // ─────────────────────────────────────────────────────────────
        public AIChat()
        {
            InitializeComponent();
            client.Timeout = TimeSpan.FromMinutes(10);

            if (mproAiThink != null)
            {
                mproAiThink.Properties.Stopped = true;
                layAiThink.Visibility = DevExpress.XtraLayout.Utils.LayoutVisibility.Never;
            }

            InitializeDatabase();
            LoadVectorsFromDb();
            LoadSystemPrompt();
            LoadModelsToComboBox();

            LoadDefaultSetting();

            // GPU 모니터링 (필요 시 주석 해제)
            //_gpuTimer = new Timer();
            //_gpuTimer.Interval = 2000; 
            //_gpuTimer.Tick += GpuTimer_Tick;
            //_gpuTimer.Start();
        }

        protected override bool ProcessCmdKey(ref System.Windows.Forms.Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Enter))
            {
                if (btnAnalyze.Enabled)
                {
                    BtnAnalyze_Click(this, EventArgs.Empty);
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ─────────────────────────────────────────────────────────────
        // 3. SQLite DB 관리 및 RAG 데이터 로드
        // ─────────────────────────────────────────────────────────────
        private void InitializeDatabase()
        {
            using (var conn = new SQLiteConnection(DB_CONNECTION_STRING))
            {
                conn.Open();
                string sql = @"
CREATE TABLE IF NOT EXISTS VectorStore (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    FileName TEXT NOT NULL,
    TextChunk TEXT NOT NULL,
    VectorJson TEXT NOT NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS idx_filename ON VectorStore(FileName);

CREATE TABLE IF NOT EXISTS SystemPrompt (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Title TEXT NOT NULL UNIQUE,
    Contents TEXT NOT NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS DefaultSetting (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    Model TEXT NOT NULL,
    SystemPrompt TEXT NOT NULL,
    Temperature TEXT,
    Num_ctx TEXT,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);
                    ";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }


        bool _isLoading = false;
        private void LoadDefaultSetting()
        {
            try
            {
                _isLoading = true;

                using (var conn = new SQLiteConnection(DB_CONNECTION_STRING))
                {
                    conn.Open();
                    string sql = "SELECT Model, SystemPrompt, Temperature, Num_ctx FROM DefaultSetting ORDER BY CreatedAt DESC LIMIT 1";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            cboSystemPrpt.EditValue = reader["SystemPrompt"];
                            cboModelList.EditValue = reader["Model"];
                            txtTemp.EditValue = reader["Temperature"];
                            txtNum_ctx.EditValue = reader["Num_ctx"];
                        }
                    }
                }
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void SaveDefaultSetting()
        {
            using (var conn = new SQLiteConnection(DB_CONNECTION_STRING))
            {
                conn.Open();
                string sql = @"INSERT INTO DefaultSetting (id, Model, SystemPrompt, Temperature, Num_ctx)
VALUES (1, @model, @systemPrompt, @temperature, @num_ctx)
ON CONFLICT(id) DO UPDATE SET
Model = excluded.Model,
SystemPrompt = excluded.SystemPrompt,
Temperature = excluded.Temperature,
Num_ctx = excluded.Num_ctx;";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@model", cboModelList.EditValue != null ? cboModelList.EditValue.ToString() : "");
                    cmd.Parameters.AddWithValue("@systemPrompt", cboSystemPrpt.EditValue != null ? cboSystemPrpt.EditValue.ToString() : "");
                    cmd.Parameters.AddWithValue("@temperature", txtTemp.EditValue != null ? txtTemp.Text : "");
                    cmd.Parameters.AddWithValue("@num_ctx", txtNum_ctx.EditValue != null ? txtNum_ctx.Text : "");
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void LoadSystemPrompt()
        {
            cboSystemPrpt.Properties.Items.Clear();

            try
            {
                using (var conn = new SQLiteConnection(DB_CONNECTION_STRING))
                {
                    conn.Open();
                    string sql = "SELECT Title, Contents FROM SystemPrompt ORDER BY CreatedAt DESC";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string title = reader["Title"].ToString();
                            string contents = reader["Contents"].ToString();

                            cboSystemPrpt.Properties.Items.Add(title);
                            SystemPrompt systemPrompt = new SystemPrompt();
                            systemPrompt.Title = title;
                            systemPrompt.Contents = contents;

                            _systemPropt.Add(systemPrompt);
                        }
                    }
                }

                if(_systemPropt.Count > 0)
                {
                    cboSystemPrpt.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DB 로드 실패: {ex.Message}");
            }
        }

        private void SaveSystemPrompt()
        {
            using (var conn = new SQLiteConnection(DB_CONNECTION_STRING))
            {
                conn.Open();
                string sql = @"INSERT INTO SystemPrompt (Title, Contents)
VALUES (@title, @contents)
ON CONFLICT(Title) DO UPDATE SET
Contents = excluded.Contents;";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@title", cboSystemPrpt.EditValue != null ? cboSystemPrpt.EditValue.ToString() : "");
                    cmd.Parameters.AddWithValue("@contents", txtSystemPrompt.Document.Text);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void LoadVectorsFromDb()
        {
            _vectorStore.Clear();
            lstFiles.Items.Clear();

            try
            {
                using (var conn = new SQLiteConnection(DB_CONNECTION_STRING))
                {
                    conn.Open();
                    string sql = "SELECT FileName, TextChunk, VectorJson FROM VectorStore";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var loadedFiles = new HashSet<string>();

                        while (reader.Read())
                        {
                            string fileName = reader["FileName"].ToString();
                            string chunk = reader["TextChunk"].ToString();
                            string json = reader["VectorJson"].ToString();

                            try
                            {
                                var vector = JsonSerializer.Deserialize<double[]>(json);
                                if (vector != null)
                                {
                                    _vectorStore.Add(new VectorData
                                    {
                                        FileName = fileName,
                                        TextChunk = chunk,
                                        Vector = vector
                                    });
                                    loadedFiles.Add(fileName);
                                }
                            }
                            catch { }
                        }

                        foreach (var file in loadedFiles)
                        {
                            if (!lstFiles.Items.Contains(file))
                                lstFiles.Items.Add(file);
                        }
                    }
                }
                UpdateFileContDisplay();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DB 로드 실패: {ex.Message}");
            }
        }

        private void SaveVectorToDb(string fileName, string textChunk, double[] vector)
        {
            using (var conn = new SQLiteConnection(DB_CONNECTION_STRING))
            {
                conn.Open();
                string sql = "INSERT INTO VectorStore (FileName, TextChunk, VectorJson) VALUES (@fn, @tc, @vj)";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@fn", fileName);
                    cmd.Parameters.AddWithValue("@tc", textChunk);
                    cmd.Parameters.AddWithValue("@vj", JsonSerializer.Serialize(vector));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void DeleteFileFromDb(string fileName)
        {
            using (var conn = new SQLiteConnection(DB_CONNECTION_STRING))
            {
                conn.Open();
                string sql = "DELETE FROM VectorStore WHERE FileName = @fn";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@fn", fileName);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private bool IsFileProcessed(string fileName)
        {
            using (var conn = new SQLiteConnection(DB_CONNECTION_STRING))
            {
                conn.Open();
                string sql = "SELECT COUNT(*) FROM VectorStore WHERE FileName = @fn";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@fn", fileName);
                    long count = (long)cmd.ExecuteScalar();
                    return count > 0;
                }
            }
        }

        // RAG 데이터 상태 표시
        private void UpdateFileContDisplay()
        {
            if (txtAttFile == null) return;

            txtAttFile.BeginUpdate();
            txtAttFile.Text = "";
            try
            {
                txtAttFile.Document.AppendText($"[RAG Knowledge Base]\nTotal Chunks: {_vectorStore.Count}\n\n[Loaded Sources]\n");
                foreach (var item in lstFiles.Items)
                {
                    txtAttFile.Document.AppendText($"- {item}\n");
                }
            }
            finally
            {
                txtAttFile.EndUpdate();
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 4. 외부 데이터 가져오기 (File & CodeSnippet DB)
        // ─────────────────────────────────────────────────────────────
        private async void btnOpenFile_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Multiselect = true;
                openFileDialog.Title = "파일 선택";
                openFileDialog.Filter = "모든 파일 (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    // DB 저장 옵션에 따라 처리 (LoadVectorsFromDb 호출 제거)
                    await ProcessFilesToVectorStoreAsync(openFileDialog.FileNames);

                    // 처리 후 현황 갱신
                    UpdateFileContDisplay();
                }
            }
        }

        private void btnRemoveFile_Click(object sender, EventArgs e)
        {
            if (lstFiles.SelectedItems.Count > 0)
            {
                if (MessageBox.Show("선택한 항목을 DB에서 삭제하시겠습니까?", "확인", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    for (int i = lstFiles.SelectedIndices.Count - 1; i >= 0; i--)
                    {
                        string fileName = lstFiles.Items[lstFiles.SelectedIndices[i]].ToString();
                        DeleteFileFromDb(fileName);
                    }
                    LoadVectorsFromDb();
                }
            }
        }

        public async void btnImportCodeSnippet_Click(object sender, EventArgs e)
        {
            if(MessageBox.Show("업무 메모를 RAG에 동기화합니다.", "확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                await ImportSnippetsFromDbAsync();
        }

        private async Task ProcessFilesToVectorStoreAsync(string[] filePaths)
        {
            lblStatus.Text = "파일 처리 중...";
            this.Cursor = Cursors.WaitCursor;

            try
            {
                foreach (string filePath in filePaths)
                {
                    if (!File.Exists(filePath)) continue;

                    string fileName = Path.GetFileName(filePath);

                    // 1. 이미 DB에 있거나, 현재 메모리 리스트에 있는 파일인지 확인
                    // (DB 저장을 안 했더라도 메모리에 있으면 중복 처리 방지)
                    if (IsFileProcessed(fileName) || lstFiles.Items.Contains(fileName))
                    {
                        // 단, DB에 없고 메모리에만 있는데 'DB 저장'을 체크했다면 저장을 수행해야 할 수도 있음.
                        // 여기서는 단순하게 "이미 처리된 파일명은 건너뜀"으로 유지합니다.
                        continue;
                    }

                    string content = File.ReadAllText(filePath);
                    int chunkSize = 500;
                    int overlap = 50;

                    // UI 리스트에 파일명 추가
                    if (!lstFiles.Items.Contains(fileName))
                    {
                        lstFiles.Items.Add(fileName);
                    }

                    for (int i = 0; i < content.Length; i += (chunkSize - overlap))
                    {
                        int length = Math.Min(chunkSize, content.Length - i);
                        string chunkText = content.Substring(i, length);

                        var embedding = await GetEmbeddingAsync(chunkText);

                        if (embedding != null)
                        {
                            // 2. [중요] 메모리(VectorStore)에 즉시 추가 (일회성 사용 보장)
                            _vectorStore.Add(new VectorData
                            {
                                FileName = fileName,
                                TextChunk = chunkText,
                                Vector = embedding
                            });

                            // 3. 체크박스가 체크된 경우에만 DB에 영구 저장
                            if (chkSaveDB.Checked)
                            {
                                SaveVectorToDb(fileName, chunkText, embedding);
                            }
                        }
                    }
                }
            }
            finally
            {
                this.Cursor = Cursors.Default;
                lblStatus.Text = "완료";
            }
        }

        // [내부 클래스] 메모리 상에서 계층 구조를 계산하기 위한 임시 모델
        private class SnippetNode
        {
            public int Id { get; set; }
            public int ParentId { get; set; }
            public string Code { get; set; } // 제목
            public string Desc { get; set; } // 설명
        }

        private async Task ImportSnippetsFromDbAsync()
        {
            lblStatus.Text = "업무 메모 분석 및 가져오는 중...";
            this.Cursor = Cursors.WaitCursor;

            int insertCount = 0;
            int updateCount = 0;
            int skipCount = 0;

            try
            {
                // 1. 전체 데이터를 메모리에 로드 (계층 구조 추적용)
                // Key: ID, Value: Node 정보
                Dictionary<int, SnippetNode> nodeMap = new Dictionary<int, SnippetNode>();

                string sourceConnString = DB_CONNECTION_STRING;
                using (var sourceConn = new SQLiteConnection(sourceConnString))
                {
                    sourceConn.Open();
                    string sql = "SELECT ID, PARENTID, CODE, CODEDESC FROM codesnippet";
                    using (var cmd = new SQLiteCommand(sql, sourceConn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int id = Convert.ToInt32(reader["ID"]);
                            int pid = Convert.ToInt32(reader["PARENTID"]);
                            string code = reader["CODE"]?.ToString() ?? "";
                            string desc = reader["CODEDESC"]?.ToString() ?? "";

                            if (!nodeMap.ContainsKey(id))
                            {
                                nodeMap.Add(id, new SnippetNode { Id = id, ParentId = pid, Code = code, Desc = desc });
                            }
                        }
                    }
                }

                if (nodeMap.Count == 0)
                {
                    MessageBox.Show("가져올 데이터가 없습니다.");
                    return;
                }

                lblStatus.Text = $"총 {nodeMap.Count}건 동기화 시작...";

                // 2. 로컬 DB(Target) 연결
                using (var targetConn = new SQLiteConnection(DB_CONNECTION_STRING))
                {
                    targetConn.Open();

                    foreach (var node in nodeMap.Values)
                    {
                        // A. 계층 경로(Breadcrumb) 생성 로직
                        // 예: "HiAps 탑재 > 탑재 네트웍 > 특정 노드..."
                        string categoryPath = GetCategoryPath(nodeMap, node.Id);

                        // B. 검색용 텍스트 조합 (분류 정보 포함)
                        StringBuilder sb = new StringBuilder();
                        if (!string.IsNullOrWhiteSpace(categoryPath))
                        {
                            sb.AppendLine($"[분류] {categoryPath}");
                        }
                        sb.AppendLine($"[제목] {node.Code}");
                        if (!string.IsNullOrWhiteSpace(node.Desc))
                        {
                            sb.AppendLine($"[설명] {node.Desc}");
                        }

                        string fullText = sb.ToString().Trim();
                        if (string.IsNullOrWhiteSpace(fullText)) continue;

                        // 텍스트 길이 제한
                        if (fullText.Length > 2000) fullText = fullText.Substring(0, 2000);

                        // C. DB 저장 로직 (기존과 동일: 변경된 것만 업데이트)
                        string uniqueKey = $"SNIPPET_{node.Id}_{node.ParentId}";
                        string existingText = null;
                        bool exists = false;

                        using (var cmdCheck = new SQLiteCommand("SELECT TextChunk FROM VectorStore WHERE FileName = @fn", targetConn))
                        {
                            cmdCheck.Parameters.AddWithValue("@fn", uniqueKey);
                            using (var reader = cmdCheck.ExecuteReader())
                            {
                                if (reader.Read()) { exists = true; existingText = reader["TextChunk"].ToString(); }
                            }
                        }

                        if (exists)
                        {
                            if (existingText != fullText) // 분류 경로가 바뀌었거나 내용이 바뀌었으면 업데이트
                            {
                                var embedding = await GetEmbeddingAsync(fullText);
                                if (embedding != null)
                                {
                                    using (var cmdUpdate = new SQLiteCommand("UPDATE VectorStore SET TextChunk=@tc, VectorJson=@vj WHERE FileName=@fn", targetConn))
                                    {
                                        cmdUpdate.Parameters.AddWithValue("@tc", fullText);
                                        cmdUpdate.Parameters.AddWithValue("@vj", JsonSerializer.Serialize(embedding));
                                        cmdUpdate.Parameters.AddWithValue("@fn", uniqueKey);
                                        cmdUpdate.ExecuteNonQuery();
                                    }
                                    updateCount++;
                                }
                            }
                            else skipCount++;
                        }
                        else
                        {
                            var embedding = await GetEmbeddingAsync(fullText);
                            if (embedding != null)
                            {
                                using (var cmdInsert = new SQLiteCommand("INSERT INTO VectorStore (FileName, TextChunk, VectorJson) VALUES (@fn, @tc, @vj)", targetConn))
                                {
                                    cmdInsert.Parameters.AddWithValue("@fn", uniqueKey);
                                    cmdInsert.Parameters.AddWithValue("@tc", fullText);
                                    cmdInsert.Parameters.AddWithValue("@vj", JsonSerializer.Serialize(embedding));
                                    cmdInsert.ExecuteNonQuery();
                                }
                                insertCount++;
                            }
                        }

                        if ((insertCount + updateCount + skipCount) % 10 == 0)
                        {
                            lblStatus.Text = $"처리 중... ({insertCount + updateCount + skipCount}/{nodeMap.Count})";
                            Application.DoEvents();
                        }
                    }
                }

                LoadVectorsFromDb();
                MessageBox.Show($"동기화 완료!\n- 신규: {insertCount}\n- 수정: {updateCount}\n- 유지: {skipCount}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"오류: {ex.Message}");
            }
            finally
            {
                this.Cursor = Cursors.Default;
                lblStatus.Text = "완료";
            }
        }

        // [Helper] 부모 노드를 타고 올라가며 경로 문자열 생성
        private string GetCategoryPath(Dictionary<int, SnippetNode> map, int currentId)
        {
            if (!map.ContainsKey(currentId)) return "";

            var pathList = new List<string>();
            var currentNode = map[currentId];

            // 자기 자신은 제외하고 부모부터 경로에 추가하고 싶으면 아래 loop 전에 currentNode = map[currentNode.ParentId] 처리
            // 여기서는 '자기 자신'의 제목은 [제목]에 들어가므로, 경로에는 '부모'까지만 포함시킵니다.

            int parentId = currentNode.ParentId;
            int safetyLoop = 0;

            while (map.ContainsKey(parentId) && safetyLoop < 20) // 무한루프 방지
            {
                var parent = map[parentId];
                if (!string.IsNullOrWhiteSpace(parent.Code))
                {
                    pathList.Insert(0, parent.Code); // 앞에 삽입 (역순 방지)
                }

                if (parent.ParentId == 0 || parent.ParentId == parent.Id) break; // 루트 도달
                parentId = parent.ParentId;
                safetyLoop++;
            }

            return string.Join(" > ", pathList);
        }

        // ─────────────────────────────────────────────────────────────
        // 5. 메인 로직 (채팅 및 분석)
        // ─────────────────────────────────────────────────────────────
        private async void BtnAnalyze_Click(object sender, EventArgs e)
        {
            try
            {
                this.Cursor = Cursors.WaitCursor;
                btnAnalyze.Enabled = false;

                if (mproAiThink != null)
                {
                    layAiThink.Visibility = DevExpress.XtraLayout.Utils.LayoutVisibility.Always;
                    mproAiThink.Properties.Stopped = false;
                }

                // 1. 입력 내용 추출
                var content = ExtractContentFromRichEdit();
                string userPrompt = content.Text;
                string base64Image = content.ImageBase64;

                if (string.IsNullOrWhiteSpace(userPrompt) && string.IsNullOrEmpty(base64Image))
                {
                    MessageBox.Show("질문을 입력하세요.");
                    return;
                }

                // 시스템 프롬프트 설정
                if (_chatHistory.Count == 0)
                {
                    string systemPrompt = chkUsePrompt.Checked ? txtSystemPrompt.Text : "";
                    if (string.IsNullOrWhiteSpace(systemPrompt))
                    {
                        systemPrompt = @"You are an expert Senior Full Stack Engineer. 
Prioritize the [Reference Context] for answers. 
If the context is not relevant to the user's specific technology (e.g., C# vs ABAP), ignore it.
Answer strictly in Professional Korean.";
                    }
                    _chatHistory.Add(new { role = "system", content = systemPrompt });
                    PrintChatMessage("System", $"대화를 시작합니다. (Total Knowledge: {_vectorStore.Count} Chunks)");
                }

                // 2. [RAG 하이브리드 검색] (ABAP 필터링 추가)
                string retrievedContext = "";
                if (_vectorStore.Count > 0 && !string.IsNullOrWhiteSpace(userPrompt))
                {
                    lblStatus.Text = "지식베이스 검색 중...";
                    var queryVector = await GetEmbeddingAsync(userPrompt);

                    if (queryVector != null)
                    {
                        // A. 키워드 준비
                        var queryKeywords = userPrompt.Split(new[] { ' ', '?', '.', ',', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                                      .Where(w => w.Length >= 2)
                                                      .ToList();

                        // [필터링 조건] 사용자가 'ABAP'이나 '아밥'을 질문에 포함하지 않았다면 ABAP 문서는 무시
                        bool isAbapQuestion = userPrompt.IndexOf("ABAP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                              userPrompt.IndexOf("아밥", StringComparison.OrdinalIgnoreCase) >= 0;

                        var relevantChunks = _vectorStore
                            .Select(v =>
                            {
                                // B. 벡터 유사도
                                double vectorScore = CosineSimilarity(queryVector, v.Vector);

                                // C. 키워드 보너스
                                double keywordBonus = 0;
                                foreach (var keyword in queryKeywords)
                                {
                                    if (v.TextChunk.Contains(keyword)) keywordBonus += 0.1;
                                }
                                keywordBonus = Math.Min(keywordBonus, 0.5); // 최대 0.5점

                                // D. [핵심] ABAP 페널티 적용
                                double finalScore = vectorScore + keywordBonus;

                                if (!isAbapQuestion)
                                {
                                    // 질문은 ABAP이 아닌데, 문서 내용에 ABAP이 포함된 경우 점수 대폭 삭감
                                    if (v.TextChunk.IndexOf("ABAP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        v.TextChunk.IndexOf("아밥", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        finalScore = 0; // 검색 대상에서 사실상 제외
                                    }
                                }

                                return new { Chunk = v, FinalScore = finalScore };
                            })
                            .Where(x => x.FinalScore > 0.35) // 임계값
                            .OrderByDescending(x => x.FinalScore)
                            .Take(3)
                            .ToList();

                        StringBuilder sb = new StringBuilder();
                        bool hasRelevant = false;
                        foreach (var item in relevantChunks)
                        {
                            sb.AppendLine($"--- Reference (Score: {item.FinalScore:F2}) ---");
                            sb.AppendLine(item.Chunk.TextChunk);
                            sb.AppendLine();
                            hasRelevant = true;
                        }

                        if (hasRelevant)
                        {
                            retrievedContext = sb.ToString();
                            userPrompt = $"[지식베이스 참고]:\r\n{retrievedContext}\r\n\r\n[사용자 질문]:\r\n{userPrompt}\r\n\r\n위 참고 자료를 바탕으로 답변하세요.";
                        }
                    }
                }

                // 3. 이미지 처리
                if (!string.IsNullOrEmpty(base64Image))
                {
                    string ocrPrompt = "Extract text from this image.";
                    var ocrResult = await CallOllamaSingleAsync(EYE_MODEL, null, ocrPrompt, base64Image);
                    userPrompt = $"[Image Text]:\r\n{ocrResult.ResponseText}\r\n\r\n{userPrompt}";
                }

                _chatHistory.Add(new { role = "user", content = userPrompt });

                //string displayMsg = string.IsNullOrEmpty(retrievedContext) ? content.Text : "[업무 메모 참조됨]\n" + content.Text;
                //PrintChatMessage("User", displayMsg);

                PrintUserMessage(content.Text, !string.IsNullOrEmpty(retrievedContext));

                lblStatus.Text = "답변 생성 중...";
                var result = await CallOllamaHistoryAsync(BRAIN_MODEL, _chatHistory);

                if (result.IsSuccess)
                {
                    _chatHistory.Add(new { role = "assistant", content = result.ResponseText });
                    PrintChatMessage("AI", result.ResponseText);
                    PrintTokenUsage(result.PromptEvalCount, result.EvalCount);
                }
                else
                {
                    PrintChatMessage("Error", result.ResponseText);
                }

                lblStatus.Text = "완료";
            }
            catch (Exception ex)
            {
                PrintChatMessage("Error", ex.Message);
            }
            finally
            {
                if (mproAiThink != null) mproAiThink.Properties.Stopped = true;
                btnAnalyze.Enabled = true;
                this.Cursor = Cursors.Default;
            }
        }

        private void PrintUserMessage(string message, bool hasReference)
        {
            Document doc = txtResult.Document;
            doc.AppendText("\r\n──────────────────────────────────────────────────\r\n");

            // 1. Role 표시 ([User])
            DocumentRange roleRange = doc.AppendText("[User] ");
            CharacterProperties cpRole = doc.BeginUpdateCharacters(roleRange);
            cpRole.Bold = true;
            cpRole.ForeColor = Color.Blue;
            doc.EndUpdateCharacters(cpRole);

            doc.AppendText("\r\n");

            // 2. 참조 알림 표시 (스타일: 작게, 마젠타색)
            if (hasReference)
            {
                DocumentRange refRange = doc.AppendText("★ [업무 메모 참조됨]\r\n");
                CharacterProperties cpRef = doc.BeginUpdateCharacters(refRange);
                cpRef.FontSize = 9;
                cpRef.ForeColor = Color.Magenta;
                cpRef.Bold = true;
                doc.EndUpdateCharacters(cpRef);
            }

            // 3. 본문 메시지 표시 (색상 문제 해결)
            DocumentRange msgRange = doc.AppendText($"{message}\r\n");
            CharacterProperties cpMsg = doc.BeginUpdateCharacters(msgRange);
            cpMsg.ForeColor = Color.Black; // [핵심] 본문 검정색 강제
            cpMsg.Bold = false;
            doc.EndUpdateCharacters(cpMsg);

            txtResult.Document.CaretPosition = txtResult.Document.CreatePosition(txtResult.Document.Range.End.ToInt());
            txtResult.ScrollToCaret();
        }


        private void btnResetChat_Click(object sender, EventArgs e)
        {
            _chatHistory.Clear();
            txtResult.Text = "";
            PrintChatMessage("System", "대화가 초기화되었습니다.");
        }

        // ─────────────────────────────────────────────────────────────
        // 6. 헬퍼 메서드 (API 호출, UI 업데이트 등)
        // ─────────────────────────────────────────────────────────────

        private async Task<double[]> GetEmbeddingAsync(string text)
        {
            var requestData = new { model = EMBEDDING_MODEL, prompt = text };
            try
            {
                var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
                var response = await client.PostAsync(EMBED_URL, content);
                if (!response.IsSuccessStatusCode) return null;
                var body = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(body);
                return result?.Embedding;
            }
            catch { return null; }
        }

        private double CosineSimilarity(double[] vectorA, double[] vectorB)
        {
            if (vectorA == null || vectorB == null || vectorA.Length != vectorB.Length) return 0;
            double dot = 0, magA = 0, magB = 0;
            for (int i = 0; i < vectorA.Length; i++)
            {
                dot += vectorA[i] * vectorB[i];
                magA += Math.Pow(vectorA[i], 2);
                magB += Math.Pow(vectorB[i], 2);
            }
            return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
        }

        double Temperature
        {
            get
            {
                double temp = 0.4;

                if (double.TryParse(txtTemp.Text, out double temp_1))
                {
                    temp = temp_1;
                }

                return temp;
            }
        }

        double Num_ctx
        {
            get
            {
                double numCtx = 8192;

                if (double.TryParse(txtNum_ctx.Text, out double num_ctx_1))
                {
                    numCtx = num_ctx_1;
                }

                return numCtx;
            }
        }

        private async Task<OllamaResponse> CallOllamaHistoryAsync(string modelName, List<object> historyMessages)
        {
            var requestData = new
            {
                model = modelName,
                messages = historyMessages,
                stream = false,
                options = new { num_ctx = Num_ctx, temperature = Temperature }
            };
            return await SendOllamaRequest(CHAT_URL, requestData);
        }

        private async Task<OllamaResponse> CallOllamaSingleAsync(string modelName, string systemPrompt, string userPrompt, string base64Image = null)
        {
            var messages = new List<object>();
            if (!string.IsNullOrWhiteSpace(systemPrompt)) messages.Add(new { role = "system", content = systemPrompt });
            messages.Add(new { role = "user", content = userPrompt, images = base64Image != null ? new[] { base64Image } : null });
            
            var requestData = new 
            { 
                model = modelName, 
                messages = messages, 
                stream = false ,
                options = new { num_ctx = Num_ctx, temperature = Temperature }
            };

            return await SendOllamaRequest(CHAT_URL, requestData);
        }

        private async Task<OllamaResponse> SendOllamaRequest(string url, object requestData)
        {
            try
            {
                string json = JsonSerializer.Serialize(requestData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url, content);
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode) return new OllamaResponse { IsSuccess = false, ResponseText = body };

                using (JsonDocument doc = JsonDocument.Parse(body))
                {
                    var root = doc.RootElement;
                    string text = "";
                    if (root.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var cnt)) text = cnt.GetString();
                    int pEval = root.TryGetProperty("prompt_eval_count", out JsonElement pec) ? pec.GetInt32() : 0;
                    int eval = root.TryGetProperty("eval_count", out JsonElement ec) ? ec.GetInt32() : 0;
                    return new OllamaResponse { IsSuccess = true, ResponseText = text, PromptEvalCount = pEval, EvalCount = eval };
                }
            }
            catch (Exception ex) { return new OllamaResponse { IsSuccess = false, ResponseText = ex.Message }; }
        }

        private async Task UnloadModel(string modelName)
        {
            try
            {
                var requestData = new { model = modelName, keep_alive = 0 };
                var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
                await client.PostAsync(OLLAMA_URL, content);
            }
            catch { }
        }

        private (string Text, string ImageBase64) ExtractContentFromRichEdit()
        {
            string text = txtQuest.Document.Text.Trim();
            string imgBase64 = null;
            var images = txtQuest.Document.Images;
            if (images.Count > 0)
            {
                try
                {
                    DocumentImage docImage = images[0];
                    Image originalImage = docImage.Image.NativeImage;
                    if (originalImage != null)
                    {
                        using (Bitmap cleanBitmap = new Bitmap(originalImage.Width, originalImage.Height))
                        {
                            using (Graphics g = Graphics.FromImage(cleanBitmap))
                            {
                                g.Clear(Color.White);
                                g.DrawImage(originalImage, 0, 0, originalImage.Width, originalImage.Height);
                            }
                            using (MemoryStream ms = new MemoryStream())
                            {
                                cleanBitmap.Save(ms, ImageFormat.Jpeg);
                                imgBase64 = Convert.ToBase64String(ms.ToArray());
                            }
                        }
                    }
                }
                catch { }
            }
            return (text, imgBase64);
        }

        private void PrintChatMessage(string role, string msg)
        {
            Document doc = txtResult.Document;
            doc.AppendText("\r\n──────────────────────────────────────────────────\r\n");

            // 1. Role 표시 ([AI], [System] 등)
            DocumentRange range = doc.AppendText($"[{role}] ");
            CharacterProperties cp = doc.BeginUpdateCharacters(range);
            cp.Bold = true;
            if (role == "User") cp.ForeColor = Color.Blue;
            else if (role == "AI") cp.ForeColor = Color.Green;
            else if (role == "Error") cp.ForeColor = Color.Red;
            else cp.ForeColor = Color.Black;
            doc.EndUpdateCharacters(cp);

            // 줄바꿈
            doc.AppendText("\r\n");

            // 2. 본문 메시지 표시 (마크다운 포맷팅 적용)
            // 기존: 단순 텍스트 추가 -> 변경: 마크다운 파싱 함수 호출
            AppendMarkdownFormatted(doc, msg);

            // 스크롤 이동
            txtResult.Document.CaretPosition = txtResult.Document.CreatePosition(txtResult.Document.Range.End.ToInt());
            txtResult.ScrollToCaret();
        }

        /// <summary>
        /// 마크다운 텍스트를 파싱하여 RichEditControl에 서식과 함께 추가합니다.
        /// (코드 블록 감지 및 스타일링)
        /// </summary>
        private void AppendMarkdownFormatted(Document doc, string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            // ``` 를 기준으로 텍스트 분리
            // 짝수 인덱스: 일반 텍스트, 홀수 인덱스: 코드 블록
            string[] segments = text.Split(new string[] { "```" }, StringSplitOptions.None);

            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i];
                if (string.IsNullOrEmpty(segment)) continue;

                if (i % 2 == 0)
                {
                    // [일반 텍스트]
                    DocumentRange range = doc.AppendText(segment);
                    CharacterProperties cp = doc.BeginUpdateCharacters(range);
                    cp.FontName = "맑은 고딕";
                    cp.FontSize = 10;
                    cp.ForeColor = Color.Black;
                    cp.BackColor = Color.Transparent;
                    doc.EndUpdateCharacters(cp);
                }
                else
                {
                    // [코드 블록]
                    string codeContent = segment;

                    // 첫 줄의 언어 식별자(예: csharp, python) 제거 로직
                    int firstLineBreak = segment.IndexOfAny(new char[] { '\r', '\n' });
                    if (firstLineBreak >= 0)
                    {
                        string firstLine = segment.Substring(0, firstLineBreak).Trim();
                        // 언어 태그로 추정되면(공백없고 짧음) 제거
                        if (firstLine.Length > 0 && firstLine.Length < 20 && !firstLine.Contains(" "))
                        {
                            codeContent = segment.Substring(firstLineBreak).TrimStart();
                        }
                    }

                    // 코드 블록 위아래로 약간의 여백 추가
                    doc.AppendText("\r\n");

                    DocumentRange range = doc.AppendText(codeContent);

                    // 1. 글자 스타일 (Consolas, 진한 파랑, 연회색 배경)
                    CharacterProperties cp = doc.BeginUpdateCharacters(range);
                    cp.FontName = "Consolas";
                    cp.FontSize = 9;
                    cp.ForeColor = Color.FromArgb(0, 0, 139); // DarkBlue
                    cp.BackColor = Color.FromArgb(240, 240, 240); // 연한 회색 배경
                    doc.EndUpdateCharacters(cp);

                    // 2. 문단 스타일 (들여쓰기 적용)
                    ParagraphProperties pp = doc.BeginUpdateParagraphs(range);
                    pp.LeftIndent = 20; // 20 픽셀 들여쓰기
                    pp.RightIndent = 20;
                    doc.EndUpdateParagraphs(pp);

                    doc.AppendText("\r\n");
                }
            }
        }

        // [복구됨] 토큰 사용량 표시
        private void PrintTokenUsage(int promptTokens, int responseTokens)
        {
            int currentMaxToken = (int)Num_ctx;

            int totalUsed = promptTokens + responseTokens;
            int remaining = currentMaxToken - totalUsed;
            double usagePercent = (double)totalUsed / currentMaxToken * 100;

            string tokenInfo = $"[Token Usage] Used: {totalUsed} / {currentMaxToken} ({usagePercent:F1}%) | Remaining: {remaining}";

            Document doc = txtResult.Document;
            doc.AppendText("\r\n");
            DocumentRange range = doc.AppendText(tokenInfo);

            CharacterProperties cp = doc.BeginUpdateCharacters(range);
            cp.FontSize = 8;
            cp.ForeColor = remaining < 1000 ? Color.Red : Color.Gray;
            doc.EndUpdateCharacters(cp);

            doc.AppendText("\r\n");
            txtResult.ScrollToCaret();
        }

        private async void LoadModelsToComboBox()
        {
            try
            {
                var response = await client.GetAsync(TAGS_URL);
                if (response.IsSuccessStatusCode)
                {
                    var data = JsonSerializer.Deserialize<OllamaModelList>(await response.Content.ReadAsStringAsync(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    cboModelList.Properties.Items.BeginUpdate();
                    cboModelList.Properties.Items.Clear();
                    if (data?.Models != null)
                    {
                        foreach (var m in data.Models)
                        {
                            if (m.Name.StartsWith("nomic-embed-text"))
                                continue;

                            cboModelList.Properties.Items.Add(new DevExpress.XtraEditors.Controls.ImageComboBoxItem(m.Name, m.Name, -1));
                        }
                    }
                    cboModelList.Properties.Items.EndUpdate();
                    if (cboModelList.Properties.Items.Count > 0) cboModelList.SelectedIndex = 0;
                }
            }
            catch { }
        }

        private void cboModelList_EditValueChanged(object sender, EventArgs e)
        {
            if (_isLoading) 
                return;

            //if (cboModelList.EditValue != null)
            //{
            //    string selected = cboModelList.EditValue.ToString().ToLower();
            //    BRAIN_MODEL = selected;
            //    int newLimit = 8192;
            //    foreach (var kvp in _modelTokenLimits)
            //    {
            //        if (selected.Contains(kvp.Key)) { newLimit = kvp.Value; break; }
            //    }
            //    _currentMaxToken = newLimit;
            //}

            SaveDefaultSetting();
        }

        // ─────────────────────────────────────────────────────────────
        // 7. GPU 모니터링 (Optional)
        // ─────────────────────────────────────────────────────────────
        private void GpuTimer_Tick(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                var info = GetNvidiaGpuInfo();
                this.BeginInvoke(new Action(() =>
                {
                    if (info.MemoryTotal > 0)
                    {
                        prgGpuUsage.EditValue = info.CoreLoad;
                        prgGpuUsage.Text = $"GPU: {info.CoreLoad}% | VRAM: {info.MemoryUsed}/{info.MemoryTotal} MB";
                    }
                    else
                    {
                        prgGpuUsage.Text = "NVIDIA GPU를 찾을 수 없음";
                        if (_gpuUsageCounter == null) SetupGpuCounters();
                    }
                }));
            });
        }

        private GpuInfo GetNvidiaGpuInfo()
        {
            GpuInfo info = new GpuInfo();
            try
            {
                if (_totalVramMB == 0) _totalVramMB = GetTotalVideoMemory();
                info.MemoryTotal = (int)_totalVramMB;

                if (_gpuUsageCounter == null) SetupGpuCounters();
                if (_gpuUsageCounter != null) info.CoreLoad = (int)_gpuUsageCounter.NextValue();
                if (_vramUsedCounter != null)
                {
                    long usedBytes = (long)_vramUsedCounter.NextValue();
                    info.MemoryUsed = (int)(usedBytes / 1024 / 1024);
                }
            }
            catch { }
            return info;
        }

        private void SetupGpuCounters()
        {
            try
            {
                var category = new PerformanceCounterCategory("GPU Engine");
                var instanceNames = category.GetInstanceNames();
                foreach (string name in instanceNames)
                {
                    if (name.Contains("engtype_3D"))
                    {
                        _gpuUsageCounter = new PerformanceCounter("GPU Engine", "Utilization Percentage", name);
                        break;
                    }
                }
                var memCategory = new PerformanceCounterCategory("GPU Adapter Memory");
                foreach (string name in memCategory.GetInstanceNames())
                {
                    if (!string.IsNullOrEmpty(name))
                    {
                        _vramUsedCounter = new PerformanceCounter("GPU Adapter Memory", "Dedicated Usage", name);
                        break;
                    }
                }
            }
            catch { }
        }

        private long GetTotalVideoMemory()
        {
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT AdapterRAM FROM Win32_VideoController");
                foreach (ManagementObject mo in searcher.Get())
                {
                    if (mo["AdapterRAM"] != null)
                    {
                        long bytes = Convert.ToInt64(mo["AdapterRAM"]);
                        if (bytes > 1024 * 1024 * 512) return bytes / 1024 / 1024;
                    }
                }
            }
            catch { }
            return 0;
        }

        // ─────────────────────────────────────────────────────────────
        // 8. 내부 클래스 및 구조체 (DTO)
        // ─────────────────────────────────────────────────────────────
        private class OllamaResponse
        {
            public bool IsSuccess { get; set; }
            public string ResponseText { get; set; }
            public int PromptEvalCount { get; set; }
            public int EvalCount { get; set; }
        }

        private class DefaultSetting
        {
            public string SystemPrompt { get; set; }
            public string Model { get; set; }
        }

        private class SystemPrompt
        {
            public string Title {  get; set; }
            public string Contents { get; set; }
        }

        private void cboSystemPrpt_EditValueChanged(object sender, EventArgs e)
        {
            if (_isLoading)
                return;

            txtSystemPrompt.Document.Text = "";

            string systemPrompt = cboSystemPrpt.EditValue == null ? "" : cboSystemPrpt.EditValue.ToString();

            if (!string.IsNullOrEmpty(systemPrompt))
            {
                var tmp = _systemPropt.Where(t => t.Title == systemPrompt);

                if(tmp.Any())
                    txtSystemPrompt.Document.Text = tmp.First().Contents;
            }

            SaveDefaultSetting();
        }

        private void btnSaveSysPrpt_Click(object sender, EventArgs e)
        {
            SaveSystemPrompt();

            SaveDefaultSetting();
        }

        private void txtTemp_EditValueChanged(object sender, EventArgs e)
        {
            SaveDefaultSetting();
        }

        private void txtNum_ctx_EditValueChanged(object sender, EventArgs e)
        {
            SaveDefaultSetting();
        }
    }

    public class OllamaModelList
    {
        [JsonPropertyName("models")]
        public List<OllamaModelInfo> Models { get; set; }
    }

    public class OllamaModelInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

    public class OllamaEmbeddingResponse
    {
        [JsonPropertyName("embedding")]
        public double[] Embedding { get; set; }

    }

    public class VectorData
    {
        public string FileName { get; set; }
        public string TextChunk { get; set; }
        public double[] Vector { get; set; }
    }

    public struct GpuInfo
    {
        public int CoreLoad;
        public int MemoryUsed;
        public int MemoryTotal;
    }
}
