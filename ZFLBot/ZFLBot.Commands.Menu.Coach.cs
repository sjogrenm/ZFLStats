using System.Diagnostics;
using System.Text;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace ZFLBot;

internal partial class ZFLBot {

    private async Task CoachMenuHandler(SocketMessageComponent component, string action, string[] ids){
        var guildId = component.GuildId.Value;
        var admin = component.User;
        switch (action)
        {
            case "open-menu-coach":
                await OpenCoachMenuMessage(component);
                break;
            case "manage-team-demands-coach":
                await CoachManageTeamDemandMessage(component, ids.FirstOrDefault());
                break;
        }
    }

    private async Task OpenCoachMenu(SocketSlashCommand arg)
    {
        var guildId = arg.GuildId.GetValueOrDefault();
        SocketUser user = arg.User;
        dataServices[arg.GuildId.Value].TryGetTeam(arg.User.Id, out TeamInfo team);
        if (team == null) {
          await arg.RespondAsync("You do not have a team connected to your user", ephemeral: true);
        }
        else {
          (string title, MessageComponent component) = GenerateCoachStartMenu(user.GlobalName ?? user.Username, user.Id, team);
          await arg.RespondAsync(title, components: component, ephemeral: true);
        }
    }
    private async Task OpenCoachMenuMessage(SocketInteraction component)
    {
        await DismissMessage(component);
        dataServices[component.GuildId.Value].TryGetTeam(component.User.Id, out TeamInfo team);
        if (team == null) {
          await component.FollowupAsync("You do not have a team connected to your user", ephemeral: true);
        }
        else {
          (string title, MessageComponent mc) = GenerateCoachStartMenu(component.User.GlobalName ?? component.User.Username, component.User.Id, team);
          await component.FollowupAsync(title, components: mc, ephemeral: true);
        }
    }

    private (string title, MessageComponent component) GenerateCoachStartMenu(string username, ulong id, TeamInfo team) {
        DiscordStringBuilder sb = new();
        sb.AppendLine($"# Welcome {username}, coach of the {team.TeamName}!");
        sb.AppendLine($"");
        sb.AppendLine($"{team.NoteText}");
        sb.AppendLine($"");
        sb.Append($"Div: **{team.Division}**");
        sb.Append($" | CAP (current/bonus/weekly): **{team.CurrentCAP}**/**{team.CurrentBonusCAP}**/**{team.CurrentWeeklyCAP}**");
//        sb.AppendLine($"Bonus CAP: **{team.CurrentBonusCAP}**");
 //       sb.AppendLine($"Weekly CAP: **{team.CurrentWeeklyCAP}**");
        sb.Append($" | Gridiron: **{team.GridironInvestment}**");
        sb.AppendLine($"");
        sb.AppendLine($"What would you like to do?");
        return (sb.ToString(), new ComponentBuilder()
                .AddRow(new ActionRowBuilder()
                    .WithButton("View Demands", $"manage-team-demands-coach({id})"))
                .AddRow(new ActionRowBuilder()
                    //.WithButton("Back", "manage-team-selection", style: ButtonStyle.Secondary)
                    .WithButton("Close", "close", style: ButtonStyle.Danger))
                .Build());
    }

    private async Task CoachManageTeamDemandMessage(SocketInteraction component, string id)
    {
        await DismissMessage(component);
        Demand[] demands = dataServices[component.GuildId.Value].GetDemands(Convert.ToUInt64(id));
        bool hasDemands = demands.Any();
        ComponentBuilder builder = new ComponentBuilder();
        DiscordStringBuilder sb = new();
        DiscordStringBuilder openSb = new(1700);
        int unlistedOpen = 0;
        sb.AppendLine($"# View Demands :clipboard:");
        if (demands.Where(d => d.IsActive).Count() == 0) {
            sb.AppendLine($"## Currently you have no active demands set for your team :wastebasket:");
        }
        else {
            sb.AppendLine($"## Active Demands");
            foreach(Demand demand in demands.Where(d => d.IsActive)) {
                StringBuilder tempSb = new();
                tempSb.AppendLine($"- **{demand.Title}**");
                tempSb.AppendLine($"  - :calendar_spiral: Deadline: {demand.Deadline}");
                if (!string.IsNullOrEmpty(demand.Source))
                    tempSb.AppendLine($"  - :satellite: Source: {demand.Source}");
                if (!string.IsNullOrEmpty(demand.Progress))
                    tempSb.AppendLine($"  - Progress: {demand.Progress}");
                tempSb.AppendLine($"```{demand.Description}```");
                if (openSb.CanFit(tempSb.ToString()))
                  openSb.Append(tempSb.ToString());
                else unlistedOpen++;
            }
            sb.Append(openSb.ToString());
            if (unlistedOpen > 0) {
                sb.AppendLine($"# {unlistedOpen} hidden due to message length");
            }
        }
        sb.AppendLine($".");
        builder.AddRow(new ActionRowBuilder()
                .WithButton("Back", $"open-menu-coach({id})", style: ButtonStyle.Secondary)
                .WithButton("Close", "close", style: ButtonStyle.Danger));
        await component.FollowupAsync(sb.ToString(), ephemeral: true, components: builder.Build());
    }
}
