using System.Text.Json;
using System.Text.Json.Serialization;
using AIHappey.ChatCompletions.Models;
using Xunit;

namespace AIHappey.ChatCompletions.Tests;

public class ChatCompletionContractTests
{
    // Official OpenAPI /chat/completions POST example: "Functions".
    [Fact]
    public void Official_function_response_exposes_typed_calls_and_usage()
    {
        const string json = """
            {
              "id": "chatcmpl-abc123",
              "object": "chat.completion",
              "created": 1699896916,
              "model": "gpt-6-astra",
              "choices": [
                {
                  "index": 0,
                  "message": {
                    "role": "assistant",
                    "content": null,
                    "tool_calls": [
                      {
                        "id": "call_abc123",
                        "type": "function",
                        "function": {
                          "name": "get_current_weather",
                          "arguments": "{\n\"location\": \"Boston, MA\"\n}"
                        }
                      }
                    ]
                  },
                  "logprobs": null,
                  "finish_reason": "tool_calls"
                }
              ],
              "usage": {
                "prompt_tokens": 82,
                "completion_tokens": 17,
                "total_tokens": 99,
                "completion_tokens_details": {
                  "reasoning_tokens": 0,
                  "accepted_prediction_tokens": 0,
                  "rejected_prediction_tokens": 0
                }
              }
            }
            """;

        var completion = JsonSerializer.Deserialize<ChatCompletion>(json)!;
        var choice = Assert.Single(completion.Choices);
        var call = Assert.IsType<ChatCompletionMessageToolCall>(Assert.Single(choice.Message.ToolCalls!));
        Assert.Equal("get_current_weather", call.Function.Name);
        Assert.Equal("{\n\"location\": \"Boston, MA\"\n}", call.Function.Arguments);
        Assert.Equal("tool_calls", choice.FinishReason);
        Assert.Equal(99, completion.Usage!.TotalTokens);
        Assert.Equal(0, completion.Usage.CompletionTokensDetails!.RejectedPredictionTokens);
        AssertJsonEqual(json, JsonSerializer.Serialize(completion));
    }

    // Official OpenAPI /chat/completions POST example: "Image input".
    [Fact]
    public void Official_image_request_exposes_typed_parts()
    {
        const string json = """
            {
              "model": "gpt-6-astra",
              "messages": [
                {
                  "role": "user",
                  "content": [
                    { "type": "text", "text": "What is in this image?" },
                    {
                      "type": "image_url",
                      "image_url": {
                        "url": "https://upload.wikimedia.org/wikipedia/commons/thumb/d/dd/Gfp-wisconsin-madison-the-nature-boardwalk.jpg/2560px-Gfp-wisconsin-madison-the-nature-boardwalk.jpg"
                      }
                    }
                  ]
                }
              ],
              "max_tokens": 300
            }
            """;

        var request = JsonSerializer.Deserialize<ChatCompletionOptions>(json)!;
        var content = Assert.Single(request.Messages).Content;
        Assert.Equal(ChatMessageContentKind.Parts, content.Kind);
        Assert.Equal("What is in this image?", Assert.IsType<ChatMessageTextPart>(content.Parts![0]).Text);
        Assert.EndsWith(".jpg", Assert.IsType<ChatMessageImagePart>(content.Parts[1]).ImageUrl.Url);
        Assert.Equal("What is in this image?", ChatMessageContentExtensions.ToText(content));
        AssertJsonEqual(JsonDocument.Parse(json).RootElement.GetProperty("messages").GetRawText(),
            JsonSerializer.SerializeToElement(request).GetProperty("messages").GetRawText());
    }

