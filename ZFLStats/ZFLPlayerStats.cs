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

    public int ArmorRollsInflicted { get; set; }

    public int ArmorBreaksInflicted { get; set; }

    public int KnockdownsInflicted { get; set; }

    public int KnockdownsSustained { get; set; }

    public int KOsInflicted { get; set; }

    public int KOsSustained { get; set; }

    public int Catches { get; set; }

    public int Rushes { get; set; }

    public int QuickPasses { get; set; }

    public int ShortPasses { get; set; }

    public int LongPasses { get; set; }

    public int LongBombPasses { get; set; }

    public int BadlyHurtInflicted { get; set; }

    public int SeriouslyHurtInflicted { get; set; }

    public int SeriousInjuryInflicted { get; set; }

    public int LastingInjuryInflicted { get; set; }

    internal void UpdateCasualtyInflicted(CasualtyOutcome casualty)
    {
        switch (casualty)
        {
            case CasualtyOutcome.NoCasualty:
                break;
            case CasualtyOutcome.BadlyHurt:
                this.BadlyHurtInflicted += 1;
                break;
            case CasualtyOutcome.SeriouslyHurt:
                this.SeriouslyHurtInflicted += 1;
                break;
            case CasualtyOutcome.SeriousInjury:
                this.SeriousInjuryInflicted += 1;
                break;
            case CasualtyOutcome.LastingInjury:
            case CasualtyOutcome.SmashedKnee:
            case CasualtyOutcome.HeadInjury:
            case CasualtyOutcome.BrokenArm:
            case CasualtyOutcome.NeckInjury:
            case CasualtyOutcome.DislocatedShoulder:
                this.LastingInjuryInflicted += 1;
                break;
            case CasualtyOutcome.Dead:
                // Already tracked in Kills elsewhere
                break;
        }
    }

    public int BadlyHurtSustained { get; set; }

    public int SeriouslyHurtSustained { get; set; }

    public int SeriousInjurySustained { get; set; }

    public int LastingInjurySustained { get; set; }

    internal void UpdateCasualtySustained(CasualtyOutcome casualty)
    {
        switch (casualty)
        {
            case CasualtyOutcome.NoCasualty:
                break;
            case CasualtyOutcome.BadlyHurt:
                this.BadlyHurtSustained += 1;
                break;
            case CasualtyOutcome.SeriouslyHurt:
                this.SeriouslyHurtSustained += 1;
                break;
            case CasualtyOutcome.SeriousInjury:
                this.SeriousInjurySustained += 1;
                break;
            case CasualtyOutcome.LastingInjury:
            case CasualtyOutcome.SmashedKnee:
            case CasualtyOutcome.HeadInjury:
            case CasualtyOutcome.BrokenArm:
            case CasualtyOutcome.NeckInjury:
            case CasualtyOutcome.DislocatedShoulder:
                this.LastingInjurySustained += 1;
                break;
            case CasualtyOutcome.Dead:
                // Already tracked in Kills elsewhere
                break;
        }
    }

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