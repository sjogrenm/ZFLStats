using System.ComponentModel.DataAnnotations.Schema;
using System.IO.Compression;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Xml;

namespace BloodBowl3;

public static class ReplayParser
{
    public static async IAsyncEnumerable<Replay> GetReplays(FileSystemInfo fileOrDir, string? coachFilter = null, string? teamFilter = null)
    {
        var replayTasks = new HashSet<Task<Replay?>>();

        if (fileOrDir is DirectoryInfo dir)
        {
            Regex? coachPattern = null, teamPattern = null;
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
                var task = LoadDocumentAsync(path).ContinueWith(t =>
                {
                    var doc = t.Result;
                    if (coachPattern != null && !GetCoachNames(doc.DocumentElement!).Any(coachPattern.IsMatch))
                        return null;
                    if (teamPattern != null && !GetTeamNames(doc.DocumentElement!).Any(teamPattern.IsMatch))
                        return null;
                    return GetReplay(path, doc.DocumentElement!);
                });

                replayTasks.Add(task);
            }
        }
        else
        {
            var task = LoadDocumentAsync((FileInfo)fileOrDir).ContinueWith(Replay? (t) => GetReplay((FileInfo)fileOrDir, t.Result.DocumentElement!));
            replayTasks.Add(task);
        }

        while (replayTasks.Count > 0)
        {
            var task = await Task.WhenAny(replayTasks);
            replayTasks.Remove(task);
            var replay = await task;
            if (replay != null)
            {
                yield return replay;
            }
        }
    }

    public static Task<Replay> GetReplayAsync(FileInfo file, XmlElement root)
    {
        return Task.Run(() => GetReplay(file, root));
    }

    private static Task<XmlDocument> LoadDocumentAsync(FileInfo path) => Task.Run(() => LoadDocument(path));

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

#if DEBUG
        if (!File.Exists(path.FullName + ".xml"))
        {
            using var fs = File.Create(path.FullName + ".xml");
            zs.CopyTo(fs);
            zs.Close();
            ms.Position = 0;
            zs = new ZLibStream(ms, CompressionMode.Decompress);
        }
#endif

        doc.Load(zs);
        zs.Close();
        return doc;
    }

    public static Replay GetReplay(FileInfo file, XmlElement root)
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

        var results = root.SelectNodes("EndGame/RulesEventGameFinished/MatchResult/GamerResults/GamerResult")!.Cast<XmlElement>().ToArray();
        GetPlayers(0, results[0]).ForEach(replay.HomeTeam.AddPlayer);
        GetPlayers(1, results[1]).ForEach(replay.VisitingTeam.AddPlayer);

        return replay;
    }

    private static IEnumerable<Player> GetPlayers(int team, XmlElement gamerResult)
    {
        foreach (var playerResult in gamerResult.SelectNodes("TeamResult/PlayerResults/PlayerResult")!.Cast<XmlElement>())
        {
            var p = GetPlayer(team, playerResult["PlayerData"]!);
            if (playerResult["SppGained"] is { } sppElem)
            {
                p.SppGained = sppElem.InnerText.ParseInt();
            }
            if (playerResult["Mvp"] != null)
            {
                p.Mvp = true;
            }
            yield return p;
        }
    }

    private static Player GetPlayer(int team, XmlElement playerData)
    {
        return new Player(team, playerData["Id"]!.InnerText.ParseInt(), playerData["Name"]!.InnerText.FromBase64(), playerData["LobbyId"]?.InnerText.FromBase64());
    }

    private static IEnumerable<string> GetTeamNames(XmlElement doc)
    {
        foreach (var node in doc.SelectNodes("NotificationGameJoined/GameInfos/GamersInfos/GamerInfos/Roster/Name")!.Cast<XmlElement>())
        {
            yield return node.InnerText.FromBase64();
        }
    }

    private static IEnumerable<string> GetCoachNames(XmlElement doc)
    {
        foreach (var node in doc.SelectNodes("NotificationGameJoined/GameInfos/GamersInfos/GamerInfos/Name")!.Cast<XmlElement>())
        {
            yield return node.InnerText.FromBase64();
        }
    }
}