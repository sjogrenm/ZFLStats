using System.Diagnostics;
using System.Text;
using Discord;
using Discord.Rest;
using Discord.WebSocket;

namespace ZFLBot;

internal partial class ZFLBot
{
    private void AddCommands()
    {
        this.commands.Add(
            "setup",
            (new SlashCommandBuilder()
                    .WithName("setup")
                    .WithDescription("Initial setup")
                    .AddOption(
                        new SlashCommandOptionBuilder()
                            .WithName("set-audit-channel")
                            .WithType(ApplicationCommandOptionType.SubCommand)
                            .WithDescription("Set the audit channel")
                            .AddOption("channel", ApplicationCommandOptionType.Channel, "The channel", isRequired: true))
                    .AddOption(
                        new SlashCommandOptionBuilder()
                            .WithName("set-status-channel")
                            .WithType(ApplicationCommandOptionType.SubCommand)
                            .WithDescription("Set the status channel")
                            .AddOption("div", ApplicationCommandOptionType.Integer, "The division", isRequired: true)
                            .AddOption("channel", ApplicationCommandOptionType.Channel, "The channel", isRequired: true))
                    .AddOption(
                        new SlashCommandOptionBuilder()
                            .WithName("set-admin-role")
                            .WithType(ApplicationCommandOptionType.SubCommand)
                            .WithDescription("Set the admin role")
                            .AddOption("role", ApplicationCommandOptionType.Role, "The role", isRequired: true))
                    .Build(),
                this.SetupCmd));
        this.commands.Add(
            "admin",
            (new SlashCommandBuilder()
                    .WithName("admin")
                    .WithDescription("Admin tools")
                    .AddOption(
                        new SlashCommandOptionBuilder()
                            .WithName("teams")
                            .WithType(ApplicationCommandOptionType.SubCommand)
                            .WithDescription("Lists all teams"))
                    .AddOption(
                        new SlashCommandOptionBuilder()
                            .WithName("add-team")
                            .WithType(ApplicationCommandOptionType.SubCommand)
                            .WithDescription("Adds a new team")
                            .AddOption("coach", ApplicationCommandOptionType.User, "The coach", isRequired: true)
                            .AddOption("team-name", ApplicationCommandOptionType.String, "The team name", isRequired: true)
                            .AddOption("division", ApplicationCommandOptionType.Integer, "The division number", isRequired: true)
                            .AddOption("allowance", ApplicationCommandOptionType.Integer, "Weekly allowance (defaults to 10/8/6)"))
                    .AddOption(
                        new SlashCommandOptionBuilder()
                            .WithName("remove-team")
                            .WithType(ApplicationCommandOptionType.SubCommand)
                            .WithDescription("Removes a team (WARNING: THIS IS PERMANENT)")
                            .AddOption("coach", ApplicationCommandOptionType.User, "The coach of the team", isRequired: true))
                    .AddOption(
                        new SlashCommandOptionBuilder()
                            .WithName("add-bonus-cap")
                            .WithType(ApplicationCommandOptionType.SubCommand)
                            .WithDescription("Adds bonus CAP to a team")
                            .AddOption("coach", ApplicationCommandOptionType.User, "The coach", isRequired: true)
                            .AddOption("amount", ApplicationCommandOptionType.Integer, "The amount of CAP", isRequired: true)
                            .AddOption("reason", ApplicationCommandOptionType.String, "The reason for the addition", isRequired: true))
                    .AddOption(
                        new SlashCommandOptionBuilder()
                            .WithName("use-gridiron-cap")
                            .WithType(ApplicationCommandOptionType.SubCommand)
                            .WithDescription("Spends CAP invested with the Gridiron Guild")
                            .AddOption("coach", ApplicationCommandOptionType.User, "The coach", isRequired: true)
                            .AddOption("amount", ApplicationCommandOptionType.Integer, "The amount of CAP", isRequired: true)
                            .AddOption("reason", ApplicationCommandOptionType.String, "The reason for the addition", isRequired: true))
                    .AddOption(
                        new SlashCommandOptionBuilder()
                            .WithName("reset-spend")
                            .WithType(ApplicationCommandOptionType.SubCommand)
                            .WithDescription("Resets this round's spending for a team")
                            .AddOption("coach", ApplicationCommandOptionType.User, "The coach", isRequired: true)
                            .AddOption("reason", ApplicationCommandOptionType.String, "The reason for the reset", isRequired: true))
                    .AddOption(
                        new SlashCommandOptionBuilder()
                            .WithName("rollover")
                            .WithType(ApplicationCommandOptionType.SubCommand)
                            .WithDescription("Adds the weekly allowances to all teams")
                            .AddOption("division", ApplicationCommandOptionType.String, "The division number (or all)", isRequired: true))
                    .Build(),
                this.AdminCmd));
        this.commands.Add(
            "cap",
            (new SlashCommandBuilder()
                    .WithName("cap")
                    .WithDescription("Tools for managing CAP")
                    .AddOption(
                        new SlashCommandOptionBuilder()
                            .WithName("balance")
                            .WithDescription("Checks your current balance of CAP (weekly and bonus)")
                            .WithType(ApplicationCommandOptionType.SubCommand))
                    .AddOption(
                        new SlashCommandOptionBuilder()
                            .WithName("spend")
                            .WithDescription("Spends CAP on an action")
                            .WithType(ApplicationCommandOptionType.SubCommand)
                            .AddOption("amount", ApplicationCommandOptionType.Integer, "The amount of CAP", isRequired: true)
                            .AddOption("reason", ApplicationCommandOptionType.String, "The reason for the spending", isRequired: true))
                    .AddOption(
                        new SlashCommandOptionBuilder()
                            .WithName("gridiron")
                            .WithDescription("Invest CAP with the Gridiron Guild")
                            .WithType(ApplicationCommandOptionType.SubCommand)
                            .AddOption("amount", ApplicationCommandOptionType.Integer, "The amount of CAP", isRequired: true))
                    .Build(),
                this.UserCapCmd));
    }