    // JSON payloads from the official OpenAPI "Streaming" SSE example.
    [Theory]
    [InlineData("""{"role":"assistant","content":""}""", null, "")]
    [InlineData("""{"content":"Hello"}""", null, "Hello")]
    [InlineData("{}", "stop", null)]
    public void Official_stream_deltas_roundtrip(string deltaJson, string? finishReason, string? text)
    {
        var json = $$"""
            {
              "id":"chatcmpl-123","object":"chat.completion.chunk","created":1694268190,
              "model":"gpt-6-astra","system_fingerprint":"fp_44709d6fcb",
              "choices":[{"index":0,"delta":{{deltaJson}},"logprobs":null,
                "finish_reason":{{JsonSerializer.Serialize(finishReason)}}}]
            }
            """;
        var update = JsonSerializer.Deserialize<ChatCompletionUpdate>(json)!;
        var choice = Assert.Single(update.Choices);
        Assert.Equal(text, choice.Delta.Content);
        Assert.Equal(finishReason, choice.FinishReason);
        Assert.Equal("fp_44709d6fcb", update.SystemFingerprint);
        AssertJsonEqual(deltaJson, JsonSerializer.Serialize(choice.Delta));
        var roundtrip = JsonSerializer.Deserialize<ChatCompletionUpdate>(JsonSerializer.Serialize(update))!;
        Assert.Equal(finishReason, Assert.Single(roundtrip.Choices).FinishReason);
    }

    [Theory]
    [InlineData("{}", ChatMessageContentKind.Undefined)]
    [InlineData("""{"content":null}""", ChatMessageContentKind.Null)]
    [InlineData("""{"content":""}""", ChatMessageContentKind.Text)]
    public void Message_content_distinguishes_omission_null_and_empty_text(
        string payload, ChatMessageContentKind kind)
    {
        var message = JsonSerializer.Deserialize<ChatMessage>(payload)!;
        message.Role = "assistant";
        Assert.Equal(kind, message.Content.Kind);
        var serialized = JsonSerializer.SerializeToElement(message);
        Assert.Equal(kind != ChatMessageContentKind.Undefined, serialized.TryGetProperty("content", out var content));
        if (kind == ChatMessageContentKind.Null)
            Assert.Equal(JsonValueKind.Null, content.ValueKind);
        if (kind == ChatMessageContentKind.Text)
            Assert.Equal("", content.GetString());
    }

    [Fact]
    public void All_message_roles_and_content_variants_are_accessible_without_raw_JSON()
    {
        var request = new ChatCompletionOptions
        {
            Model = "gpt-6-astra",
            Messages =
            [
                new ChatMessage { Role = "developer", Content = "You are a helpful assistant." },
                new ChatMessage { Role = "system", Content = "Answer concisely." },
                new ChatMessage
                {
                    Role = "user",
                    Content = new ChatMessageContentPart[]
                    {
                        new ChatMessageTextPart { Text = "Describe the inputs." },
                        new ChatMessageInputAudioPart
                        {
                            InputAudio = new() { Data = "UklGRg==", Format = "wav" }
                        },
                        new ChatMessageFilePart { File = new() { FileId = "file-123" } }
                    }
                },
                new ChatMessage
                {
                    Role = "assistant",
                    Content = new ChatMessageContentPart[]
                    {
                        new ChatMessageRefusalPart { Refusal = "I cannot help with that request." }
                    },
                    Audio = new() { Id = "audio_123" }
                },
                new ChatMessage { Role = "tool", ToolCallId = "call_123", Content = "Done." },
                new ChatMessage { Role = "function", Name = "legacy", Content = ChatMessageContent.Null }
            ]
        };
        var roundtrip = JsonSerializer.Deserialize<ChatCompletionOptions>(JsonSerializer.Serialize(request))!;
        var messages = roundtrip.Messages.ToArray();
        Assert.Equal(new[] { "developer", "system", "user", "assistant", "tool", "function" },
            messages.Select(m => m.Role));
        Assert.Equal("file-123", Assert.IsType<ChatMessageFilePart>(messages[2].Content.Parts![2]).File.FileId);
        Assert.Equal("I cannot help with that request.",
            Assert.IsType<ChatMessageRefusalPart>(Assert.Single(messages[3].Content.Parts!)).Refusal);
        Assert.Equal("audio_123", messages[3].Audio!.Id);
        Assert.Equal("call_123", messages[4].ToolCallId);
        Assert.Equal(ChatMessageContentKind.Null, messages[5].Content.Kind);
    }

