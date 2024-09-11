using System.Text;

namespace BloodBowl3;

public static class Extensions
{
    public static int ParseInt(this string str)
    {
        return int.Parse(str);
    }

    private static readonly Encoding encoding = new UTF8Encoding(false);

    public static string FromBase64(this string base64)
    {
        return encoding.GetString(Convert.FromBase64String(base64));
    }
}
