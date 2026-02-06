using DevExpress.XtraRichEdit.API.Native;
using DevTools.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
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
using System.Windows.Forms;

namespace DevTools.UI.Control
{
    [Browsable(true)]
    [ToolboxItem(true)]
    public partial class AIChat : UserControlBase
    {
        private string EYE_MODEL = "qwen2-vl";
        private string BRAIN_MODEL = "deepseek-r1-8b";
        private const string OLLAMA_URL = "http://localhost:11434/api/generate";

        // [RAG] 임베딩 모델 (Ollama에서 'ollama pull nomic-embed-text' 필요)
        private const string EMBEDDING_MODEL = "nomic-embed-text";

        private int _currentMaxToken = 8192;

        // GPU 모니터링용 타이머 및 카운터
        private Timer _gpuTimer;
        private PerformanceCounter _gpuUsageCounter;
        private PerformanceCounter _vramUsedCounter;
        private long _totalVramMB = 0;

        // [RAG] 벡터 데이터 저장소 (메모리 DB 역할)
        private List<VectorData> _vectorStore = new List<VectorData>();

        // 모델별 권장 컨텍스트 크기 매핑
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

        public AIChat()
        {
            InitializeComponent();
            client.Timeout = TimeSpan.FromMinutes(10);

            if (mproAiThink != null)
            {
                mproAiThink.Properties.Stopped = true;
                layAiThink.Visibility = DevExpress.XtraLayout.Utils.LayoutVisibility.Never;
            }

            LoadModelsToComboBox();

            // GPU 모니터링 타이머 주석 해제 시 사용
            //_gpuTimer = new Timer();
            //_gpuTimer.Interval = 2000; 
            //_gpuTimer.Tick += GpuTimer_Tick;
            //_gpuTimer.Start();
        }

