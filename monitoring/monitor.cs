using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;

namespace ConsoleApp1
{
    internal class Program
    {
        private static CancellationTokenSource _cts = null;
        private static Task _monitorTask = null;

        private static ProxyServer _proxyServer;

        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            _proxyServer = new ProxyServer();

            try
            {
                _proxyServer.CertificateManager.EnsureRootCertificate();
                _proxyServer.CertificateManager.TrustRootCertificate(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[설정 오류] 인증서 설정 중 오류: " + ex.Message);
            }

            _proxyServer.BeforeRequest += OnRequest;

            var explicitEndPoint = new ExplicitProxyEndPoint(IPAddress.Any, 8000, true);
            _proxyServer.AddEndPoint(explicitEndPoint);
            _proxyServer.Start();
            _proxyServer.SetAsSystemHttpProxy(explicitEndPoint);
            _proxyServer.SetAsSystemHttpsProxy(explicitEndPoint);

            Console.WriteLine("모니터링 시작... (포트: 8000)");
            Console.WriteLine("종료하려면 Enter를 누르세요.");
            Console.ReadLine();

            _proxyServer.Stop();
        }

        private static async Task OnRequest(object sender, SessionEventArgs e)
        {
            var request = e.HttpClient.Request;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(string.Format("\n[{0:HH:mm:ss}] {1} {2}", DateTime.Now, request.Method, request.Url));
            Console.ResetColor();

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

                // 결과 출력
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
                    PrintParsedSoapBody(resultText);
                }
            }
        }

        static void PrintParsedSoapBody(string soapXml)
        {
            try
            {
                var doc = XDocument.Parse(soapXml);

                var mapperName = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "MapperName")?.Value;
                var queryId = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "QueryId")?.Value;
                Console.WriteLine($"MapperName : {mapperName}");
                Console.WriteLine($"QueryId : {queryId}");

                var parameters = doc.Descendants().Where(x => x.Name.LocalName == "Parameter");

                foreach (var param in parameters)
                {
                    var key = param.Descendants().FirstOrDefault(x => x.Name.LocalName == "Key")?.Value;
                    var value = param.Descendants().FirstOrDefault(x => x.Name.LocalName == "Value")?.Value;

                    Console.WriteLine($"{key} : {value}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("XML 파싱 중 오류 발생: " + ex.Message);
            }
        }

        private static byte[] DecompressGzip(byte[] data)
        {
            using (var input = new MemoryStream(data))
            using (var output = new MemoryStream())
            using (var stream = new GZipStream(input, CompressionMode.Decompress))
            {
                stream.CopyTo(output);
                return output.ToArray();
            }
        }

        private static byte[] DecompressDeflate(byte[] data)
        {
            using (var input = new MemoryStream(data))
            using (var output = new MemoryStream())
            using (var stream = new DeflateStream(input, CompressionMode.Decompress))
            {
                stream.CopyTo(output);
                return output.ToArray();
            }
        }

        private static bool IsWcfBinary(string contentType, byte[] data)
        {
            if (contentType.Contains("msbin") || contentType.Contains("soap+bin")) return true;
            if (data.Length > 0 && (data[0] == 0x40 || data[0] == 0x56)) return true;
            return false;
        }

        private static string DecodeWcfBinary(byte[] data)
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





          
