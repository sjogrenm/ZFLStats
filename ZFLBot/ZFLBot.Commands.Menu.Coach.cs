using System.Diagnostics;
using System.Text;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace ZFLBot;

internal partial class ZFLBot {

    private async Task CoachMenuHandler(SocketMessageComponent component){
        (string action, string[] ids) = ParseIdFromAction(component.Data.CustomId);
        Debug.WriteLine($"Component: {JsonConvert.SerializeObject(component.Data, new JsonSerializerSettings{ReferenceLoopHandling = ReferenceLoopHandling.Ignore})}");
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
            case "close":
                await DismissMessage(component);
                break;
        }
    }

    private async Task OpenCoachMenu(SocketSlashCommand arg)
    {
        var guildId = arg.GuildId.GetValueOrDefault();
        SocketUser user = arg.User;
        dataServices[arg.GuildId.Value].TryGetTeam(arg.User.Id, out TeamInfo team);
        if (team == null) {
          await arg.FollowupAsync("You do not have a team connected to your user");
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
          await component.FollowupAsync("You do not have a team connected to your user");
        }
        else {
          (string title, MessageComponent mc) = GenerateCoachStartMenu(component.User.GlobalName ?? component.User.Username, component.User.Id, team);
          await component.FollowupAsync(title, components: mc, ephemeral: true);
        }
    }

    private (string title, MessageComponent component) GenerateCoachStartMenu(string username, ulong id, TeamInfo team) {
        StringBuilder sb = new();
        sb.AppendLine($"# Welcome {username}!");
        sb.AppendLine($"What would you like to do?");
        sb.AppendLine($"Name: **{team.TeamName}**");
        sb.AppendLine($"Div: **{team.Division}**");
        sb.AppendLine($"Current CAP: **{team.CurrentCAP}**");
        sb.AppendLine($"Bonus CAP: **{team.CurrentBonusCAP}**");
        sb.AppendLine($"Weekly CAP: **{team.CurrentWeeklyCAP}**");
        sb.AppendLine($"Gridiron investment: **{team.GridironInvestment}**");
        sb.AppendLine($"_");
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
        StringBuilder sb = new();
        sb.AppendLine($"# View Demands :clipboard:");
        if (demands.Length == 0) {
            sb.AppendLine($"## Currently you have no active demands set for your team :wastebasket:");
        }
        else {
            sb.AppendLine($"## Active Demands");
            foreach(Demand demand in demands.Where(d => d.IsActive)) {
                sb.AppendLine($"- **{demand.Title}**");
                sb.AppendLine($"  - :calendar_spiral: Deadline: {demand.Deadline}");
                if (!string.IsNullOrEmpty(demand.Source))
                    sb.AppendLine($"  - :satellite: Source: {demand.Source}");
                if (!string.IsNullOrEmpty(demand.Progress))
                    sb.AppendLine($"  - Progress: {demand.Progress}");
                sb.AppendLine($"```{demand.Description}```");
            }
        }
        sb.AppendLine($"_");
        builder.AddRow(new ActionRowBuilder()
                .WithButton("Back", $"open-menu-coach({id})", style: ButtonStyle.Secondary)
                .WithButton("Close", "close", style: ButtonStyle.Danger));
        await component.FollowupAsync(sb.ToString(), ephemeral: true, components: builder.Build());
    }

    private async Task CoachDemandSelectionMessage(SocketInteraction component, string id, DemandOperation operation)
    {
        await DismissMessage(component);
        StringBuilder sb = new();
        string operationString = Enum.GetName(typeof(DemandOperation), operation);
        sb.AppendLine($"# {operationString} Demand");
        ComponentBuilder builder = new ComponentBuilder();
        SelectMenuBuilder smBuilder = new SelectMenuBuilder()
            .WithCustomId($"{operationString.ToLower()}-demand-selection({id})")
            .WithMaxValues(1)
            .WithMinValues(1);
        Demand[] demands = dataServices[component.GuildId.Value].GetDemands(Convert.ToUInt64(id));
        if (operation == DemandOperation.OPEN)
            demands = demands.Where(d => !d.IsActive).ToArray();
        if (operation == DemandOperation.CLOSE)
            demands = demands.Where(d => d.IsActive).ToArray();
        for(int i = 0; i < demands.Length; i++){
            smBuilder.AddOption(demands[i].Title, demands[i].Id, $"{(demands[i].IsActive ? "Active" : "Inactive")} - Progress: {demands[i].Progress}");
            Debug.WriteLine($"Added selection option: {demands[i].Title}, {demands[i].Id}");
        }
        if (!string.IsNullOrEmpty(id)){
            builder.AddRow(new ActionRowBuilder()
                    .WithSelectMenu(smBuilder));
        }
        builder.AddRow(new ActionRowBuilder()
                .WithButton("Back", $"manage-team-demands({id})", style: ButtonStyle.Secondary)
                .WithButton("Close", "close", style: ButtonStyle.Danger));
        await component.FollowupAsync(sb.ToString(), ephemeral: true, components: builder.Build());
    }
}