    [Fact]
    public void Structured_output_function_tools_and_custom_grammar_deserialize_to_DTOs()
    {
        const string json = """
            {
              "model":"gpt-6-astra","messages":[],
              "tools":[
                {"type":"function","function":{"name":"get_weather","strict":true,
                  "parameters":{"type":"object","properties":{"location":{"type":"string"}},
                    "required":["location"],"additionalProperties":false}}},
                {"type":"custom","custom":{"name":"code","format":{"type":"grammar",
                  "grammar":{"syntax":"lark","definition":"start: \"ok\"","future":42},
                  "format_hint":"keep"}}}
              ],
              "response_format":{"type":"json_schema","json_schema":{
                "name":"answer","strict":true,"schema":{"type":"object",
                  "properties":{"answer":{"type":"string"}},"required":["answer"],
                  "additionalProperties":false}}},
              "tool_choice":{"type":"custom","custom":{"name":"code"}}
            }
            """;
        var request = JsonSerializer.Deserialize<ChatCompletionOptions>(json)!;
        var tools = request.Tools.ToArray();
        Assert.True(Assert.IsType<ChatCompletionTool>(tools[0]).Function.Strict);
        var custom = Assert.IsType<ChatCompletionCustomTool>(tools[1]);
        Assert.Equal("lark", custom.Custom.Format!.Grammar!.Syntax);
        Assert.Equal(42, custom.Custom.Format.Grammar.AdditionalProperties!["future"].GetInt32());
        var format = Assert.IsType<ChatCompletionResponseFormatJsonSchema>(request.ResponseFormat);
        Assert.True(format.JsonSchema.Strict);
        Assert.False(format.JsonSchema.Schema!.Value.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal("code", Assert.IsType<ChatCompletionNamedCustomToolChoice>(request.ToolChoiceOption).Custom.Name);
        var serialized = JsonSerializer.SerializeToElement(request);
        foreach (var field in new[] { "tools", "response_format", "tool_choice" })
            AssertJsonEqual(JsonDocument.Parse(json).RootElement.GetProperty(field).GetRawText(),
                serialized.GetProperty(field).GetRawText());
    }

    [Theory]
    [InlineData("""{"type":"text"}""", typeof(ChatCompletionResponseFormatText))]
    [InlineData("""{"type":"json_object"}""", typeof(ChatCompletionResponseFormatJsonObject))]
    public void Simple_response_formats_roundtrip(string json, Type expectedType)
    {
        var format = JsonSerializer.Deserialize<ChatCompletionResponseFormat>(json)!;
        Assert.Equal(expectedType, format.GetType());
        AssertJsonEqual(json, JsonSerializer.Serialize(format));
    }

    [Fact]
    public void Custom_tool_calls_do_not_invent_a_function_payload()
    {
        const string json = """
            {"role":"assistant","content":null,"tool_calls":[
              {"id":"call_custom","type":"custom","custom":{
                "name":"code","input":"print('hello world')","future":true}}]}
            """;
        var response = JsonSerializer.Deserialize<ChatCompletionResponseMessage>(json)!;
        var call = Assert.IsType<ChatCompletionMessageCustomToolCall>(Assert.Single(response.ToolCalls!));
        Assert.Equal("print('hello world')", call.Custom.Input);
        var serialized = JsonSerializer.SerializeToElement(response);
        Assert.False(serialized.GetProperty("tool_calls")[0].TryGetProperty("function", out _));
        AssertJsonEqual(json, serialized.GetRawText());

        var requestMessage = JsonSerializer.Deserialize<ChatMessage>(json)!;
        Assert.IsType<ChatCompletionMessageCustomToolCall>(Assert.Single(requestMessage.ToolCalls!));
        AssertJsonEqual(json, JsonSerializer.Serialize(requestMessage));
    }

    [Fact]
    public void Allowed_tools_choice_preserves_open_tool_references()
    {
        const string json = """
            {"type":"allowed_tools","allowed_tools":{"mode":"required","tools":[
              {"type":"function","function":{"name":"get_weather"}},
              {"type":"custom","custom":{"name":"code"},"future":true}
            ]}}
            """;
        var choice = Assert.IsType<ChatCompletionAllowedToolsChoice>(
            JsonSerializer.Deserialize<ChatCompletionToolChoice>(json));
        Assert.Equal("required", choice.AllowedTools.Mode);
        Assert.Equal(2, choice.AllowedTools.Tools.Count());
        AssertJsonEqual(json, JsonSerializer.Serialize<ChatCompletionToolChoice>(choice));
    }

    [Fact]
    public void Legacy_tool_choice_facade_replaces_previously_selected_objects()
    {
        var request = new ChatCompletionOptions
        {
            ToolChoiceOption = new ChatCompletionNamedToolChoice { Function = new() { Name = "get_weather" } }
        };
        Assert.Null(request.ToolChoice);
        request.ToolChoice = "required";
        Assert.Equal("required", Assert.IsType<ChatCompletionToolChoiceMode>(request.ToolChoiceOption).Mode);
        Assert.Equal("required", JsonSerializer.SerializeToElement(request).GetProperty("tool_choice").GetString());
        request.ToolChoiceOption = "auto";
        Assert.Equal("auto", request.ToolChoice);
        request.ToolChoice = null;
        Assert.Null(request.ToolChoiceOption);
        Assert.Equal("\"auto\"", JsonSerializer.Serialize(new ChatCompletionToolChoiceMode { Mode = "auto" }));
    }

    [Theory]
    [InlineData("\"alloy\"", typeof(ChatCompletionBuiltInVoice))]
    [InlineData("""{"id":"voice_1234","future":{"enabled":true}}""", typeof(ChatCompletionCustomVoice))]
    [InlineData("""{"future_voice":{"name":"example"}}""", typeof(ChatCompletionUnknownVoice))]
    public void Audio_voice_supports_names_and_custom_voice_objects(string voiceJson, Type expectedType)
    {
        var audio = JsonSerializer.Deserialize<ChatCompletionAudioOptions>(
            $$"""{"voice":{{voiceJson}},"format":"aac"}""")!;
        Assert.Equal(expectedType, audio.Voice.GetType());
        Assert.Equal("aac", audio.Format);
        AssertJsonEqual(voiceJson, JsonSerializer.SerializeToElement(audio).GetProperty("voice").GetRawText());
        Assert.Equal("\"marin\"", JsonSerializer.Serialize(new ChatCompletionBuiltInVoice { Name = "marin" }));
    }

    [Fact]
    public void Complete_audio_response_is_distinct_from_request_audio_reference()
    {
        const string json = """
            {"role":"assistant","content":null,"audio":{
              "id":"audio_123","expires_at":2200000000,"data":"UklGRg==",
              "transcript":"Hello!","future":{"codec":"wav"}}}
            """;
        var message = JsonSerializer.Deserialize<ChatCompletionResponseMessage>(json)!;
        Assert.Equal(2200000000L, message.Audio!.ExpiresAt);
        Assert.Equal("Hello!", message.Audio.Transcript);
        AssertJsonEqual(json, JsonSerializer.Serialize(message));

        var request = new ChatMessage { Role = "assistant", Audio = new() { Id = message.Audio.Id } };
        var reference = JsonSerializer.SerializeToElement(request).GetProperty("audio");
        Assert.Equal("audio_123", reference.GetProperty("id").GetString());
        Assert.False(reference.TryGetProperty("data", out _));
    }

    [Fact]
    public void Search_prompt_cache_and_safety_options_roundtrip()
    {
        const string json = """
            {
              "model":"gpt-6-astra","messages":[{"role":"user","content":[
                {"type":"text","text":"Reusable prefix",
                  "prompt_cache_breakpoint":{"mode":"explicit","ttl":"12h","future":true}}]}],
              "web_search_options":{"search_context_size":"high","user_location":{
                "type":"approximate","approximate":{"city":"San Francisco","country":"US",
                  "region":"California","timezone":"America/Los_Angeles","future":1}}},
              "safety_identifier":"safety-identifier-1234","prompt_cache_key":"prompt-cache-key-1234",
              "prompt_cache_retention":"24h","prompt_cache_options":{"mode":"explicit","ttl":"30m"},
              "metadata":{"purpose":"audit"},"user":"user-1234","reasoning_effort":"max"
            }
            """;
        var request = JsonSerializer.Deserialize<ChatCompletionOptions>(json)!;
        Assert.Equal("San Francisco", request.WebSearchOptions!.UserLocation!.Approximate.City);
        Assert.Equal("12h", Assert.IsType<ChatMessageTextPart>(
            Assert.Single(request.Messages.Single().Content.Parts!)).PromptCacheBreakpoint!.Ttl);
        Assert.Equal("30m", request.PromptCacheOptions!.Ttl);
        Assert.Equal("safety-identifier-1234", request.SafetyIdentifier);
        Assert.Equal("prompt-cache-key-1234", request.PromptCacheKey);
        Assert.Equal(ChatCompletionReasoningEffort.Max, request.ReasoningEffort);
        Assert.Equal("audit", request.Metadata!["purpose"]);
        var serialized = JsonSerializer.SerializeToElement(request);
        foreach (var field in JsonDocument.Parse(json).RootElement.EnumerateObject())
            AssertJsonEqual(field.Value.GetRawText(), serialized.GetProperty(field.Name).GetRawText());
    }

    [Theory]
    [InlineData("\"prefix\"")]
    [InlineData("""[{"type":"text","text":"prefix","future":1}]""")]
    public void Prediction_supports_text_and_typed_text_parts(string contentJson)
    {
        var prediction = JsonSerializer.Deserialize<ChatCompletionPrediction>(
            $$"""{"type":"content","content":{{contentJson}}}""")!;
        Assert.Equal("prefix", ChatMessageContentExtensions.ToText(prediction.Content));
        AssertJsonEqual(contentJson, JsonSerializer.SerializeToElement(prediction).GetProperty("content").GetRawText());
    }

    [Theory]
    [InlineData("\"END\"")]
    [InlineData("""["END","STOP"]""")]
    public void Stop_supports_both_official_shapes(string json)
    {
        var stop = JsonSerializer.Deserialize<ChatCompletionStop>(json)!;
        AssertJsonEqual(json, JsonSerializer.Serialize(stop));
    }

    [Theory]
    [InlineData("\"auto\"")]
    [InlineData("\"none\"")]
    [InlineData("""{"name":"get_weather","future":true}""")]
    public void Deprecated_function_selection_roundtrips(string json)
    {
        var choice = JsonSerializer.Deserialize<ChatCompletionFunctionChoice>(json)!;
        AssertJsonEqual(json, JsonSerializer.Serialize(choice));
        var request = new ChatCompletionOptions
        {
            FunctionCall = choice,
            Functions = [new ChatCompletionFunctionDefinition { Name = "get_weather" }]
        };
        var roundtrip = JsonSerializer.Deserialize<ChatCompletionOptions>(JsonSerializer.Serialize(request))!;
        Assert.Equal("get_weather", Assert.Single(roundtrip.Functions!).Name);
    }

    [Fact]
    public void Stream_final_usage_chunk_and_partial_audio_updates_remain_typed()
    {
        const string json = """
            {"id":"chatcmpl-123","object":"chat.completion.chunk","created":1694268190,
             "model":"gpt-6-astra","choices":[],"usage":{
               "prompt_tokens":10,"completion_tokens":5,"total_tokens":15,
               "prompt_tokens_details":{"cache_write_tokens":8,"cached_tokens":2,"future":1}},
             "obfuscation":"opaque","future":{"enabled":true}}
            """;
        var update = JsonSerializer.Deserialize<ChatCompletionUpdate>(json)!;
        Assert.Empty(update.Choices);
        Assert.Equal(15, update.Usage!.TotalTokens);
        Assert.Equal(8, update.Usage.PromptTokensDetails!.CacheWriteTokens);
        var roundtrip = JsonSerializer.Deserialize<ChatCompletionUpdate>(JsonSerializer.Serialize(update))!;
        Assert.Equal(8, roundtrip.Usage!.PromptTokensDetails!.CacheWriteTokens);
        Assert.True(roundtrip.AdditionalProperties!["future"].GetProperty("enabled").GetBoolean());

        var delta = JsonSerializer.Deserialize<ChatCompletionDelta>("""{"audio":{"expires_at":2200000000}}""")!;
        Assert.Equal(2200000000L, delta.Audio!.ExpiresAt);
        AssertJsonEqual("""{"audio":{"expires_at":2200000000}}""", JsonSerializer.Serialize(delta));
        var argumentsOnly = JsonSerializer.Deserialize<ChatCompletionDelta>(
            """{"tool_calls":[{"index":0,"function":{"arguments":"{\"location\":"}}]}""")!;
        Assert.Null(Assert.Single(argumentsOnly.ToolCalls!).Id);
        Assert.Equal("{\"location\":", Assert.Single(argumentsOnly.ToolCalls!).Function!.Arguments);
        var streamed = JsonSerializer.SerializeToElement(argumentsOnly).GetProperty("tool_calls")[0];
        Assert.False(streamed.TryGetProperty("id", out _));
        Assert.False(streamed.TryGetProperty("type", out _));
    }

    [Fact]
    public void Logprobs_keep_required_null_bytes_and_refusal()
    {
        const string json = """
            {"content":[{"token":" How","logprob":-0.000054669687,"bytes":[32,72,111,119],
              "top_logprobs":[{"token":"<|end|>","logprob":-10.953937,"bytes":null}]}],
             "refusal":null}
            """;
        var logprobs = JsonSerializer.Deserialize<ChatCompletionLogprobs>(json)!;
        var token = Assert.Single(logprobs.Content!);
        Assert.Equal(new[] { 32, 72, 111, 119 }, token.Bytes);
        Assert.Null(Assert.Single(token.TopLogprobs).Bytes);
        AssertJsonEqual(json, JsonSerializer.Serialize(logprobs));
    }

    [Fact]
    public void Required_nullable_response_fields_override_global_null_omission()
    {
        var options = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        var choice = new ChatCompletionChoice { FinishReason = "tool_calls" };
        var json = JsonSerializer.SerializeToElement(choice, options);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("logprobs").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("message").GetProperty("content").ValueKind);
        AssertJsonEqual("""{"content":null,"refusal":null}""",
            JsonSerializer.Serialize(new ChatCompletionLogprobs(), options));
        Assert.Equal(JsonValueKind.Null, JsonSerializer.SerializeToElement(
            new ChatCompletionTopLogprob { Token = "<|end|>" }, options).GetProperty("bytes").ValueKind);
    }

