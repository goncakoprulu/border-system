using System.Text.Json;
using System.Text.Json.Serialization;

namespace Border.Api.Serialization;

public sealed class DayOfWeekJsonConverter : JsonConverter<DayOfWeek>
{
    public override DayOfWeek Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var numeric) && Enum.IsDefined((DayOfWeek)numeric))
            return (DayOfWeek)numeric;

        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (int.TryParse(value, out numeric) && Enum.IsDefined((DayOfWeek)numeric)) return (DayOfWeek)numeric;
            if (Enum.TryParse<DayOfWeek>(value, true, out var named) && Enum.IsDefined(named)) return named;
        }

        throw new JsonException("DayOfWeek 0 ile 6 arasında sayısal bir değer olmalıdır.");
    }

    public override void Write(Utf8JsonWriter writer, DayOfWeek value, JsonSerializerOptions options) =>
        writer.WriteNumberValue((int)value);
}
