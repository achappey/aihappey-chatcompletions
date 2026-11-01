using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIHappey.ChatCompletions.Models;

[JsonConverter(typeof(ChatCompletionToolDefinitionConverter))]
public abstract class ChatCompletionToolDefinition;

public sealed class ChatCompletionUnknownTool : ChatCompletionToolDefinition
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalProperties { get; set; } = [];
}

[JsonConverter(typeof(ChatCompletionToolChoiceConverter))]
public abstract class ChatCompletionToolChoice
{
    public static implicit operator ChatCompletionToolChoice(string mode) =>
        new ChatCompletionToolChoiceMode { Mode = mode };
}

[JsonConverter(typeof(ChatCompletionToolChoiceConverter))]
public sealed class ChatCompletionToolChoiceMode : ChatCompletionToolChoice
{
    [JsonIgnore]
    public string Mode { get; set; } = "auto";
}

public sealed class ChatCompletionAllowedToolsChoice : ChatCompletionToolChoice
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "allowed_tools";

    [JsonPropertyName("allowed_tools")]
    public ChatCompletionAllowedTools AllowedTools { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public sealed class ChatCompletionAllowedTools
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "auto";

    // The specification deliberately leaves allowed tool references open-ended.
    [JsonPropertyName("tools")]
    public IEnumerable<JsonElement> Tools { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public sealed class ChatCompletionUnknownToolChoice : ChatCompletionToolChoice
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalProperties { get; set; } = [];
}

[JsonConverter(typeof(ChatCompletionResponseFormatConverter))]
public abstract class ChatCompletionResponseFormat;

public sealed class ChatCompletionUnknownResponseFormat : ChatCompletionResponseFormat
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalProperties { get; set; } = [];
}

[JsonConverter(typeof(ChatCompletionToolCallConverter))]
public abstract class ChatCompletionToolCall;

public sealed class ChatCompletionMessageCustomToolCall : ChatCompletionToolCall
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = default!;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "custom";

    [JsonPropertyName("custom")]
    public ChatCompletionCustomToolCallInput Custom { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public sealed class ChatCompletionCustomToolCallInput
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;

    [JsonPropertyName("input")]
    public string Input { get; set; } = default!;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public sealed class ChatCompletionUnknownToolCall : ChatCompletionToolCall
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalProperties { get; set; } = [];
}

[JsonConverter(typeof(ChatCompletionMessageAnnotationConverter))]
public abstract class ChatCompletionMessageAnnotation;

public sealed class ChatCompletionUnknownAnnotation : ChatCompletionMessageAnnotation
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalProperties { get; set; } = [];
}

[JsonConverter(typeof(ChatCompletionVoiceConverter))]
public abstract class ChatCompletionVoice
{
    public static implicit operator ChatCompletionVoice(string name) =>
        new ChatCompletionBuiltInVoice { Name = name };
}

[JsonConverter(typeof(ChatCompletionVoiceConverter))]
public sealed class ChatCompletionBuiltInVoice : ChatCompletionVoice
{
    [JsonIgnore]
    public string Name { get; set; } = default!;
}

public sealed class ChatCompletionCustomVoice : ChatCompletionVoice
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = default!;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public sealed class ChatCompletionUnknownVoice : ChatCompletionVoice
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalProperties { get; set; } = [];
}

[JsonConverter(typeof(ChatCompletionFunctionChoiceConverter))]
public sealed class ChatCompletionFunctionChoice
{
    public string? Mode { get; }
    public string? Name { get; }

    private ChatCompletionFunctionChoice(string? mode, string? name)
    {
        Mode = mode;
        Name = name;
    }

    public static ChatCompletionFunctionChoice Named(string name) => new(null, name);
    public static implicit operator ChatCompletionFunctionChoice(string mode) => new(mode, null);

    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

[JsonConverter(typeof(ChatCompletionStopConverter))]
public sealed class ChatCompletionStop
{
    public string? Sequence { get; }
    public IReadOnlyList<string>? Sequences { get; }

    public ChatCompletionStop(string sequence)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        Sequence = sequence;
    }

