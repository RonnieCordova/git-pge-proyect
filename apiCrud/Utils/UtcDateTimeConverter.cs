using System.Buffers;
using System.Buffers.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace utils;

public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            if (DateTime.TryParse(reader.GetString(), out DateTime date))
            {
                // Al leer una fecha, especifica que es UTC para evitar conversiones
                return DateTime.SpecifyKind(date, DateTimeKind.Utc);
            }
        }
        return reader.GetDateTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        // Al escribir una fecha, asegúrate de que se mantenga como UTC
        writer.WriteStringValue(value.ToUniversalTime().ToString("o"));
    }
}