using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http.Json;
using Camille.Enums;
using Camille.RiotGames;
using Camille.RiotGames.MatchV5;
using Camille.RiotGames.SummonerV4;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DataScraper
{
    class Program
    {
        static void Main(string[] args)
        {
            var stream = new StreamReader("../../../apiKey.txt");
            var key = stream.ReadToEnd();
            RiotGamesApi camApi = RiotGamesApi.NewInstance(key);
            var matchData = new MatchCollection();
            var summoner = camApi.SummonerV4().GetBySummonerName(PlatformRoute.NA1, "Kertaak");
            var collection = new MatchCollection();
            AddPlayersMatches(summoner, camApi, 0, collection);
            string test = collection.GetMatchesInJson();
            var writer = new StreamWriter("../../../stuff.json");
            writer.Write(test);
            writer.Close();
        }

        static public void AddPlayersMatches(Summoner player, RiotGamesApi api, int matchCount, MatchCollection collection, int maxMatchCount = 1)
        {
            if (matchCount >= maxMatchCount)
            {
                return;
            }
            var matches = api.MatchV5().GetMatchIdsByPUUID(RegionalRoute.AMERICAS, player.Puuid);
            foreach (var matchId in matches)
            {
                Match match = api.MatchV5().GetMatch(RegionalRoute.AMERICAS, matchId);
                collection.AddMatch(match);
                matchCount += 1;
                var players = match.Info.Participants;
            }
        }

        public class MatchCollection
        {
            private Dictionary<string, Match> _matches = new Dictionary<string, Match>();

            public void AddMatch(Match matchToAdd)
            {
                if (_matches.ContainsKey(matchToAdd.Metadata.MatchId))
                {
                    return;
                }

                _matches.Add(matchToAdd.Metadata.MatchId, matchToAdd);
            }

            public string GetMatchesInJson()
            {
                return JsonConvert.SerializeObject(_matches);
            }
        }
    }
}