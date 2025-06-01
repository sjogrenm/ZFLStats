namespace ZFLBot;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var token = Environment.GetEnvironmentVariable("ZFLBotToken") ?? throw new ArgumentException("ZFLBotToken env var not set");
        var dataServices = new Dictionary<ulong, IDataService>();
        foreach (var guild in args.Chunk(2))
        {
            if (guild.Length != 2)
            {
                Console.WriteLine("Must provide both guild id and db");
                return;
            }

            dataServices.Add(ulong.Parse(guild[0]), new JsonDataService(guild[1]));
        }

        var bot = new ZFLBot(dataServices);
        await bot.Start(token);
        await Task.Delay(-1);
    }
}
