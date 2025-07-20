using System.Diagnostics;
using System.Text;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace ZFLBot;

internal partial class ZFLBot
{
    private async Task AdminMenuHandler(SocketMessageComponent component, string action, string[] ids){
        var guildId = component.GuildId.Value;
        var admin = component.User;
        switch (action)
        {
            case "open-menu":
                await OpenMenuMessage(component);
                break;
            case "manage-team-div":
                await ManageTeamsDivMessage(component);
                break;
            case "manage-team-selection":
                await ManageTeamsMessage(component, int.Parse(ids.FirstOrDefault()));
                break;
            case "manage-team-text":
                await ManageTeamsTextModal(component, ids.FirstOrDefault());
                break;
            case "manage-team-menu":
                await ManageTeamMenuMessage(component, ids.FirstOrDefault());
                break;
            case "manage-team-demands":
                await ManageTeamDemandMessage(component, ids.FirstOrDefault());
                break;
            case "new-demand":
                await NewDemandModal(component, ids.FirstOrDefault());
                break;
            case "edit-demand":
                await DemandSelectionMessage(component, ids.FirstOrDefault(), DemandOperation.EDIT);
                break;
            case "edit-demand-selection":
                await EditDemandModal(component, ids.FirstOrDefault());
                break;
            case "open-demand":
                await DemandSelectionMessage(component, ids.FirstOrDefault(), DemandOperation.OPEN);
                break;
            case "open-demand-selection":
                {
                    var userId = Convert.ToUInt64(ids.FirstOrDefault());
                    var user = this.GetUser(guildId, userId);
                    dataServices[guildId].OpenDemand(userId, component.Data.Values.First());
                    await this.AuditLog(guildId, $"{admin.Username} ({admin.Id}) opened demand for {user?.Username} ({user?.Id})");
                    await ManageTeamDemandMessage(component, ids.FirstOrDefault());
                    break;
                }
            case "close-demand":
                await DemandSelectionMessage(component, ids.FirstOrDefault(), DemandOperation.CLOSE);
                break;
            case "close-demand-selection":
                await CloseDemandModal(component, ids.FirstOrDefault());
                break;
            case "remove-demand":
                await DemandSelectionMessage(component, ids.FirstOrDefault(), DemandOperation.REMOVE);
                break;
            case "remove-demand-selection":
                {
                    var userId = Convert.ToUInt64(ids.FirstOrDefault());
                    var user = this.GetUser(guildId, userId);
                    dataServices[guildId].RemoveDemand(userId, component.Data.Values.First());
                    await this.AuditLog(guildId, $"{admin.Username} ({admin.Id}) removed demand for {user?.Username} ({user?.Id})");
                    await ManageTeamDemandMessage(component, ids.FirstOrDefault());
                    break;
                }
        }
    }

    private async Task AdminMenuModalHandler(SocketModal modal, string action, string[] ids) {
        var guildId = modal.GuildId.Value;
        var admin = modal.User;
        IReadOnlyCollection<SocketMessageComponentData> components = modal.Data.Components;
        SocketMessageComponentData title = components.GetById("demand_title");
        SocketMessageComponentData deadline = components.GetById("demand_deadline");
        SocketMessageComponentData description = components.GetById("demand_description");
        SocketMessageComponentData source = components.GetById("demand_source");
        SocketMessageComponentData progress = components.GetById("demand_progress");
        SocketMessageComponentData success = components.GetById("demand_success");
        SocketMessageComponentData noteText = components.GetById("team_text");
        switch(action){
            case "new-demand-modal":
                {
                    var userId = Convert.ToUInt64(ids.FirstOrDefault());
                    var user = this.GetUser(guildId, userId);
                    await this.AuditLog(guildId, $"{admin.Username} ({admin.Id}) added demand for {user?.Username} ({user?.Id})");
                    dataServices[modal.GuildId.Value].AddDemand(userId, title.Value, description.Value, deadline.Value, source.Value);
                    await ManageTeamDemandMessage(modal, ids.FirstOrDefault());
                    break;
                }
            case "edit-demand-modal":
                {
                    var userId = Convert.ToUInt64(ids.FirstOrDefault());
                    var user = this.GetUser(guildId, userId);
                    await this.AuditLog(guildId, $"{admin.Username} ({admin.Id}) edited demand for {user?.Username} ({user?.Id})");
                    dataServices[modal.GuildId.Value].EditDemand(userId, ids.LastOrDefault(), title.Value, description.Value, deadline.Value, source.Value, progress.Value);
                    await ManageTeamDemandMessage(modal, ids.FirstOrDefault());
                    break;
                }
            case "manage-team-text-modal":
                {
                    var userId = Convert.ToUInt64(ids.FirstOrDefault());
                    var user = this.GetUser(guildId, userId);
                    await this.AuditLog(guildId, $"{admin.Username} ({admin.Id}) modified text for {user?.Username} ({user?.Id})");
                    dataServices[modal.GuildId.Value].SetNoteText(userId, noteText.Value);
                    await ManageTeamMenuMessage(modal, ids.FirstOrDefault());
                    break;
                }
            case "close-demand-modal":
                {
                    var userId = Convert.ToUInt64(ids.FirstOrDefault());
                    var user = this.GetUser(guildId, userId);
                    await this.AuditLog(guildId, $"{admin.Username} ({admin.Id}) closed demand for {user?.Username} ({user?.Id})");
                    if (!bool.TryParse(success.Value, out bool wasSuccessful)) {
                        if (success.Value.Equals("yes", StringComparison.OrdinalIgnoreCase)) {
                            wasSuccessful = true;
                        }
                        else if (success.Value.Equals("no", StringComparison.OrdinalIgnoreCase)) {
                            wasSuccessful = false;
                        }
                        else {
                            throw new Exception();
                        }
                    }
                    dataServices[modal.GuildId.Value].CloseDemand(userId, ids.LastOrDefault(), wasSuccessful);
                    await ManageTeamDemandMessage(modal, ids.FirstOrDefault());
                    break;
                }
        }
    }