    private async Task SetupCmd(SocketSlashCommand arg)
    {
        var guildId = arg.GuildId.GetValueOrDefault();

        if (arg.User is not SocketGuildUser guildUser || !this.IsAdmin(guildUser))
        {
            await this.AuditLog(guildId, $"{arg.User.GlobalName} ({arg.User.Id}) tried to use an admin command");
            await arg.RespondAsync("You are not an admin", ephemeral: true);
            return;
        }

        Debug.Assert(arg.Data.Options.Count == 1);
        var cmd = arg.Data.Options.First();
        switch (cmd.Name)
        {
            case "set-audit-channel":
            {
                var channel = (SocketTextChannel)cmd.Options.First().Value;
                await arg.RespondAsync($"Setting audit log channel to {channel.Name}", ephemeral: true);
                this.SetAuditChannel(guildId, channel.Id);
                await this.AuditLog(guildId, $"{arg.User.GlobalName} ({arg.User.Id}) set {channel.Name} as audit log channel");
                break;
            }

            case "set-status-channel":
            {
                var div = (int)(long)cmd.GetOption("div").Value;
                var channel = (SocketTextChannel)cmd.GetOption("channel").Value;
                await arg.RespondAsync($"Setting status channel for division {div} to {channel.Name}", ephemeral: true);
                this.SetStatusChannel(guildId, div, channel.Id);
                await this.AuditLog(guildId, $"{arg.User.GlobalName} ({arg.User.Id}) set {channel.Name} as status channel for division {div}");
                break;
            }

            case "set-admin-role":
            {
                var role = (SocketRole)cmd.GetOption("role").Value;
                await arg.RespondAsync($"Setting admin role to {role.Name}", ephemeral: true);
                this.SetAdminRole(guildId, role.Id);
                await this.AuditLog(guildId, $"{arg.User.GlobalName} ({arg.User.Id}) set {role.Name} as admin role");
                break;
            }

            default:
                Log($"Unknown command /setup {cmd.Name}");
                break;
        }
    }

    private async Task UserCapCmd(SocketSlashCommand arg)
    {
        Debug.Assert(arg.Data.Options.Count == 1);
        var cmd = arg.Data.Options.First();
        switch (cmd.Name)
        {
            case "balance":
                await this.CheckBalance(arg);
                break;

            case "spend":
            {
                var amountArg = cmd.GetOption("amount")!;
                var reasonArg = cmd.GetOption("reason")!;
                var amount = (long) amountArg.Value;
                var reason = (string)reasonArg.Value;
                await this.SpendCAP(arg, (int)amount, reason);
                break;
            }

            case "gridiron":
            {
                var amountArg = cmd.GetOption("amount")!;
                var amount = (long)amountArg.Value;
                await this.GridironInvestment(arg, (int)amount);
                break;
            }

            default:
                Log($"Unknown command /cap {cmd.Name}");
                break;
        }
    }

