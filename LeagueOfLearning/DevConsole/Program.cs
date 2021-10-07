using System;
using System.Linq;
using System.Net.Http;
using KertiLCU;

namespace DevConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            var leagueClient = new LeagueClient();
            string body = "{\"profileIconId\": "+22+"}";
            leagueClient.Request("put", "/lol-summoner/v1/current-summoner/icon", body);
        }

        
    }
}