    [Fact]
    public void URL_citations_are_typed_and_roundtrip()
    {
        const string json = """
            {"type":"url_citation","url_citation":{
              "start_index":0,"end_index":10,"url":"https://example.com","title":"Example","future":true}}
            """;
        var annotation = Assert.IsType<ChatCompletionAnnotation>(
            JsonSerializer.Deserialize<ChatCompletionMessageAnnotation>(json));
        Assert.Equal(10, annotation.UrlCitation.EndIndex);
        AssertJsonEqual(json, JsonSerializer.Serialize<ChatCompletionMessageAnnotation>(annotation));
    }

    [Fact]
    public void Inline_moderation_exposes_success_and_error_variants()
    {
        const string json = """
            {"input":{"type":"moderation_results","model":"omni-moderation-latest","results":[
              {"type":"moderation_result","model":"omni-moderation-latest","flagged":false,
               "categories":{"violence":false,"future/category":true},
               "category_scores":{"violence":0.01},
               "category_applied_input_types":{"violence":["text","image"]},"future":true}]},
             "output":{"type":"error","code":"moderation_unavailable","message":"Try again.","future":42}}
            """;
        var moderation = JsonSerializer.Deserialize<ChatCompletionModeration>(json)!;
        var results = Assert.IsType<ChatCompletionModerationResults>(moderation.Input);
        Assert.True(Assert.Single(results.Results).Categories["future/category"]);
        Assert.Equal("moderation_unavailable", Assert.IsType<ChatCompletionModerationError>(moderation.Output).Code);
        AssertJsonEqual(json, JsonSerializer.Serialize(moderation));
        var request = JsonSerializer.Deserialize<ChatCompletionOptions>(
            """
            {"moderation":{"model":"omni-moderation-latest","policy":{
              "input":{"mode":"score"},"output":{"mode":"block"}}}}
            """)!;
        Assert.Equal("block", request.Moderation!.Policy!.Output!.Mode);
        var update = new ChatCompletionUpdate { Moderation = moderation };
        var roundtrip = JsonSerializer.Deserialize<ChatCompletionUpdate>(JsonSerializer.Serialize(update))!;
        Assert.IsType<ChatCompletionModerationError>(roundtrip.Moderation!.Output);
        var completion = new ChatCompletion { Moderation = moderation };
        Assert.IsType<ChatCompletionModerationResults>(
            JsonSerializer.Deserialize<ChatCompletion>(JsonSerializer.Serialize(completion))!.Moderation!.Input);
    }

