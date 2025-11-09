using System.Text.Json.Serialization;

namespace Safe.Host.Extensions;

internal static class SafeJsonOptions
{
    public static readonly JsonStringEnumConverter EnumConverter = new(namingPolicy: null, allowIntegerValues: false);
}
