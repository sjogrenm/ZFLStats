namespace ZFLStats;

using System.CommandLine;
using System.Text.Json;
using BloodBowl3;

public class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

    private static async Task RunAsync(FileSystemInfo fileOrDir, string? coachFilter, string? teamFilter, FileInfo? outputFile, bool autoOut, string? format)
    {
        await using var outFile = outputFile?.CreateText();

        await foreach (var replay in ReplayParser.GetReplays(fileOrDir, coachFilter, teamFilter))
        {
            var analyzer = new ZFLStatsAnalyzer(replay);
            await analyzer.AnalyzeAsync();

            await using var autoOutFile = autoOut ? File.CreateText(Path.ChangeExtension(replay.File.FullName, format)) : null;
            
            void WriteToCsv(string text)
            {
                if (format != "csv")
                    return;
                outFile?.WriteLine(text);
                autoOutFile?.WriteLine(text);
            }

            void WriteJson(object value)
            {
                if (format != "json")
                    return;
                var text = JsonSerializer.Serialize(value, options: JsonOptions);
                outFile?.WriteLine(text);
                autoOutFile?.WriteLine(text);
            }

            var properties = typeof(ZFLPlayerStats).GetProperties();

            WriteToCsv($"{replay.HomeTeam.Name} vs {replay.VisitingTeam.Name}");
            WriteToCsv($" Fan attendance home: {analyzer.HomeTeamStats.Fans}");
            WriteToCsv($" Fan attendance away: {analyzer.VisitingTeamStats.Fans}");
            WriteToCsv(string.Join(';', properties.Select(p => p.Name)));

            Console.WriteLine($"{replay.HomeTeam.Name} vs {replay.VisitingTeam.Name}");
            Console.WriteLine($" Fan attendance: {analyzer.HomeTeamStats.Fans} / {analyzer.VisitingTeamStats.Fans}");
            foreach (var playerStats in analyzer.HomeTeamStats.Players.Concat(analyzer.VisitingTeamStats.Players))
            {
                Console.WriteLine($"  {playerStats.Name} (id={playerStats.Id}):");
                playerStats.PrintToConsole(4);
                if (playerStats.ExpectedSPP != playerStats.SppEarned)
                {
                    Console.WriteLine($"      !!! Expected {playerStats.ExpectedSPP}spp but found {playerStats.SppEarned}. Bug or prayer to Nuffle?");
                }

                WriteToCsv(string.Join(';', properties.Select(p => p.GetValue(playerStats))));
            }

            WriteJson(new
            {
                id = replay.File.Name.Replace(".bbr", string.Empty),
                home = analyzer.HomeTeamStats,
                away = analyzer.VisitingTeamStats
            });
        }
    }

    private static void Run(FileSystemInfo fileOrDir, string? coachFilter, string? teamFilter, FileInfo? outputFile, bool autoOut, bool silent, string? format)
    {
        if (silent)
        {
            Console.SetOut(TextWriter.Null);
        }

        format ??= "csv";
        RunAsync(fileOrDir, coachFilter, teamFilter, outputFile, autoOut, format).Wait();
    }

    public static int Main(string[] args)
    {
        var fileOrDirArg = new Argument<FileSystemInfo>("file or directory").ExistingOnly();
        var coachOpt = new Option<string>(["--coach", "-c"]);
        var teamOpt = new Option<string>(["--team", "-t"]);
        var outputOpt = new Option<FileInfo>(["--output", "-o"]);
        var autoOpt = new Option<bool>("--auto");
        var silentOpt = new Option<bool>("--silent");
        var formatOpt = new Option<string>(["--format", "-f"]).FromAmong("csv", "json");

        var rootCommand = new RootCommand
        {
            fileOrDirArg,
            coachOpt,
            teamOpt,
            outputOpt,
            autoOpt,
            silentOpt,
            formatOpt,
        };

        rootCommand.SetHandler(Run, fileOrDirArg, coachOpt, teamOpt, outputOpt, autoOpt, silentOpt, formatOpt);

        return rootCommand.Invoke(args);
    }
}