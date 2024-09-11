namespace BloodBowl3;

public class Team
{
    public string Name { get; set; }

    public string Coach { get; set; }

    public Dictionary<int, Player> Players { get; set; } = new();
}