    private async Task OpenMenu(SocketSlashCommand arg)
    {
        var guildId = arg.GuildId.GetValueOrDefault();
        SocketUser user = arg.User;
        (string title, MessageComponent component) = GenerateStartMenu(user.GlobalName ?? user.Username);
        await arg.RespondAsync(title, components: component, ephemeral: true);
    }

    private (string title, MessageComponent component) GenerateStartMenu(string username) {
        DiscordStringBuilder sb = new();
        sb.AppendLine($"# Welcome {username}!");
        sb.AppendLine($"What would you like to do?");
        sb.AppendLine($"- **Manage Teams** lets you:");
        sb.AppendLine($"  - Manage Demands");
        sb.AppendLine($"  - Update text note");
        return (sb.ToString(), new ComponentBuilder()
                .AddRow(new ActionRowBuilder()
                    .WithButton("Manage Teams", "manage-team-div"))
                    //.WithButton("Get Status", "get-status")
                    //.WithButton("Roll Round", "roll-round"))
                .AddRow(new ActionRowBuilder()
                    .WithButton("Close", "close", style: ButtonStyle.Danger))
                .Build());
    }

    private async Task NewDemandModal(SocketInteraction component, string id)
    {
        await component.RespondWithModalAsync(new ModalBuilder()
                .WithTitle("New Demand")
                .WithCustomId($"new-demand-modal({id})")
                .AddTextInput(new TextInputBuilder()
                    .WithLabel("Title")
                    .WithCustomId("demand_title")
                    .WithRequired(true))
                .AddTextInput(new TextInputBuilder()
                    .WithLabel("Deadline")
                    .WithCustomId("demand_deadline")
                    .WithRequired(true))
                .AddTextInput(new TextInputBuilder()
                    .WithLabel("Description")
                    .WithCustomId("demand_description")
                    .WithStyle(TextInputStyle.Paragraph)
                    .WithMaxLength(1500)
                    .WithRequired(true))
                .AddTextInput(new TextInputBuilder()
                    .WithLabel("Source")
                    .WithCustomId("demand_source")
                    .WithRequired(false))
                /*
                   .AddTextInput(new TextInputBuilder()
                   .WithLabel("Progress")
                   .WithCustomId("demand_progress")
                   .WithRequired(false))
                   */
                .Build());
    }

