using Discord.WebSocket;

namespace ZFLBot;

internal static class DiscordExtensions
{
    public static SocketSlashCommandDataOption? GetOption(this SocketSlashCommandDataOption cmd, string name)
    {
        return cmd.Options.FirstOrDefault(o => o.Name == name);
    }
}