using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIHappey.ChatCompletions.Models;

public sealed class ChatCompletionAudio
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = default!;

    [JsonPropertyName("expires_at")]
    public long ExpiresAt { get; set; }

    [JsonPropertyName("data")]
    public string Data { get; set; } = default!;

    [JsonPropertyName("transcript")]
    public string Transcript { get; set; } = default!;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public sealed class ChatCompletionWebSearchOptions
{
    [JsonPropertyName("search_context_size")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SearchContextSize { get; set; }

    [JsonPropertyName("user_location")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ChatCompletionWebSearchUserLocation? UserLocation { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public sealed class ChatCompletionWebSearchUserLocation
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "approximate";

    [JsonPropertyName("approximate")]
    public ChatCompletionWebSearchLocation Approximate { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public sealed class ChatCompletionWebSearchLocation
{
    [JsonPropertyName("city")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? City { get; set; }

    [JsonPropertyName("country")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Country { get; set; }

    [JsonPropertyName("region")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Region { get; set; }

    [JsonPropertyName("timezone")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Timezone { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public sealed class ChatCompletionPromptCacheOptions
{
    [JsonPropertyName("mode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Mode { get; set; }

    [JsonPropertyName("ttl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Ttl { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public sealed class ChatCompletionPromptCacheBreakpoint
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "explicit";

    [JsonPropertyName("ttl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Ttl { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public sealed class ChatCompletionModerationOptions
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = default!;

    [JsonPropertyName("policy")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ChatCompletionModerationPolicy? Policy { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public sealed class ChatCompletionModerationPolicy
{
    [JsonPropertyName("input")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ChatCompletionModerationConfig? Input { get; set; }

    [JsonPropertyName("output")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ChatCompletionModerationConfig? Output { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public sealed class ChatCompletionModerationConfig
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = default!;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public sealed class ChatCompletionModeration
{
    [JsonPropertyName("input")]
    public ChatCompletionModerationOutcome Input { get; set; } = default!;

    [JsonPropertyName("output")]
    public ChatCompletionModerationOutcome Output { get; set; } = default!;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

[JsonConverter(typeof(ChatCompletionModerationOutcomeConverter))]
public abstract class ChatCompletionModerationOutcome;

public sealed class ChatCompletionModerationResults : ChatCompletionModerationOutcome
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "moderation_results";

    [JsonPropertyName("model")]
    public string Model { get; set; } = default!;

    [JsonPropertyName("results")]
    public IEnumerable<ChatCompletionModerationResult> Results { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public sealed class ChatCompletionModerationResult
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "moderation_result";

    [JsonPropertyName("model")]
    public string Model { get; set; } = default!;

    [JsonPropertyName("flagged")]
    public bool Flagged { get; set; }

    [JsonPropertyName("categories")]
    public Dictionary<string, bool> Categories { get; set; } = [];

    [JsonPropertyName("category_scores")]
    public Dictionary<string, double> CategoryScores { get; set; } = [];

    [JsonPropertyName("category_applied_input_types")]
    public Dictionary<string, string[]> CategoryAppliedInputTypes { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public sealed class ChatCompletionModerationError : ChatCompletionModerationOutcome
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "error";

    [JsonPropertyName("code")]
    public string Code { get; set; } = default!;

    [JsonPropertyName("message")]
    public string Message { get; set; } = default!;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public sealed class ChatCompletionUnknownModerationOutcome : ChatCompletionModerationOutcome
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalProperties { get; set; } = [];
}

public sealed class ChatCompletionModerationOutcomeConverter
    : ChatCompletionDiscriminatedConverter<ChatCompletionModerationOutcome>
{
    protected override Type VariantType(string discriminator) => discriminator switch
    {
        "moderation_results" => typeof(ChatCompletionModerationResults),
        "error" => typeof(ChatCompletionModerationError),
        _ => typeof(ChatCompletionUnknownModerationOutcome)
    };
}
