using System.ComponentModel.DataAnnotations.Schema;
using System.IO.Compression;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Xml;

namespace BloodBowl3;

public static class ReplayParser
{
    public static IList<Replay> GetReplays(FileSystemInfo fileOrDir, string? coachFilter = null, string? teamFilter = null)
    {
        if (fileOrDir is DirectoryInfo dir)
        {
            var replayTasks = new List<Task<Replay>>();

            Regex coachPattern = null, teamPattern = null;
            if (!string.IsNullOrEmpty(coachFilter))
            {
                coachPattern = new Regex(coachFilter, RegexOptions.IgnoreCase);
            }

            if (!string.IsNullOrEmpty(teamFilter))
            {
                teamPattern = new Regex(teamFilter, RegexOptions.IgnoreCase);
            }

            foreach (var path in dir.EnumerateFiles("*.bbr"))
            {
                var doc = LoadDocument(path);
                var coaches = GetCoachNames(doc.DocumentElement).ToArray();
                var teamNames = GetTeamNames(doc.DocumentElement).ToArray();
                var teamMatches = teamPattern == null || teamNames.Any(teamPattern.IsMatch);
                var coachMatches = coachPattern == null || coaches.Any(coachPattern.IsMatch);
                if (teamMatches && coachMatches)
                {
                    replayTasks.Add(GetReplayAsync(path, doc.DocumentElement));
                }
            }

            Task.WaitAll(replayTasks.ToArray<Task>());
            return replayTasks.Select(t => t.Result).ToList();
        }

        {
            var doc = LoadDocument((FileInfo)fileOrDir);
            return new[] { GetReplayAsync((FileInfo)fileOrDir, doc.DocumentElement).Result };
        }
    }

    public static Task<Replay> GetReplayAsync(FileInfo file, XmlElement root)
    {
        return Task.Run(() => GetReplayImpl(file, root));
    }

    private static XmlDocument LoadDocument(FileInfo path)
    {
        var doc = new XmlDocument();

        // TODO base64 decode lazily
        var contents = File.ReadAllText(path.FullName);
        var bytes = Convert.FromBase64String(contents);
        using var ms = new MemoryStream();
        ms.Write(bytes);
        ms.Position = 0;
        var zs = new ZLibStream(ms, CompressionMode.Decompress, true);

//#if DEBUG
//        if (!File.Exists(path.FullName + ".xml"))
//        {
//            using var fs = File.Create(path.FullName + ".xml");
//            zs.CopyTo(fs);
//            zs.Close();
//            ms.Position = 0;
//            zs = new ZLibStream(ms, CompressionMode.Decompress);
//        }
//#endif

        doc.Load(zs);
        zs.Close();
        return doc;
    }

    private static Replay GetReplayImpl(FileInfo file, XmlElement root)
    {
        var replay = new Replay(file, root.SelectSingleNode("ClientVersion").InnerText, root);

        if (root.SelectSingleNode("NotificationGameJoined/GameInfos") is XmlElement gameInfos)
        {
            if (gameInfos.SelectSingleNode("Competition/CompetitionInfos") is XmlElement compInfos)
            {
                replay.CompetitionName = compInfos["Name"].InnerText.FromBase64();
            }

            if (gameInfos.SelectSingleNode("GamersInfos") is XmlElement gamersInfos)
            {
                var coaches = gamersInfos.SelectNodes("GamerInfos/Name").Cast<XmlNode>().ToArray();
                replay.HomeCoach = coaches[0].InnerText.FromBase64();
                replay.VisitingCoach = coaches[1].InnerText.FromBase64();

                var teamNameNodes = gamersInfos.SelectNodes("GamerInfos/Roster/Name").Cast<XmlElement>().ToArray();
                replay.HomeTeam = new Team(teamNameNodes[0].InnerText.FromBase64(), replay.HomeCoach);
                replay.VisitingTeam = new Team(teamNameNodes[1].InnerText.FromBase64(), replay.VisitingCoach);
            }
        }

        foreach (var listTeams in root.SelectNodes("ReplayStep/BoardState/ListTeams").Cast<XmlElement>())
        {
            var teamPlayerLists = listTeams.SelectNodes("TeamState/ListPitchPlayers").Cast<XmlElement>().ToArray();

            foreach (var playerData in teamPlayerLists[0].SelectNodes("PlayerState/Data").Cast<XmlElement>())
            {
                var player = GetPlayer(0, playerData);
                replay.HomeTeam.Players.TryAdd(player.Id, player);
            }

            foreach (var playerData in teamPlayerLists[1].SelectNodes("PlayerState/Data").Cast<XmlElement>())
            {
                var player = GetPlayer(1, playerData);
                replay.VisitingTeam.Players.TryAdd(player.Id, player);
            }
        }

        return replay;
    }

    private static Player GetPlayer(int team, XmlElement playerData)
    {
        return new Player(team, playerData["Id"].InnerText.ParseInt(), playerData["Name"].InnerText.FromBase64(), playerData["LobbyId"]?.InnerText.FromBase64());
    }

    private static IEnumerable<string> GetTeamNames(XmlElement doc)
    {
        foreach (var node in doc.SelectNodes("NotificationGameJoined/GameInfos/GamersInfos/GamerInfos/Roster/Name").Cast<XmlElement>())
        {
            yield return node.InnerText.FromBase64();
        }
    }

    private static IEnumerable<string> GetCoachNames(XmlElement doc)
    {
        foreach (var node in doc.SelectNodes("NotificationGameJoined/GameInfos/GamersInfos/GamerInfos/Name").Cast<XmlElement>())
        {
            yield return node.InnerText.FromBase64();
        }
    }
}