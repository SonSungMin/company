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

            SetMsg("--- 모니터링이 시작되었습니다.");
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

                SetMsg("--- 모니터링이 중지되었습니다.");
            }
        }

        /// <summary>
        /// 요청 발생 시 처리 로직
        /// </summary>
        private async Task OnRequest(object sender, SessionEventArgs e)
        {
            var request = e.HttpClient.Request;

            requestListView.BeginUpdate();

            ListViewItem item = new ListViewItem();
            item.SubItems.Add(DateTime.Now.ToString("HH:mm:ss"));
            item.SubItems.Add(request.Url);

            if ((request.Method == "POST" || request.Method == "PUT") && request.HasBody)
            {
                byte[] bodyBytes = await e.GetRequestBody();

                string contentType = request.ContentType ?? "null";
                string contentEncoding = request.ContentEncoding != null ? request.ContentEncoding.ToLower() : "none";

                Console.WriteLine(string.Format("[Header Info] Type: {0} | Encoding: {1}", contentType, contentEncoding));

                byte[] decompressedBytes = bodyBytes;

                bool isGzip = contentEncoding.Contains("gzip") ||
                                contentType.Contains("gzip") ||
                                (bodyBytes.Length >= 2 && bodyBytes[0] == 0x1F && bodyBytes[1] == 0x8B);

                try
                {
                    if (isGzip)
                    {
                        decompressedBytes = DecompressGzip(bodyBytes);
                        Console.WriteLine("[Info] Gzip 압축 해제됨 (서명 또는 헤더 감지)");
                    }
                    else if (contentEncoding.Contains("deflate"))
                    {
                        decompressedBytes = DecompressDeflate(bodyBytes);
                        Console.WriteLine("[Info] Deflate 압축 해제됨");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[Error] 압축 해제 시도 중 오류: " + ex.Message);
                }

                string resultText = "";

                if (IsWcfBinary(contentType, decompressedBytes))
                {
                    try
                    {
                        resultText = DecodeWcfBinary(decompressedBytes);
                        Console.WriteLine("[Success] WCF 바이너리 데이터 디코딩 성공:");
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

                // 결과 출력: 바이너리 여부 확인
                bool isBinaryLikely = resultText.Any(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t');

                if (isBinaryLikely)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[Warning] 데이터가 여전히 바이너리 형식이거나 알 수 없는 인코딩입니다.");

                    byte[] preview = decompressedBytes.Take(32).ToArray();
                    Console.WriteLine("Hex Dump (First 32 bytes): " + BitConverter.ToString(preview));
                    Console.ResetColor();
                }
                else
                {
                    Console.WriteLine("[Body Data]:");

                    try
                    {
                        // SOAP Body 등 XML 파싱 시도 (루트 요소가 없거나 형식이 안 맞을 수 있으므로 try-catch)
                        var doc = XDocument.Parse(resultText);

                        var mapperName = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "MapperName")?.Value;
                        var queryId = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "QueryId")?.Value;

                        item.SubItems.Add(mapperName);
                        item.SubItems.Add(queryId);

                        var parameters = doc.Descendants().Where(x => x.Name.LocalName == "Parameter");
                        string param_str = "";

                        foreach (var param in parameters)
                        {
                            var key = param.Descendants().FirstOrDefault(x => x.Name.LocalName == "Key")?.Value;
                            var value = param.Descendants().FirstOrDefault(x => x.Name.LocalName == "Value")?.Value;

                            if (key != null)
                                param_str += $"{key}:{value} ";
                        }

                        item.SubItems.Add(param_str);

                        requestListView.Items.Add(item);

                        requestListView.EndUpdate();
                    }
                    catch (Exception)
                    {
                        // XML 파싱 실패 시 원본 텍스트 출력 (또는 조용히 무시)
                        Console.WriteLine(resultText);
                    }
                }
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
    }
}

















