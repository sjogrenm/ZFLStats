using System.Xml;

namespace BloodBowl3;

public class Replay
{
    public FileInfo File { get; set; }

    public string ClientVersion { get; set; }

    public string HomeCoach { get; set; }

    public string VisitingCoach { get; set; }

    public Team HomeTeam { get; set; }

    public Team VisitingTeam { get; set; }

    public string CompetitionName { get; set; }

    public XmlElement ReplayRoot { get; set; }

    public Player GetPlayer(int id)
    {
        if (this.HomeTeam.Players.TryGetValue(id, out var p))
        {
            return p;
        }

        return this.VisitingTeam.Players[id];
    }
}