﻿namespace ZFLBot;

using System.Diagnostics;

internal interface IDataService
{
    IDictionary<ulong, TeamInfo> GetAllTeams();

    void AddTeam(ulong discordUserId, TeamInfo teamInfo);

    void RemoveTeam(ulong discordUserId);

    bool TryGetTeam(ulong discordUserId, out TeamInfo teamInfo);

    TeamInfo SpendCAP(ulong discordUserId, int spend, string reason);

    TeamInfo AddBonusCAP(ulong discordUserId, int amount, string reason);

    TeamInfo GridironInvestment(ulong discordUserId, int spend);

    TeamInfo UseGridironCAP(ulong discordUserId, int amount);

    TeamInfo ResetSpentCAP(ulong discordUserId);

    TeamInfo RolloverTeam(ulong discordUserId);

    TeamInfo UpdateStatusMessage(ulong discordUserId, ulong statusMessageId);

    ulong AuditChannel { get; set; }

    ulong AdminRole { get; set; }

    DivisionInfo[] GetAllDivisions();

    ulong GetStatusChannel(int div);

    void SetStatusChannel(int div, ulong channelId);
    
    Demand AddDemand(ulong discordUserId, string title, string description, string deadline, string source);

    Demand EditDemand(ulong discordUserId, string demandId, string title, string description, string deadline, string source, string progress);

    void RemoveDemand(ulong discordUserId, string demandId);

    Demand CloseDemand(ulong discordUserId, string demandId, bool wasSuccessful);

    Demand OpenDemand(ulong discordUserId, string demandId);

    Demand[] GetDemands(ulong discordUserId);
    
    void SetNoteText(ulong discordUserId, string text);
}

internal class DivisionInfo(int div, ulong statusChannelId)
{
    public int Division => div;

    public ulong StatusChannel => statusChannelId;
}

internal enum ActionType
{
    CAPSpend,

    BonusCAP,

    GridironInvestment
}

internal class TeamAction(ActionType type, int delta, string reason)
{
    public ActionType Type => type;

    public int CAPDelta => delta;

    public string Reason => reason;
}

internal class Demand(
    string id,
    string title,
    string description,
    string deadline,
    string source,
    string progress,
    bool active,
    bool success)
{
  public static Demand Create(string id, string title, string description, string deadline, string source, string progress, bool active, bool success)
  {
    return new Demand(id, title, description, deadline, source, progress, active, success);
  }

  public string Id => id;

  public string Title => title;

  public string Description => description;

  public string Deadline => deadline;

  public string Progress => progress;

  public string Source => source;

  public bool IsActive => active;

  public bool WasSuccessful => success;
}

internal class Tracker(string key, string value){
  public string Key => key;
  public string Value => value;
}

internal class TeamInfo(string teamName, int div, int weeklyAllowance, int carryover, int gridironInvestment, IReadOnlyList<TeamAction> actions, ulong statusMessageId, List<Demand> demands, string noteText)
{
    public static TeamInfo Create(string teamName, int div, int weeklyAllowance)
    {
        return new TeamInfo(teamName, div, weeklyAllowance, 0, 0, [], 0, [], "");
    }

    public string Id => teamName.Replace(' ', '_').Trim().ToLower();

    public List<Demand> Demands => demands;

    public string TeamName => teamName;

    public int Division => div;

    public IReadOnlyList<TeamAction> Actions => actions;

    public int CurrentCAP => carryover + weeklyAllowance + actions.Select(a => a.CAPDelta).Sum();

    public int SpentCAP => actions.Where(a => a.CAPDelta < 0).Select(a => -a.CAPDelta).Sum();

    public int CurrentWeeklyCAP => Math.Max(0, weeklyAllowance + actions.Where(a => a.Type != ActionType.BonusCAP).Select(a => a.CAPDelta).Sum());

    public int CurrentBonusCAP => this.CurrentCAP - this.CurrentWeeklyCAP;

    public int TotalWeeklyAllowance => weeklyAllowance;

    public int Carryover => carryover;

    public int GridironInvestment => gridironInvestment;

    public ulong StatusMessageId => statusMessageId;

    public string NoteText => noteText;

    public TeamInfo UpdateNote(string note)
    {
      return new(teamName, div, weeklyAllowance, carryover, gridironInvestment, actions, statusMessageId, demands, note);
    }

    public TeamInfo WithCAPSpent(int spend, string reason)
    {
        if (spend > this.CurrentCAP)
        {
            throw new ArgumentException("Overspend!");
        }

        return new(teamName, div, weeklyAllowance, carryover, gridironInvestment, [.. actions, new(ActionType.CAPSpend, -spend, reason)], statusMessageId, demands, noteText);
    }

    public TeamInfo WithAddedBonusCAP(int amount, string reason)
    {
        return new(teamName, div, weeklyAllowance, carryover, gridironInvestment, [.. actions, new(ActionType.BonusCAP, amount, reason)], statusMessageId, demands, noteText);
    }

    public TeamInfo WithSpentCAPReset()
    {
        var newActions = actions.Where(a => a.Type == ActionType.BonusCAP).ToList();
        var investmentThisRound = actions.Where(a => a.Type == ActionType.GridironInvestment).Select(a => -a.CAPDelta).Sum();
        Debug.Assert(gridironInvestment >= investmentThisRound);
        return new TeamInfo(teamName, div, weeklyAllowance, carryover, gridironInvestment - investmentThisRound, newActions, statusMessageId, demands, noteText);
    }

    public TeamInfo WithGridironInvestment(int spend)
    {
        if (spend > this.CurrentCAP)
        {
            throw new ArgumentException("Overspend!");
        }

        return new(teamName, div, weeklyAllowance, carryover, gridironInvestment + spend, [.. actions, new(ActionType.GridironInvestment, -spend, "Gridiron Investment")], statusMessageId, demands, noteText);
    }

    public TeamInfo WithGridironCAPSpent(int amount)
    {
        if (amount > gridironInvestment)
        {
            throw new ArgumentException("Overspend!");
        }

        return new(teamName, div, weeklyAllowance, carryover, gridironInvestment - amount, actions, statusMessageId, demands, noteText);
    }

    public TeamInfo WithNewStatusMessage(ulong messageId)
    {
        return new(teamName, div, weeklyAllowance, carryover, gridironInvestment, actions, messageId, demands, noteText);
    }

    public TeamInfo Rollover()
    {
        // +2, +10, -5, -4
        // -> carryover 2
        // +2, +10, -5, -6
        // -> carryover 1
        var allGainedCAP = carryover + weeklyAllowance + actions.Where(a => a.CAPDelta > 0).Select(a => a.CAPDelta).Sum();
        var lostCAP = Math.Max(this.TotalWeeklyAllowance, this.SpentCAP);
        var newCarryover = allGainedCAP - lostCAP;

        return new(teamName, div, weeklyAllowance, newCarryover, gridironInvestment, [], statusMessageId, demands, noteText);
    }
}