    public ChatCompletionStop(IEnumerable<string> sequences)
    {
        ArgumentNullException.ThrowIfNull(sequences);
        Sequences = sequences.ToArray();
        if (Sequences.Any(s => s is null))
            throw new ArgumentException("Stop sequences cannot contain null.", nameof(sequences));
    }

    public static implicit operator ChatCompletionStop(string sequence) => new(sequence);
    public static implicit operator ChatCompletionStop(string[] sequences) => new(sequences);
}

[JsonConverter(typeof(ChatCompletionReasoningEffortConverter))]
public readonly record struct ChatCompletionReasoningEffort(string Value)
{
    public static ChatCompletionReasoningEffort None => new("none");
    public static ChatCompletionReasoningEffort Minimal => new("minimal");
    public static ChatCompletionReasoningEffort Low => new("low");
    public static ChatCompletionReasoningEffort Medium => new("medium");
    public static ChatCompletionReasoningEffort High => new("high");
    public static ChatCompletionReasoningEffort XHigh => new("xhigh");
    public static ChatCompletionReasoningEffort Max => new("max");

    public static implicit operator ChatCompletionReasoningEffort(string value) => new(value);
    public override string ToString() => Value;
}

public abstract class ChatCompletionDiscriminatedConverter<T> : JsonConverter<T> where T : class
{
    protected abstract Type VariantType(string discriminator);

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var value = document.RootElement;
        if (value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty("type", out var discriminator) ||
            discriminator.ValueKind != JsonValueKind.String)
        {
            throw new JsonException($"Expected a {typeof(T).Name} object with a string 'type'.");
        }

        return (T)value.Deserialize(VariantType(discriminator.GetString()!), options)!;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
}

public sealed class ChatCompletionToolDefinitionConverter
    : ChatCompletionDiscriminatedConverter<ChatCompletionToolDefinition>
{
    protected override Type VariantType(string discriminator) => discriminator switch
    {
        "function" => typeof(ChatCompletionTool),
        "custom" => typeof(ChatCompletionCustomTool),
        _ => typeof(ChatCompletionUnknownTool)
    };
}

public sealed class ChatCompletionResponseFormatConverter
    : ChatCompletionDiscriminatedConverter<ChatCompletionResponseFormat>
{
    protected override Type VariantType(string discriminator) => discriminator switch
    {
        "text" => typeof(ChatCompletionResponseFormatText),
        "json_object" => typeof(ChatCompletionResponseFormatJsonObject),
        "json_schema" => typeof(ChatCompletionResponseFormatJsonSchema),
        _ => typeof(ChatCompletionUnknownResponseFormat)
    };
}

public sealed class ChatCompletionToolCallConverter
    : ChatCompletionDiscriminatedConverter<ChatCompletionToolCall>
{
    protected override Type VariantType(string discriminator) => discriminator switch
    {
        "function" => typeof(ChatCompletionMessageToolCall),
        "custom" => typeof(ChatCompletionMessageCustomToolCall),
        _ => typeof(ChatCompletionUnknownToolCall)
    };
}

public sealed class ChatCompletionMessageAnnotationConverter
    : ChatCompletionDiscriminatedConverter<ChatCompletionMessageAnnotation>
{
    protected override Type VariantType(string discriminator) => discriminator switch
    {
        "url_citation" => typeof(ChatCompletionAnnotation),
        _ => typeof(ChatCompletionUnknownAnnotation)
    };
}

public sealed class ChatCompletionToolChoiceConverter
    : ChatCompletionDiscriminatedConverter<ChatCompletionToolChoice>
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert == typeof(ChatCompletionToolChoiceMode) || base.CanConvert(typeToConvert);

    protected override Type VariantType(string discriminator) => discriminator switch
    {
        "function" => typeof(ChatCompletionNamedToolChoice),
        "custom" => typeof(ChatCompletionNamedCustomToolChoice),
        "allowed_tools" => typeof(ChatCompletionAllowedToolsChoice),
        _ => typeof(ChatCompletionUnknownToolChoice)
    };

    public override ChatCompletionToolChoice Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return new ChatCompletionToolChoiceMode { Mode = reader.GetString()! };
        if (typeToConvert == typeof(ChatCompletionToolChoiceMode))
            throw new JsonException("Expected a tool choice mode string.");
        return base.Read(ref reader, typeToConvert, options);
    }

    public override void Write(
        Utf8JsonWriter writer, ChatCompletionToolChoice value, JsonSerializerOptions options)
    {
        if (value is ChatCompletionToolChoiceMode mode)
            writer.WriteStringValue(mode.Mode);
        else
            base.Write(writer, value, options);
    }
}

