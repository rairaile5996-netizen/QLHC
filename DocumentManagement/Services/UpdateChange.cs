using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocumentManagement.Services;

[JsonConverter(typeof(UpdateChangeJsonConverter))]
public sealed class UpdateChange
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "FileDocumentOutline";
    public string Color { get; set; } = "#2196F3";
}

public sealed class UpdateChangeJsonConverter : JsonConverter<UpdateChange>
{
    public override UpdateChange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString() ?? string.Empty;
            return new UpdateChange { Title = value, Description = value };
        }

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        return new UpdateChange
        {
            Title = root.TryGetProperty("title", out var title) ? title.GetString() ?? string.Empty : string.Empty,
            Description = root.TryGetProperty("description", out var description) ? description.GetString() ?? string.Empty : string.Empty,
            Icon = root.TryGetProperty("icon", out var icon) ? icon.GetString() ?? "FileDocumentOutline" : "FileDocumentOutline",
            Color = root.TryGetProperty("color", out var color) ? color.GetString() ?? "#2196F3" : "#2196F3"
        };
    }

    public override void Write(Utf8JsonWriter writer, UpdateChange value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("title", value.Title);
        writer.WriteString("description", value.Description);
        writer.WriteString("icon", value.Icon);
        writer.WriteString("color", value.Color);
        writer.WriteEndObject();
    }
}
