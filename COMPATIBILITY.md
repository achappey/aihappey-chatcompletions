# Chat Completions compatibility and migration

This library contains standalone JSON contracts, not an API client or tool
execution engine. This second audit compared the create request, complete response
and streaming chunk with OpenAI's published OpenAPI specification on 2026-11-01.
No provider, gateway, SSE parser, network calls or new package dependencies were
added.

## Implemented improvements

| Surface | Capabilities now exposed |
| --- | --- |
| Requests | Typed tool definitions, tool choice, response format, stop sequences and prediction content instead of `object`. |
| Messages | Typed string/content-part union, including text, image, input audio, file and the previously missing assistant refusal part. Omitted content and explicit JSON null remain distinct. |
| Tools | Function/custom definitions and calls deserialize into their respective DTOs. Custom calls expose `custom.name` and free-form `custom.input`, without inventing a `function` payload. |
| Tool choice | String modes, named function/custom tools and `allowed_tools` mode/subset selection. The legacy `ToolChoice` string facade reads and replaces the same backing value as `ToolChoiceOption`. |
| Structured outputs | `text`, `json_object` and `json_schema` deserialize as typed variants; schema name, description, strictness and arbitrary JSON Schema remain accessible. |
| Audio | Output voices support built-in names and `{ "id": "voice_1234" }` custom references. Full audio responses expose ID, expiry, base64 data and transcript separately from the request's previous-audio ID reference and partial streaming audio. |
| Web search | Search context size and approximate city/country/region/timezone options, plus typed URL citations. |
| Reasoning | Open, string-valued `ChatCompletionReasoningEffort` with named `None`, `Minimal`, `Low`, `Medium`, `High`, `XHigh` and `Max` values. Future strings survive roundtrips. |
| Caching/safety | Safety identifier, prompt-cache key, legacy retention, request cache options, per-content-part cache breakpoints and prompt `cache_write_tokens`. |
| Moderation | Inline request model and score/block policies; complete/streamed input and output outcomes discriminate successful moderation results from errors. Categories and scores remain extensible dictionaries. |
| Responses/streaming | Choices, deltas and usage are now wired to existing typed DTOs. Partial tool arguments, audio expiry-only deltas, obfuscation and the terminal empty-choices usage chunk remain supported. |
| Metadata/legacy | Metadata is correctly string-to-string. Deprecated `user`, `functions` and `function_call` request fields are exposed for existing integrations. |

Unknown object fields are preserved in `AdditionalProperties`, including custom
tool grammar/format fields that previously lost extensions. Unknown discriminated
tool definitions, choices, calls, formats, content parts, annotations and moderation
outcomes use explicit `ChatCompletionUnknown*` / `ChatMessageUnknownContentPart`
DTOs, preserving the original object without adding known-variant payloads.
Future voice objects without an ID use `ChatCompletionUnknownVoice` rather than
inventing a null custom-voice ID.
Converters reject malformed union shapes, such as numeric content, non-string
discriminators or null content parts, with `JsonException`.

Required nullable response members (`message.content`, complete-choice `logprobs`,
logprob `content`/`refusal` and token `bytes`) now serialize as JSON null rather than
being omitted, even when global serializer options omit nulls. Unset optional usage
detail counters are omitted rather than written as schema-invalid nulls.

## Breaking public API changes

Recompile consumers and replace anonymous objects or boxed `JsonElement` values
assigned to the following properties with the corresponding DTOs. Existing
concrete function/custom/format DTO names remain available.

