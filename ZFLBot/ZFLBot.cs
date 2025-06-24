using System.Diagnostics;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace ZFLBot;

internal partial class ZFLBot
{
    private const ulong Bazik = 74168571369881600;

    private const ulong Harringzord = 149222835850706944;

    private readonly Dictionary<ulong, IDataService> dataServices;

    private readonly Dictionary<string, (SlashCommandProperties Properties, Func<SocketSlashCommand, Task> Handler)> commands = [];

    private readonly DiscordSocketClient client = new(new DiscordSocketConfig { GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers });

    public ZFLBot(Dictionary<ulong, IDataService> dataServices)
    {
        this.dataServices = dataServices;
        this.client.Log += Log;
        this.client.Ready += this.ClientReady;
        this.client.ButtonExecuted += this.ButtonExecuted;
        this.client.SlashCommandExecuted += this.SlashCommandHandler;

        this.AddCommands();
    }

    private SocketGuild? GetGuild(ulong id) => this.client.Guilds.FirstOrDefault(g => g.Id == id);

    private async Task SlashCommandHandler(SocketSlashCommand arg)
    {
        if (!arg.GuildId.HasValue)
        {
            Log("Command run outside guild?");
            return;
        }

        if (this.commands.TryGetValue(arg.CommandName, out var cmd))
        {
            await cmd.Handler(arg);
        }
        else
        {
            Log("Unknown command");
        }
    }

    private bool IsAdmin(SocketGuildUser user)
    {
        if (user.Id is Bazik or Harringzord)
        {
            return true;
        }

        var guildId = user.Guild.Id;
        var adminRole = this.dataServices[guildId].AdminRole;
        return user.Roles.Any(r => r.Id == adminRole);
    }

    private Task ClientReady()
    {
        _ = this.UpdateCommandsAsync();
        foreach (var guild in this.client.Guilds)
        {
            _ = guild.DownloadUsersAsync();
        }

        return Task.CompletedTask;
    }

    private Task ButtonExecuted(SocketMessageComponent arg)
    {
        if (arg.Data.CustomId.StartsWith("admin.rollover."))
        {
            var user = (SocketGuildUser)arg.User;
            Debug.Assert(this.IsAdmin(user));
            return this.CommitRollover(user, arg);
        }

        return Task.CompletedTask;
    }

    private async Task UpdateCommandsAsync()
    {
        foreach (var guildId in this.dataServices.Keys)
        {
            var oldCommands = (await this.client.Rest.GetGuildApplicationCommands(guildId)).ToList();
            foreach (var cmd in oldCommands)
            {
                await cmd.DeleteAsync();
            }

            foreach (var cmd in this.commands.Values)
            {
                try
                {
                    await this.client.Rest.CreateGuildCommand(cmd.Properties, guildId);
                }
                catch (HttpException ex)
                {
                    var json = JsonConvert.SerializeObject(ex.Errors, Formatting.Indented);
                    Console.Write(json);
                }
            }
        }
    }

    private void SetAuditChannel(ulong guildId, ulong channelId)
    {
        this.dataServices[guildId].AuditChannel = channelId;
    }

    private void SetAdminRole(ulong guildId, ulong roleId)
    {
        this.dataServices[guildId].AdminRole = roleId;
    }

    private void SetStatusChannel(ulong guildId, int div, ulong channelId)
    {
        this.dataServices[guildId].SetStatusChannel(div, channelId);
    }

    private async Task AuditLog(ulong guildId, string message)
    {
        Log(message);
        var channel = this.GetAuditChannel(guildId);
        if (channel != null)
        {
            await channel.SendMessageAsync(message);
        }
    }

    private SocketTextChannel? GetStatusChannel(ulong guildId, int div) => this.GetChannel(guildId, this.dataServices[guildId].GetStatusChannel(div));

    private SocketTextChannel? GetAuditChannel(ulong guildId) => this.GetChannel(guildId, this.dataServices[guildId].AuditChannel);

    private SocketTextChannel? GetChannel(ulong guildId, ulong channelId) => this.GetGuild(guildId)?.TextChannels.FirstOrDefault(ch => ch.Id == channelId);

    private SocketGuildUser? GetUser(ulong guildId, ulong userId) => this.GetGuild(guildId)?.Users?.FirstOrDefault(u => u.Id == userId);

    private static Task Log(LogMessage arg)
    {
        Console.WriteLine(arg.ToString());
        return Task.CompletedTask;
    }

    private static void Log(string message)
    {
        Console.WriteLine("{0:HH}:{0:mm}:{0:ss} ZFLBot    {1}", DateTime.Now, message);
    }

    public async Task Start(string token)
    {
        await this.client.LoginAsync(TokenType.Bot, token);
        await this.client.StartAsync();
    }
}
