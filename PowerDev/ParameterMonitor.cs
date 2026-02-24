using DevTools.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;


namespace DevTools.UI.Control
{
    public partial class ParameterMonitor : UserControlBase
    {
        private ProxyServer _proxyServer;
        private ExplicitProxyEndPoint _explicitEndPoint;
        private const int Port = 8000;

        public ParameterMonitor()
        {
            InitializeComponent();
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            // 모니터링이 시작된 경우
            // 종료
            if(IsStarted)
            {
                this.btnStart.ImageOptions.ImageIndex = 0;

                StopMonitoring();
            }
            // 시작
            else
            {
                this.btnStart.ImageOptions.ImageIndex = 1;

                StartMonitoring();
            }
        }


        void SetMsg(string msg)
        {
            msg = $"[{DateTime.Now.ToString("HH:mm:ss")}] {msg}";
            lstLog.Items.Add(msg);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            StopMonitoring();

            base.Dispose(disposing);
        }

        /// <summary>
        /// 모니터링 시작
        /// </summary>
        public void StartMonitoring()
        {
            if (_proxyServer != null && _proxyServer.ProxyRunning)
            {
                return;
            }

            _proxyServer = new ProxyServer();

            try
            {
                // 인증서 관리 설정
                _proxyServer.CertificateManager.EnsureRootCertificate();
                _proxyServer.CertificateManager.TrustRootCertificate(true);
            }
            catch (Exception ex)
            {
                SetMsg("[설정 오류] 인증서 설정 중 오류: " + ex.Message);
            }

            // 이벤트 핸들러 등록
            _proxyServer.BeforeRequest += OnRequest;

            // 엔드포인트 설정 (모든 IP, 지정된 포트, HTTP/HTTPS)
            _explicitEndPoint = new ExplicitProxyEndPoint(IPAddress.Any, Port, true);
            _proxyServer.AddEndPoint(_explicitEndPoint);

            // 프록시 서버 시작
            _proxyServer.Start();

            // 시스템 프록시로 설정
            _proxyServer.SetAsSystemHttpProxy(_explicitEndPoint);
            _proxyServer.SetAsSystemHttpsProxy(_explicitEndPoint);

            SetMsg("● 모니터링이 시작되었습니다.");
        }


        public bool IsStarted
        {
            get
            {
                return _proxyServer != null;
            }
        }

        /// <summary>
        /// 모니터링 중지
        /// </summary>
        public void StopMonitoring()
        {
            if (_proxyServer != null)
            {
                // 이벤트 핸들러 해제
                _proxyServer.BeforeRequest -= OnRequest;

                // 프록시 서버 중지 및 시스템 프록시 설정 해제
                _proxyServer.Stop();
                _proxyServer.Dispose();
                _proxyServer = null;

                SetMsg("○ 모니터링이 중지되었습니다.");
            }
        }

        /// <summary>
        /// 요청 발생 시 처리 로직
        /// </summary>
        private async Task OnRequest(object sender, SessionEventArgs e)
        {
            var request = e.HttpClient.Request;
            string url = request.Url;

            // txtUrlFilter는 UI에 있는 텍스트박스 이름으로 가정합니다. (Designer 파일 확인 필요)
            string filterText = "";
            this.Invoke((MethodInvoker)delegate { filterText = txtUrl.Text; });

            if (!string.IsNullOrWhiteSpace(filterText))
            {
                // 세미콜론(;)으로 구분하여 키워드 배열 생성
                string[] keywords = filterText.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                // URL에 키워드 중 하나라도 포함되어 있는지 확인 (대소문자 구분 없음)
                bool isMatch = keywords.Any(k => url.IndexOf(k.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);

                // 일치하는 키워드가 없으면 처리를 중단하고 리턴
                if (!isMatch) 
                    return;
            }

            // 1. UI에 표시할 데이터 준비 (백그라운드 스레드에서 수행)
            string timeStr = DateTime.Now.ToString("HH:mm:ss");
            string mapperName = "";
            string queryId = "";
            string paramStr = "";
            bool hasBodyData = false;

            // POST 또는 PUT 요청이고 Body가 있는 경우 데이터 파싱
            if ((request.Method == "POST" || request.Method == "PUT") && request.HasBody)
            {
                try
                {
                    byte[] bodyBytes = await e.GetRequestBody();

                    string contentType = request.ContentType ?? "null";
                    string contentEncoding = request.ContentEncoding != null ? request.ContentEncoding.ToLower() : "none";

                    SetMsg(string.Format("[Header Info] Type: {0} | Encoding: {1}", contentType, contentEncoding));

                    byte[] decompressedBytes = bodyBytes;

                    // 압축 해제 로직
                    bool isGzip = contentEncoding.Contains("gzip") || contentType.Contains("gzip") || (bodyBytes.Length >= 2 && bodyBytes[0] == 0x1F && bodyBytes[1] == 0x8B);

                    try
                    {
                        if (isGzip)
                        {
                            decompressedBytes = DecompressGzip(bodyBytes);
                        }
                        else if (contentEncoding.Contains("deflate"))
                        {
                            decompressedBytes = DecompressDeflate(bodyBytes);
                        }
                    }
                    catch (Exception ex)
                    {
                        SetMsg("[Error] 압축 해제 시도 중 오류: " + ex.Message);
                    }

                    string resultText = "";

                    // WCF 바이너리 또는 일반 텍스트 디코딩
                    if (IsWcfBinary(contentType, decompressedBytes))
                    {
                        try
                        {
                            resultText = DecodeWcfBinary(decompressedBytes);
                        }
                        catch
                        {
                            resultText = "[Failed] WCF 바이너리 디코딩 실패";
                        }
                    }
                    else
                    {
                        resultText = Encoding.UTF8.GetString(decompressedBytes);
                    }

                    // 바이너리 데이터 여부 확인 (제어 문자 체크)
                    bool isBinaryLikely = resultText.Any(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t');

                    if (!isBinaryLikely)
                    {
                        // XML 파싱 시도
                        try
                        {
                            var doc = XDocument.Parse(resultText);

                            mapperName = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "MapperName")?.Value ?? "";
                            queryId = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "QueryId")?.Value ?? "";

                            var parameters = doc.Descendants().Where(x => x.Name.LocalName == "Parameter");
                            StringBuilder sbParam = new StringBuilder();

                            foreach (var param in parameters)
                            {
                                var key = param.Descendants().FirstOrDefault(x => x.Name.LocalName == "Key")?.Value;
                                var value = param.Descendants().FirstOrDefault(x => x.Name.LocalName == "Value")?.Value;

                                if (key != null)
                                    sbParam.Append($"{key}:{value} ");
                            }
                            paramStr = sbParam.ToString();
                            hasBodyData = true;
                        }
                        catch
                        {
                            // XML 파싱 실패 시 원본 텍스트를 파라미터 컬럼에 표시하거나 무시
                            // paramStr = resultText; 
                            SetMsg($":: XML 파싱오류 :: {resultText}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    SetMsg("Body 데이터 처리 중 오류: " + ex.Message);
                }
            }

            // 2. UI 업데이트 (Invoke를 통해 UI 스레드에서 수행)
            if (this.IsHandleCreated && !this.IsDisposed)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    requestListView.BeginUpdate();
                    try
                    {
                        // 1. 첫 번째 데이터(시간)를 생성자에 바로 전달하여 0번 컬럼으로 지정합니다.
                        ListViewItem item = new ListViewItem(timeStr);

                        // 2. 그 다음 데이터부터 SubItems에 추가합니다.
                        item.SubItems.Add(url);

                        if (hasBodyData)
                        {
                            item.SubItems.Add(mapperName);
                            item.SubItems.Add(queryId);
                            item.SubItems.Add(paramStr);
                        }
                        else
                        {
                            // Body 데이터가 없는 경우에도 컬럼 위치를 맞추기 위해 빈 값을 넣어줄 수 있습니다.
                            item.SubItems.Add(""); // MapperName
                            item.SubItems.Add(""); // QueryId
                            item.SubItems.Add(""); // ParamStr
                        }

                        requestListView.Items.Add(item);
                        requestListView.EnsureVisible(requestListView.Items.Count - 1);
                    }
                    finally
                    {
                        requestListView.EndUpdate();
                    }
                });
            }
        }

