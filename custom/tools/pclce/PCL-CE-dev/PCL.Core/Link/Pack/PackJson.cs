using System.Text.Json;
using System.Text.Json.Serialization;
using PCL.Core.Utils;

namespace PCL.Core.Link.Pack;

internal static class PackJson
{
    public static readonly JsonSerializerOptions Options = new(JsonCompat.SerializerOptions)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
