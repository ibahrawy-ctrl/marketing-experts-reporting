using System.Text.Json;
using System.Text.Json.Serialization;

namespace Reporting.IntegrationTests;

/// <summary>قراءة JSON مع محوّل التعدادات النصية لمطابقة إعدادات الـAPI.</summary>
public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<T?> ReadAsync<T>(this HttpResponseMessage res)
        => JsonSerializer.Deserialize<T>(await res.Content.ReadAsStringAsync(), Options);
}
