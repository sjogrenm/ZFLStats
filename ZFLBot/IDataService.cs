namespace ZFLBot;

internal interface IDataService
{
    IDictionary<ulong, TeamInfo> GetAllTeams();

    void AddTeam(ulong discordUserId, TeamInfo teamInfo);

    void RemoveTeam(ulong discordUserId);

    bool TryGetTeam(ulong discordUserId, out TeamInfo teamInfo);

    TeamInfo SpendCAP(ulong discordUserId, int spend, string reason);

    TeamInfo AddBonusCAP(ulong discordUserId, int amount, string reason);

    TeamInfo ResetSpentCAP(ulong discordUserId);

    TeamInfo RolloverTeam(ulong discordUserId, ulong statusMessageId);

    ulong AuditChannel { get; set; }

    ulong AdminRole { get; set; }

    DivisionInfo[] GetAllDivisions();

    ulong GetStatusChannel(int div);

    void SetStatusChannel(int div, ulong channelId);
}

internal class DivisionInfo(int div, ulong statusChannelId)
{
    public int Division => div;

    public ulong StatusChannel => statusChannelId;
}

internal class TeamAction(int delta, string reason)
{
    public int CAPDelta => delta;

    public string Reason => reason;
}

internal class TeamInfo(string teamName, int div, int weeklyAllowance, int carryover, IReadOnlyList<TeamAction> actions, ulong statusMessageId)
{
    public static TeamInfo Create(string teamName, int div, int weeklyAllowance)
    {
        return new TeamInfo(teamName, div, weeklyAllowance, 0, [], 0);
    }

    public string TeamName => teamName;

    public int Division => div;

    public IReadOnlyList<TeamAction> Actions => actions;

    public int CurrentCAP => carryover + weeklyAllowance + actions.Select(a => a.CAPDelta).Sum();

    public int SpentCAP => actions.Where(a => a.CAPDelta < 0).Select(a => -a.CAPDelta).Sum();

    public int CurrentWeeklyCAP => Math.Max(0, weeklyAllowance + actions.Where(a => a.CAPDelta < 0).Select(a => a.CAPDelta).Sum());

    public int CurrentBonusCAP => this.CurrentCAP - this.CurrentWeeklyCAP;

    public int TotalWeeklyAllowance => weeklyAllowance;

    public int Carryover => carryover;

    public ulong StatusMessageId => statusMessageId;

    public TeamInfo WithCAPSpent(int spend, string reason)
    {
        if (spend > this.CurrentCAP)
        {
            throw new ArgumentException("Overspend!");
        }

        return new(teamName, div, weeklyAllowance, carryover, [.. actions, new(-spend, reason)], statusMessageId);
    }

    public TeamInfo WithAddedBonusCAP(int amount, string reason)
    {
        return new(teamName, div, weeklyAllowance, carryover, [.. actions, new(amount, reason)], statusMessageId);
    }

    public TeamInfo WithSpentCAPReset()
    {
        var newActions = actions.Where(a => a.CAPDelta > 0).ToList();
        return new TeamInfo(teamName, div, weeklyAllowance, carryover, newActions, statusMessageId);
    }

    public TeamInfo Rollover(ulong newStatusMessageId)
    {
        // +2, +10, -5, -4
        // -> carryover 2
        // +2, +10, -5, -6
        // -> carryover 1
        var allGainedCAP = carryover + weeklyAllowance + actions.Where(a => a.CAPDelta > 0).Select(a => a.CAPDelta).Sum();
        var lostCAP = Math.Max(this.TotalWeeklyAllowance, this.SpentCAP);
        var newCarryover = allGainedCAP - lostCAP;

        return new(teamName, div, weeklyAllowance, newCarryover, [], newStatusMessageId);
    }
}