        #region GPU Monitoring
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
                        if (_gpuUsageCounter == null || _vramUsedCounter == null)
                        {
                            System.Diagnostics.Debug.WriteLine("GPU Counter is not initialized");
                        }
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GPU Counter Error: {ex.Message}");
            }
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
                var memInstances = memCategory.GetInstanceNames();
                foreach (string name in memInstances)
                {
                    if (!string.IsNullOrEmpty(name))
                    {
                        _vramUsedCounter = new PerformanceCounter("GPU Adapter Memory", "Dedicated Usage", name);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Setup Counters Failed: {ex.Message}");
            }
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
        #endregion

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
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

        private async void LoadModelsToComboBox()
        {
            var models = await GetOllamaModelsAsync();

            if (models.Count > 0)
            {
                cboModelList.Properties.Items.BeginUpdate();
                try
                {
                    cboModelList.Properties.Items.Clear();
                    foreach (string modelName in models)

                    {
                        cboModelList.Properties.Items.Add(new DevExpress.XtraEditors.Controls.ImageComboBoxItem(modelName, modelName, -1));
                    }
                    cboModelList.SelectedIndex = 0;
                }
                finally
                {
                    cboModelList.Properties.Items.EndUpdate();
                }
            }
            else
            {
                txtResult.Document.AppendText("[알림] 설치된 Ollama 모델을 찾을 수 없습니다.\r\n");
            }
        }

        private async Task<List<string>> GetOllamaModelsAsync()
        {
            string url = "http://localhost:11434/api/tags";
            try
            {
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var modelData = JsonSerializer.Deserialize<OllamaModelList>(responseBody, options);
                return modelData?.Models.Select(m => m.Name).ToList() ?? new List<string>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"모델 조회 실패: {ex.Message}");
                return new List<string>();
            }
        }

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
                    MessageBox.Show("내용을 입력하거나 이미지를 붙여넣으세요.");
                    return;
                }

                // 2. 시스템 프롬프트 설정 (최초 1회)
                if (_chatHistory.Count == 0)
                {
                    string systemPrompt = chkUsePrompt.Checked ? txtSystemPrompt.Text : "";
                    if (string.IsNullOrWhiteSpace(systemPrompt))
                    {
                        if (BRAIN_MODEL.ToLower().Contains("deepseek"))
                        {
                            systemPrompt = @"
You are an expert Senior Full Stack Engineer.
[Instructions]
1. Analyze Intent: Check if it's Technical or General.
2. Reasoning: Output thinking process in <think> tags.
3. Final Output: Answer strictly in Professional Korean.
";
                        }
                        else
                        {
                            systemPrompt = @"You are an expert Senior Full Stack Software Engineer. Translate responses to professional Korean.";
                        }
                    }
                    _chatHistory.Add(new { role = "system", content = systemPrompt });
                    txtSystemPrompt.Text = systemPrompt;
                    PrintChatMessage("System", $"대화를 시작합니다. (Context Limit: {_currentMaxToken} Tokens)");
                }

                // [RAG] 3. RAG 검색 및 컨텍스트 주입
                string retrievedContext = "";
                if (_vectorStore.Count > 0 && !string.IsNullOrWhiteSpace(userPrompt))
                {
                    lblStatus.Text = "[RAG] 문서 검색 중...";

                    // 질문 임베딩
                    var queryVector = await GetEmbeddingAsync(userPrompt);

                    if (queryVector != null)
                    {
                        // 유사도 검색 (상위 3개)
                        var relevantChunks = _vectorStore
                            .Select(v => new { Chunk = v, Score = CosineSimilarity(queryVector, v.Vector) })
                            .OrderByDescending(x => x.Score)
                            .Take(3)
                            .ToList();

                        StringBuilder sb = new StringBuilder();
                        bool hasRelevant = false;
                        foreach (var item in relevantChunks)
                        {
                            // 유사도 임계값 (예: 0.4 이상)
                            if (item.Score > 0.4)
                            {
                                sb.AppendLine($"--- Source: {item.Chunk.FileName} (Score: {item.Score:F2}) ---");
                                sb.AppendLine(item.Chunk.TextChunk);
                                sb.AppendLine();
                                hasRelevant = true;
                            }
                        }

                        if (hasRelevant)
                        {
                            retrievedContext = sb.ToString();
                            // 프롬프트에 검색 결과 추가
                            userPrompt = $"[Reference Context]:\r\n{retrievedContext}\r\n\r\n[User Question]:\r\n{userPrompt}\r\n\r\nAnswer based on the Reference Context if relevant.";
                        }
                    }
                }

                // 4. 이미지 처리
                if (!string.IsNullOrEmpty(base64Image))
                {
                    lblStatus.Text = $"[1단계] {EYE_MODEL} 이미지 분석 중...";
                    string ocrPrompt = "Please transcribe the text visible in this screenshot exactly.";
                    var ocrResult = await CallOllamaSingleAsync(EYE_MODEL, null, ocrPrompt, base64Image);
                    userPrompt = $"[Image Text]:\r\n{ocrResult.ResponseText}\r\n\r\n{userPrompt}";
                    await UnloadModel(EYE_MODEL);
                }

                // 5. 사용자 질문 히스토리 추가
                _chatHistory.Add(new { role = "user", content = userPrompt });

                // 화면에는 전체 RAG 프롬프트를 보여주면 너무 길 수 있으므로, 원본 질문 혹은 요약 표시
                string displayMsg = string.IsNullOrEmpty(retrievedContext) ? userPrompt : "[문서 참조됨] " + content.Text;
                PrintChatMessage("User", displayMsg);

                // 6. 뇌(Brain) 모델 호출
                lblStatus.Text = $"[2단계] {BRAIN_MODEL} 답변 생성 중...";

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

                await UnloadModel(BRAIN_MODEL);
                lblStatus.Text = "완료";
                txtQuest.Document.Text = "";
            }
            catch (Exception ex)
            {
                PrintChatMessage("Error", ex.Message);
                lblStatus.Text = "오류 발생";
            }
            finally
            {
                if (mproAiThink != null)
                {
                    mproAiThink.Properties.Stopped = true;
                    layAiThink.Visibility = DevExpress.XtraLayout.Utils.LayoutVisibility.Never;
                }
                btnAnalyze.Enabled = true;
                this.Cursor = Cursors.Default;
            }
        }

        private void PrintTokenUsage(int promptTokens, int responseTokens)
        {
            int totalUsed = promptTokens + responseTokens;
            int remaining = _currentMaxToken - totalUsed;
            double usagePercent = (double)totalUsed / _currentMaxToken * 100;

            string tokenInfo = $"[Token Usage] Used: {totalUsed} / {_currentMaxToken} ({usagePercent:F1}%) | Remaining: {remaining}";

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

        private void PrintChatMessage(string role, string message)
        {
            Document doc = txtResult.Document;
            doc.AppendText("\r\n──────────────────────────────────────────────────\r\n");
            DocumentRange range = doc.AppendText($"[{role}] ");
            CharacterProperties cp = doc.BeginUpdateCharacters(range);
            cp.Bold = true;
            if (role == "User") cp.ForeColor = Color.Blue;
            else if (role == "AI") cp.ForeColor = Color.Green;
            else if (role == "Error") cp.ForeColor = Color.Red;
            doc.EndUpdateCharacters(cp);
            doc.AppendText($"\r\n{message}\r\n");
            txtResult.Document.CaretPosition = txtResult.Document.CreatePosition(txtResult.Document.Range.End.ToInt());
            txtResult.ScrollToCaret();
        }

        #region Ollama API Wrapper
        private class OllamaResponse
        {
            public bool IsSuccess { get; set; }
            public string ResponseText { get; set; }
            public int PromptEvalCount { get; set; }
            public int EvalCount { get; set; }
        }

        private async Task<OllamaResponse> CallOllamaHistoryAsync(string modelName, List<object> historyMessages)
        {
            var requestData = new
            {
                model = modelName,
                messages = historyMessages,
                stream = false,
                options = new { num_ctx = _currentMaxToken, temperature = 0.6, num_thread = 6 }
            };
            return await SendOllamaRequest("http://localhost:11434/api/chat", requestData);
        }

        private async Task<OllamaResponse> CallOllamaSingleAsync(string modelName, string systemPrompt, string userPrompt, string base64Image = null)
        {
            var messages = new List<object>();
            if (!string.IsNullOrWhiteSpace(systemPrompt)) messages.Add(new { role = "system", content = systemPrompt });
            var userMessage = new { role = "user", content = userPrompt, images = !string.IsNullOrEmpty(base64Image) ? new[] { base64Image } : null };
            messages.Add(userMessage);
            var requestData = new { model = modelName, messages = messages, stream = false, options = new { num_ctx = _currentMaxToken, temperature = 0.1 } };
            return await SendOllamaRequest("http://localhost:11434/api/chat", requestData);
        }

        private async Task<OllamaResponse> SendOllamaRequest(string url, object requestData)
        {
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
            string jsonContent = JsonSerializer.Serialize(requestData, jsonOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                HttpResponseMessage response = await client.PostAsync(url, content);
                string responseBody = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode) return new OllamaResponse { IsSuccess = false, ResponseText = $"[서버 오류 {response.StatusCode}] {responseBody}" };
                using (JsonDocument doc = JsonDocument.Parse(responseBody))
                {
                    var root = doc.RootElement;
                    string text = "";
                    if (root.TryGetProperty("message", out JsonElement msg) && msg.TryGetProperty("content", out JsonElement cnt)) text = cnt.GetString();
                    int pEval = root.TryGetProperty("prompt_eval_count", out JsonElement pec) ? pec.GetInt32() : 0;
                    int eval = root.TryGetProperty("eval_count", out JsonElement ec) ? ec.GetInt32() : 0;
                    return new OllamaResponse { IsSuccess = true, ResponseText = text, PromptEvalCount = pEval, EvalCount = eval };
                }
            }
            catch (Exception ex) { return new OllamaResponse { IsSuccess = false, ResponseText = $"[예외 발생] {ex.Message}" }; }
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
        #endregion

        private void btnResetChat_Click(object sender, EventArgs e)
        {
            _chatHistory.Clear();
            txtResult.Text = "";
            PrintChatMessage("System", "대화 내용이 초기화되었습니다.");
        }

        #region File & RAG Logic
        private async void btnOpenFile_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Multiselect = true;
                openFileDialog.Title = "파일을 선택하세요";
                openFileDialog.Filter = "모든 파일 (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    foreach (string filePath in openFileDialog.FileNames)
                    {
                        if (!lstFiles.Items.Contains(filePath)) lstFiles.Items.Add(filePath);
                    }
                }
            }
            // 파일 변경 시 벡터 DB 업데이트
            await ProcessFilesToVectorStoreAsync();
            UpdateFileContDisplay();
        }

        private async void btnRemoveFile_Click(object sender, EventArgs e)
        {
            if (lstFiles.SelectedItems.Count > 0)
            {
                for (int i = lstFiles.SelectedIndices.Count - 1; i >= 0; i--)
                {
                    lstFiles.Items.RemoveAt(lstFiles.SelectedIndices[i]);
                }
                // 파일 변경 시 벡터 DB 업데이트
                await ProcessFilesToVectorStoreAsync();
                UpdateFileContDisplay();
            }
        }

        // 화면 표시용 (참고용으로 텍스트만 보여줌, 실제 프롬프트엔 안 들어감)
        void UpdateFileContDisplay()
        {
            txtAttFile.BeginUpdate();
            txtAttFile.Text = "";
            try
            {
                txtAttFile.Document.AppendText($"[RAG Mode On]\nLoaded Chunks: {_vectorStore.Count}개\n\n");
                foreach (string filePath in lstFiles.Items)
                {
                    txtAttFile.Document.AppendText($"- {Path.GetFileName(filePath)}\n");
                }
            }
            finally
            {
                txtAttFile.EndUpdate();
            }
        }

        // [RAG] 핵심: 파일을 읽어 임베딩 후 메모리에 저장
        private async Task ProcessFilesToVectorStoreAsync()
        {
            _vectorStore.Clear();
            lblStatus.Text = "문서 벡터화 진행 중...";
            this.Cursor = Cursors.WaitCursor;

            try
            {
                foreach (string filePath in lstFiles.Items)
                {
                    if (!File.Exists(filePath)) continue;

                    string content = File.ReadAllText(filePath);

                    // 1. Chunking (500자 단위, 50자 오버랩)
                    int chunkSize = 500;
                    int overlap = 50;

                    for (int i = 0; i < content.Length; i += (chunkSize - overlap))
                    {
                        int length = Math.Min(chunkSize, content.Length - i);
                        string chunkText = content.Substring(i, length);

                        // 2. 임베딩 생성
                        var embedding = await GetEmbeddingAsync(chunkText);

                        if (embedding != null)
                        {
                            _vectorStore.Add(new VectorData
                            {
                                FileName = Path.GetFileName(filePath),
                                TextChunk = chunkText,
                                Vector = embedding
                            });
                        }
                    }
                }
                lblStatus.Text = $"벡터화 완료 ({_vectorStore.Count} 청크)";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"벡터화 실패: {ex.Message}");
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        // [RAG] 임베딩 API 호출
        private async Task<double[]> GetEmbeddingAsync(string text)
        {
            var requestData = new { model = EMBEDDING_MODEL, prompt = text };
            var jsonContent = JsonSerializer.Serialize(requestData);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PostAsync("http://localhost:11434/api/embeddings", content);
                if (!response.IsSuccessStatusCode) return null;

                var body = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(body);
                return result?.Embedding;
            }
            catch { return null; }
        }

        // [RAG] 코사인 유사도 계산
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
        #endregion

        private void cboModelList_EditValueChanged(object sender, EventArgs e)
        {
            if (cboModelList.EditValue == null) return;
            string selectedModel = BRAIN_MODEL = cboModelList.EditValue.ToString().ToLower();
            int newLimit = 8192;
            foreach (var kvp in _modelTokenLimits)
            {
                if (selectedModel.Contains(kvp.Key)) { newLimit = kvp.Value; break; }
            }
            _currentMaxToken = newLimit;
        }
    }

    public struct GpuInfo
    {
        public int CoreLoad;
        public int MemoryUsed;
        public int MemoryTotal;
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

    // [RAG] 데이터 클래스
    public class VectorData
    {
        public string FileName { get; set; }
        public string TextChunk { get; set; }
        public double[] Vector { get; set; }
    }

    public class OllamaEmbeddingResponse
    {
        [JsonPropertyName("embedding")]
        public double[] Embedding { get; set; }
    }
}
