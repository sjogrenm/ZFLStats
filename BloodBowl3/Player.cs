using System.Diagnostics;

namespace BloodBowl3;

[DebuggerDisplay("Player({Id}, {Name})")]
public class Player : IComparable<Player>
{
    public Player(int team, int id, string name)
    {
        this.Team = team;
        this.Id = id;
        this.Name = name;
    }

    public int Team { get; }

    public int Id { get; }

    public string Name { get; }

    public int FirstXP = -1;

    public int LastXP = -1;

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