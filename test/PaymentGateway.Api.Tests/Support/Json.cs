using System.Text.Json;
using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Tests.Support;

public static class Json
{
    /// <summary>How the API serializes: camelCase, enums by name.</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}