using BloodBowl3;

namespace ZFLStats;

public class ZFLPlayerStats(int id, string name, string? lobbyId, string teamName)
{
    internal int Id => id;

    public string Name => name;

    public string? LobbyId => lobbyId;

    public string Team => teamName;

    public int TouchdownsScored { get; set; }

    public int CasInflicted { get; set; }

    public int CasSustained { get; set; }

    public int PassCompletions { get; set; }

    public int FoulsInflicted { get; set; }

    public int FoulsSustained { get; set; }

    public int SppEarned { get; set; }

    public int Sacks { get; set; }

    public int Kills { get; set; }

    public int SurfsInflicted { get; set; }

    public int SurfsSustained { get; set; }

    public int Expulsions { get; set; }

    public int DodgeTurnovers { get; set; }

    public int DubskullsRolled { get; set; }

    public int ArmorRollsSustained { get; set; }

    public int ArmorBreaksSustained { get; set; }

    public int BlocksInflicted { get; set; }

    public int BlocksSustained { get; set; }

    public int Blitzes { get; set; }

    public int Deaths { get; set; }

    public Dictionary<string, int> AllBlockDice { get; } = new();

    public Dictionary<string, int> ChosenBlockDice { get; } = new();

    public Dictionary<int, int> ArmorAndInjuryDice { get; } = new();

    public Dictionary<int, int> OtherDice { get; } = new();

    internal bool Mvp { get; set; }

    internal Dictionary<RollStatType, Dictionary<int[], int>> Rolls { get; } = new ();

    internal int ExpectedSPP => this.TouchdownsScored * 3 + this.CasInflicted * 2 + this.PassCompletions + (this.Mvp ? 4 : 0);

    public void PrintToConsole(int indent)
    {
        var properties = typeof(ZFLPlayerStats).GetProperties().Where(p => p.PropertyType == typeof(int));
        properties.ForEach(p => Print(indent, p.Name, (int)p.GetValue(this)));
    }

    private static void Print(int indent, string text, int value)
    {
        if (value <= 0)
            return;
        Console.Write(new string(' ', indent));
        Console.WriteLine($"{text}: {value}");
    }
}