using System.Text.Json;
using AIHappey.ChatCompletions.Models;
using Xunit;

namespace AIHappey.ChatCompletions.Tests;

public class NativeMetadataTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("false")]
    [InlineData("9007199254740993")]
    [InlineData("{\"nested\":[1,true,null]}")]
    public void MetadataAndRootExtensionsRemainSeparate(string value)
    {
        var json = JsonSerializer.Deserialize<JsonElement>("{\"model\":\"test\",\"messages\":[],\"metadata\":{\"openai\":\"app\"},\"future\":" + value + "}");
        var request = json.Deserialize<ChatCompletionOptions>()!;
        Assert.Equal("app", request.Metadata!["openai"]);
        Assert.True(JsonElement.DeepEquals(json.GetProperty("future"), request.AdditionalProperties!["future"]));
        var serialized = JsonSerializer.SerializeToElement(request, JsonSerializerOptions.Web);
        Assert.True(JsonElement.DeepEquals(json.GetProperty("future"), serialized.GetProperty("future")));
        Assert.True(JsonElement.DeepEquals(json.GetProperty("metadata"), serialized.GetProperty("metadata")));
        Assert.False(serialized.TryGetProperty("openai", out _));
    }
}
