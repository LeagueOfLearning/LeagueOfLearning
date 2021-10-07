using System;
using System.IO;
using Camille.Enums;
using Camille.RiotGames;

namespace DataScraper
{
    class Program
    {
        static void Main(string[] args)
        {
            var stream = new StreamReader("../../../apiKey.txt");
            var key = stream.ReadToEnd();
           var camApi = RiotGamesApi.NewInstance(key);
           var summoners = new[]
           {
               camApi.SummonerV4().GetBySummonerName(PlatformRoute.NA1, "Kertaak")
           };
           foreach (var summoner in summoners)
           {
               Console.WriteLine($"{summoner.Name}'s Top 10 Champs:");

               var masteries =
                   camApi.ChampionMasteryV4().GetAllChampionMasteries(PlatformRoute.NA1, summoner.Id);

               for (var i = 0; i < 10; i++)
               {
                   var mastery = masteries[i];
                   // Get champion for this mastery.
                   var champ = (Champion) mastery.ChampionId;
                   // print i, champ id, champ mastery points, and champ level
                   Console.WriteLine("{0,3}) {1,-16} {2,10:N0} ({3})", i + 1, champ.ToString(),
                       mastery.ChampionPoints, mastery.ChampionLevel);
               }
               Console.WriteLine();
           } 
        }
    }
}