| Public API | Previous type | New type / migration |
| --- | --- | --- |
| `ChatCompletion.Choices` | `IEnumerable<object>` | `IEnumerable<ChatCompletionChoice>`; access `Message`, `FinishReason` and `Logprobs` directly. |
| `ChatCompletionUpdate.Choices` | `IEnumerable<object>` | `IEnumerable<ChatCompletionStreamChoice>`; access `Delta` directly. |
| Both completion types' `Usage` | `object?` | `ChatCompletionUsage?`. |
| `ChatCompletionOptions.Tools` | `IEnumerable<object>` | `IEnumerable<ChatCompletionToolDefinition>` containing `ChatCompletionTool`, `ChatCompletionCustomTool` or an unknown DTO. |
| `ChatCompletionOptions.ToolChoiceOption` | `object?` | `ChatCompletionToolChoice?`; use a string, named-choice DTO or `ChatCompletionAllowedToolsChoice`. `ToolChoice` remains a string facade. |
| `ChatCompletionOptions.ResponseFormat` | `object?` | `ChatCompletionResponseFormat?`; use the existing concrete text/JSON-object/JSON-schema DTOs. |
| `ChatCompletionOptions.Stop` | `object?` | `ChatCompletionStop?`; strings and `string[]` convert implicitly; other sequences use its constructor. |
| `ChatMessage.Content` | `JsonElement` | `ChatMessageContent`; use `Text`, `Parts` and `Kind`, or `ToJsonElement()` for raw inspection. Valid existing `JsonElement` assignments still convert implicitly. |
| `ChatCompletionPrediction.Content` | `object` | `ChatMessageContent`; assign text or construct typed text parts. |
| `ChatMessage.ToolCalls` | `IEnumerable<object>?` | `IEnumerable<ChatCompletionToolCall>?`; pattern-match function/custom/unknown calls. |
| `ChatCompletionResponseMessage.ToolCalls` | `IEnumerable<ChatCompletionMessageToolCall>?` | `IEnumerable<ChatCompletionToolCall>?`; pattern-match before accessing `Function` or `Custom`. Existing function-call collections remain assignable through covariance. |
| `ChatCompletionResponseMessage.Annotations` | `IEnumerable<ChatCompletionAnnotation>?` | `IEnumerable<ChatCompletionMessageAnnotation>?`; pattern-match URL citations/unknown annotations. Existing citation collections remain assignable. |
| Request/completion `Metadata` | `Dictionary<string, object?>?` | `Dictionary<string, string>?`; convert values deliberately to strings. Numeric/object metadata was not an official API contract. |
| `ChatCompletionAudioOptions.Voice` | `string` | `ChatCompletionVoice`; string assignments remain valid, reads pattern-match built-in/custom/unknown voice DTOs. |
| `ChatCompletionOptions.ReasoningEffort` | `string?` | `ChatCompletionReasoningEffort?`; string assignments remain valid, read the underlying string with `ReasoningEffort?.Value`. |
| `ChatCompletionChoice.FinishReason` | `string?` | Non-nullable `string`, matching the complete-response schema. Streaming `FinishReason` remains nullable/optional. |

`ChatCompletionFunctionDefinition` now inherits the common legacy function
definition fields from `ChatCompletionLegacyFunctionDefinition`, and retains
`Strict` for modern function tools. Message/content/tool/format/citation DTOs now
inherit their respective union bases. These inheritance changes and changed
property signatures require recompilation, even where source assignments still
work.

Wire corrections also affect consumers that asserted omission of required null
response fields or explicit null optional usage counters. Request content remains
omitted when default/undefined; use `ChatMessageContent.Null` for explicit null.

```csharp
var request = new ChatCompletionOptions
{
    Model = "gpt-6-astra",
    Messages =
    [
        new ChatMessage { Role = "developer", Content = "You are a helpful assistant." },
        new ChatMessage
        {
            Role = "user",
            Content = new ChatMessageContentPart[]
            {
                new ChatMessageTextPart { Text = "Describe this image." },
                new ChatMessageImagePart
                {
                    ImageUrl = new() { Url = "https://example.com/image.png", Detail = "high" }
                }
            }
        }
    ],
    ToolChoiceOption = "auto",
    ReasoningEffort = ChatCompletionReasoningEffort.Medium,
    Metadata = new() { ["purpose"] = "example" }
};

var completion = JsonSerializer.Deserialize<ChatCompletion>(responseJson)!;
foreach (var call in completion.Choices.First().Message.ToolCalls ?? [])
{
    if (call is ChatCompletionMessageCustomToolCall custom)
        Console.WriteLine(custom.Custom.Input);
}
```

