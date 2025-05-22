using System.Text.Json.Serialization;

namespace ZFLStats;

public class ZFLTeamStats(int id)
{
    internal int Id => id;

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("fans")]
    public int Fans { get; set; }

    [JsonPropertyName("players")]
    public List<ZFLPlayerStats> Players { get; set; }

    public List<int> BribeRolls { get; } = new();

    public List<int> ArgueTheCallRolls { get; } = new();

    public Dictionary<string, int> AllBlockDice { get; } = new();

    public Dictionary<string, int> ChosenBlockDice { get; } = new();

    public Dictionary<int, int> ArmorAndInjuryDice { get; } = new();

    public Dictionary<int, int> OtherDice { get; } = new();

    public List<int> TurnsPerTouchdown {  get; } = new();
}