    [Theory]
    [InlineData(typeof(ChatCompletionToolDefinition), typeof(ChatCompletionUnknownTool))]
    [InlineData(typeof(ChatCompletionToolChoice), typeof(ChatCompletionUnknownToolChoice))]
    [InlineData(typeof(ChatCompletionResponseFormat), typeof(ChatCompletionUnknownResponseFormat))]
    [InlineData(typeof(ChatCompletionToolCall), typeof(ChatCompletionUnknownToolCall))]
    [InlineData(typeof(ChatMessageContentPart), typeof(ChatMessageUnknownContentPart))]
    [InlineData(typeof(ChatCompletionMessageAnnotation), typeof(ChatCompletionUnknownAnnotation))]
    [InlineData(typeof(ChatCompletionModerationOutcome), typeof(ChatCompletionUnknownModerationOutcome))]
    public void Unknown_discriminated_variants_preserve_every_field(Type contract, Type unknownType)
    {
        const string json = """{"type":"future_variant","future":{"nested":[1,null,{"enabled":true}]}}""";
        var value = JsonSerializer.Deserialize(json, contract)!;
        Assert.Equal(unknownType, value.GetType());
        AssertJsonEqual(json, JsonSerializer.Serialize(value, contract));
        AssertJsonEqual(json, JsonSerializer.Serialize(value, unknownType));
    }

