using System.Text.Json;
using AIHappey.ChatCompletions.Models;
using Xunit;

namespace AIHappey.ChatCompletions.Tests;

public class ChatCompletionJsonTests
{
    [Fact]
    public void Request_roundtrips_structured_output_and_function_tool_choice()
    {
        const string json = """
            {
              "model": "gpt-4o-mini",
              "messages": [
                { "role": "user", "content": "Return a short greeting." }
              ],
              "tools": [
                {
                  "type": "function",
                  "function": {
                    "name": "greet",
                    "description": "Create a greeting",
                    "parameters": {
                      "type": "object",
                      "properties": { "name": { "type": "string" } },
                      "required": ["name"],
                      "additionalProperties": false
                    },
                    "strict": true
                  }
                },
                {
                  "type": "custom",
                  "custom": {
                    "name": "code",
                    "format": {
                      "type": "grammar",
                      "grammar": { "definition": "root ::= \"ok\"", "syntax": "lark" }
                    }
                  }
                }
              ],
              "tool_choice": {
                "type": "function",
                "function": { "name": "greet" }
              },
              "response_format": {
                "type": "json_schema",
                "json_schema": {
                  "name": "greeting",
                  "strict": true,
                  "schema": {
                    "type": "object",
                    "properties": { "message": { "type": "string" } },
                    "required": ["message"],
                    "additionalProperties": false
                  }
                }
              },
              "max_completion_tokens": 64,
              "top_p": 0.9,
              "logprobs": true,
              "top_logprobs": 3,
              "reasoning_effort": "medium",
              "unknown_option": { "enabled": true }
            }
            """;

        var request = JsonSerializer.Deserialize<ChatCompletionOptions>(json)!;
        var serialized = JsonSerializer.SerializeToElement(request);

        Assert.Equal(64, serialized.GetProperty("max_completion_tokens").GetInt32());
        Assert.Equal("greet", serialized.GetProperty("tool_choice")
            .GetProperty("function").GetProperty("name").GetString());
        Assert.Equal("json_schema", serialized.GetProperty("response_format")
            .GetProperty("type").GetString());
        Assert.Equal("custom", serialized.GetProperty("tools")[1].GetProperty("type").GetString());
        Assert.Equal(0.9, serialized.GetProperty("top_p").GetDouble());
        Assert.Equal(3, serialized.GetProperty("top_logprobs").GetInt32());
        Assert.True(serialized.GetProperty("unknown_option").GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public void User_content_roundtrips_image_audio_and_file_parts()
    {
        var request = new ChatCompletionOptions
        {
            Model = "gpt-4o-mini",
            Messages =
            [
                new ChatMessage
                {
                    Role = "user",
                    Content = JsonSerializer.SerializeToElement(new object[]
                    {
                        new ChatMessageTextPart { Text = "Describe these inputs." },
                        new ChatMessageImagePart
                        {
                            ImageUrl = new ChatMessageImageUrl
                            {
                                Url = "https://example.com/image.png",
                                Detail = "high"
                            }
                        },
                        new ChatMessageInputAudioPart
                        {
                            InputAudio = new ChatMessageInputAudio
                            {
                                Data = "UklGRg==",
                                Format = "wav"
                            }
                        },
                        new ChatMessageFilePart
                        {
                            File = new ChatMessageFile
                            {
                                Filename = "notes.txt",
                                FileData = "bm90ZXM="
                            }
                        }
                    })
                }
            ],
            Modalities = ["text", "audio"],
            Audio = new ChatCompletionAudioOptions { Voice = "alloy", Format = "wav" }
        };

        var serialized = JsonSerializer.SerializeToElement(request);
        var content = serialized.GetProperty("messages")[0].GetProperty("content");

        Assert.Equal("text", content[0].GetProperty("type").GetString());
        Assert.Equal("image_url", content[1].GetProperty("type").GetString());
        Assert.Equal("input_audio", content[2].GetProperty("type").GetString());
        Assert.Equal("file", content[3].GetProperty("type").GetString());
        Assert.Equal("audio", serialized.GetProperty("modalities")[1].GetString());
        Assert.Equal("alloy", serialized.GetProperty("audio").GetProperty("voice").GetString());

        var roundtripped = JsonSerializer.Deserialize<ChatCompletionOptions>(serialized.GetRawText())!;
        Assert.Equal("wav", roundtripped.Messages.Single().Content[2]
            .GetProperty("input_audio").GetProperty("format").GetString());
    }

    [Fact]
    public void Completion_usage_and_stream_delta_support_current_fields()
    {
        var completion = new ChatCompletion
        {
            Id = "chatcmpl-example",
            Created = 1741570283,
            Model = "gpt-4o-mini",
            Choices =
            [
                new ChatCompletionChoice
                {
                    Index = 0,
                    Message = new ChatCompletionResponseMessage
                    {
                        Content = "Hello!",
                        ToolCalls =
                        [
                            new ChatCompletionMessageToolCall
                            {
                                Id = "call_123",
                                Function = new ChatCompletionFunctionCall
                                {
                                    Name = "greet",
                                    Arguments = "{\"name\":\"Ada\"}"
                                }
                            }
                        ]
                    },
                    FinishReason = "tool_calls"
                }
            ],
            Usage = new ChatCompletionUsage
            {
                PromptTokens = 12,
                CompletionTokens = 8,
                TotalTokens = 20,
                PromptTokensDetails = new ChatCompletionPromptTokenDetails { CachedTokens = 4 },
                CompletionTokensDetails = new ChatCompletionCompletionTokenDetails
                {
                    ReasoningTokens = 2,
                    AcceptedPredictionTokens = 1
                }
            }
        };

        var completionJson = JsonSerializer.SerializeToElement(completion);
        Assert.Equal("tool_calls", completionJson.GetProperty("choices")[0]
            .GetProperty("finish_reason").GetString());
        Assert.Equal(2, completionJson.GetProperty("usage")
            .GetProperty("completion_tokens_details").GetProperty("reasoning_tokens").GetInt32());
        Assert.Equal("greet", completionJson.GetProperty("choices")[0]
            .GetProperty("message").GetProperty("tool_calls")[0]
            .GetProperty("function").GetProperty("name").GetString());
        var completionRoundtrip = JsonSerializer.SerializeToElement(
            JsonSerializer.Deserialize<ChatCompletion>(completionJson.GetRawText())!);
        Assert.Equal(20, completionRoundtrip.GetProperty("usage").GetProperty("total_tokens").GetInt32());

        var update = new ChatCompletionUpdate
        {
            Id = "chatcmpl-example",
            Created = 1741570283,
            Model = "gpt-4o-mini",
            Choices =
            [
                new ChatCompletionStreamChoice
                {
                    Index = 0,
                    Delta = new ChatCompletionDelta
                    {
                        ToolCalls =
                        [
                            new ChatCompletionToolCallChunk
                            {
                                Index = 0,
                                Id = "call_123",
                                Type = "function",
                                Function = new ChatCompletionFunctionCallDelta
                                {
                                    Name = "greet",
                                    Arguments = "{\"name\":"
                                }
                            }
                        ]
                    }
                }
            ],
            Usage = new ChatCompletionUsage { PromptTokens = 12, CompletionTokens = 8, TotalTokens = 20 },
            Obfuscation = "opaque"
        };

        var updateJson = JsonSerializer.SerializeToElement(update);
        Assert.Equal("function", updateJson.GetProperty("choices")[0]
            .GetProperty("delta").GetProperty("tool_calls")[0].GetProperty("type").GetString());
        Assert.Equal("opaque", updateJson.GetProperty("obfuscation").GetString());
        var updateRoundtrip = JsonSerializer.SerializeToElement(
            JsonSerializer.Deserialize<ChatCompletionUpdate>(updateJson.GetRawText())!);
        Assert.Equal("{\"name\":",
            updateRoundtrip.GetProperty("choices")[0].GetProperty("delta")
                .GetProperty("tool_calls")[0].GetProperty("function")
                .GetProperty("arguments").GetString());
    }

    [Fact]
    public void Undefined_message_content_is_omitted_and_legacy_tool_choice_stays_a_string()
    {
        var json = JsonSerializer.Serialize(new ChatCompletionOptions
        {
            Model = "gpt-4o-mini",
            Messages = [new ChatMessage { Role = "assistant" }],
            ToolChoice = "auto"
        });

        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.GetProperty("messages")[0].TryGetProperty("content", out _));
        Assert.Equal("auto", document.RootElement.GetProperty("tool_choice").GetString());

        var deserialized = JsonSerializer.Deserialize<ChatCompletionOptions>(
            """{"model":"gpt-4o-mini","messages":[],"tool_choice":"required"}""")!;
        Assert.Equal("required", deserialized.ToolChoice);
    }
}
