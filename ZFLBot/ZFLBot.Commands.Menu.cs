using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace ZFLBot;

internal partial class ZFLBot
{
  private void SetupMenuHandlers() {
    this.client.ModalSubmitted += this.AdminMenuModalHandler;
    this.client.ButtonExecuted += this.AdminMenuHandler;
    this.client.SelectMenuExecuted += this.AdminMenuHandler;
  }

  private (string action, string[] ids) ParseIdFromAction(string CustomId){
    string pattern = @"([^\(\)]+)\(?([^\(\)]+)?\)?";
    Regex r = new Regex(pattern);
    Match m = r.Match(CustomId);
    string action = m.Groups[1].Value;
    string[] ids = m.Groups.Count > 2 ? m.Groups[2].Value.Split(';') : [];
    Debug.WriteLine($"Input: {CustomId}, Action: {action}, Ids: {JsonConvert.SerializeObject(ids)}");
    return (action, ids);
  }

  private async Task AdminMenuHandler(SocketMessageComponent component){
    (string action, string[] ids) = ParseIdFromAction(component.Data.CustomId);
    var guildId = component.GuildId.Value;
    var admin = component.User;
    switch (action)
    {
      case "open-menu":
        await OpenMenuMessage(component);
        break;
      case "manage-team-selection":
        await ManageTeamsMessage(component);
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
          await this.AuditLog(guildId, $"{admin.Username} ({admin.Id}) opened demand for {user?.Username} ({user?.Id})");
          dataServices[guildId].OpenDemand(userId, Convert.ToInt32(component.Data.Value));
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
          await this.AuditLog(guildId, $"{admin.Username} ({admin.Id}) removed demand for {user?.Username} ({user?.Id})");
          dataServices[guildId].RemoveDemand(userId, Convert.ToInt32(component.Data.Value));
          await ManageTeamDemandMessage(component, ids.FirstOrDefault());
          break;
      }
      case "new-team":
        await NewTeamMessage(component);
        break;
      case "cancel":
        await DismissMessage(component);
        break;
    }
  }

  private async Task AdminMenuModalHandler(SocketModal modal) {
    Debug.WriteLine(JsonConvert.SerializeObject(modal.Data));
    (string action, string[] ids) = ParseIdFromAction(modal.Data.CustomId);
    var guildId = modal.GuildId.Value;
    var admin = modal.User;
    IReadOnlyCollection<SocketMessageComponentData> components = modal.Data.Components;
    SocketMessageComponentData title = components.GetById("demand_title");
    SocketMessageComponentData deadline = components.GetById("demand_deadline");
    SocketMessageComponentData description = components.GetById("demand_description");
    SocketMessageComponentData source = components.GetById("demand_source");
    SocketMessageComponentData progress = components.GetById("demand_progress");
    SocketMessageComponentData success = components.GetById("demand_success");
    switch(action){
      case "new-team-modal":
        await ManageTeamsMessage(modal);
        break;
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
          dataServices[modal.GuildId.Value].EditDemand(userId, Convert.ToInt32(ids.LastOrDefault()), title.Value, description.Value, deadline.Value, source.Value, progress.Value);
          await ManageTeamDemandMessage(modal, ids.FirstOrDefault());
          break;
      }
      case "close-demand-modal":
      {
          var userId = Convert.ToUInt64(ids.FirstOrDefault());
          var user = this.GetUser(guildId, userId);
          await this.AuditLog(guildId, $"{admin.Username} ({admin.Id}) closed demand for {user?.Username} ({user?.Id})");
          dataServices[modal.GuildId.Value].CloseDemand(userId, Convert.ToInt32(ids.LastOrDefault()), bool.Parse(success.Value));
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
    StringBuilder sb = new();
    sb.AppendLine($"# Welcome {username}!");
    sb.AppendLine($"What would you like to do?");
    return (sb.ToString(), new ComponentBuilder()
        .AddRow(new ActionRowBuilder()
          .WithButton("Manage Teams", "manage-team-selection")
          .WithButton("Get Status", "get-status")
          .WithButton("Roll Round", "roll-round"))
        .AddRow(new ActionRowBuilder()
          .WithButton("Cancel", "cancel", style: ButtonStyle.Danger))
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
    StringBuilder sb = new();
    string operationString = Enum.GetName(typeof(DemandOperation), operation);
    sb.AppendLine($"# {operationString} Demand");
    ComponentBuilder builder = new ComponentBuilder();
    SelectMenuBuilder smBuilder = new SelectMenuBuilder()
      .WithCustomId($"{operationString.ToLower()}-demand-selection({id})");
    Demand[] demands = dataServices[component.GuildId.Value].GetDemands(Convert.ToUInt64(id));
    for(int i = 0; i < demands.Length; i++){
      smBuilder.AddOption(demands[i].Title, i.ToString(), $"{(demands[i].IsActive ? "Active" : "Inactive")} - Progress: {demands[i].Progress}");
    }
    if (!string.IsNullOrEmpty(id)){
      builder.AddRow(new ActionRowBuilder()
          .WithSelectMenu(smBuilder));
    }
    builder.AddRow(new ActionRowBuilder()
        .WithButton("Back", $"manage-team-demands({id})", style: ButtonStyle.Secondary)
        .WithButton("Cancel", "cancel", style: ButtonStyle.Danger));
    await component.FollowupAsync(sb.ToString(), ephemeral: true, components: builder.Build());
  }

  private async Task EditDemandModal(SocketInteraction component, string id)
  {
    Demand[] demands = dataServices[component.GuildId.Value].GetDemands(Convert.ToUInt64(id));
    string demandId = ((SocketMessageComponent)component).Data.Values.FirstOrDefault();
    Demand demand = demands[int.Parse(demandId)];
    await component.RespondWithModalAsync(new ModalBuilder()
      .WithTitle("Edit Demand")
      .WithCustomId($"edit-demand-modal({id};{demandId})")
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

  private async Task CloseDemandModal(SocketInteraction component, string id)
  {
    Demand[] demands = dataServices[component.GuildId.Value].GetDemands(Convert.ToUInt64(id));
    string demandId = ((SocketMessageComponent)component).Data.Values.FirstOrDefault();
    Demand demand = demands[int.Parse(demandId)];
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
    StringBuilder sb = new();
    sb.AppendLine($"# Manage Demands");
    sb.AppendLine($"### Active Demands");
    foreach(Demand demand in demands.Where(d => d.IsActive))
      sb.AppendLine($"- **{demand.Title}**");
    sb.AppendLine($"### Inactive Demands");
    foreach(Demand demand in demands.Where(d => !d.IsActive))
      sb.AppendLine($"- **{demand.Title}**");
    if (!string.IsNullOrEmpty(id)){
      builder.AddRow(new ActionRowBuilder()
        .WithButton("New Demand", $"new-demand({id})", style: ButtonStyle.Success)
        .WithButton("Edit Demand", $"edit-demand({id})", style: ButtonStyle.Primary, disabled: !hasDemands)
        .WithButton("Open Demand", $"open-demand({id})", style: ButtonStyle.Primary, disabled: !hasDemands)
        .WithButton("Close Demand", $"close-demand({id})", style: ButtonStyle.Primary, disabled: !hasDemands)
        .WithButton("Remove Demand", $"remove-demand({id})", style: ButtonStyle.Danger, disabled: !hasDemands));
    }
    builder.AddRow(new ActionRowBuilder()
        .WithButton("Back", $"manage-team-menu({id})", style: ButtonStyle.Secondary)
        .WithButton("Cancel", "cancel", style: ButtonStyle.Danger));
    await component.FollowupAsync(sb.ToString(), ephemeral: true, components: builder.Build());
  }

  private async Task ManageTeamMenuMessage(SocketInteraction component, string id)
  {
    await DismissMessage(component);
    StringBuilder sb = new();
    string coachId = ((SocketMessageComponent)component).Data.Values?.FirstOrDefault() ?? id;
    Debug.WriteLine(JsonConvert.SerializeObject(((SocketMessageComponent)component).Data.Values));
    sb.AppendLine($"# Manage Team");
    await component.FollowupAsync(sb.ToString(), ephemeral: true, components: new ComponentBuilder()
        .AddRow(new ActionRowBuilder()
          .WithButton("Manage Demands", $"manage-team-demands({coachId})")
          .WithButton("Manage CAP", "manage-cap")
          .WithButton("Manage Coach", "manage-user"))
        .AddRow(new ActionRowBuilder()
          .WithButton("Back", "manage-team-selection", style: ButtonStyle.Secondary)
          .WithButton("Cancel", "cancel", style: ButtonStyle.Danger))
        .Build());
  }

  private async Task ManageTeamsMessage(SocketInteraction component)
  {
    await DismissMessage(component);
    StringBuilder sb = new();
    sb.AppendLine($"# Manage Teams");
    List<KeyValuePair<ulong, TeamInfo>> teams = GetTeamsSortedByDivision(component.GuildId.Value);
    SelectMenuBuilder smBuilder = new SelectMenuBuilder()
          .WithPlaceholder("Select a team")
          .WithCustomId($"manage-team-menu")
          .WithMinValues(1)
          .WithMaxValues(1);
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
          .WithButton("Back", "open-menu", style: ButtonStyle.Secondary)
          .WithButton("Cancel", "cancel", style: ButtonStyle.Danger))
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
    StringBuilder sb = new();
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