    [Fact]
    public void Future_reasoning_effort_and_string_tool_choice_modes_are_not_rejected()
    {
        var request = JsonSerializer.Deserialize<ChatCompletionOptions>(
            """{"reasoning_effort":"future_effort","tool_choice":"future_mode"}""")!;
        Assert.Equal("future_effort", request.ReasoningEffort!.Value.Value);
        Assert.Equal("future_mode", request.ToolChoice);
        Assert.Equal("\"future_effort\"", JsonSerializer.Serialize(request.ReasoningEffort));
    }

    [Theory]
    [InlineData(typeof(ChatMessageContent), "42")]
    [InlineData(typeof(ChatMessageContent), "{}")]
    [InlineData(typeof(ChatMessageContent), "[null]")]
    [InlineData(typeof(ChatMessageContentPart), """{"type":42}""")]
    [InlineData(typeof(ChatCompletionToolDefinition), "{}")]
    [InlineData(typeof(ChatCompletionToolCall), """{"type":false}""")]
    [InlineData(typeof(ChatCompletionToolChoice), "42")]
    [InlineData(typeof(ChatCompletionStop), "42")]
    [InlineData(typeof(ChatCompletionStop), "[null]")]
    [InlineData(typeof(ChatCompletionFunctionChoice), """{"name":42}""")]
    [InlineData(typeof(ChatCompletionVoice), "42")]
    [InlineData(typeof(ChatCompletionVoice), """{"id":null}""")]
    [InlineData(typeof(ChatCompletionBuiltInVoice), """{"id":"voice_1234"}""")]
    [InlineData(typeof(ChatCompletionToolChoiceMode), """{"type":"function","function":{"name":"test"}}""")]
    [InlineData(typeof(ChatCompletionOptions), """{"metadata":{"value":42}}""")]
    public void Malformed_known_union_shapes_fail_explicitly(Type contract, string json) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(json, contract));

    private static void AssertJsonEqual(string expected, string actual)
    {
        using var expectedDocument = JsonDocument.Parse(expected);
        using var actualDocument = JsonDocument.Parse(actual);
        Assert.True(JsonElement.DeepEquals(expectedDocument.RootElement, actualDocument.RootElement),
            $"Expected {expected}, got {actual}");
    }
}