    private async Task CheckBalance(SocketSlashCommand arg)
    {
        var guildId = arg.GuildId.GetValueOrDefault();

        if (!this.dataServices[guildId].TryGetTeam(arg.User.Id, out var teamInfo))
        {
            await arg.RespondAsync("You do not appear to have a ZFL team", ephemeral: true);
            return;
        }

        var text = $"Your current balance is {teamInfo.CurrentWeeklyCAP} weekly allowance and {teamInfo.CurrentBonusCAP} bonus CAP. You also have {teamInfo.GridironInvestment} CAP invested with the Gridiron Guild.";
        if (teamInfo.Actions.Where(a => a.Type != ActionType.BonusCAP).ToList() is { Count : > 0 } actions)
        {
            text += "\nCAP spent this round:\n";
            text += string.Join('\n', actions.Select(a => $"* {-a.CAPDelta} CAP - {a.Reason}"));
        }

        await arg.RespondAsync(text, ephemeral: true);
    }

    private async Task SpendCAP(SocketSlashCommand arg, int amount, string reason)
    {
        var guildId = arg.GuildId.GetValueOrDefault();

        if (amount <= 0)
        {
            await arg.RespondAsync("Cute, now how much do you really want to spend?", ephemeral: true);
            return;
        }

        if (!this.dataServices[guildId].TryGetTeam(arg.User.Id, out var teamInfo))
        {
            await arg.RespondAsync("You do not appear to have a ZFL team", ephemeral: true);
            return;
        }

        if (amount > teamInfo.CurrentCAP)
        {
            await arg.RespondAsync($"You only have {teamInfo.CurrentCAP} to spend", ephemeral: true);
            return;
        }

        await arg.RespondAsync($"Spending {amount} CAP", ephemeral: true);
        await this.AuditLog(guildId, $"{arg.User.GlobalName} ({arg.User.Id}) ({teamInfo.TeamName}) spent {amount} CAP: \"{reason}\"");
        teamInfo = this.dataServices[guildId].SpendCAP(arg.User.Id, amount, reason);
        await this.UpdateStatusMessage(guildId, arg.User.Id, teamInfo);
    }

    private async Task AdminCmd(SocketSlashCommand arg)
    {
        var guildId = arg.GuildId.GetValueOrDefault();

        if (arg.User is not SocketGuildUser guildUser || !this.IsAdmin(guildUser))
        {
            await this.AuditLog(guildId, $"{arg.User.GlobalName} ({arg.User.Id}) tried to use an admin command");
            await arg.RespondAsync("You are not an admin", ephemeral: true);
            return;
        }

        Debug.Assert(arg.Data.Options.Count == 1);
        var cmd = arg.Data.Options.First();
        switch (cmd.Name)
        {
            case "rollover":
            {
                var divArg = cmd.GetOption("division")!;
                var div = (string) divArg.Value;
                await this.Rollover(arg, div);
                break;
            }

            case "teams":
                await this.ListTeams(arg);
                break;

            case "add-team":
            {
                var coachArg = cmd.GetOption("coach")!;
                var teamArg = cmd.GetOption("team-name")!;
                var divArg = cmd.GetOption("division")!;
                var allowanceArg = cmd.GetOption("allowance");
                var user = (SocketGuildUser)coachArg.Value;
                var team = (string) teamArg.Value;
                var div = (long) divArg.Value;
                var allowance = allowanceArg?.Value as long?;
                await this.AddTeam(arg, user, team, (int)div, (int?)allowance);
                break;
            }

            case "remove-team":
            {
                var coachArg = cmd.GetOption("coach")!;
                var user = (SocketGuildUser)coachArg.Value;
                await this.RemoveTeam(arg, user);
                break;
            }

            case "add-bonus-cap":
            {
                var coachArg = cmd.GetOption("coach")!;
                var amountArg = cmd.GetOption("amount")!;
                var reasonArg = cmd.GetOption("reason")!; 
                var user = (SocketGuildUser)coachArg.Value;
                var amount = (long)amountArg.Value;
                var reason = (string) reasonArg.Value;
                await this.AddBonusCAP(arg, user, (int)amount, reason);
                break;
            }

            case "use-gridiron-cap":
            {
                var coachArg = cmd.GetOption("coach")!;
                var amountArg = cmd.GetOption("amount")!;
                var reasonArg = cmd.GetOption("reason")!;
                var user = (SocketGuildUser)coachArg.Value;
                var amount = (long)amountArg.Value;
                var reason = (string)reasonArg.Value;
                await this.UseGridironCAP(arg, user, (int)amount, reason);
                break;
            }

            case "reset-spend":
            {
                var coachArg = cmd.GetOption("coach")!;
                var reasonArg = cmd.GetOption("reason")!;
                var user = (SocketGuildUser)coachArg.Value;
                var reason = (string)reasonArg.Value;
                await this.ResetSpentCAP(arg, user, reason);
                break;
            }

            default:
                Log($"Unknown command /admin {cmd.Name}");
                break;
        }
    }

