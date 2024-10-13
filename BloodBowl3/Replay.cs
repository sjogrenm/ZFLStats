using System.Xml;

namespace BloodBowl3;

public class Replay(FileInfo file, string clientVersion, XmlElement root)
{
    public FileInfo File => file;

    public string ClientVersion => clientVersion;

    public string HomeCoach { get; set; }

    public string VisitingCoach { get; set; }

    public Team HomeTeam { get; set; }

    public Team VisitingTeam { get; set; }

    public string CompetitionName { get; set; }

    public XmlElement ReplayRoot => root;

    public Player GetPlayer(int id)
    {
        if (this.HomeTeam.Players.TryGetValue(id, out var p))
        {
            return p;
        }

        return this.VisitingTeam.Players[id];
    }
}