    private enum DemandOperation {
        EDIT,
        OPEN,
        CLOSE,
        REMOVE,
    }
    private async Task DemandSelectionMessage(SocketInteraction component, string id, DemandOperation operation)
    {
        await DismissMessage(component);
        DiscordStringBuilder sb = new();
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

    private async Task EditDemandModal(SocketInteraction component, string id)
    {
        Demand[] demands = dataServices[component.GuildId.Value].GetDemands(Convert.ToUInt64(id));
        string demandId = ((SocketMessageComponent)component).Data.Values.FirstOrDefault();
        Demand demand = demands.Where(d => d.Id.Equals(demandId)).First();
        await component.RespondWithModalAsync(new ModalBuilder()
                .WithTitle("Edit Demand")
                .WithCustomId($"edit-demand-modal({id};{demand.Id})")
                .AddTextInput(new TextInputBuilder()
                    .WithLabel("Title")
                    .WithCustomId("demand_title")
                    .WithValue(demand.Title)
                    .WithRequired(true))
                .AddTextInput(new TextInputBuilder()
                    .WithLabel("Deadline")
                    .WithCustomId("demand_deadline")
                    .WithValue(demand.Deadline)
                    .WithRequired(true))
                .AddTextInput(new TextInputBuilder()
                    .WithLabel("Description")
                    .WithCustomId("demand_description")
                    .WithStyle(TextInputStyle.Paragraph)
                    .WithValue(demand.Description)
                    .WithRequired(true))
                .AddTextInput(new TextInputBuilder()
                    .WithLabel("Source")
                    .WithCustomId("demand_source")
                    .WithValue(demand.Source)
                    .WithRequired(false))
                .AddTextInput(new TextInputBuilder()
                        .WithLabel("Progress")
                        .WithCustomId("demand_progress")
                        .WithValue(demand.Progress)
                        .WithRequired(false))
                .Build());
    }

    private async Task ManageTeamsTextModal(SocketInteraction component, string id)
    {
        dataServices[component.GuildId.Value].TryGetTeam(Convert.ToUInt64(id), out TeamInfo? team);
        await component.RespondWithModalAsync(new ModalBuilder()
                .WithTitle("Edit Text")
                .WithCustomId($"manage-team-text-modal({id})")
                .AddTextInput(new TextInputBuilder()
                    .WithLabel("Text")
                    .WithCustomId("team_text")
                    .WithStyle(TextInputStyle.Paragraph)
                    .WithValue(team.NoteText)
                    .WithMaxLength(1700)
                    .WithRequired(true))
                .Build());
    }

    private async Task CloseDemandModal(SocketInteraction component, string id)
    {
        Demand[] demands = dataServices[component.GuildId.Value].GetDemands(Convert.ToUInt64(id));
        string demandId = ((SocketMessageComponent)component).Data.Values.FirstOrDefault();
        Demand demand = demands.Where(d => d.Id.Equals(demandId)).First();
        await component.RespondWithModalAsync(new ModalBuilder()
                .WithTitle("Close Demand")
                .WithCustomId($"close-demand-modal({id};{demandId})")
                .AddTextInput(new TextInputBuilder()
                    .WithLabel("Was the demand successful?")
                    .WithCustomId("demand_success")
                    .WithPlaceholder("True/False")
                    .WithRequired(true))
                .Build());
    }

    private async Task ManageTeamDemandMessage(SocketInteraction component, string id)
    {
        await DismissMessage(component);
        Demand[] demands = dataServices[component.GuildId.Value].GetDemands(Convert.ToUInt64(id));
        bool hasDemands = demands.Any();
        ComponentBuilder builder = new ComponentBuilder();
        DiscordStringBuilder sb = new();
        DiscordStringBuilder openSb = new(1400);
        DiscordStringBuilder closedSb = new(300);
        int unlistedOpen = 0;
        int unlistedClosed = 0;
        sb.AppendLine($"# Manage Demands");
        sb.AppendLine($"New demands will be created as **inactive** so that the coach will not see it prematurely");
        if (demands.Length == 0) {
            sb.AppendLine($"## Currently no demands have been set for the team");
            sb.AppendLine($"Add a demand with the button below");
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
                sb.AppendLine($"");
            }
            sb.AppendLine($"## Inactive Demands");
            foreach(Demand demand in demands.Where(d => !d.IsActive)) {
                StringBuilder tempSb = new();
                tempSb.AppendLine($"- **{demand.Title}** :calendar_spiral: Deadline: {demand.Deadline} :calendar_spiral: Success: {(demand.WasSuccessful ? ":green_circle:" : ":red_circle:")}");
                if (closedSb.CanFit(tempSb.ToString()))
                    closedSb.Append(tempSb.ToString());
                else
                    unlistedClosed++;
            }
            sb.Append(closedSb.ToString());
            if (unlistedClosed > 0)
                sb.AppendLine($"# {unlistedClosed} hidden due to message length");
        }
        if (!string.IsNullOrEmpty(id)){
            builder.AddRow(new ActionRowBuilder()
                    .WithButton("New Demand", $"new-demand({id})", style: ButtonStyle.Success, disabled: demands.Length >= 25)
                    .WithButton("Edit Demand", $"edit-demand({id})", style: ButtonStyle.Primary, disabled: !hasDemands)
                    .WithButton("Activate Demand", $"open-demand({id})", style: ButtonStyle.Primary, disabled: !hasDemands || !demands.Any(d => !d.IsActive))
                    .WithButton("Inactivate Demand", $"close-demand({id})", style: ButtonStyle.Primary, disabled: !hasDemands || !demands.Any(d => d.IsActive))
                    .WithButton("Remove Demand", $"remove-demand({id})", style: ButtonStyle.Danger, disabled: !hasDemands));
        }
        builder.AddRow(new ActionRowBuilder()
                .WithButton("Back", $"manage-team-menu({id})", style: ButtonStyle.Secondary)
                .WithButton("Close", "close", style: ButtonStyle.Danger));
        await component.FollowupAsync(sb.ToString(), ephemeral: true, components: builder.Build());
    }