    private async Task UseGridironCAP(SocketSlashCommand arg, SocketGuildUser user, int amount, string reason)
    {
        var guildId = arg.GuildId.GetValueOrDefault();

        if (!this.dataServices[guildId].TryGetTeam(user.Id, out var teamInfo))
        {
            await arg.RespondAsync($"{user.GlobalName} has no ZFL team", ephemeral: true);
            return;
        }
        
        if (amount > teamInfo.GridironInvestment)
        {
            await arg.RespondAsync($"That's more than {user.GlobalName} has invested", ephemeral: true);
            return;
        }

        await arg.RespondAsync($"Spending {amount} Gridiron Guild CAP for {user.GlobalName} ({teamInfo.TeamName})", ephemeral: true);
        await this.AuditLog(guildId, $"{arg.User.GlobalName} ({arg.User.Id}) spent {amount} of Gridiron Guild CAP for {user.GlobalName} ({user.Id}) ({teamInfo.TeamName}): \"{reason}\"");
        teamInfo = this.dataServices[guildId].UseGridironCAP(user.Id, amount);
        await this.UpdateStatusMessage(guildId, user.Id, teamInfo);
    }

    private async Task AddTeam(SocketSlashCommand arg, SocketGuildUser user, string team, int div, int? allowance)
    {
        var guildId = arg.GuildId.GetValueOrDefault();

        if (div < 1 || div > 3)
        {
            await arg.RespondAsync("Invalid division number", ephemeral: true);
            return;
        }

        if (this.dataServices[guildId].TryGetTeam(user.Id, out var teamInfo))
        {
            await arg.RespondAsync($"{user.GlobalName} already has a ZFL team", ephemeral: true);
            return;
        }

        await arg.RespondAsync($"Adding team for {user.GlobalName}", ephemeral: true);
        teamInfo = TeamInfo.Create(team, div, allowance ?? GetDefaultAllowance(div));
        await this.AuditLog(guildId, $"{arg.User.GlobalName} ({arg.User.Id}) added team for {user.GlobalName} ({user.Id}) ({teamInfo.TeamName})");
        this.dataServices[guildId].AddTeam(user.Id, teamInfo);
    }

    private async Task RemoveTeam(SocketSlashCommand arg, SocketGuildUser user)
    {
        var guildId = arg.GuildId.GetValueOrDefault();

        if (!this.dataServices[guildId].TryGetTeam(user.Id, out var teamInfo))
        {
            await arg.RespondAsync($"{user.GlobalName} has no ZFL team", ephemeral: true);
            return;
        }

        await arg.RespondAsync($"Removing {user.GlobalName}'s team {teamInfo.TeamName}", ephemeral: true);
        await this.AuditLog(guildId, $"{arg.User.GlobalName} ({arg.User.Id}) removed team of {user.GlobalName} ({user.Id}) ({teamInfo.TeamName})");
        this.dataServices[guildId].RemoveTeam(user.Id);
    }

    private async Task ListTeams(SocketSlashCommand arg)
    {
        var guildId = arg.GuildId.GetValueOrDefault();

        var teams = this.GetTeamsSortedByDivision(guildId);
        var msg = new StringBuilder();
        msg.Append("```");
        msg.AppendFormat("{0,-10} {1,-20} {2,3} {3,6} {4,5}\n", "Coach", "Team", "Div", "Weekly", "Bonus");
        foreach (var (discordUserId, teamInfo) in teams)
        {
            var user = this.GetUser(guildId, discordUserId);
            msg.AppendFormat("{0,-10} {1,-20} {2,3} {3,6} {4,5}\n", user?.GlobalName ?? discordUserId.ToString(), teamInfo.TeamName, teamInfo.Division, teamInfo.CurrentWeeklyCAP, teamInfo.CurrentBonusCAP);
        }
        msg.Append("```");
        await arg.RespondAsync(msg.ToString(), ephemeral: true);
    }

