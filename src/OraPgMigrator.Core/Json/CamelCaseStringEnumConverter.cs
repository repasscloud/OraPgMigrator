using System.Text.Json;
using System.Text.Json.Serialization;

namespace OraPgMigrator.Core.Json;

/// <summary>
/// AOT-safe camelCase string enum converter (generic <see cref="JsonStringEnumConverter{TEnum}"/>
/// requires no reflection, unlike the non-generic <see cref="JsonStringEnumConverter"/>).
/// </summary>
public sealed class CamelCaseStringEnumConverter<TEnum> : JsonStringEnumConverter<TEnum>
    where TEnum : struct, Enum
{
    public CamelCaseStringEnumConverter() : base(JsonNamingPolicy.CamelCase)
    {
    }
}
