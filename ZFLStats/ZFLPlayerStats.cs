namespace ZFLStats;

public class ZFLPlayerStats(string name, string? lobbyId)
{
    public string Name => name;

    public string? LobbyId => lobbyId;

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

    internal bool Mvp { get; set; }

    internal int ExpectedSPP => this.TouchdownsScored * 3 + this.CasInflicted * 2 + this.PassCompletions + (this.Mvp ? 4 : 0);

    public void PrintToConsole(int indent)
    {
        Print(indent, nameof(this.TouchdownsScored), this.TouchdownsScored);
        Print(indent, nameof(this.CasInflicted), this.CasInflicted);
        Print(indent, nameof(this.CasSustained), this.CasSustained);
        Print(indent, nameof(this.PassCompletions), this.PassCompletions);
        Print(indent, nameof(this.FoulsInflicted), this.FoulsInflicted);
        Print(indent, nameof(this.FoulsSustained), this.FoulsSustained);
        Print(indent, nameof(this.SppEarned), this.SppEarned);
        Print(indent, nameof(this.Sacks), this.Sacks);
        Print(indent, nameof(this.Kills), this.Kills);
        Print(indent, nameof(this.SurfsInflicted), this.SurfsInflicted);
        Print(indent, nameof(this.SurfsSustained), this.SurfsSustained);
        Print(indent, nameof(this.Expulsions), this.Expulsions);
        Print(indent, nameof(this.DodgeTurnovers), this.DodgeTurnovers);
        Print(indent, nameof(this.DubskullsRolled), this.DubskullsRolled);
        Print(indent, nameof(this.ArmorRollsSustained), this.ArmorRollsSustained);
    }

    private static void Print(int indent, string text, int value)
    {
        if (value <= 0)
            return;
        Console.Write(new string(' ', indent));
        Console.WriteLine($"{text}: {value}");
    }
}