public sealed class ChatCompletionVoiceConverter : JsonConverter<ChatCompletionVoice>
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert == typeof(ChatCompletionBuiltInVoice) || base.CanConvert(typeToConvert);

    public override ChatCompletionVoice Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return new ChatCompletionBuiltInVoice { Name = reader.GetString()! };
        if (reader.TokenType != JsonTokenType.StartObject || typeToConvert == typeof(ChatCompletionBuiltInVoice))
            throw new JsonException("Expected a voice name or custom voice object.");
        using var document = JsonDocument.ParseValue(ref reader);
        var value = document.RootElement;
        if (!value.TryGetProperty("id", out var id))
            return value.Deserialize<ChatCompletionUnknownVoice>(options)!;
        if (id.ValueKind != JsonValueKind.String)
            throw new JsonException("Expected a custom voice ID string.");
        return value.Deserialize<ChatCompletionCustomVoice>(options)!;
    }

    public override void Write(Utf8JsonWriter writer, ChatCompletionVoice value, JsonSerializerOptions options)
    {
        if (value is ChatCompletionBuiltInVoice voice)
            writer.WriteStringValue(voice.Name);
        else
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}

public sealed class ChatCompletionFunctionChoiceConverter : JsonConverter<ChatCompletionFunctionChoice>
{
    public override ChatCompletionFunctionChoice Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return reader.GetString()!;
        using var document = JsonDocument.ParseValue(ref reader);
        var value = document.RootElement;
        if (value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String)
            throw new JsonException("Expected a function choice mode or an object with a string 'name'.");
        var result = ChatCompletionFunctionChoice.Named(name.GetString()!);
        result.AdditionalProperties = value.EnumerateObject()
            .Where(p => p.Name != "name").ToDictionary(p => p.Name, p => p.Value.Clone());
        return result;
    }

    public override void Write(
        Utf8JsonWriter writer, ChatCompletionFunctionChoice value, JsonSerializerOptions options)
    {
        if (value.Mode is not null)
        {
            writer.WriteStringValue(value.Mode);
            return;
        }
        writer.WriteStartObject();
        writer.WriteString("name", value.Name);
        if (value.AdditionalProperties is not null)
            foreach (var property in value.AdditionalProperties)
            {
                writer.WritePropertyName(property.Key);
                property.Value.WriteTo(writer);
            }
        writer.WriteEndObject();
    }
}

public sealed class ChatCompletionStopConverter : JsonConverter<ChatCompletionStop>
{
    public override ChatCompletionStop Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return new(reader.GetString()!);
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected a stop string or an array of strings.");
        var sequences = JsonSerializer.Deserialize<string[]>(ref reader, options)!;
        if (sequences.Any(s => s is null))
            throw new JsonException("Stop sequences cannot contain null.");
        return new(sequences);
    }

    public override void Write(Utf8JsonWriter writer, ChatCompletionStop value, JsonSerializerOptions options)
    {
        if (value.Sequences is not null)
            JsonSerializer.Serialize(writer, value.Sequences, options);
        else
            writer.WriteStringValue(value.Sequence);
    }
}

public sealed class ChatCompletionReasoningEffortConverter : JsonConverter<ChatCompletionReasoningEffort>
{
    public override ChatCompletionReasoningEffort Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
            ? new(reader.GetString()!)
            : throw new JsonException("Expected a reasoning effort string.");

    public override void Write(
        Utf8JsonWriter writer, ChatCompletionReasoningEffort value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
