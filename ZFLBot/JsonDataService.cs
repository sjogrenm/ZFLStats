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
    public TeamInfo RolloverTeam(ulong discordUserId, ulong statusMessageId)
    {
        TeamInfo teamInfo;
        lock (this.lck)
        {
            if (!this.teams.TryGetValue(discordUserId, out teamInfo))
            {
                return null;
            }

            teamInfo = teamInfo.Rollover(statusMessageId);
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
        public int CapDelta;

        public string Reason;

        public static SerializedTeamAction FromTeamAction(TeamAction action) => new() { CapDelta = action.CAPDelta, Reason = action.Reason };

        public TeamAction ToTeamAction() => new(this.CapDelta, this.Reason);
    }

    private class SerializedTeamInfo
    {
        public static SerializedTeamInfo FromTeamInfo(TeamInfo teamInfo) => new()
        {
            TeamName = teamInfo.TeamName,
            Division = teamInfo.Division,
            WeeklyAllowance = teamInfo.TotalWeeklyAllowance,
            Carryover = teamInfo.Carryover,
            Actions = teamInfo.Actions.Select(SerializedTeamAction.FromTeamAction).ToArray(),
            StatusMessageId = teamInfo.StatusMessageId
        };

        public string TeamName;

        public int Division;

        public int WeeklyAllowance;

        public int Carryover;

        public SerializedTeamAction[] Actions;

        public ulong StatusMessageId;

        public TeamInfo ToTeamInfo() => new(this.TeamName, this.Division, this.WeeklyAllowance, this.Carryover, this.Actions.Select(a => a.ToTeamAction()).ToList(), this.StatusMessageId);
    }
}
