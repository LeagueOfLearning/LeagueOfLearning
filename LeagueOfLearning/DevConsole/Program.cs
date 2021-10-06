using System;
using PoniLCU;

namespace DevConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello");
            LeagueClient leagueClient = new LeagueClient();
            while (!leagueClient.IsConnected)
            {
                continue;
            }
            string body = "{\"profileIconId\": "+23+"}";
            leagueClient.Request("put", "/lol-summoner/v1/current-summoner/icon", body); 
        }
    }
}