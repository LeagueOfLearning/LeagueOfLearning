using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using RestSharp;

/*
 * Originally forked from PoniLCU,
 * This forms a wrapper for the League of Legends client API with both windows and mac support
 */
namespace KertiLCU
{
    public class OnWebsocketEventArgs : EventArgs
    {
        // URI    
        public string Path { get; set; }

        // Update create delete     
        public string Type { get; set; }

        // data :D
        public dynamic Data { get; set; }
    }

    public class LeagueClient
    {
        private static HttpClient HTTP_CLIENT;
        private static readonly int RETRY_TIMES = 5;

        private Tuple<string, string> processInfo;

        private readonly Dictionary<string, List<Action<OnWebsocketEventArgs>>> Subscribers =
            new();

        public LeagueClient()
        {
            try
            {
                HTTP_CLIENT = new HttpClient(new HttpClientHandler
                {
                    SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls,
                    ServerCertificateCustomValidationCallback = (a, b, c, d) => true
                });
            }
            catch
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 |
                                                       SecurityProtocolType.Tls;

                HTTP_CLIENT = new HttpClient(new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (a, b, c, d) => true
                });
            }

            Task.Delay(2000).ContinueWith(e => TryConnectOrRetry());
            var trytimes = 0;
            while (!IsConnected)
                if (trytimes < RETRY_TIMES)
                {
                    trytimes++;
                    TryConnectOrRetry();
                }
                else
                {
                    Console.WriteLine("Connection timed out");
                    throw new Exception("Unable to connect to league client.");
                }
        }

        public bool IsConnected { get; private set; }

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<OnWebsocketEventArgs> OnWebsocketEvent;

        public Task<HttpResponseMessage> Request(string method, string url, object body)
        {
            if (!IsConnected) throw new InvalidOperationException("Not connected to LCU");

            return HTTP_CLIENT.SendAsync(
                new HttpRequestMessage(new HttpMethod(method), "https://127.0.0.1:" + processInfo.Item2 + url)
                {
                    Content = body == null
                        ? null
                        : new StringContent(body.ToString(), Encoding.UTF8, "application/json")
                });
        }

        public async Task<dynamic> getStringJsoned(string url)
        {
            if (!IsConnected) throw new InvalidOperationException("Not connected to LCU");

            var res = await HTTP_CLIENT.GetAsync("https://127.0.0.1:" + processInfo.Item2 + url);
            var stringContent = await res.Content.ReadAsStringAsync();

            if (res.StatusCode == HttpStatusCode.NotFound) return null;
            return SimpleJson.DeserializeObject(stringContent);
        }

        public async void GetData(string url, Action<dynamic> handler)
        {
            OnWebsocketEvent += data =>
            {
                if (data.Path == url) handler(data.Data);
            };

            if (IsConnected)
            {
                handler(await getStringJsoned(url));
            }
            else
            {
                Action connectHandler = null;
                connectHandler = async () =>
                {
                    OnConnected -= connectHandler;
                    handler(await getStringJsoned(url));
                };

                OnConnected += connectHandler;
            }
        }

        private async void TryConnect()
        {
            try
            {
                if (IsConnected) return;

                var status = LeagueUtils.GetLeagueStatus();
                if (status == null) return;

                var byteArray = Encoding.ASCII.GetBytes("riot:" + status.Item1);
                HTTP_CLIENT.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                try
                {
                    var testEndpoint = "lol-summoner/v1/current-summoner";
                    var test = await HTTP_CLIENT.GetAsync("wss://127.0.0.1:" + status.Item2 + "/" + testEndpoint);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }


                processInfo = status;
                IsConnected = true;

                OnConnected?.Invoke();
            }
            catch (Exception e)
            {
                processInfo = null;
                IsConnected = false;
                Debug.WriteLine($"Exception occurred trying to connect to League of Legends: {e}");
            }
        }

        public void Subscribe(string URI, Action<OnWebsocketEventArgs> args)
        {
            if (!Subscribers.ContainsKey(URI))
                Subscribers.Add(URI, new List<Action<OnWebsocketEventArgs>> {args});
            else
                Subscribers[URI].Add(args);
        }

        public void Unsubscribe(string URI, Action<OnWebsocketEventArgs> action)
        {
            if (Subscribers.ContainsKey(URI))
            {
                if (Subscribers[URI].Count == 1)
                    Subscribers.Remove(URI);
                else if (Subscribers[URI].Count > 1)
                    foreach (var item in Subscribers[URI].ToArray())
                        if (item == action)
                        {
                            var index = Subscribers[URI].IndexOf(action);
                            Subscribers[URI].RemoveAt(index);
                        }
                        else
                            return;
            }
        }

        private void TryConnectOrRetry()
        {
            if (IsConnected) return;
            TryConnect();

            Task.Delay(2000).ContinueWith(a => TryConnectOrRetry());
        }

        public async Task<byte[]> GetAsset(string url)
        {
            if (!IsConnected) throw new InvalidOperationException("Not connected to LCU");

            var res = await HTTP_CLIENT.GetAsync("https://127.0.0.1:" + processInfo.Item2 + url);
            return await res.Content.ReadAsByteArrayAsync();
        }

        private static class LeagueUtils
        {
            private static readonly Regex AUTH_TOKEN_REGEX = new("\"--remoting-auth-token=(.+?)\"");
            private static readonly Regex PORT_REGEX = new("\"--app-port=(\\d+?)\"");

            public static Tuple<string, string> GetLeagueStatus()
            {
                foreach (var p in Process.GetProcessesByName("LeagueClientUx"))
                {
                    var osVersion = Environment.OSVersion;
                    // Win32NT is Windows NT or later, all other windows values are not in use (link: https://docs.microsoft.com/en-us/dotnet/api/system.platformid?view=net-5.0)
                    if (osVersion.Platform == PlatformID.Win32NT)
                    {
                        using (var mos =
                            new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " +
                                                         p.Id))
                        using (var moc = mos.Get())
                        {
                            var commandLine = (string) moc.OfType<ManagementObject>().First()["CommandLine"];

                            try
                            {
                                var authToken = AUTH_TOKEN_REGEX.Match(commandLine).Groups[1].Value;
                                var port = PORT_REGEX.Match(commandLine).Groups[1].Value;
                                return new Tuple<string, string>
                                (
                                    authToken,
                                    port
                                );
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(
                                    $"Error while trying to get the status for LeagueClientUx: {e}\n\n(CommandLine = {commandLine})");
                            }
                        }
                    }
                    else if (osVersion.Platform == PlatformID.Unix || osVersion.Platform == PlatformID.MacOSX)
                    {
                        var command = "ps -A | grep LeagueClientUx";
                        var proc = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = "/bin/bash",
                                Arguments = "-c \"" + command + "\"",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                CreateNoWindow = true
                            }
                        };

                        proc.Start();
                        proc.WaitForExit();

                        var result = proc.StandardOutput.ReadToEnd();
                        var portRegex = Regex.Match(result, "--app-port=([0-9]*)");
                        var tokenRegex = Regex.Match(result, "--remoting-auth-token=([\\w-]*)");
                        var port = portRegex.Value.Split('=')[1];
                        var token = tokenRegex.Value.Split('=')[1];
                        return new Tuple<string, string>(token, port);
                    }
                }
                return null;
            }
        }
    }
}