    private List<KeyValuePair<ulong, TeamInfo>> GetTeamsSortedByDivision(ulong guildId)
    {
        var teams = this.dataServices[guildId].GetAllTeams().ToList();
        teams.Sort(DivisionComparer.Default);
        return teams;
    }

    private class DivisionComparer : IComparer<TeamInfo>, IComparer<KeyValuePair<ulong, TeamInfo>>
    {
        public static readonly DivisionComparer Default = new();

        /// <inheritdoc />
        public int Compare(TeamInfo? x, TeamInfo? y)
        {
            return x.Division - y.Division;
        }

        /// <inheritdoc />
        public int Compare(KeyValuePair<ulong, TeamInfo> x, KeyValuePair<ulong, TeamInfo> y)
        {
            return this.Compare(x.Value, y.Value);
        }
    }

    private async Task AddBonusCAP(SocketSlashCommand arg, SocketGuildUser user, int amount, string reason)
    {
        var guildId = arg.GuildId.GetValueOrDefault();

        if (!this.dataServices[guildId].TryGetTeam(user.Id, out var teamInfo))
        {
            await arg.RespondAsync($"{user.GlobalName} has no ZFL team", ephemeral: true);
            return;
        }

        if (amount <= 0)
        {
            await arg.RespondAsync("Bonus CAP must be positive", ephemeral: true);
            return;
        }

        await arg.RespondAsync($"Giving {user.GlobalName} ({teamInfo.TeamName}) {amount} bonus CAP", ephemeral: true);
        await this.AuditLog(guildId, $"{arg.User.GlobalName} ({arg.User.Id}) gave {user.GlobalName} ({user.Id}) ({teamInfo.TeamName}) {amount} bonus CAP: \"{reason}\"");
        teamInfo = this.dataServices[guildId].AddBonusCAP(user.Id, amount, reason);
        await this.UpdateStatusMessage(guildId, user.Id, teamInfo);
        try
        {
            await user.SendMessageAsync($"Your ZFL team was just granted {amount} bonus CAP!");
        }
        catch (Discord.Net.HttpException e) when (e.DiscordCode == DiscordErrorCode.CannotSendMessageToUser)
        {
            Log($"Could not send message to user {user.GlobalName} ({user.Id})");
        }
    }

    private async Task GridironInvestment(SocketSlashCommand arg, int amount)
    {
        var guildId = arg.GuildId.GetValueOrDefault();

        if (amount <= 0)
        {
            await arg.RespondAsync("Cute, now how much do you really want to spend?", ephemeral: true);
            return;
        }

        if (!this.dataServices[guildId].TryGetTeam(arg.User.Id, out var teamInfo))
        {
            await arg.RespondAsync("You do not appear to have a ZFL team", ephemeral: true);
            return;
        }

        if (amount > 3)
        {
            await arg.RespondAsync("You can only invest up to 3 CAP per round", ephemeral: true);
            return;
        }

        if (amount > teamInfo.CurrentCAP)
        {
            await arg.RespondAsync($"You only have {teamInfo.CurrentCAP} to spend", ephemeral: true);
            return;
        }

        await arg.RespondAsync($"Spending {amount} CAP", ephemeral: true);
        await this.AuditLog(guildId, $"{arg.User.GlobalName} ({arg.User.Id}) ({teamInfo.TeamName}) invested {amount} CAP with the Gridiron Guild");
        teamInfo = this.dataServices[guildId].GridironInvestment(arg.User.Id, amount);
        await this.UpdateStatusMessage(guildId, arg.User.Id, teamInfo);
    }

    private async Task ResetSpentCAP(SocketSlashCommand arg, SocketGuildUser user, string reason)
    {
        var guildId = arg.GuildId.GetValueOrDefault();

        if (!this.dataServices[guildId].TryGetTeam(user.Id, out var teamInfo))
        {
            await arg.RespondAsync($"{user.GlobalName} has no ZFL team", ephemeral: true);
            return;
        }

        await arg.RespondAsync($"Resetting CAP spent this round for {user.GlobalName} ({teamInfo.TeamName})", ephemeral: true);
        await this.AuditLog(guildId, $"{arg.User.GlobalName} ({arg.User.Id}) reset CAP spent for {user.GlobalName} ({user.Id}) ({teamInfo.TeamName}): \"{reason}\"");
        teamInfo = this.dataServices[guildId].ResetSpentCAP(user.Id);
        await this.UpdateStatusMessage(guildId, user.Id, teamInfo);
    }

