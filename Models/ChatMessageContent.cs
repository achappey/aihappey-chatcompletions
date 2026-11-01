
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIHappey.ChatCompletions.Models;

public enum ChatMessageContentKind
{
    Undefined,
    Null,
    Text,
    Parts
}

[JsonConverter(typeof(ChatMessageContentConverter))]
public readonly struct ChatMessageContent
{
    public ChatMessageContentKind Kind { get; }
    public string? Text { get; }
    public IReadOnlyList<ChatMessageContentPart>? Parts { get; }

    private ChatMessageContent(ChatMessageContentKind kind) => Kind = kind;

    public ChatMessageContent(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Kind = ChatMessageContentKind.Text;
        Text = text;
    }

    public ChatMessageContent(IEnumerable<ChatMessageContentPart> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        Kind = ChatMessageContentKind.Parts;
        Parts = parts.ToArray();
        if (Parts.Any(p => p is null))
            throw new ArgumentException("Content parts cannot contain null.", nameof(parts));
    }

    public static ChatMessageContent Null => new(ChatMessageContentKind.Null);
    public static implicit operator ChatMessageContent(string text) => new(text);
    public static implicit operator ChatMessageContent(ChatMessageContentPart[] parts) => new(parts);
    public static implicit operator ChatMessageContent(JsonElement value) =>
        value.ValueKind == JsonValueKind.Undefined
            ? default
            : value.Deserialize<ChatMessageContent>();

    public JsonElement ToJsonElement() => JsonSerializer.SerializeToElement(this);
}

[JsonConverter(typeof(ChatMessageContentPartConverter))]
public abstract class ChatMessageContentPart;

public sealed class ChatMessageRefusalPart : ChatMessageContentPart
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "refusal";

    [JsonPropertyName("refusal")]
    public string Refusal { get; set; } = default!;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public sealed class ChatMessageUnknownContentPart : ChatMessageContentPart
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalProperties { get; set; } = [];
}

public sealed class ChatMessageContentPartConverter
    : ChatCompletionDiscriminatedConverter<ChatMessageContentPart>
{
    protected override Type VariantType(string discriminator) => discriminator switch
    {
        "text" => typeof(ChatMessageTextPart),
        "image_url" => typeof(ChatMessageImagePart),
        "input_audio" => typeof(ChatMessageInputAudioPart),
        "file" => typeof(ChatMessageFilePart),
        "refusal" => typeof(ChatMessageRefusalPart),
        _ => typeof(ChatMessageUnknownContentPart)
    };
}

public sealed class ChatMessageContentConverter : JsonConverter<ChatMessageContent>
{
    public override bool HandleNull => true;

    public override ChatMessageContent Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Null => ChatMessageContent.Null,
            JsonTokenType.String => new(reader.GetString()!),
            JsonTokenType.StartArray => ReadParts(ref reader, options),
            _ => throw new JsonException("Expected message content to be a string, content-part array, or null.")
        };

    private static ChatMessageContent ReadParts(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var parts = JsonSerializer.Deserialize<ChatMessageContentPart[]>(ref reader, options)!;
        if (parts.Any(p => p is null))
            throw new JsonException("Content parts cannot contain null.");
        return new(parts);
    }

    public override void Write(Utf8JsonWriter writer, ChatMessageContent value, JsonSerializerOptions options)
    {
        switch (value.Kind)
        {
            case ChatMessageContentKind.Null:
                writer.WriteNullValue();
                break;
            case ChatMessageContentKind.Text:
                writer.WriteStringValue(value.Text);
                break;
            case ChatMessageContentKind.Parts:
                JsonSerializer.Serialize(writer, value.Parts, options);
                break;
            default:
                throw new JsonException("Undefined content must be omitted or explicitly set to null.");
        }
    }
}

public static class ChatMessageContentExtensions
{
    public static string? ToText(ChatMessageContent content) => content.Kind switch
    {
        ChatMessageContentKind.Text => content.Text,
        ChatMessageContentKind.Parts => string.Concat(
            content.Parts!.OfType<ChatMessageTextPart>().Select(p => p.Text)),
        _ => null
    };

    public static string? ToText(JsonElement content)
    {
        return content.ValueKind switch
        {
            JsonValueKind.String => content.GetString(),
            JsonValueKind.Array => string.Concat(
                content.EnumerateArray()
                       .Where(p => p.TryGetProperty("type", out var t) && t.GetString() == "text")
                       .Select(p => p.TryGetProperty("text", out var txt) ? txt.GetString() : "")
            ),
            JsonValueKind.Null => null,
            _ => content.GetRawText() // fallback
        };
    }
}
