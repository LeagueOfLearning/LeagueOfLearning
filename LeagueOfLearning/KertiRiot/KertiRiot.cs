using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;

namespace KertiRiot
{
    public class RiotApi
    {
        private HttpClient _httpClient;

        public RiotApi(string apiKey)
        {
            _httpClient = new HttpClient(new HttpClientHandler
            {
                SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls,
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true // Auto validate certifs 
            });
            _httpClient.DefaultRequestHeaders.Add("X-Riot-Token", apiKey);
        }

        public struct Response
        {
            public Response(HttpResponseHeaders header, string body, HttpStatusCode code)
            {
                ResponseHeader = header;
                ResponseBody = body;
                ResponseCode = code;
            }

            public HttpResponseHeaders ResponseHeader;
            public string ResponseBody;
            public HttpStatusCode ResponseCode;
        }

        public Response CallEndpoint(string url)
        {
            var result = _httpClient.GetAsync(url);
            result.Wait();
            var body = result.Result.Content.ReadAsStringAsync();
            HttpStatusCode responseCode = result.Result.StatusCode;
            HttpResponseHeaders header = result.Result.Headers;
            body.Wait();
            return new Response(
                header,
                body.Result,
                responseCode
            );
        }
    }
}