        private byte[] DecompressGzip(byte[] data)
        {
            using (var input = new MemoryStream(data))
            using (var output = new MemoryStream())
            using (var stream = new GZipStream(input, CompressionMode.Decompress))
            {
                stream.CopyTo(output);
                return output.ToArray();
            }
        }

        private byte[] DecompressDeflate(byte[] data)
        {
            using (var input = new MemoryStream(data))
            using (var output = new MemoryStream())
            using (var stream = new DeflateStream(input, CompressionMode.Decompress))
            {
                stream.CopyTo(output);
                return output.ToArray();
            }
        }

        private bool IsWcfBinary(string contentType, byte[] data)
        {
            if (contentType.Contains("msbin") || contentType.Contains("soap+bin")) return true;
            if (data.Length > 0 && (data[0] == 0x40 || data[0] == 0x56)) return true; // WCF Binary Dictionary 시작 바이트
            return false;
        }

        private string DecodeWcfBinary(byte[] data)
        {
            using (var stream = new MemoryStream(data))
            using (var reader = XmlDictionaryReader.CreateBinaryReader(stream, XmlDictionaryReaderQuotas.Max))
            {
                var sb = new StringBuilder();
                using (var writer = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true }))
                {
                    writer.WriteNode(reader, true);
                }
                return sb.ToString();
            }
        }

        private void requestListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (requestListView.SelectedItems.Count > 0)
            {
                string txt = "";
                string param = "";

                ListViewItem row = requestListView.SelectedItems[0];

                param = row.SubItems[4].Text;

                if (string.IsNullOrEmpty(param))
                    param = "없음";

                txt = $@"
## URL ##
{row.SubItems[1].Text}

## Mapper ##
{row.SubItems[2].Text}

## Query ID ##
{row.SubItems[3].Text}

## Parameter ##
{GetParam(param)}
";

                mmoDetail.Text = txt;
            }
        }


        string GetParam(string rawData)
        {
            string rtn = "";

            // 1. 결과를 담을 딕셔너리 생성
            Dictionary<string, string> resultDict = new Dictionary<string, string>();

            // 2. 공백을 기준으로 먼저 자름 (StringSplitOptions.RemoveEmptyEntries로 연속된 공백 방지)
            string[] pairs = rawData.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string pair in pairs)
            {
                // 3. 각 쌍을 첫 번째 ':'를 기준으로 나눔
                int colonIndex = pair.IndexOf(':');

                if (colonIndex != -1)
                {
                    string key = pair.Substring(0, colonIndex).Trim();
                    string value = pair.Substring(colonIndex + 1); // 값이 비어있어도 빈 문자열("")이 들어감

                    resultDict[key] = value;
                }
            }

            // 출력 테스트
            foreach (var item in resultDict)
            {
                rtn += $"> {item.Key} : {item.Value}{Environment.NewLine}";
            }

            return rtn;
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            requestListView.Items.Clear();
            mmoDetail.Text = "";
        }
    }
}
