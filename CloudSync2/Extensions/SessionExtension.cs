using Newtonsoft.Json;

namespace CloudSync2.Extensions;
public static class SessionExtensions
{
    public static void Set<T>(this ISession session, string key, T value)
    {
        session.SetString(key, JsonConvert.SerializeObject(value));
    }

    public static T Get<T>(this ISession session, string key)
    {
        var value = session.GetString(key);
        return value == null ? default(T) : JsonConvert.DeserializeObject<T>(value);
    }

    public static void SetByteArray(this ISession session, string key, byte[] value)
    {
        session.Set(key, value);
    }

    public static byte[] GetByteArray(this ISession session, string key)
    {
        session.TryGetValue(key, out var value);
        return value;
    }
}
