using Newtonsoft.Json;

namespace ZFLBot;

internal class JsonDataService : IDataService, IDisposable
{
    private static readonly JsonSerializerSettings JsonSettings = new() { Formatting = Formatting.Indented };

    private readonly object lck = new();

    private readonly Timer flushTimer;

    private readonly FileStream db;

    private readonly Dictionary<ulong, TeamInfo> teams = new();

    private readonly Dictionary<int, DivisionInfo> divs = new();

    private ulong auditChannelId;

    private ulong adminRoleId;

    private bool flushRequested;

    public JsonDataService(string fileName)
    {
        this.flushTimer = new Timer(this.FlushWorker, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        this.db = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        using var reader = new StreamReader(this.db, leaveOpen: true);
        var serialized = JsonSerializer.CreateDefault().Deserialize(reader, typeof(SerializedData));
        if (serialized is SerializedData data)
        {
            this.auditChannelId = data.AuditChannelId;
            this.adminRoleId = data.AdminRoleId;
            foreach (var team in data.Teams)
            {
                this.teams.Add(team.DiscordUserId, team.Team.ToTeamInfo());
            }

            foreach (var div in data.Divisions)
            {
                this.divs.Add(div.Division, new DivisionInfo(div.Division, div.StatusChannelId));
            }
        }
    }

    public void Dispose()
    {
        this.flushTimer.Dispose();
        this.FlushWorker(null);
        this.db.Dispose();
    }

    /// <inheritdoc />
    public IDictionary<ulong, TeamInfo> GetAllTeams()
    {
        lock (this.lck)
        {
            return new Dictionary<ulong, TeamInfo>(this.teams);
        }
    }

    /// <inheritdoc />
    public void AddTeam(ulong discordUserId, TeamInfo teamInfo)
    {
        lock (this.lck)
        {
            if (this.teams.TryAdd(discordUserId, teamInfo))
            {
                this.flushRequested = true;
            }
        }
    }

    /// <inheritdoc />
    public Demand AddDemand(ulong discordUserId, string title, string description, string deadline, string source)
    {
        lock (this.lck)
        {
            if (!this.teams.TryGetValue(discordUserId, out TeamInfo teamInfo))
            {
                return null;
            }

            string id = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            Demand demand = Demand.Create(id, title, description, deadline, source, "", false, false);
            teamInfo.Demands.Add(demand);
            this.flushRequested = true;
            return demand;
        }
    }

    /// <inheritdoc />
    public Demand EditDemand(ulong discordUserId, string demandId, string title, string description, string deadline, string source, string progress)
    {
        lock (this.lck)
        {
            if (!this.teams.TryGetValue(discordUserId, out TeamInfo teamInfo))
            {
                return null;
            }

            Demand oldDemand = teamInfo.Demands.Where(d => d.Id.Equals(demandId)).First();
            Demand demand = Demand.Create(oldDemand.Id, title, description, deadline, source, progress, oldDemand.IsActive, oldDemand.WasSuccessful);
            teamInfo.Demands.RemoveAt(teamInfo.Demands.IndexOf(oldDemand));
            teamInfo.Demands.Add(demand);
            this.flushRequested = true;
            return demand;
        }
    }

    /// <inheritdoc />
    public void RemoveDemand(ulong discordUserId, string demandId)
    {
        lock (this.lck)
        {
            if (this.teams.TryGetValue(discordUserId, out TeamInfo teamInfo))
            {
                Demand oldDemand = teamInfo.Demands.Where(d => d.Id.Equals(demandId)).First();
                teamInfo.Demands.RemoveAt(teamInfo.Demands.IndexOf(oldDemand));
                this.flushRequested = true;
            }
        }
    }

    /// <inheritdoc />
    public Demand CloseDemand(ulong discordUserId, string demandId, bool wasSuccessful)
    {
        lock (this.lck)
        {
            if (!this.teams.TryGetValue(discordUserId, out TeamInfo teamInfo))
            {
                return null;
            }

            Demand oldDemand = teamInfo.Demands.Where(d => d.Id.Equals(demandId)).First();
            Demand demand = Demand.Create(oldDemand.Id, oldDemand.Title, oldDemand.Description, oldDemand.Deadline, oldDemand.Source, oldDemand.Progress, false, wasSuccessful);
            teamInfo.Demands.RemoveAt(teamInfo.Demands.IndexOf(oldDemand));
            teamInfo.Demands.Add(demand);
            this.flushRequested = true;
            return demand;
        }
    }

    /// <inheritdoc />
    public Demand OpenDemand(ulong discordUserId, string demandId)
    {
        lock (this.lck)
        {
            if (!this.teams.TryGetValue(discordUserId, out TeamInfo teamInfo))
            {
                return null;
            }

            Demand oldDemand = teamInfo.Demands.Where(d => d.Id.Equals(demandId)).First();
            Demand demand = Demand.Create(oldDemand.Id, oldDemand.Title, oldDemand.Description, oldDemand.Deadline, oldDemand.Source, oldDemand.Progress, true, oldDemand.WasSuccessful);
            teamInfo.Demands.RemoveAt(teamInfo.Demands.IndexOf(oldDemand));
            teamInfo.Demands.Add(demand);
            this.flushRequested = true;
            return demand;
        }
    }

    /// <inheritdoc />
    public Demand[] GetDemands(ulong discordUserId)
    {
        lock (this.lck)
        {
            if (!this.teams.TryGetValue(discordUserId, out TeamInfo teamInfo))
            {
                return null;
            }

            return teamInfo.Demands.ToArray();
        }
    }

    /// <inheritdoc />
    public void RemoveTeam(ulong discordUserId)
    {
        lock (this.lck)
        {
            if (this.teams.Remove(discordUserId))
            {
                this.flushRequested = true;
            }
        }
    }

    /// <inheritdoc />
    public bool TryGetTeam(ulong discordUserId, out TeamInfo teamInfo)
    {
        lock (this.lck)
        {
            return this.teams.TryGetValue(discordUserId, out teamInfo);
        }
    }

    /// <inheritdoc />
    public TeamInfo SpendCAP(ulong discordUserId, int spend, string reason)
    {
        TeamInfo teamInfo;
        lock (this.lck)
        {
            if (!this.teams.TryGetValue(discordUserId, out teamInfo))
            {
                return null;
            }

            teamInfo = teamInfo.WithCAPSpent(spend, reason);
            this.teams[discordUserId] = teamInfo;
            this.flushRequested = true;
        }

        return teamInfo;
    }

    /// <inheritdoc />
    public TeamInfo AddBonusCAP(ulong discordUserId, int amount, string reason)
    {
        TeamInfo teamInfo;
        lock (this.lck)
        {
            if (!this.teams.TryGetValue(discordUserId, out teamInfo))
            {
                return null;
            }

            teamInfo = teamInfo.WithAddedBonusCAP(amount, reason);
            this.teams[discordUserId] = teamInfo;
            this.flushRequested = true;
        }

        return teamInfo;
    }

    /// <inheritdoc />
    public TeamInfo GridironInvestment(ulong discordUserId, int spend)
    {
        TeamInfo teamInfo;
        lock (this.lck)
        {
            if (!this.teams.TryGetValue(discordUserId, out teamInfo))
            {
                return null;
            }

            teamInfo = teamInfo.WithGridironInvestment(spend);
            this.teams[discordUserId] = teamInfo;
            this.flushRequested = true;
        }

        return teamInfo;
    }

    /// <inheritdoc />
    public TeamInfo UseGridironCAP(ulong discordUserId, int amount)
    {
        TeamInfo teamInfo;
        lock (this.lck)
        {
            if (!this.teams.TryGetValue(discordUserId, out teamInfo))
            {
                return null;
            }

            teamInfo = teamInfo.WithGridironCAPSpent(amount);
            this.teams[discordUserId] = teamInfo;
            this.flushRequested = true;
        }

        return teamInfo;
    }

    /// <inheritdoc />
    public TeamInfo ResetSpentCAP(ulong discordUserId)
    {
        TeamInfo teamInfo;
        lock (this.lck)
        {
            if (!this.teams.TryGetValue(discordUserId, out teamInfo))
            {
                return null;
            }

            teamInfo = teamInfo.WithSpentCAPReset();
            this.teams[discordUserId] = teamInfo;
            this.flushRequested = true;
        }

        return teamInfo;
    }

    /// <inheritdoc />
    public TeamInfo RolloverTeam(ulong discordUserId)
    {
        TeamInfo teamInfo;
        lock (this.lck)
        {
            if (!this.teams.TryGetValue(discordUserId, out teamInfo))
            {
                return null;
            }

            teamInfo = teamInfo.Rollover();
            this.teams[discordUserId] = teamInfo;
            this.flushRequested = true;
        }

        return teamInfo;
    }

    /// <inheritdoc />
    public TeamInfo UpdateStatusMessage(ulong discordUserId, ulong statusMessageId)
    {
        TeamInfo teamInfo;
        lock (this.lck)
        {
            if (!this.teams.TryGetValue(discordUserId, out teamInfo))
            {
                return null;
            }

            teamInfo = teamInfo.WithNewStatusMessage(statusMessageId);
            this.teams[discordUserId] = teamInfo;
            this.flushRequested = true;
        }

        return teamInfo;
    }

    /// <inheritdoc />
    public ulong AuditChannel
    {
        get
        {
            lock (this.lck)
            {
                return this.auditChannelId;
            }
        }
        set
        {
            lock (this.lck)
            {
                this.auditChannelId = value;
                this.flushRequested = true;
            }
        }
    }

    /// <inheritdoc />
    public ulong AdminRole
    {
        get
        {
            lock (this.lck)
            {
                return this.adminRoleId;
            }
        }
        set
        {
            lock (this.lck)
            {
                this.adminRoleId = value;
                this.flushRequested = true;
            }
        }
    }

    /// <inheritdoc />
    public DivisionInfo[] GetAllDivisions()
    {
        lock (this.lck)
        {
            return this.divs.Values.ToArray();
        }
    }

    /// <inheritdoc />
    public ulong GetStatusChannel(int div)
    {
        lock (this.lck)
        {
            if (this.divs.TryGetValue(div, out var divInfo))
            {
                return divInfo.StatusChannel;
            }
        }

        return 0;
    }

    /// <inheritdoc />
    public void SetStatusChannel(int div, ulong channelId)
    {
        if (div < 1 || div > 3)
        {
            throw new ArgumentException();
        }

        lock (this.lck)
        {
            this.divs[div] = new DivisionInfo(div, channelId);
            this.flushRequested = true;
        }
    }

    public void SetNoteText(ulong discordUserId, string text) 
    {
        TeamInfo teamInfo;
        lock (this.lck)
        {
            if (this.teams.TryGetValue(discordUserId, out teamInfo))
            {
              teamInfo = teamInfo.UpdateNote(text);
              this.teams[discordUserId] = teamInfo;
              this.flushRequested = true;
            }
        }
    }

    private void FlushWorker(object? dummy)
    {
        lock (this.lck)
        {
            if (!this.flushRequested)
            {
                return;
            }

            this.db.Position = 0;
            using var writer = new StreamWriter(this.db, leaveOpen: true);
            JsonSerializer.CreateDefault(JsonSettings).Serialize(
                writer,
                new SerializedData
                {
                    AuditChannelId = this.auditChannelId,
                    AdminRoleId = this.adminRoleId,
                    Divisions = this.divs.Values.Select(div => new SerializedDivision { Division = div.Division, StatusChannelId = div.StatusChannel }).ToArray(),
                    Teams = this.teams.Select(kvp => new SerializedUserAndTeamInfo { DiscordUserId = kvp.Key, Team = SerializedTeamInfo.FromTeamInfo(kvp.Value) }).ToArray()
                });
            writer.Flush();
            this.db.SetLength(this.db.Position);
            this.db.Flush();

            this.flushRequested = false;
        }
    }

    private class SerializedData
    {
        public ulong AuditChannelId;

        public ulong AdminRoleId;

        public SerializedUserAndTeamInfo[] Teams;

        public SerializedDivision[] Divisions;
    }

    private class SerializedDivision
    {
        public int Division;

        public ulong StatusChannelId;
    }

    private class SerializedUserAndTeamInfo
    {
        public ulong DiscordUserId;

        public SerializedTeamInfo Team;
    }

    private class SerializedTeamAction
    {
        public int Type;

        public int CapDelta;

        public string Reason;

        public static SerializedTeamAction FromTeamAction(TeamAction action) => new() { Type = (int)action.Type, CapDelta = action.CAPDelta, Reason = action.Reason };

        public TeamAction ToTeamAction() => new((ActionType)this.Type, this.CapDelta, this.Reason);
    }

    private class SerializedDemand
    {
        public string Id; 

        public string Title;

        public string Description;

        public string Deadline;

        public string Progress;

        public string Source;

        public bool Active;

        public bool Success;

        public static SerializedDemand FromDemand(Demand demand) => new()
        {
            Id = demand.Id,
            Title = demand.Title,
            Description = demand.Description,
            Deadline = demand.Deadline,
            Source = demand.Source,
            Progress = demand.Progress,
            Active = demand.IsActive,
            Success = demand.WasSuccessful
        };

        public Demand ToDemand() => new(this.Id, this.Title, this.Description, this.Deadline, this.Source, this.Progress, this.Active, this.Success);
    }

    private class SerializedTeamInfo
    {
        public static SerializedTeamInfo FromTeamInfo(TeamInfo teamInfo) => new()
        {
            TeamName = teamInfo.TeamName,
            Division = teamInfo.Division,
            WeeklyAllowance = teamInfo.TotalWeeklyAllowance,
            Carryover = teamInfo.Carryover,
            GridironInvestment = teamInfo.GridironInvestment,
            Actions = teamInfo.Actions?.Select(SerializedTeamAction.FromTeamAction).ToArray() ?? [],
            StatusMessageId = teamInfo.StatusMessageId,
            Demands = teamInfo.Demands?.Select(SerializedDemand.FromDemand).ToArray() ?? [],
            NoteText = teamInfo.NoteText,
        };

        public string TeamName;

        public int Division;

        public int WeeklyAllowance;

        public int Carryover;

        public int GridironInvestment;

        public SerializedTeamAction[] Actions;

        public ulong StatusMessageId;

        public SerializedDemand[] Demands;

        public string NoteText;

        public TeamInfo ToTeamInfo() => new(this.TeamName, this.Division, this.WeeklyAllowance, this.Carryover, this.GridironInvestment, this.Actions?.Select(a => a.ToTeamAction()).ToList() ?? [], this.StatusMessageId, this.Demands?.Select(d => d.ToDemand()).ToList() ?? [], this.NoteText != null ? this.NoteText.Substring(0, Math.Min(this.NoteText.Length, 1700)) : "");
    }
}
