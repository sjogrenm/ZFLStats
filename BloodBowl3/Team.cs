namespace BloodBowl3;

public class Team(string name, string coach)
{
    public string Name => name;

    public string Coach => coach;

    public Dictionary<int, Player> Players { get; set; } = new();
}