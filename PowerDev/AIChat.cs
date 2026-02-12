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
        private string LIGHT_MODLE = "gemma2-2b";
        private const string OLLAMA_URL = "http://localhost:11434/api/generate";
        private const string CHAT_URL = "http://localhost:11434/api/chat";
        private const string EMBED_URL = "http://localhost:11434/api/embeddings";
        private const string TAGS_URL = "http://localhost:11434/api/tags";

        private const string EMBEDDING_MODEL = "nomic-embed-text";
        private string DB_CONNECTION_STRING = SQLiteExt.ConnString;

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
        }

        private void SetGlobalFontSettings()
        {
            // 디자인에 있는 모든 리치 에디트 컨트롤 목록
            RichEditControl[] editors = { txtResult, txtResultInfo };

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
    Title TEXT NOT NULL,
    ChunkIndex INTEGER NOT NULL,
    TextChunk TEXT NOT NULL,
    VectorJson TEXT NOT NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_vector_title ON VectorStore(Title, ChunkIndex);

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
    UseRAG TEXT,
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
                    string sql = "SELECT Model, SystemPrompt, Temperature, Num_ctx, UseRAG FROM DefaultSetting ORDER BY CreatedAt DESC LIMIT 1";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            cboSystemPrpt.EditValue = reader["SystemPrompt"];
                            txtSystemPrompt.Text = GetSystemPrompt;

                            cboModelList.EditValue = reader["Model"];
                            txtTemp.EditValue = reader["Temperature"];
                            txtNum_ctx.EditValue = reader["Num_ctx"];
                            chkUseRAG.Checked = reader["UseRAG"] != null && reader["UseRAG"].ToString() == "Y";

                            if (int.TryParse(txtNum_ctx.EditValue == null ? "" : txtNum_ctx.EditValue.ToString(), out int vals))
                                txtNumCtxGB.EditValue = (int)(vals / 1027);
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
                string sql = @"INSERT INTO DefaultSetting (id, Model, SystemPrompt, Temperature, Num_ctx, UseRAG)
VALUES (1, @model, @systemPrompt, @temperature, @num_ctx, @userag)
ON CONFLICT(id) DO UPDATE SET
Model = excluded.Model,
SystemPrompt = excluded.SystemPrompt,
Temperature = excluded.Temperature,
Num_ctx = excluded.Num_ctx,
UseRAG = excluded.UseRAG;";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@model", cboModelList.EditValue != null ? cboModelList.EditValue.ToString() : "");
                    cmd.Parameters.AddWithValue("@systemPrompt", cboSystemPrpt.EditValue != null ? cboSystemPrpt.EditValue.ToString() : "");
                    cmd.Parameters.AddWithValue("@temperature", txtTemp.EditValue != null ? txtTemp.Text : "");
                    cmd.Parameters.AddWithValue("@num_ctx", txtNum_ctx.EditValue != null ? txtNum_ctx.Text : "");
                    cmd.Parameters.AddWithValue("@userag", chkUseRAG.Checked ? "Y" : "N");
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
                    // [변경] FileName -> Title, ChunkIndex 추가
                    // 정렬: 같은 문서끼리 모으고(Title), 그 안에서 순서대로(ChunkIndex)
                    string sql = "SELECT Title, ChunkIndex, TextChunk, VectorJson FROM VectorStore ORDER BY Title, ChunkIndex";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        // 중복 방지용 (Key: Title)
                        var loadedItems = new Dictionary<string, FileListItem>();

                        while (reader.Read())
                        {
                            string title = reader["Title"].ToString();
                            int chunkIndex = Convert.ToInt32(reader["ChunkIndex"]); // [추가]
                            string chunk = reader["TextChunk"].ToString();
                            string json = reader["VectorJson"].ToString();

                            try
                            {
                                var vector = JsonSerializer.Deserialize<double[]>(json);
                                if (vector != null)
                                {
                                    _vectorStore.Add(new VectorData
                                    {
                                        Title = title,             // [변경]
                                        ChunkIndex = chunkIndex,   // [추가]
                                        TextChunk = chunk,
                                        Vector = vector
                                    });

                                    // 리스트박스용 아이템 생성 (Title 기준 중복 확인)
                                    if (!loadedItems.ContainsKey(title))
                                    {
                                        string displayText = title;

                                        // 스니펫인 경우 내용에서 [분류] 또는 [제목] 라인을 찾아 표시명으로 사용
                                        if (title.StartsWith("SNIPPET_"))
                                        {
                                            using (StringReader sr = new StringReader(chunk))
                                            {
                                                string line;
                                                while ((line = sr.ReadLine()) != null)
                                                {
                                                    if (line.StartsWith("[분류]") || line.StartsWith("[제목]"))
                                                    {
                                                        displayText = line;
                                                        break;
                                                    }
                                                }
                                            }
                                        }

                                        loadedItems.Add(title, new FileListItem { Title = title, DisplayText = displayText });
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

        private void DeleteFileFromDb(string title)
        {
            using (var conn = new SQLiteConnection(DB_CONNECTION_STRING))
            {
                conn.Open();
                string sql = "DELETE FROM VectorStore WHERE Title = @title";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@title", title);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private bool IsFileProcessed(string title)
        {
            using (var conn = new SQLiteConnection(DB_CONNECTION_STRING))
            {
                conn.Open();
                string sql = "SELECT COUNT(*) FROM VectorStore WHERE Title = @title";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@title", title);
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
                        DeleteFileFromDb(item.Title);
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
            lblStatus.Text = "지식 데이터 처리 중...";
            this.Cursor = Cursors.WaitCursor;

            try
            {
                foreach (string filePath in filePaths)
                {
                    if (!File.Exists(filePath)) continue;

                    // 파일명을 기본 Title로 사용 (확장자 포함 or 제거 선택 가능)
                    string title = Path.GetFileName(filePath);

                    // 중복 확인 로직 (Title 기준)
                    if (lstFiles.Items.Cast<FileListItem>().Any(x => x.Title == title))
                        continue;

                    string content = File.ReadAllText(filePath);
                    int chunkSize = 500;
                    int overlap = 50;

                    // UI 리스트에 추가
                    lstFiles.Items.Add(new FileListItem { Title = title, DisplayText = title });

                    int i = 0;
                    int chunkIndex = 0; // 순서 번호

                    while (i < content.Length)
                    {
                        // [이전과 동일] 줄바꿈/공백 기준 자르기 로직
                        int length = Math.Min(chunkSize, content.Length - i);

                        if (i + length < content.Length)
                        {
                            int lastNewLine = content.LastIndexOf('\n', i + length, Math.Min(length, 150));
                            if (lastNewLine > i) length = lastNewLine - i;
                            else
                            {
                                int lastSpace = content.LastIndexOf(' ', i + length, Math.Min(length, 150));
                                if (lastSpace > i) length = lastSpace - i;
                            }
                        }

                        string chunkText = content.Substring(i, length).Trim();

                        if (!string.IsNullOrEmpty(chunkText))
                        {
                            var embedding = await GetEmbeddingAsync(chunkText);
                            if (embedding != null)
                            {
                                // [변경] Title을 저장
                                if (chkSaveDB.Checked)
                                    SaveVectorToDb(title, chunkIndex, chunkText, embedding);

                                _vectorStore.Add(new VectorData
                                {
                                    Title = title,
                                    ChunkIndex = chunkIndex,
                                    TextChunk = chunkText,
                                    Vector = embedding
                                });
                            }
                        }

                        chunkIndex++; // 순서 증가

                        // [이전과 동일] 다음 시작점 계산 (앞쪽 줄바꿈 맞춤)
                        int nextStart = i + length - overlap;
                        if (nextStart < content.Length)
                        {
                            int startNewLine = content.LastIndexOf('\n', nextStart, Math.Min(nextStart - i, overlap + 50));
                            if (startNewLine > i) nextStart = startNewLine + 1;
                        }
                        if (nextStart <= i) nextStart = i + 1;

                        i = nextStart;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"처리 중 오류: {ex.Message}");
            }
            finally
            {
                this.Cursor = Cursors.Default;
                lblStatus.Text = "완료";
            }
        }

        private void SaveVectorToDb(string title, int chunkIndex, string textChunk, double[] vector)
        {
            using (var conn = new SQLiteConnection(DB_CONNECTION_STRING))
            {
                conn.Open();
                
                string sql = "INSERT INTO VectorStore (Title, ChunkIndex, TextChunk, VectorJson) VALUES (@title, @idx, @tc, @vj)";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@title", title);
                    cmd.Parameters.AddWithValue("@idx", chunkIndex);
                    cmd.Parameters.AddWithValue("@tc", textChunk);
                    cmd.Parameters.AddWithValue("@vj", JsonSerializer.Serialize(vector));
                    cmd.ExecuteNonQuery();
                }
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
                // 1. 원본 데이터 로드 (계층 구조 복원을 위해 메모리에 적재)
                Dictionary<int, SnippetNode> nodeMap = new Dictionary<int, SnippetNode>();

                using (var sourceConn = new SQLiteConnection(DB_CONNECTION_STRING))
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
                                nodeMap.Add(id, new SnippetNode { Id = id, ParentId = pid, Code = code, Desc = desc });
                        }
                    }
                }

                if (nodeMap.Count == 0)
                {
                    MessageBox.Show("가져올 데이터가 없습니다.");
                    return;
                }

                lblStatus.Text = $"총 {nodeMap.Count}건 로드됨. 동기화 시작...";

                // 2. RAG DB에 저장 (청킹 및 임베딩)
                using (var targetConn = new SQLiteConnection(DB_CONNECTION_STRING))
                {
                    targetConn.Open();

                    foreach (var node in nodeMap.Values)
                    {
                        if (string.IsNullOrWhiteSpace(node.Desc)) continue;

                        var rootNode = GetRootNode(nodeMap, node.Id);
                        // 필터링 조건 (예: '조선' 루트만 가져오기 등 필요 시 활성화)
                        if (rootNode == null || rootNode.Code != "조선") continue;

                        // A. 타이틀 및 내용 생성
                        string categoryPath = GetCategoryPath(nodeMap, node.Id);
                        string cleanCategory = categoryPath.Replace(" > ", " ").Trim();
                        string title = $"{cleanCategory} {node.Code}".Trim();

                        // 공백 정리
                        while (title.Contains("  ")) title = title.Replace("  ", " ");

                        StringBuilder sb = new StringBuilder();
                        if (!string.IsNullOrWhiteSpace(categoryPath)) sb.AppendLine($"[분류] {categoryPath}");
                        sb.AppendLine($"[제목] {node.Code}");
                        sb.AppendLine($"[설명] {node.Desc}");

                        string newFullText = sb.ToString().Trim();
                        if (string.IsNullOrWhiteSpace(newFullText)) continue;

                        // B. 기존 데이터 확인 (Title 기준) -> 존재 시 삭제 후 재생성 전략
                        bool exists = false;
                        using (var cmdCheck = new SQLiteCommand("SELECT count(*) FROM VectorStore WHERE Title = @title", targetConn))
                        {
                            cmdCheck.Parameters.AddWithValue("@title", title);
                            long count = (long)cmdCheck.ExecuteScalar();
                            if (count > 0) exists = true;
                        }

                        if (exists)
                        {
                            using (var cmdDel = new SQLiteCommand("DELETE FROM VectorStore WHERE Title = @title", targetConn))
                            {
                                cmdDel.Parameters.AddWithValue("@title", title);
                                cmdDel.ExecuteNonQuery();
                            }
                            updateCount++;
                        }
                        else
                        {
                            insertCount++;
                        }

                        // C. 청킹 및 저장 (개선된 로직)
                        int targetChunkSize = 1000; // 코드가 포함되므로 청크 크기 확대
                        int minChunkSize = 300;     // 최소 크기 보장
                        int overlap = 100;          // 문맥 유지를 위한 중복 구간
                        int i = 0;
                        int chunkIndex = 0;

                        while (i < newFullText.Length)
                        {
                            // 기본 청크 길이 설정
                            int length = Math.Min(targetChunkSize, newFullText.Length - i);

                            // 마지막 부분이 아니라면 분할 지점 최적화
                            if (i + length < newFullText.Length)
                            {
                                int splitPoint = -1;
                                int searchStart = i + length;

                                // 최소 길이(minChunkSize)를 제외한 뒷부분에서만 분할 지점을 검색
                                // 검색 범위: (i + minChunkSize) ~ (i + length) 사이
                                int searchRange = length - minChunkSize;

                                if (searchRange > 0)
                                {
                                    // [우선순위 1] 문단 바꿈 (\n\n) - 코드 블록이나 문단 사이
                                    splitPoint = newFullText.LastIndexOf("\n\n", searchStart, searchRange);
                                    if (splitPoint == -1)
                                        splitPoint = newFullText.LastIndexOf("\r\n\r\n", searchStart, searchRange);

                                    // [우선순위 2] 일반 줄바꿈 (\n) - 문단 바꿈이 없으면 줄바꿈 위치
                                    if (splitPoint == -1)
                                        splitPoint = newFullText.LastIndexOf('\n', searchStart, searchRange);
                                }

                                // 적절한 분할 지점을 찾았다면 길이 조정
                                if (splitPoint > i)
                                {
                                    length = splitPoint - i;
                                }
                            }

                            string chunkText = newFullText.Substring(i, length).Trim();

                            if (!string.IsNullOrEmpty(chunkText))
                            {
                                var embedding = await GetEmbeddingAsync(chunkText);
                                if (embedding != null)
                                {
                                    using (var cmdInsert = new SQLiteCommand("INSERT INTO VectorStore (Title, ChunkIndex, TextChunk, VectorJson) VALUES (@title, @idx, @tc, @vj)", targetConn))
                                    {
                                        cmdInsert.Parameters.AddWithValue("@title", title);
                                        cmdInsert.Parameters.AddWithValue("@idx", chunkIndex);
                                        cmdInsert.Parameters.AddWithValue("@tc", chunkText);
                                        cmdInsert.Parameters.AddWithValue("@vj", JsonSerializer.Serialize(embedding));
                                        cmdInsert.ExecuteNonQuery();
                                    }
                                }
                            }

                            chunkIndex++;

                            // 다음 시작 지점 계산 (오버랩 적용)
                            int nextStart = i + length - overlap;

                            // [안전장치] 무한 루프 및 역행 방지: 최소한 현재 위치보다는 1이라도 커야 함
                            if (nextStart <= i) nextStart = i + Math.Max(1, length / 2);

                            // [정렬] 다음 시작점이 문맥 중간에 걸치지 않도록 줄바꿈 위치로 조정
                            if (nextStart < newFullText.Length)
                            {
                                // nextStart 근처의 앞쪽 줄바꿈을 찾아 깔끔하게 시작
                                // overlap 범위 내에서 줄바꿈을 찾음
                                int alignNewLine = newFullText.LastIndexOf('\n', nextStart, Math.Min(nextStart - i, overlap + 50));
                                if (alignNewLine > i)
                                {
                                    nextStart = alignNewLine + 1; // 줄바꿈 다음 글자부터 시작
                                }
                            }

                            i = nextStart;
                        }

                        if ((insertCount + updateCount + skipCount) % 5 == 0)
                        {
                            lblStatus.Text = $"처리 중... (I:{insertCount} / U:{updateCount})";
                            Application.DoEvents();
                        }
                    }
                }

                LoadVectorsFromDb();
                MessageBox.Show($"동기화 완료!\n- 신규: {insertCount}\n- 업데이트: {updateCount}");

            }
            catch (Exception ex)
            {
                MessageBox.Show($"오류: {ex.Message}\n{ex.StackTrace}");
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
                model = LIGHT_MODLE,
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

        private List<dynamic> SearchByKeyword(string userQuery)
        {
            var results = new List<dynamic>();

            // 한국어 주요 조사 제거
            string[] josa = { "은", "는", "이", "가", "을", "를", "에", "의", "로", "과", "와", "에서", "으로" };

            var keywords = userQuery.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(k => {
                                        string clean = k;
                                        foreach (var j in josa)
                                        {
                                            if (clean.EndsWith(j) && clean.Length > j.Length)
                                            {
                                                clean = clean.Substring(0, clean.Length - j.Length);
                                                break;
                                            }
                                        }
                                        return clean.ToUpper();
                                    })
                                    .Where(k => k.Length >= 2)
                                    .Distinct()
                                    .ToList();

            if (keywords.Count == 0) return results;

            foreach (var doc in _vectorStore)
            {
                double score = 0;
                string content = doc.TextChunk.ToUpper();

                foreach (var kw in keywords)
                {
                    if (content.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        score += 1.0;
                        // [분류]나 [제목]에 포함되면 가중치 부여
                        if (content.IndexOf($"[분류] ", StringComparison.OrdinalIgnoreCase) >= 0 && content.Contains(kw)) score += 2.0;
                        if (content.IndexOf($"[제목] ", StringComparison.OrdinalIgnoreCase) >= 0 && content.Contains(kw)) score += 2.0;
                    }
                }

                if (score > 0)
                {
                    // 여기서 doc은 VectorData 타입이므로 Title 속성을 포함하고 있음
                    results.Add(new { Chunk = doc, Score = score });
                }
            }

            return results.OrderByDescending(x => x.Score).Take(5).ToList();
        }

        // [Helper] 텍스트 청크에서 [분류] 또는 [제목] 정보를 추출하여 표시용 문자열 생성
        private string ExtractReferenceInfo(string textChunk)
        {
            if (string.IsNullOrWhiteSpace(textChunk)) return "알 수 없는 문서";

            using (StringReader sr = new StringReader(textChunk))
            {
                string line;
                string category = "";
                string title = "";

                // 상위 10줄 정도만 검사하여 메타데이터 추출
                int checkLineCount = 0;
                while ((line = sr.ReadLine()) != null && checkLineCount < 10)
                {
                    if (line.StartsWith("[분류]"))
                        category = line.Replace("[분류]", "").Trim();

                    if (line.StartsWith("[제목]"))
                        title = line.Replace("[제목]", "").Trim();

                    if (!string.IsNullOrEmpty(category) && !string.IsNullOrEmpty(title))
                        break;

                    checkLineCount++;
                }

                if (!string.IsNullOrEmpty(category) && !string.IsNullOrEmpty(title))
                    return $"{category} > {title}";

                if (!string.IsNullOrEmpty(category)) return category;
                if (!string.IsNullOrEmpty(title)) return title;

                // 태그가 없으면 첫 줄을 제목으로 간주
                return textChunk.Split('\n')[0].Trim();
            }
        }

        private async Task<string> ExpandQueryWithAIAsync(string userPrompt, CancellationToken token)
        {
            string systemPrompt = @"You are a Technical Search Assistant. 
Extract the core technical entities and nouns from the user's query. 
Convert natural language intent into potential technical keywords (English/Korean) that might appear in source code or database objects.";

            systemPrompt = @"You are a search keyword extractor.
- Extract ONLY technical keywords from the query.
- Convert natural language to code-related terms.
- OUTPUT: Only keywords separated by spaces.
- NO explanation, NO headers, NO markdown.";

            try
            {
                var requestData = new
                {
                    model = LIGHT_MODLE,
                    messages = new[] {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    stream = false,
                    options = new
                    {
                        temperature = 0.1, // 낮을수록 지시를 더 잘 따름
                        num_predict = 30    // 길게 말하지 못하게 제한
                    }
                };

                var response = await SendOllamaRequest(CHAT_URL, requestData, token);

                if (response.IsSuccess)
                {
                    string raw = response.ResponseText.Trim();

                    // AI가 혹시라도 줄바꿈이나 기호(#, *)를 넣었을 경우를 대비해 청소합니다.
                    string clean = System.Text.RegularExpressions.Regex.Replace(raw, @"[#*:\-]", "");
                    clean = clean.Replace("\n", " ").Replace("\r", " ");

                    return clean;
                }
            }
            catch { }
            return "";
        }

        private async void BtnAnalyze_Click(object sender, EventArgs e)
        {
            if (_cts != null)
            {
                try { _cts.Cancel(); } catch (ObjectDisposedException) { }
                return;
            }

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                btnAnalyze.Text = "중단";
                btnAnalyze.ImageOptions.Image = imgStopStart.Images[0];
                this.Cursor = Cursors.WaitCursor;

                if (mproAiThink != null)
                {
                    layAiThink.Visibility = DevExpress.XtraLayout.Utils.LayoutVisibility.Always;
                    mproAiThink.Properties.Stopped = false;
                }

                var content = ExtractContentFromRichEdit();
                string userPrompt = content.Text;
                string base64Image = content.ImageBase64;

                if (string.IsNullOrWhiteSpace(userPrompt) && string.IsNullOrEmpty(base64Image))
                {
                    MessageBox.Show("질문을 입력하세요.");
                    return;
                }

                string strictSystemPrompt = @"You are a Senior Full-Stack Engineer and Database Expert.
Analyze the provided [Context] using your professional programming knowledge.

### GUIDELINES:
1. **Semantic Analysis:** Do not just look for keywords. Interpret the code structure (SQL, C#, etc.) to identify technical objects like Procedures, Tables, and Functions based on their syntax and usage.
2. **Confident Inference:** If the code pattern (e.g., BEGIN...END, Method calls) implies a specific technical entity, treat it as such and explain it to the user.
3. **Fact-Based:** While you should infer meaning, do not invent names or logic that have no basis in the provided snippets.
4. **Professionalism:** Provide technical insights that a developer would find useful.
5. **Language:** Answer strictly in Professional Korean.";

                string generalSystemPrompt = @"You are a helpful AI Assistant. Answer strictly in Professional Korean.";

                if (_chatHistory.Count == 0)
                {
                    _chatHistory.Add(new { role = "system", content = generalSystemPrompt });
                }

                bool isSearchNeeded = chkUseRAG.Checked;
                string retrievedContext = "";
                bool isContextFound = false;
                List<string> references = new List<string>();

                txtResultInfo.Document.Text = "";

                if (isSearchNeeded && _vectorStore.Count > 0)
                {
                    lblStatus.Text = "지식 베이스 분석 중...";

                    // [STEP 1] 단순 명사 추출 및 검색어 확장
                    string expandedKeywords = await ExpandQueryWithAIAsync(userPrompt, token);
                    string combinedQuery = $"{userPrompt} {expandedKeywords}";

                    txtResultInfo.Document.AppendText("-----최종 쿼리-----\r\n");
                    txtResultInfo.Document.AppendText($"{combinedQuery}\r\n");
                    txtResultInfo.Document.AppendText("-------------------\r\n");

                    // [STEP 2] 하이브리드 검색 (Vector + Keyword)
                    var vectorResults = new List<dynamic>();
                    var queryVector = await GetEmbeddingAsync(userPrompt, token);

                    if (queryVector != null)
                    {
                        vectorResults = _vectorStore
                            .Select(v => new { Chunk = v, Score = CosineSimilarity(queryVector, v.Vector) })
                            .Where(x => x.Score > 0.25)
                            .OrderByDescending(x => x.Score)
                            .Take(5)
                            .Select(x => (dynamic)new { x.Chunk, Score = x.Score, Type = "Vector" })
                            .ToList();
                    }

                    var keywordResults = SearchByKeyword(combinedQuery);
                    foreach (var kwItem in keywordResults)
                    {
                        // [수정] FileName -> Title 로 변경
                        // 키워드 검색 결과가 벡터 검색 결과에 이미 있는지 확인 (Title과 TextChunk 기준)
                        if (!vectorResults.Any(v => v.Chunk.Title == kwItem.Chunk.Title && v.Chunk.TextChunk == kwItem.Chunk.TextChunk))
                        {
                            vectorResults.Add(new { Chunk = kwItem.Chunk, Score = kwItem.Score, Type = "Keyword" });
                        }
                    }

                    // [STEP 3] 최종 결과 통합
                    var finalResults = vectorResults.OrderByDescending(x => x.Score).Take(5).ToList();

                    if (finalResults.Count > 0)
                    {
                        isContextFound = true;
                        StringBuilder sb = new StringBuilder();
                        foreach (var item in finalResults)
                        {
                            // [수정] FileName -> Title 로 변경 (문서 소스 표시)
                            sb.AppendLine($"<doc source='{item.Chunk.Title}'>\n{item.Chunk.TextChunk}\n</doc>");

                            string refInfo = ExtractReferenceInfo(item.Chunk.TextChunk);
                            if (!references.Contains(refInfo)) references.Add(refInfo);
                        }
                        retrievedContext = sb.ToString();
                    }
                }

                string finalUserMessage = isContextFound ? $"[Context]\n{retrievedContext}\n\n[User Question]\n{userPrompt}" : userPrompt;

                if (_chatHistory.Count > 0)
                    _chatHistory[0] = new { role = "system", content = isContextFound ? strictSystemPrompt : generalSystemPrompt };

                PrintUserMessage(userPrompt, isContextFound);
                _chatHistory.Add(new { role = "user", content = finalUserMessage });

                lblStatus.Text = "답변 생성 중...";
                var result = await CallOllamaHistoryAsync(BRAIN_MODEL, _chatHistory, token);
                sw.Stop();

                if (result.IsSuccess)
                {
                    StringBuilder finalResponse = new StringBuilder(result.ResponseText);
                    if (references.Count > 0)
                    {
                        finalResponse.AppendLine("\r\n\r\n---\r\n**[참조된 지식]**");
                        for (int i = 0; i < references.Count; i++)
                            finalResponse.AppendLine($"{i + 1}. {references[i]}");
                    }

                    string responseWithRef = finalResponse.ToString();
                    _chatHistory.Add(new { role = "assistant", content = responseWithRef });
                    PrintChatMessage("AI", responseWithRef);

                    PrintExecutionTime(sw.Elapsed);
                    PrintTokenUsage(result.PromptEvalCount, result.EvalCount);
                    PrintGenerationInfo(result);
                }
                else if (!token.IsCancellationRequested)
                {
                    PrintChatMessage("Error", result.ResponseText);
                }
            }
            catch (OperationCanceledException) { lblStatus.Text = "취소됨"; }
            catch (Exception ex) { PrintChatMessage("Error", ex.Message); }
            finally
            {
                btnAnalyze.Text = "분석";
                btnAnalyze.ImageOptions.Image = imgStopStart.Images[1];
                if (mproAiThink != null) mproAiThink.Properties.Stopped = true;
                layAiThink.Visibility = DevExpress.XtraLayout.Utils.LayoutVisibility.Never;
                this.Cursor = Cursors.Default;
                var ctsToDispose = _cts;
                _cts = null;
                if (ctsToDispose != null) ctsToDispose.Dispose();
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
                                    if (contentStr.Contains("[지식베이스 참고]"))
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
            string text = txtQuest.Text.Trim();
            string imgBase64 = null;
            // 이미지는 제외
            //var images = txtQuest.Docent.Images;
            //if (images.Count > 0)
            //{
            //    try
            //    {
            //        DocumentImage docImage = images[0];
            //        Image originalImage = docImage.Image.NativeImage;
            //        if (originalImage != null)
            //        {
            //            using (Bitmap cleanBitmap = new Bitmap(originalImage.Width, originalImage.Height))
            //            {
            //                using (Graphics g = Graphics.FromImage(cleanBitmap))
            //                {
            //                    g.Clear(Color.White);
            //                    g.DrawImage(originalImage, 0, 0, originalImage.Width, originalImage.Height);
            //                }
            //                using (MemoryStream ms = new MemoryStream())
            //                {
            //                    cleanBitmap.Save(ms, ImageFormat.Jpeg);
            //                    imgBase64 = Convert.ToBase64String(ms.ToArray());
            //                }
            //            }
            //        }
            //    }
            //    catch { }
            //}

            return (text, imgBase64);
        }

        // 실행 시간 출력
        private void PrintExecutionTime(TimeSpan elapsed)
        {
            // 요청하신 포맷: [AI] 100초 (1분 40초)
            string timeText = $"[AI] {(int)elapsed.TotalSeconds}초 ({elapsed.Minutes}분 {elapsed.Seconds}초)";

            Document doc = txtResult.Document;
            doc.AppendText("\r\n"); // 줄바꿈

            DocumentRange range = doc.AppendText(timeText);

            CharacterProperties cp = doc.BeginUpdateCharacters(range);
            cp.FontName = "Consolas"; // 고정폭 글꼴 권장
            cp.FontSize = 8;          // 토큰 사용량과 동일한 크기
            cp.ForeColor = Color.Gray; // 토큰 사용량과 동일한 색상
            doc.EndUpdateCharacters(cp);
        }

        private void PrintChatMessage(string role, string msg)
        {
            Document doc = txtResult.Document;
            doc.AppendText("\r\n──────────────────────────────────────────────────\r\n");

            // 1. Role 표시
            DocumentRange range = doc.AppendText($"[{role}] ");
            CharacterProperties cp = doc.BeginUpdateCharacters(range);
            cp.Bold = true;
            if (role == "User") cp.ForeColor = Color.Blue;
            else if (role == "AI") cp.ForeColor = Color.Green;
            else if (role == "Error") cp.ForeColor = Color.Red;
            else cp.ForeColor = Color.Black;
            doc.EndUpdateCharacters(cp);

            doc.AppendText("\r\n");

            // 2. 본문 메시지 (마크다운 파싱 및 스타일 적용)
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

            // 1. 코드 블록(```) 분리
            string[] codeSegments = text.Split(new string[] { "```" }, StringSplitOptions.None);

            for (int i = 0; i < codeSegments.Length; i++)
            {
                // 홀수 인덱스는 코드 블록, 짝수는 일반 텍스트
                if (i % 2 == 1)
                {
                    AppendCodeBlock(doc, codeSegments[i]);
                    continue;
                }

                // 2. 일반 텍스트: 줄 단위 처리
                string[] lines = codeSegments[i].Replace("\r\n", "\n").Split('\n');

                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        doc.AppendText("\r\n");
                        continue;
                    }

                    string cleanLine = line.TrimEnd();
                    bool isHeader = false;
                    bool isList = false;
                    float fontSize = 10f; // 기본 폰트 크기

                    // 문단 스타일 적용을 위한 범위 시작점
                    int paragraphStart = doc.Range.End.ToInt();

                    // A. 헤더 감지 (# 또는 1. 2. 등의 숫자 목차)
                    if (System.Text.RegularExpressions.Regex.IsMatch(cleanLine.Trim(), @"^(#+\s|\d+\.\s)"))
                    {
                        isHeader = true;
                        fontSize = 12f; // 헤더는 조금 크게
                                        // 마크다운 헤더 기호(#) 제거
                        if (cleanLine.Trim().StartsWith("#"))
                            cleanLine = cleanLine.TrimStart('#', ' ');
                    }
                    // B. 리스트 감지 (* 또는 -)
                    else if (cleanLine.Trim().StartsWith("* ") || cleanLine.Trim().StartsWith("- "))
                    {
                        isList = true;
                        // 불릿 기호 제거 (DevExpress 글머리 기호 대신 깔끔한 유니코드 사용)
                        cleanLine = "  • " + cleanLine.Trim().Substring(2);
                    }

                    // C. 인라인 볼드 처리 (**text**) 및 텍스트 추가
                    string[] boldSegments = cleanLine.Split(new string[] { "**" }, StringSplitOptions.None);
                    for (int j = 0; j < boldSegments.Length; j++)
                    {
                        DocumentRange range = doc.AppendText(boldSegments[j]);
                        CharacterProperties cp = doc.BeginUpdateCharacters(range);
                        cp.FontName = "맑은 고딕";
                        cp.FontSize = fontSize;
                        cp.ForeColor = Color.Black;

                        // 헤더는 전체 볼드, 일반 문장은 ** 사이만 볼드
                        if (isHeader)
                        {
                            cp.Bold = true;
                        }
                        else
                        {
                            // 짝수 인덱스: 일반, 홀수 인덱스: 볼드 (split 특성상)
                            if (j % 2 == 1) cp.Bold = true;
                        }
                        doc.EndUpdateCharacters(cp);
                    }

                    // 줄바꿈
                    doc.AppendText("\r\n");

                    // D. 문단 속성(들여쓰기) 적용
                    DocumentRange paragraphRange = doc.CreateRange(paragraphStart, doc.Range.End.ToInt() - paragraphStart);
                    ParagraphProperties pp = doc.BeginUpdateParagraphs(paragraphRange);

                    if (isList)
                    {
                        pp.LeftIndent = 30; // 목록 들여쓰기 (픽셀 단위)
                        pp.FirstLineIndentType = ParagraphFirstLineIndent.Hanging; // 내어쓰기
                        pp.FirstLineIndent = 15;
                    }
                    else if (isHeader)
                    {
                        pp.SpacingBefore = 10; // 헤더 위쪽 여백
                        pp.SpacingAfter = 5;   // 헤더 아래쪽 여백
                    }
                    else
                    {
                        pp.LeftIndent = 0;
                    }

                    doc.EndUpdateParagraphs(pp);
                }
            }
        }

        // 코드 블록 스타일링 헬퍼 함수
        private void AppendCodeBlock(Document doc, string codeText)
        {
            // 언어 태그 제거 (예: csharp)
            string cleanCode = codeText;
            int firstBreak = codeText.IndexOfAny(new char[] { '\r', '\n' });
            if (firstBreak >= 0 && firstBreak < 20)
            {
                string firstLine = codeText.Substring(0, firstBreak).Trim();
                if (!firstLine.Contains(" ")) // 언어 태그로 간주
                    cleanCode = codeText.Substring(firstBreak).TrimStart();
            }

            doc.AppendText("\r\n"); // 코드 블록 위 여백
            DocumentRange range = doc.AppendText(cleanCode.TrimEnd());

            // 1. 폰트 스타일 (Consolas, 파란색)
            CharacterProperties cp = doc.BeginUpdateCharacters(range);
            cp.FontName = "Consolas";
            cp.FontSize = 9.5f;
            cp.ForeColor = Color.FromArgb(0, 0, 160); // Dark Blue
            cp.BackColor = Color.FromArgb(245, 245, 245); // 연한 회색 배경
            doc.EndUpdateCharacters(cp);

            // 2. 문단 스타일 (박스 형태 들여쓰기)
            ParagraphProperties pp = doc.BeginUpdateParagraphs(range);
            pp.LeftIndent = 40;
            pp.RightIndent = 40;
            pp.SpacingBefore = 5;
            pp.SpacingAfter = 5;
            doc.EndUpdateParagraphs(pp);

            doc.AppendText("\r\n"); // 코드 블록 아래 여백
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
                            if (m.Name.StartsWith("nomic-embed-text") || m.Name.StartsWith("bge-m3"))
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

            txtSystemPrompt.Text = GetSystemPrompt;

            //SaveDefaultSetting();
        }

        string GetSystemPrompt
        {
            get
            {
                string systemPrompt = cboSystemPrpt.EditValue == null ? "" : cboSystemPrpt.EditValue.ToString();
                string val = "";

                if (!string.IsNullOrEmpty(systemPrompt))
                {
                    var tmp = _systemPropt.Where(t => t.Title == systemPrompt);

                    if (tmp.Any())
                        val = tmp.First().Contents;
                }

                return val;
            }
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
                    cmd.Parameters.AddWithValue("@contents", txtSystemPrompt.Text);
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
            // 선택 해제 시
            if (lstFiles.SelectedIndex == -1)
            {
                txtAttFile.Text = "";
                return;
            }

            // 1. 선택된 Title 가져오기
            string selectedTitle = "";
            var selectedItem = lstFiles.SelectedItem;

            if (selectedItem is FileListItem fli)
                selectedTitle = fli.Title;
            else
                selectedTitle = selectedItem.ToString();

            // 2. 해당 Title의 청크 검색 및 정렬
            var chunks = _vectorStore
                            .Where(v => v.Title == selectedTitle)
                            .OrderBy(v => v.ChunkIndex)
                            .ToList();

            // 3. MemoEdit에 표시 (StringBuilder 사용)
            StringBuilder sb = new StringBuilder();

            if (chunks.Count > 0)
            {
                sb.AppendLine($"[문서 정보]");
                sb.AppendLine($"제목: {selectedTitle}");
                sb.AppendLine($"총 청크 수: {chunks.Count}개");
                sb.AppendLine(new string('-', 50));
                sb.AppendLine();

                foreach (var chunk in chunks)
                {
                    // 구분선 및 헤더
                    sb.AppendLine($"--- Chunk #{chunk.ChunkIndex} ---");

                    // 본문 내용
                    sb.AppendLine(chunk.TextChunk);
                    sb.AppendLine(); // 청크 간 공백
                }
            }
            else
            {
                sb.AppendLine("해당 문서의 저장된 내용을 찾을 수 없습니다.");
            }

            // MemoEdit 업데이트
            txtAttFile.Text = sb.ToString();

            // 스크롤을 맨 위로 이동
            txtAttFile.SelectionStart = 0;
            txtAttFile.ScrollToCaret();
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

        private void chkUseRAG_CheckedChanged(object sender, EventArgs e)
        {
            if (_isLoading)
                return;

            SaveDefaultSetting();
        }

        private void btnRefreshRAG_Click(object sender, EventArgs e)
        {
            LoadVectorsFromDb();
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
        public string Title { get; set; }
        public int ChunkIndex { get; set; }
        public string TextChunk { get; set; }
        public double[] Vector { get; set; }
    }

    public class FileListItem
    {
        public string Title { get; set; }
        public string DisplayText { get; set; }

        public override string ToString()
        {
            return DisplayText;
        }
    }

    public struct GpuInfo
    {
        public int CoreLoad;
        public int MemoryUsed;
        public int MemoryTotal;
    }
}
