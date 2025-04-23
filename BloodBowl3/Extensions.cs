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

    public static void ForEach<T>(this IEnumerable<T> values, Action<T> action)
    {
        foreach (var item in values)
        {
            action(item);
        }
    }

    public static void AddOrUpdate<K, V>(this IDictionary<K, V> dict, K key, V value, Func<V, V> updater)
    {
        if (dict.TryGetValue(key, out var origValue))
        {
            dict[key] = updater(origValue);
        }
        else
        {
            dict.Add(key, value);
        }
    }

    public static V AddOrGet<K, V>(this IDictionary<K, V> dict, K key, Func<V> valueF)
    {
        if (dict.TryGetValue(key, out var oldValue))
        {
            return oldValue;
        }

        var value = valueF();
        dict.Add(key, value);
        return value;
    }

    public static bool IsBigGuyNegaTrait(this RollType roll)
    {
        switch (roll)
        {
            case RollType.BoneHead:
            case RollType.ReallyStupid:
            case RollType.UnchannelledFury:
            case RollType.AnimalSavagery:
            case RollType.TakeRoot:
                return true;
            default:
                return false;
        }
    }
}