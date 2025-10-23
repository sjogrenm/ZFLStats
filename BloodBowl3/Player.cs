using System.Diagnostics;

namespace BloodBowl3;

[DebuggerDisplay("Player({Id}, {Name})")]
public class Player(int team, int id, string name, string? lobbyId) : IComparable<Player>
{
    private static readonly Dictionary<string, string> StarPlayerNames = new()
        { ["name_sp_cindy"] = "Cindy Piewhistle" };

    public int Team => team;

    public int Id => id;

    public string Name => StarPlayerNames.GetValueOrDefault(name, name);

    public string? LobbyId => lobbyId;

    public int SppGained = 0;

    public bool Mvp;

    public int CompareTo(Player other)
    {
        return this.Id.CompareTo(other.Id);
    }

    public override bool Equals(object obj)
    {
        if (obj is Player other)
        {
            return this.CompareTo(other) == 0;
        }

        return false;
    }

    public override int GetHashCode()
    {
        return this.Id.GetHashCode();
    }
}