    private async Task ManageTeamMenuMessage(SocketInteraction component, string id)
    {
        await DismissMessage(component);
        DiscordStringBuilder sb = new();
        string coachId = null;
        if (component is SocketMessageComponent) {
          coachId = ((SocketMessageComponent)component).Data.Values?.FirstOrDefault() ?? id;
        }
        else {
          coachId = id;
        }
        dataServices[component.GuildId.Value].TryGetTeam(Convert.ToUInt64(coachId), out TeamInfo? team);
        sb.AppendLine($"# Manage Team - {team.TeamName}");
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
        await component.FollowupAsync(sb.ToString(), ephemeral: true, components: new ComponentBuilder()
                .AddRow(new ActionRowBuilder()
                    .WithButton("Manage Demands", $"manage-team-demands({coachId})")
                    .WithButton("Update Text Note", $"manage-team-text({coachId})"))
                    //.WithButton("Manage CAP", "manage-cap")
                    //.WithButton("Manage Coach", "manage-user"))
                .AddRow(new ActionRowBuilder()
                    .WithButton("Back", $"manage-team-selection({team.Division})", style: ButtonStyle.Secondary)
                    .WithButton("Close", "close", style: ButtonStyle.Danger))
                .Build());
    }

    private async Task ManageTeamsDivMessage(SocketInteraction component)
    {
        await DismissMessage(component);
        DiscordStringBuilder sb = new();
        sb.AppendLine($"# Manage Teams");
        sb.AppendLine($"Select the div of team that you would like to manage");
        ComponentBuilder cb = new ComponentBuilder();
        ActionRowBuilder arb = new ActionRowBuilder();
        List<KeyValuePair<ulong, TeamInfo>> teams = GetTeamsSortedByDivision(component.GuildId.Value);
        foreach(var div in teams.Select(kvp => kvp.Value.Division).Distinct()){
          arb.WithButton($"Div{div}", $"manage-team-selection({div})", style: ButtonStyle.Primary);
        }
        cb.AddRow(arb);
        cb.AddRow(new ActionRowBuilder()
            .WithButton("Back", "open-menu", style: ButtonStyle.Secondary)
            .WithButton("Close", "close", style: ButtonStyle.Danger));
        await component.FollowupAsync(sb.ToString(), ephemeral: true, components: cb.Build());
    }
    private async Task ManageTeamsMessage(SocketInteraction component, int div)
    {
        await DismissMessage(component);
        DiscordStringBuilder sb = new();
        sb.AppendLine($"# Manage Teams");
        sb.AppendLine($"Select the team that you would like to manage from the list below");
        SelectMenuBuilder smBuilder = new SelectMenuBuilder()
            .WithPlaceholder("Select a team")
            .WithCustomId($"manage-team-menu")
            .WithMinValues(1)
            .WithMaxValues(1);
        List<KeyValuePair<ulong, TeamInfo>> teams = GetTeamsSortedByDivision(component.GuildId.Value).Where(kvp => kvp.Value.Division == div).ToList();
        foreach(var kvp in teams){
            TeamInfo team = kvp.Value;
            SocketGuildUser user = GetUser(component.GuildId.Value, kvp.Key);
            smBuilder.AddOption(team.TeamName, kvp.Key.ToString(), $"Div {team.Division} - {user.Username}");
        }
        await component.FollowupAsync(sb.ToString(), ephemeral: true, components: new ComponentBuilder()
                .AddRow(new ActionRowBuilder()
                    .WithSelectMenu(smBuilder))
                //.WithButton("New team", "new-team", style: ButtonStyle.Success))
                .AddRow(new ActionRowBuilder()
                        .WithButton("Back", "manage-team-div", style: ButtonStyle.Secondary)
                        .WithButton("Close", "close", style: ButtonStyle.Danger))
                .Build());
    }

    private async Task OpenMenuMessage(SocketInteraction component)
    {
        await DismissMessage(component);
        (string title, MessageComponent mc) = GenerateStartMenu(component.User.GlobalName ?? component.User.Username);
        await component.FollowupAsync(title, components: mc, ephemeral: true);
    }

    private static async Task NewTeamMessage(SocketInteraction component)
    {
        DiscordStringBuilder sb = new();
        sb.AppendLine($"# New Team");
        await component.RespondWithModalAsync(new ModalBuilder()
                .WithTitle(sb.ToString())
                .WithCustomId("new-team-modal")
                .AddTextInput(new TextInputBuilder()
                    .WithLabel("Team Name")
                    .WithRequired(true)
                    .WithCustomId("team-name"))
                .AddTextInput(new TextInputBuilder()
                    .WithLabel("Description")
                    .WithCustomId("team-description")
                    .WithRequired(false)
                    .WithStyle(style: TextInputStyle.Paragraph))
                .Build());
    }
}
