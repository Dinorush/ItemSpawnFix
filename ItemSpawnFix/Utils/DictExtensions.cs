using System.Collections.Generic;

namespace ItemSpawnFix.Utils
{
    internal static class DictExtensions
    {
        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key) where TValue : new()
        {
            if (!dict.TryGetValue(key, out TValue? value))
                dict.Add(key, value = new());
            return value;
        }
    }
}
