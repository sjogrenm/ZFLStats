using Discord.WebSocket;

namespace ZFLBot;

internal static class DiscordExtensions
{
    public static SocketSlashCommandDataOption? GetOption(this SocketSlashCommandDataOption cmd, string name)
    {
        return cmd.Options.FirstOrDefault(o => o.Name == name);
    }
    
    public static SocketMessageComponentData GetById(this IReadOnlyCollection<SocketMessageComponentData> list, string customId) {
      return list.Where(c => c.CustomId.Equals(customId, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
    }

    public static string Truncate(this string text, int maxLength)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }

        return text[..(maxLength - 3)] + "...";
    }
}
