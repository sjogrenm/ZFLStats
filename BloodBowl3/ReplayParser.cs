using System.ComponentModel.DataAnnotations.Schema;
using System.IO.Compression;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Xml;

namespace BloodBowl3;

public static partial class ReplayParser
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

    [GeneratedRegex(@"<(MessageData)>([^<]*)<\/MessageData>")]
    private static partial Regex DoubleBase64Regex();

    [GeneratedRegex(@"<(Name|LobbyId|GamerId|CreatorGamerId|MatchId)>([^<]{3,})<\/(Name|LobbyId|GamerId|CreatorGamerId|MatchId)>")]
    private static partial Regex SingleBase64Regex();

    private static XmlDocument LoadDocument(FileInfo path)
    {
        var doc = new XmlDocument();

        var contents = File.ReadAllText(path.FullName);
        var bytes = Convert.FromBase64String(contents);
        using var ms = new MemoryStream();
        ms.Write(bytes);
        ms.Position = 0;
        using var zs = new ZLibStream(ms, CompressionMode.Decompress, true);

        using var reader = new StreamReader(zs);
        var xmlContents = reader.ReadToEnd();
        xmlContents = DoubleBase64Regex().Replace(xmlContents, ReplaceDoubleBase64);
        xmlContents = SingleBase64Regex().Replace(xmlContents, ReplaceSingleBase64);

#if DEBUG
        if (!File.Exists(path.FullName + ".xml"))
        {
            File.WriteAllText(path.FullName + ".xml", xmlContents);
        }
#endif

        doc.LoadXml(xmlContents);
        return doc;

        static string ReplaceDoubleBase64(Match match) => $"<{match.Groups[1].ValueSpan}>{match.Groups[2].Value.FromBase64().FromBase64()}</{match.Groups[1].ValueSpan}>";

        static string ReplaceSingleBase64(Match match) => $"<{match.Groups[1].ValueSpan}>{match.Groups[2].Value.FromBase64()}</{match.Groups[1].ValueSpan}>";
    }

    public static Replay GetReplay(FileInfo file, XmlElement root)
    {
        var replay = new Replay(file, root.SelectSingleNode("ClientVersion").InnerText, root);

        if (root.SelectSingleNode("NotificationGameJoined/GameInfos") is XmlElement gameInfos)
        {
            if (gameInfos.SelectSingleNode("Competition/CompetitionInfos") is XmlElement compInfos)
            {
                replay.CompetitionName = compInfos["Name"]!.InnerText;
            }

            if (gameInfos.SelectSingleNode("GamersInfos") is XmlElement gamersInfos)
            {
                var coaches = gamersInfos.SelectNodes("GamerInfos/Name").Cast<XmlNode>().ToArray();
                replay.HomeCoach = coaches[0].InnerText;
                replay.VisitingCoach = coaches[1].InnerText;

                var teamNameNodes = gamersInfos.SelectNodes("GamerInfos/Roster/Name").Cast<XmlElement>().ToArray();
                replay.HomeTeam = new Team(teamNameNodes[0].InnerText, replay.HomeCoach);
                replay.VisitingTeam = new Team(teamNameNodes[1].InnerText, replay.VisitingCoach);
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
        return new Player(team, playerData["Id"]!.InnerText.ParseInt(), playerData["Name"]!.InnerText, playerData["LobbyId"]?.InnerText);
    }

    private static IEnumerable<string> GetTeamNames(XmlElement doc)
    {
        foreach (var node in doc.SelectNodes("NotificationGameJoined/GameInfos/GamersInfos/GamerInfos/Roster/Name")!.Cast<XmlElement>())
        {
            yield return node.InnerText;
        }
    }

    private static IEnumerable<string> GetCoachNames(XmlElement doc)
    {
        foreach (var node in doc.SelectNodes("NotificationGameJoined/GameInfos/GamersInfos/GamerInfos/Name")!.Cast<XmlElement>())
        {
            yield return node.InnerText;
        }
    }
}