    private async Task Rollover(SocketSlashCommand arg, string div)
    {
        var divNum = -1;
        if (div != "all" && (!int.TryParse(div, out divNum) || divNum < 1 || divNum > 3))
        {
            await arg.RespondAsync("Invalid division number", ephemeral: true);
            return;
        }

        var divMessage = div == "all" ? "all divisions" : $"division {divNum}";
        var suffix = div == "all" ? "all" : divNum.ToString();

        await arg.RespondAsync(
            $"Initiate rollover of {divMessage}?",
            components: new ComponentBuilder().WithButton("Confirm", $"admin.rollover.{suffix}").Build(),
            ephemeral: true);
    }

    private async Task CommitRollover(SocketGuildUser admin, SocketMessageComponent component)
    {
        // Have to defer before deleting the message
        await component.DeferAsync(ephemeral: true);
        await component.DeleteOriginalResponseAsync();

        var div = component.Data.CustomId.Split('.')[2];
        int.TryParse(div, out var divNum);
        Debug.Assert(div == "all" || (divNum >= 1 && divNum <= 3));

        var divMessage = div == "all" ? "all divisions" : $"division {divNum}";

        var guildId = admin.Guild.Id;
        await this.AuditLog(guildId, $"{admin.GlobalName} ({admin.Id}) initiated round rollover for {divMessage}");

        SocketTextChannel? statusChannel = null;
        int currentDiv = -1;
        foreach (var (discordUserId, teamInfo) in this.GetTeamsSortedByDivision(guildId))
        {
            if (div != "all" && teamInfo.Division != divNum)
            {
                continue;
            }

            var user = this.GetUser(guildId, discordUserId);
            if (teamInfo.Division != currentDiv)
            {
                currentDiv = teamInfo.Division;
                statusChannel = this.GetStatusChannel(guildId, currentDiv);
                if (statusChannel != null)
                {
                    await statusChannel.SendMessageAsync("=== NEW ROUND ===");
                }
            }

            RestUserMessage? statusMessage = null;
            if (statusChannel != null)
            {
                statusMessage = await statusChannel.SendMessageAsync($"{teamInfo.TeamName} ({user?.GlobalName ?? discordUserId.ToString()}) - rollover...");
            }

            var newTeamInfo = this.dataServices[guildId].RolloverTeam(discordUserId, statusMessage?.Id ?? 0);

            if (statusMessage != null)
            {
                await UpdateStatusMessage(user, discordUserId, statusMessage, newTeamInfo);
            }
        }
    }

    private async Task UpdateStatusMessage(ulong guildId, ulong discordUserId, TeamInfo teamInfo)
    {
        if (teamInfo.StatusMessageId == 0)
        {
            return;
        }

        var statusChannel = this.GetStatusChannel(guildId, teamInfo.Division);
        if (statusChannel == null)
        {
            return;
        }

        var user = this.GetUser(guildId, discordUserId);
        var message = (RestUserMessage) await statusChannel.GetMessageAsync(teamInfo.StatusMessageId);
        if (message != null)
        {
            await UpdateStatusMessage(user, discordUserId, message, teamInfo);
        }
    }

    private static async Task UpdateStatusMessage(IUser? user, ulong discordUserId, RestUserMessage message, TeamInfo teamInfo)
    {
        var builder = new StringBuilder();
        builder.Append($"{teamInfo.TeamName} ({user?.GlobalName ?? discordUserId.ToString()}) - {teamInfo.CurrentWeeklyCAP}+{teamInfo.CurrentBonusCAP} CAP remaining ({teamInfo.GridironInvestment} invested)");
        if (teamInfo.Carryover > 0)
        {
            builder.Append($"\n* {teamInfo.Carryover} CAP - Carryover");
        }

        builder.Append($"\n* {teamInfo.TotalWeeklyAllowance} CAP - Weekly Allowance");

        foreach (var action in teamInfo.Actions)
        {
            builder.Append($"\n* {action.CAPDelta:+#;-#;0} CAP - {action.Reason}");
        }

        await message.ModifyAsync(m => m.Content = builder.ToString());
    }

    private static int GetDefaultAllowance(int division) => division switch
    {
        1 => 10,
        2 => 8,
        3 => 6
    };
}
