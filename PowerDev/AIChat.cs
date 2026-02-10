using DevExpress.Charts.Model;
using DevExpress.XtraRichEdit;
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
using System.Threading;
using System.Threading.Tasks;
using System.Web.Services.Description;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

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
        private string BRAIN_MODEL = "llama3-kor";
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
        private static readonly HttpClient client = new HttpClient();
        private List<object> _chatHistory = new List<object>();

        private CancellationTokenSource _cts;

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
            LoadModelsToComboBox();
            LoadSystemPrompt();

            LoadDefaultSetting();

            // 리스트박스 선택 시 상세 내용 보기 이벤트 연결
            lstFiles.SelectedIndexChanged += lstFiles_SelectedIndexChanged;

            SetGlobalFontSettings();

            // GPU 모니터링 (필요 시 주석 해제)
            //_gpuTimer = new Timer();
            //_gpuTimer.Interval = 2000;
            //_gpuTimer.Tick += GpuTimer_Tick;
            //_gpuTimer.Start();
        }

        private void SetGlobalFontSettings()
        {
            // 디자인에 있는 모든 리치 에디트 컨트롤 목록
            RichEditControl[] editors = { txtQuest, txtResult, txtSystemPrompt, txtAttFile, txtResultInfo };

            foreach (var edit in editors)
            {
                if (edit == null) continue;

                // 1. 현재 로드된 문서에 즉시 적용
                SetDefaultFont(edit);

                // 2. 문서가 초기화(CreateNewDocument 등) 될 때마다 다시 적용되도록 이벤트 연결
                edit.EmptyDocumentCreated -= RichEdit_EmptyDocumentCreated; // 중복 방지 제거
                edit.EmptyDocumentCreated += RichEdit_EmptyDocumentCreated;
            }
        }

        private void RichEdit_EmptyDocumentCreated(object sender, EventArgs e)
        {
            SetDefaultFont(sender as RichEditControl);
        }

        private void SetDefaultFont(RichEditControl editor)
        {
            if (editor == null) return;

            editor.Document.DefaultCharacterProperties.FontName = "Consolas";
            editor.Document.DefaultCharacterProperties.FontSize = 10;
            editor.Document.DefaultCharacterProperties.ForeColor = Color.Black;
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DB 로드 실패: {ex.Message}");
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
                        // 중복 방지용 (Key: FileName)
                        var loadedItems = new Dictionary<string, FileListItem>();

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

                                    // 리스트박스용 아이템 생성 (아직 목록에 없으면)
                                    if (!loadedItems.ContainsKey(fileName))
                                    {
                                        string displayText = fileName; // 기본값: 파일명

                                        // 스니펫인 경우 내용에서 [분류] 또는 [제목] 라인을 찾아 표시명으로 사용
                                        if (fileName.StartsWith("SNIPPET_"))
                                        {
                                            using (StringReader sr = new StringReader(chunk))
                                            {
                                                string line;
                                                while ((line = sr.ReadLine()) != null)
                                                {
                                                    if (line.StartsWith("[분류]") || line.StartsWith("[제목]"))
                                                    {
                                                        displayText = line;
                                                        break; // 첫 번째 발견된 태그를 사용
                                                    }
                                                }
                                            }
                                        }

                                        loadedItems.Add(fileName, new FileListItem { FileName = fileName, DisplayText = displayText });
                                    }
                                }
                            }
                            catch { }
                        }

                        // UI 리스트박스에 추가
                        foreach (var item in loadedItems.Values)
                        {
                            lstFiles.Items.Add(item);
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

            // 선택된 항목이 없을 때는 안내 문구만 표시
            if (lstFiles.SelectedIndex == -1)
            {
                txtAttFile.Text = $"[RAG Knowledge Base]\nTotal Chunks: {_vectorStore.Count}\n\n(목록을 클릭하면 해당 파일의 상세 내용이 표시됩니다)";
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
                    // 삭제할 아이템 수집
                    var itemsToRemove = new List<FileListItem>();
                    foreach (int index in lstFiles.SelectedIndices)
                    {
                        var item = lstFiles.Items[index] as FileListItem;
                        if (item != null)
                            itemsToRemove.Add(item);
                    }

                    // DB 삭제 실행
                    foreach (var item in itemsToRemove)
                    {
                        DeleteFileFromDb(item.FileName);
                    }

                    // 목록 새로고침
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

                    // 1. 중복 확인 로직 수정 (FileListItem 호환)
                    bool isDuplicate = false;
                    if (IsFileProcessed(fileName)) isDuplicate = true;
                    else
                    {
                        foreach (var item in lstFiles.Items)
                        {
                            if (item is FileListItem fli && fli.FileName == fileName)
                            {
                                isDuplicate = true;
                                break;
                            }
                        }
                    }

                    if (isDuplicate) continue;

                    string content = File.ReadAllText(filePath);
                    int chunkSize = 500;
                    int overlap = 50;

                    // UI 리스트에 파일명 추가 (FileListItem 사용)
                    lstFiles.Items.Add(new FileListItem { FileName = fileName, DisplayText = fileName });

                    for (int i = 0; i < content.Length; i += (chunkSize - overlap))
                    {
                        int length = Math.Min(chunkSize, content.Length - i);
                        string chunkText = content.Substring(i, length);

                        var embedding = await GetEmbeddingAsync(chunkText);

                        if (embedding != null)
                        {
                            _vectorStore.Add(new VectorData
                            {
                                FileName = fileName,
                                TextChunk = chunkText,
                                Vector = embedding
                            });

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

        // [Helper] 해당 노드의 최상위(Root) 노드를 찾는 메서드
        private SnippetNode GetRootNode(Dictionary<int, SnippetNode> map, int currentId)
        {
            if (!map.ContainsKey(currentId)) return null;

            var currentNode = map[currentId];
            int safetyLoop = 0;

            // ParentId가 0이거나 자기 자신일 때까지 상위로 이동
            while (currentNode.ParentId != 0 && currentNode.ParentId != currentNode.Id && map.ContainsKey(currentNode.ParentId))
            {
                currentNode = map[currentNode.ParentId];
                if (safetyLoop++ > 100) break; // 무한루프 방지
            }

            return currentNode;
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
                Dictionary<int, SnippetNode> nodeMap = new Dictionary<int, SnippetNode>();

                string sourceConnString = DB_CONNECTION_STRING;
                using (var sourceConn = new SQLiteConnection(sourceConnString))
                {
                    sourceConn.Open();
                    // 하이라키 구성을 위해 전체 조회
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

                lblStatus.Text = $"총 {nodeMap.Count}건 로드됨. '조선' 관련 데이터 동기화 시작...";

                // 2. 로컬 DB(Target) 연결
                using (var targetConn = new SQLiteConnection(DB_CONNECTION_STRING))
                {
                    targetConn.Open();

                    foreach (var node in nodeMap.Values)
                    {
                        // [조건 1] 내용(CODEDESC)이 없으면 제외 (기존 요청사항)
                        if (string.IsNullOrWhiteSpace(node.Desc))
                            continue;

                        // [조건 2 - 신규] 최상위(Root) 대분류가 "조선"인지 확인
                        var rootNode = GetRootNode(nodeMap, node.Id);
                        if (rootNode == null || rootNode.Code != "조선")
                            continue;

                        // A. 계층 경로(Breadcrumb) 생성
                        string categoryPath = GetCategoryPath(nodeMap, node.Id);

                        // B. 검색용 텍스트 조합
                        StringBuilder sb = new StringBuilder();
                        if (!string.IsNullOrWhiteSpace(categoryPath))
                        {
                            sb.AppendLine($"[분류] {categoryPath}");
                        }
                        sb.AppendLine($"[제목] {node.Code}");
                        sb.AppendLine($"[설명] {node.Desc}");

                        string fullText = sb.ToString().Trim();
                        if (string.IsNullOrWhiteSpace(fullText)) continue;

                        if (fullText.Length > 2000) fullText = fullText.Substring(0, 2000);

                        // C. DB 저장 로직 (업서트)
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
                            if (existingText != fullText)
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
                            lblStatus.Text = $"처리 중... (S:{skipCount} / I:{insertCount} / U:{updateCount})";
                            Application.DoEvents();
                        }
                    }
                }

                LoadVectorsFromDb();
                MessageBox.Show($"'조선' 대분류 동기화 완료!\n- 신규: {insertCount}\n- 수정: {updateCount}\n- 유지: {skipCount}");
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
        /// <summary>
        /// 사용자의 질문 의도를 파악합니다. (CHAT: 일반 대화 / SEARCH: 지식 검색 필요)
        /// </summary>
        private async Task<bool> IsSearchRequiredAsync(string userPrompt, CancellationToken token = default)
        {
            // 1. 질문이 너무 짧으면(5자 미만) AI 호출 없이 바로 일반 대화로 처리 (속도 최적화)
            if (userPrompt.Length < 5) return false;

            // 2. 분류를 위한 가벼운 시스템 프롬프트
            string routerSystemPrompt = @"
Classify the user's input into 'CHAT' or 'SEARCH'.
- 'CHAT': Greetings, self-introductions, jokes, compliments, or general conversation.
- 'SEARCH': Technical questions, asking for definitions, requesting code, or questions about the company/project.
Output ONLY one word: CHAT or SEARCH.";

            // 3. 요청 데이터 생성
            // 의도 파악은 창의성이 필요 없으므로 Temperature 0
            // 응답 토큰도 10개면 충분함 (속도 향상)
            var requestData = new
            {
                model = "bge-m3",//BRAIN_MODEL, // 또는 더 가벼운 모델(gemma:2b 등) 사용 권장
                messages = new[] {
                    new { role = "system", content = routerSystemPrompt },
                    new { role = "user", content = userPrompt }
                },
                stream = false,
                options = new { temperature = 0, num_predict = 10 }
            };

            try
            {
                // 여기서 토큰(token)을 넘겨주어야 취소 버튼 클릭 시 중단됨
                var response = await SendOllamaRequest("http://localhost:11434/api/embeddings", requestData, token);

                if (response.IsSuccess)
                {
                    string intent = response.ResponseText.Trim().ToUpper();
                    Debug.WriteLine($"[Intent Router] Input: {userPrompt} / Intent: {intent}");

                    // 'SEARCH'라고 명확히 답한 경우에만 RAG 검색 수행
                    return intent.Contains("SEARCH");
                }
            }
            catch (OperationCanceledException)
            {
                // 취소 시 상위로 예외 전파 (그래야 '취소됨' 메시지가 뜸)
                throw;
            }
            catch (Exception ex)
            {
                // 그 외 에러(네트워크 등) 발생 시 안전하게 검색 수행(True)으로 처리
                Debug.WriteLine($"[Router Error] {ex.Message}");
            }

            return true;
        }

        // [AIChat.cs] 키워드 기반 검색 (정확도 보장)
        private List<VectorData> SearchByKeyword(string userQuery)
        {
            var results = new List<VectorData>();

            // 1. 검색어에서 의미 있는 단어 추출 (2글자 이상)
            // 예: "RFC 호출 샘플" -> "RFC", "호출", "샘플"
            var keywords = userQuery.Split(' ', '\t', '\n')
                                    .Where(k => k.Length >= 2 && !string.IsNullOrWhiteSpace(k))
                                    .ToList();

            if (keywords.Count == 0) return results;

            using (var conn = new SQLiteConnection(DB_CONNECTION_STRING))
            {
                conn.Open();

                // 2. 각 키워드에 대해 LIKE 검색 수행
                foreach (var keyword in keywords)
                {
                    // 영문 대소문자 구분 없이 검색하기 위해 UPPER 사용 권장 (SQLite 설정에 따라 다름)
                    string sql = "SELECT FileName, TextChunk FROM VectorStore WHERE TextChunk LIKE @kw LIMIT 3";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@kw", $"%{keyword}%");

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string fileName = reader["FileName"].ToString();
                                string chunk = reader["TextChunk"].ToString();

                                // 중복 방지 체크
                                if (!results.Any(r => r.FileName == fileName && r.TextChunk == chunk))
                                {
                                    results.Add(new VectorData
                                    {
                                        FileName = fileName,
                                        TextChunk = chunk,
                                        Vector = null // 키워드 검색은 벡터 계산 불필요
                                    });
                                }
                            }
                        }
                    }
                }
            }
            return results;
        }

        private async void BtnAnalyze_Click(object sender, EventArgs e)
        {
            // ─────────────────────────────────────────────────────────────
            // 1. [취소 로직] 실행 중인 작업 중단
            // ─────────────────────────────────────────────────────────────
            if (_cts != null)
            {
                _cts.Cancel(); // 1. 취소 신호 전송
                _cts.Dispose();
                _cts = null;

                // UI 상태 즉시 복구 (애니메이션 중지 등)
                if (mproAiThink != null)
                    mproAiThink.Properties.Stopped = true;

                layAiThink.Visibility = DevExpress.XtraLayout.Utils.LayoutVisibility.Never;

                // 버튼 상태는 finally 블록에서 복구되거나, 여기서 강제로 원복 가능
                btnAnalyze.Text = "분석";
                btnAnalyze.ImageOptions.Image = imgStopStart.Images[1];

                return; // 작업 종료
            }

            // ─────────────────────────────────────────────────────────────
            // 2. 신규 작업 시작 준비
            // ─────────────────────────────────────────────────────────────
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                // [UI 변경] '분석' -> '중단' 버튼으로 변경
                btnAnalyze.Text = "중단";
                btnAnalyze.ImageOptions.Image = imgStopStart.Images[0];
                this.Cursor = Cursors.WaitCursor;

                if (mproAiThink != null)
                {
                    layAiThink.Visibility = DevExpress.XtraLayout.Utils.LayoutVisibility.Always;
                    mproAiThink.Properties.Stopped = false;
                }

                // 3. 입력 내용 추출 (텍스트 + 이미지)
                var content = ExtractContentFromRichEdit();
                string userPrompt = content.Text;

                string base64Image = content.ImageBase64;

                if (string.IsNullOrWhiteSpace(userPrompt) && string.IsNullOrEmpty(base64Image))
                {
                    MessageBox.Show("질문을 입력하세요.");
                    return;
                }

                // 4. 시스템 프롬프트 정의
                // [Strict Mode] RAG 정보가 있을 때: 문서 기반 답변 강제 + 유추 허용
                // [Strict Mode] RAG Context Analysis
                string strictSystemPrompt = @"You are an expert Senior Full-Stack Engineer.
Your task is to precisely analyze the provided [Context] to answer the user's query.

### Instructions:
1. **Inference:** Even if the code is incomplete or fragmentary, infer its functionality based on variable names, logic, or structure.
2. **Valid Input:** Treat partial code (e.g., parameter configuration, initialization steps) as valid 'sample code' and provide a full analysis based on it.
3. **Output Format:** Provide specific, actionable explanations that a developer can immediately use.
4. **Fallback:** Only reply with 'I do not know' if there is absolutely no relevance found in the context.
5. **Language:** YOU MUST ANSWER STRICTLY IN PROFESSIONAL KOREAN.";

                // [General Mode] General AI Assistant
                string generalSystemPrompt = @"You are a helpful and versatile AI Assistant.
Engage in a natural and free-flowing conversation with the user.

### Guidelines:
- Provide expert-level advice and insights for technical questions.
- Maintain a helpful and polite tone.
- **Language:** YOU MUST ANSWER STRICTLY IN PROFESSIONAL KOREAN.";

                // 대화 기록이 없으면 초기화 (일단 일반 모드로 시작)
                if (_chatHistory.Count == 0)
                {
                    _chatHistory.Add(new { role = "system", content = generalSystemPrompt });
                }

                // ─────────────────────────────────────────────────────────────
                // 5. 하이브리드 검색 (Vector + Keyword)
                // ─────────────────────────────────────────────────────────────
                // 체크박스가 체크되어 있을 때만 지식베이스 검색 수행
                bool isSearchNeeded = chkUseRAG.Checked;

                string retrievedContext = "";
                string finalUserMessage = userPrompt;
                bool isContextFound = false;

                if (isSearchNeeded && _vectorStore.Count > 0)
                {
                    lblStatus.Text = "하이브리드 검색 중...";

                    // A. [벡터 검색] 의미 기반 (유사도 0.25 이상)
                    var vectorResults = new List<dynamic>();
                    var queryVector = await GetEmbeddingAsync(userPrompt, token);

                    if (queryVector != null)
                    {
                        vectorResults = _vectorStore
                            .Select(v => new { Chunk = v, Score = CosineSimilarity(queryVector, v.Vector) })
                            .Where(x => x.Score > 0.25) // 기준값 완화 (0.35 -> 0.25)
                            .OrderByDescending(x => x.Score)
                            .Take(5) // 상위 5개
                            .Select(x => (dynamic)new { x.Chunk, Score = x.Score, Type = "Vector" })
                            .ToList();
                    }

                    // B. [키워드 검색] 정확한 단어 매칭 (Keyword Search 함수 필요)
                    // (SearchByKeyword 함수는 별도로 구현되어 있어야 합니다)
                    var keywordResults = SearchByKeyword(userPrompt);

                    foreach (var kwItem in keywordResults)
                    {
                        // 중복 제거 (이미 벡터 검색에 나온 파일이면 패스)
                        bool exists = vectorResults.Any(v => v.Chunk.FileName == kwItem.FileName && v.Chunk.TextChunk == kwItem.TextChunk);
                        if (!exists)
                        {
                            // 키워드 매칭은 점수 1.0(최상위) 부여
                            vectorResults.Add(new { Chunk = kwItem, Score = 1.0, Type = "Keyword" });
                        }
                    }

                    // C. [최종 통합] 점수순 정렬 후 상위 5개 선정
                    var finalResults = vectorResults
                        .OrderByDescending(x => x.Score)
                        .Take(5)
                        .ToList();

                    if (finalResults.Count > 0)
                    {
                        isContextFound = true;
                        StringBuilder sb = new StringBuilder();
                        foreach (var item in finalResults)
                        {
                            // 디버깅을 위해 Type(Vector/Keyword)과 점수를 태그에 포함
                            sb.AppendLine($"<doc type='{item.Type}' score='{item.Score:F2}'>");
                            sb.AppendLine(item.Chunk.TextChunk);
                            sb.AppendLine("</doc>");
                        }
                        retrievedContext = sb.ToString();

                        // LLM에게 전달할 최종 프롬프트 구성
                        finalUserMessage = $@"
[Context]:
{retrievedContext}

[Question]: 
{userPrompt}

[Instruction]:
Based on the [Context] above, answer the question.";
                    }
                    else
                    {
                        // 체크박스는 켰으나, 검색 결과가 없는 경우
                        Debug.WriteLine("[RAG] 검색 결과 없음");
                    }
                }

                // 6. 시스템 프롬프트 교체
                if (_chatHistory.Count > 0)
                {
                    if (isContextFound)
                    {
                        // 문서를 찾았으면 Strict Mode 적용
                        _chatHistory[0] = new { role = "system", content = strictSystemPrompt };
                        PrintChatMessage("System", $"[지식베이스 참조] {userPrompt} (관련 문서 {retrievedContext.Split(new[] { "<doc" }, StringSplitOptions.None).Length - 1}건)");
                    }
                    else
                    {
                        // 문서를 못 찾았으면 General Mode 적용
                        _chatHistory[0] = new { role = "system", content = generalSystemPrompt };
                        if (isSearchNeeded)
                            PrintChatMessage("System", "관련된 문서를 찾지 못해 일반 지식으로 답변합니다.");
                    }
                }

                // 7. 이미지 처리 (Vision)
                if (!string.IsNullOrEmpty(base64Image))
                {
                    string ocrPrompt = "Extract text from this image.";
                    var ocrResult = await CallOllamaSingleAsync(EYE_MODEL, null, ocrPrompt, base64Image, token);
                    finalUserMessage = $"[Image Text]:\r\n{ocrResult.ResponseText}\r\n\r\n{finalUserMessage}";
                }

                // 8. 메시지 출력 및 전송
                PrintUserMessage(userPrompt, isContextFound);
                _chatHistory.Add(new { role = "user", content = finalUserMessage });

                lblStatus.Text = "답변 생성 중...";
                var result = await CallOllamaHistoryAsync(BRAIN_MODEL, _chatHistory, token);

                // 9. 결과 처리
                if (result.IsSuccess)
                {
                    _chatHistory.Add(new { role = "assistant", content = result.ResponseText });
                    PrintChatMessage("AI", result.ResponseText);
                    PrintTokenUsage(result.PromptEvalCount, result.EvalCount);
                    PrintGenerationInfo(result);
                }
                else
                {
                    // 취소된 경우 예외가 발생하여 catch로 이동하지만,
                    // 혹시 성공 플래그가 false인 경우 에러 메시지 출력
                    if (!token.IsCancellationRequested)
                        PrintChatMessage("Error", result.ResponseText);
                }
            }
            catch (OperationCanceledException)
            {
                PrintChatMessage("System", "작업이 사용자에 의해 취소되었습니다.");
                lblStatus.Text = "취소됨";
            }
            catch (Exception ex)
            {
                PrintChatMessage("Error", $"오류 발생: {ex.Message}");
            }
            finally
            {
                // [UI 복구] 작업 종료 후 버튼 상태 원복
                btnAnalyze.Text = "분석";
                btnAnalyze.ImageOptions.Image = imgStopStart.Images[1];

                if (mproAiThink != null)
                    mproAiThink.Properties.Stopped = true;

                this.Cursor = Cursors.Default;

                // 토큰 정리
                if (_cts != null)
                {
                    _cts.Dispose();
                    _cts = null;
                }

                lblStatus.Text = "대기";
            }
        }

        /// <summary>
        /// 실제 API로 전송된 JSON 데이터를 파싱하여 표시합니다. (무결성 검증용)
        /// </summary>
        private void PrintGenerationInfo(OllamaResponse result)
        {
            if (txtResultInfo == null || result == null) return;
            if (string.IsNullOrEmpty(result.RequestBody)) return;

            txtResultInfo.Document.BeginUpdate();
            try
            {
                txtResultInfo.Document.Text = "";
                Document doc = txtResultInfo.Document;

                // 헤더
                doc.AppendText("[Actual Request Data (검증됨)]\n");
                doc.AppendText("※ 아래 정보는 실제 서버로 전송된 JSON 패킷에서 추출했습니다.\n\n");

                // JSON 파싱
                using (JsonDocument requestDoc = JsonDocument.Parse(result.RequestBody))
                {
                    var root = requestDoc.RootElement;

                    // 1. 모델 확인
                    if (root.TryGetProperty("model", out var model))
                        doc.AppendText($"• Used Model: {model.GetString()}\n");

                    // 2. 옵션 확인 (Temperature, Num_ctx)
                    if (root.TryGetProperty("options", out var options))
                    {
                        if (options.TryGetProperty("temperature", out var temp))
                            doc.AppendText($"• Temperature: {temp.GetDouble()}\n");

                        if (options.TryGetProperty("num_ctx", out var numCtx))
                            doc.AppendText($"• Context Limit: {numCtx.GetInt32()}\n");
                    }
                    else
                    {
                        doc.AppendText("• Options: (Not Sent / Default)\n");
                    }

                    // ─────────────────────────────────────────────────────────────
                    // [추가] 3. RAG 포함 여부 및 시스템 프롬프트 확인
                    // ─────────────────────────────────────────────────────────────
                    bool isRagIncluded = false;
                    int ragLength = 0;
                    bool systemFound = false;

                    if (root.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var msg in messages.EnumerateArray())
                        {
                            if (msg.TryGetProperty("role", out var role))
                            {
                                string roleStr = role.GetString();
                                string contentStr = msg.TryGetProperty("content", out var content) ? content.GetString() : "";

                                // System Prompt 확인
                                if (roleStr == "system")
                                {
                                    doc.AppendText($"\n[Used System Prompt]\n{contentStr}\n");
                                    systemFound = true;
                                }

                                // RAG Context 확인 (User 메시지 내부에 포함되어 있음)
                                // BtnAnalyze_Click에서 "[지식베이스 참고]:" 문자열을 추가했는지 확인
                                if (roleStr == "user")
                                {
                                    if (contentStr.Contains("[지식베이스 참고]:"))
                                    {
                                        isRagIncluded = true;
                                        // 참고 자료의 대략적인 길이 계산
                                        int startIdx = contentStr.IndexOf("[지식베이스 참고]:");
                                        int endIdx = contentStr.IndexOf("[사용자 질문]:");
                                        if (endIdx > startIdx)
                                        {
                                            ragLength = endIdx - startIdx;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (!systemFound) doc.AppendText("\n[Used System Prompt]\n(시스템 프롬프트가 전송되지 않았습니다)\n");

                    // RAG 검증 결과 출력
                    doc.AppendText("\n[RAG Context Verification]\n");
                    if (isRagIncluded)
                    {
                        DocumentRange range = doc.AppendText($"• Status: INCLUDED ✅ (Length: {ragLength} chars)\n");
                        // 녹색으로 강조
                        CharacterProperties cp = doc.BeginUpdateCharacters(range);
                        cp.ForeColor = Color.Green;
                        cp.Bold = true;
                        doc.EndUpdateCharacters(cp);

                        doc.AppendText("  (실제 프롬프트에 지식베이스 내용이 포함되어 전송됨)\n");
                    }
                    else
                    {
                        DocumentRange range = doc.AppendText("• Status: NOT FOUND ❌\n");
                        // 빨간색으로 강조
                        CharacterProperties cp = doc.BeginUpdateCharacters(range);
                        cp.ForeColor = Color.Red;
                        cp.Bold = true;
                        doc.EndUpdateCharacters(cp);

                        doc.AppendText("  (단순 사용자 질문만 전송됨)\n");
                    }
                }

                // 4. 토큰 사용량 (이건 응답에서 온 정보)
                if (result.IsSuccess)
                {
                    int total = result.PromptEvalCount + result.EvalCount;
                    doc.AppendText($"\n[Actual Token Usage]\n");
                    doc.AppendText($"• Prompt: {result.PromptEvalCount}\n");
                    doc.AppendText($"• Response: {result.EvalCount}\n");
                    doc.AppendText($"• Total: {total}\n");
                }
            }
            catch (Exception ex)
            {
                txtResultInfo.Document.AppendText($"\n[Error] 정보 파싱 실패: {ex.Message}");
            }
            finally
            {
                txtResultInfo.Document.EndUpdate();
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
            //if (hasReference)
            //{
            //    DocumentRange refRange = doc.AppendText("★ [업무 메모 참조됨]\r\n");
            //    CharacterProperties cpRef = doc.BeginUpdateCharacters(refRange);
            //    cpRef.FontSize = 9;
            //    cpRef.ForeColor = Color.Magenta;
            //    cpRef.Bold = true;
            //    doc.EndUpdateCharacters(cpRef);
            //}

            // 3. 본문 메시지 표시 (색상 문제 해결)
            DocumentRange msgRange = doc.AppendText($"{message}\r\n");
            CharacterProperties cpMsg = doc.BeginUpdateCharacters(msgRange);
            cpMsg.ForeColor = Color.Black; // [핵심] 본문 검정색 강제
            cpMsg.Bold = false;
            doc.EndUpdateCharacters(cpMsg);

            txtResult.Document.CaretPosition = txtResult.Document.CreatePosition(txtResult.Document.Range.End.ToInt());
            txtResult.ScrollToCaret();
        }


        private async void btnResetChat_Click(object sender, EventArgs e)
        {
            await UnloadModel();

            _chatHistory.Clear();
            txtResult.Text = "";
            PrintChatMessage("System", "대화가 초기화되었습니다.");
        }

        // ─────────────────────────────────────────────────────────────
        // 6. 헬퍼 메서드 (API 호출, UI 업데이트 등)
        // ─────────────────────────────────────────────────────────────

        private async Task<double[]> GetEmbeddingAsync(string text, CancellationToken token = default)
        {
            var requestData = new { model = EMBEDDING_MODEL, prompt = text };
            try
            {
                var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");

                // [수정] 토큰 전달
                var response = await client.PostAsync(EMBED_URL, content, token);

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

        private async Task<OllamaResponse> CallOllamaHistoryAsync(string modelName, List<object> historyMessages, CancellationToken token = default)
        {
            // [개선] 슬라이딩 윈도우 로직 (메모리 관리)
            // 대화 턴이 너무 많으면(예: 20개 초과), 시스템 프롬프트(0번)는 남기고 오래된 대화(1번부터)를 삭제
            const int MAX_HISTORY_TURNS = 20;

            if (historyMessages.Count > MAX_HISTORY_TURNS)
            {
                // 0번(System)은 유지하고, 1번(가장 오래된 User)과 2번(가장 오래된 AI)을 삭제
                // User-AI 쌍으로 삭제하여 대화 흐름 유지
                if (historyMessages.Count >= 3)
                {
                    historyMessages.RemoveAt(1);
                    historyMessages.RemoveAt(1);
                }
            }

            var requestData = new
            {
                model = modelName,
                messages = historyMessages,
                stream = false,
                options = new { num_ctx = Num_ctx, temperature = Temperature }
            };
            return await SendOllamaRequest(CHAT_URL, requestData, token);
        }

        private async Task<OllamaResponse> CallOllamaSingleAsync(string modelName, string systemPrompt, string userPrompt, string base64Image = null, CancellationToken token = default)
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

            return await SendOllamaRequest(CHAT_URL, requestData, token);
        }

        private async Task<OllamaResponse> SendOllamaRequest(string url, object requestData, CancellationToken token = default)
        {
            try
            {
                string json = JsonSerializer.Serialize(requestData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // ★ [중요] 여기서 토큰을 사용하여 요청을 즉시 취소할 수 있게 합니다.
                var response = await client.PostAsync(url, content, token);

                string body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return new OllamaResponse { IsSuccess = false, ResponseText = body, RequestBody = json };

                using (JsonDocument doc = JsonDocument.Parse(body))
                {
                    var root = doc.RootElement;
                    string text = "";
                    if (root.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var cnt)) text = cnt.GetString();
                    int pEval = root.TryGetProperty("prompt_eval_count", out JsonElement pec) ? pec.GetInt32() : 0;
                    int eval = root.TryGetProperty("eval_count", out JsonElement ec) ? ec.GetInt32() : 0;

                    return new OllamaResponse
                    {
                        IsSuccess = true,
                        ResponseText = text,
                        PromptEvalCount = pEval,
                        EvalCount = eval,
                        RequestBody = json
                    };
                }
            }
            // 취소 발생 시 상위 메서드로 예외를 던짐
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { return new OllamaResponse { IsSuccess = false, ResponseText = ex.Message }; }
        }

        private async Task UnloadModel()
        {
            try
            {
                var requestData = new { model = BRAIN_MODEL, keep_alive = 0 };
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
                    //if (cboModelList.Properties.Items.Count > 0) cboModelList.SelectedIndex = 0;
                }
            }
            catch { }
        }

        private void cboModelList_EditValueChanged(object sender, EventArgs e)
        {
            BRAIN_MODEL = cboModelList.EditValue == null ? "" : cboModelList.EditValue.ToString().ToLower();

            if (_isLoading) 
                return;

            SaveDefaultSetting();
        }

        // ─────────────────────────────────────────────────────────────
        // 7. GPU 모니터링 (Optional)
        // ─────────────────────────────────────────────────────────────
        // 여러 엔진의 카운터를 동시에 모니터링하기 위한 리스트
        private List<PerformanceCounter> _gpuLoadCounters = new List<PerformanceCounter>();

        private void GpuTimer_Tick(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                var info = GetPerformanceCounterValues();

                this.BeginInvoke(new Action(() =>
                {
                    // 상태 표시 업데이트
                    if (info.MemoryTotal > 0)
                    {
                        prgGpuUsage.Text = $"GPU: {info.CoreLoad}% | VRAM: {info.MemoryUsed}/{info.MemoryTotal} MB";
                        prgGpuUsage.EditValue = info.CoreLoad;
                    }
                    else if (info.CoreLoad > 0)
                    {
                        prgGpuUsage.Text = $"GPU: {info.CoreLoad}% | VRAM: {info.MemoryUsed} MB";
                        prgGpuUsage.EditValue = info.CoreLoad;
                    }
                    else
                    {
                        prgGpuUsage.Text = "GPU 모니터링 초기화 중...";

                        // 카운터가 없거나 로드가 계속 0이면 재설정 시도 (5초마다 등 제한 필요하지만 간단히 처리)
                        if (_gpuLoadCounters.Count == 0) SetupGpuCounters();
                    }
                }));
            });
        }

        private GpuInfo GetPerformanceCounterValues()
        {
            GpuInfo info = new GpuInfo();
            try
            {
                // 1. GPU 로드율 가져오기 (가장 높은 부하를 사용)
                // AI 작업은 3D가 아닌 Compute/Cuda 엔진을 사용하므로 여러 엔진 중 최대값을 찾음
                float maxLoad = 0;
                foreach (var counter in _gpuLoadCounters)
                {
                    try
                    {
                        float val = counter.NextValue();
                        if (val > maxLoad) maxLoad = val;
                    }
                    catch { } // 특정 카운터 에러 무시
                }
                info.CoreLoad = (int)maxLoad;

                // 2. VRAM 사용량 가져오기
                if (_vramUsedCounter != null)
                {
                    long bytes = (long)_vramUsedCounter.NextValue();
                    info.MemoryUsed = (int)(bytes / 1024 / 1024);
                }

                // 3. 전체 VRAM 용량 (최초 1회만 레지스트리로 조회)
                if (_totalVramMB == 0)
                {
                    _totalVramMB = GetTotalVideoMemoryRegistry();
                }
                info.MemoryTotal = (int)_totalVramMB;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"카운터 읽기 오류: {ex.Message}");
            }
            return info;
        }

        private void SetupGpuCounters()
        {
            try
            {
                _gpuLoadCounters.Clear();
                var category = new PerformanceCounterCategory("GPU Engine");
                var instanceNames = category.GetInstanceNames();

                // 모니터링할 엔진 키워드: 3D, Compute, Cuda
                // Ollama/Llama 등 AI는 주로 'Compute_0' 또는 'Cuda' 엔진을 사용함
                var targetEngines = new[] { "engtype_3D", "engtype_Compute", "engtype_Cuda" };

                foreach (string name in instanceNames)
                {
                    // "pid_..."로 시작하는 인스턴스가 실제 프로세스별 사용량일 수 있으므로
                    // 전체 GPU 사용량을 보려면 "phys_..."가 포함된 인스턴스를 찾거나
                    // 단순히 모든 엔진을 등록해서 최대값을 봅니다.

                    bool isTarget = false;
                    foreach (var target in targetEngines)
                    {
                        if (name.Contains(target) && !name.Contains("Software"))
                        {
                            isTarget = true;
                            break;
                        }
                    }

                    if (isTarget)
                    {
                        try
                        {
                            var tempCounter = new PerformanceCounter("GPU Engine", "Utilization Percentage", name);
                            tempCounter.NextValue(); // 초기화
                            _gpuLoadCounters.Add(tempCounter);
                            Debug.WriteLine($"GPU Engine Found: {name}");
                        }
                        catch { }
                    }
                }

                // VRAM 카운터 ("Dedicated Usage")
                var memCategory = new PerformanceCounterCategory("GPU Adapter Memory");
                var memInstances = memCategory.GetInstanceNames();

                foreach (string name in memInstances)
                {
                    if (!string.IsNullOrEmpty(name))
                    {
                        try
                        {
                            var tempCounter = new PerformanceCounter("GPU Adapter Memory", "Dedicated Usage", name);
                            tempCounter.NextValue();
                            _vramUsedCounter = tempCounter;
                            Debug.WriteLine($"VRAM Counter Found: {name}");
                            break; // VRAM은 보통 하나만 잡으면 됨
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"카운터 설정 실패: {ex.Message}");
            }
        }

        // [중요] 레지스트리에서 64비트 VRAM 용량 직접 읽기 (4GB 제한 해결)
        private long GetTotalVideoMemoryRegistry()
        {
            try
            {
                // 디스플레이 어댑터 클래스 GUID
                string keyPath = @"SYSTEM\ControlSet001\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath))
                {
                    if (key != null)
                    {
                        foreach (var subKeyName in key.GetSubKeyNames())
                        {
                            using (var subKey = key.OpenSubKey(subKeyName))
                            {
                                // HardwareInformation.QwMemorySize는 64비트(long) 용량을 담고 있음
                                object sizeObj = subKey.GetValue("HardwareInformation.QwMemorySize");
                                if (sizeObj != null)
                                {
                                    long size = Convert.ToInt64(sizeObj);
                                    if (size > 1024 * 1024 * 1024) // 1GB 이상인 경우만 유효한 외장/메인 그래픽으로 간주
                                    {
                                        return size / 1024 / 1024; // MB 단위 반환
                                    }
                                }

                                // QwMemorySize가 없으면 HardwareInformation.MemorySize (32비트) 확인
                                // 하지만 4GB 이상인 경우 이 값은 부정확할 수 있음
                                object sizeObj32 = subKey.GetValue("HardwareInformation.MemorySize");
                                if (sizeObj32 != null)
                                {
                                    long size = Convert.ToInt64(sizeObj32);
                                    // 32비트라도 0이 아니면 일단 후보로 둠 (우선순위 낮음)
                                    // 그러나 보통 QwMemorySize가 있는 최신 드라이버가 타겟임
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"레지스트리 읽기 실패: {ex.Message}");
            }

            // 레지스트리 실패 시 기존 WMI 방식 Fallback (4095MB로 나오더라도 없는 것보단 나음)
            return GetTotalVideoMemory();
        }

        // WMI를 이용해 물리적 VRAM 총 용량 조회
        private long GetTotalVideoMemory()
        {
            try
            {
                // Win32_VideoController 클래스에서 AdapterRAM 속성 조회
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT AdapterRAM FROM Win32_VideoController");
                foreach (ManagementObject mo in searcher.Get())
                {
                    // 내장 그래픽 등 일부는 AdapterRAM을 제대로 보고하지 않을 수 있음
                    if (mo["AdapterRAM"] != null)
                    {
                        long bytes = Convert.ToInt64(mo["AdapterRAM"]);
                        // 512MB 이상인 경우만 외장/메인 그래픽으로 간주하여 반환 (작은 값은 무시)
                        if (bytes > 1024 * 1024 * 512)
                            return bytes / 1024 / 1024;
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

            public string RequestBody { get; set; }
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

            //SaveDefaultSetting();
        }

        private void btnSaveSysPrpt_Click(object sender, EventArgs e)
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

            SaveDefaultSetting();
        }

        private void txtTemp_EditValueChanged(object sender, EventArgs e)
        {
            if (_isLoading)
                return;

            SaveDefaultSetting();
        }

        private void txtNum_ctx_EditValueChanged(object sender, EventArgs e)
        {
            if (_isLoading)
                return;

            SaveDefaultSetting();
        }

        private void lstFiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstFiles.SelectedIndex == -1) return;

            // 1. 선택된 파일명 식별 (FileListItem 객체인지 문자열인지 확인)
            string selectedFileName = "";
            var selectedItem = lstFiles.SelectedItem;

            if (selectedItem == null) return;

            if (selectedItem is FileListItem fli)
            {
                selectedFileName = fli.FileName; // 실제 키값(파일명) 사용
            }
            else
            {
                selectedFileName = selectedItem.ToString();
            }

            // 2. 해당 파일명에 해당하는 모든 청크 데이터 수집
            var chunks = _vectorStore.Where(v => v.FileName == selectedFileName).ToList();

            // 3. 내용 표시
            txtAttFile.BeginUpdate();
            try
            {
                txtAttFile.Document.Text = ""; // 기존 내용 초기화 (중복 방지)

                if (chunks.Count > 0)
                {
                    // 헤더 정보
                    txtAttFile.Document.AppendText($"[File Info]\nName: {selectedFileName}\nTotal Chunks: {chunks.Count}\n");
                    txtAttFile.Document.AppendText(new string('=', 50) + "\n\n");

                    // 각 청크 내용 출력
                    for (int i = 0; i < chunks.Count; i++)
                    {
                        txtAttFile.Document.AppendText($"--- Chunk #{i + 1} ---\n");
                        txtAttFile.Document.AppendText(chunks[i].TextChunk);
                        txtAttFile.Document.AppendText("\n\n");
                    }
                }
                else
                {
                    txtAttFile.Document.AppendText("메모리에 로드된 데이터가 없습니다.");
                }
            }
            finally
            {
                txtAttFile.EndUpdate();
            }
        }


        private class FileListItem
        {
            public string FileName { get; set; }
            public string DisplayText { get; set; }

            public override string ToString()
            {
                return DisplayText; // 리스트박스에는 이 값이 표시됨
            }
        }

        private void txtNumCtxGB_EditValueChanged(object sender, EventArgs e)
        {
            if (txtNumCtxGB.EditValue == null)
                return;

            if(int.TryParse(txtNumCtxGB.EditValue.ToString(), out int gb))
            {
                txtNum_ctx.EditValue = gb * 1024;
            }
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
