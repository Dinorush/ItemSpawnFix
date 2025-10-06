using System;

namespace ItemSpawnFix.Utils
{
    internal static class StringExtensions
    {
        public static T ToEnum<T>(this string? value, T defaultValue) where T : struct
        {
            if (string.IsNullOrEmpty(value)) return defaultValue;

            return Enum.TryParse(value.Replace(" ", null), true, out T result) ? result : defaultValue;
        }
    }
}