For raw JSON migration, deserialize a tool or format into its union base using
`JsonElement.Deserialize<ChatCompletionToolDefinition>()` or
`JsonElement.Deserialize<ChatCompletionResponseFormat>()`. Do not put known wire
field names in `AdditionalProperties` alongside their typed properties.

## Official sources and regression coverage

The authoritative schema revision is
[`c2aecc2fbe1ddff3e7500a0eaa5c2bc4c19c5318`](https://raw.githubusercontent.com/openai/openai-openapi/c2aecc2fbe1ddff3e7500a0eaa5c2bc4c19c5318/openapi.json)
(published 2026-10-31). Relevant components include
`CreateChatCompletionRequest`, `CreateModelResponseProperties`,
`ChatCompletionRequestMessage`, `ChatCompletionResponseMessage`,
`ChatCompletionMessageToolCalls`, `ChatCompletionToolChoiceOption`,
`VoiceIdsOrCustomVoice`, `PromptCacheOptionsParam`,
`PromptCacheBreakpointParam`, `ChatCompletionModeration` and `CompletionUsage`.

The official generated Python
[request contract](https://raw.githubusercontent.com/openai/openai-python/main/src/openai/types/chat/completion_create_params.py),
[message contract](https://raw.githubusercontent.com/openai/openai-python/main/src/openai/types/chat/chat_completion_message.py)
and [chunk contract](https://raw.githubusercontent.com/openai/openai-python/main/src/openai/types/chat/chat_completion_chunk.py)
were cross-checked. The current
[Chat Completions reference](https://developers.openai.com/api/reference/resources/chat/subresources/completions/methods/create)
and [function-calling guide](https://developers.openai.com/api/docs/guides/function-calling)
could not be fetched directly because their hosts did not resolve in the audit
environment; published official schemas, descriptions and examples were used
instead of unverified search summaries.

Regression tests include the published OpenAPI Functions response, Image input
request, Streaming delta examples and a token excerpt from the Logprobs example.
Additional synthetic schema-derived fixtures cover structured outputs, custom
tools/calls, audio, search, cache breakpoints, metadata, moderation, unknown
variants and malformed union inputs. They are not claims of live endpoint/model
support.

The unchanged .NET 11 projects build with zero warnings/errors, and all 59 tests
pass using SDK `11.0.100-rc.3.26522.148`. The SDK was installed from official release
metadata after the environment's preinstalled .NET 10 SDK could not target .NET 11.

```sh
dotnet build AIHappey.ChatCompletions.csproj
dotnet test tests/AIHappey.ChatCompletions.Tests/AIHappey.ChatCompletions.Tests.csproj
```

## Remaining scope limits

- `ChatMessage` remains a shared role envelope supporting developer, system, user,
  assistant, tool and deprecated function messages. Role-specific required fields
  and allowed content combinations are not enforced by separate message classes.
  Prediction content must contain text/text parts, even though the shared union
  can represent other message parts.
- String-valued model IDs, roles, finish reasons, service tiers, modalities, audio
  formats, grammar syntax and search/cache policy values stay open for forward
  compatibility. The DTO layer does not validate model-specific capability
  restrictions, numeric bounds, stop counts, metadata limits or JSON Schema rules.
- `allowed_tools.tools` deliberately retains arbitrary JSON objects, as specified.
  Function parameters and structured-output schemas retain `JsonElement`; this
  library does not attempt to implement a JSON Schema type system.
- The official stream tool-call chunk schema currently defines only function
  deltas. Custom streaming payloads, if introduced, survive via extension data;
  no speculative custom streaming contract was added.
- Stored-completion list/retrieve/update/delete endpoint envelopes, Responses API
  contracts, HTTP error envelopes, SSE framing and tool execution remain outside
  the create/completion/chunk scope.
