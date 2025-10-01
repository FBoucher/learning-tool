using System.Text.Json.Serialization;

namespace LearningTool.Services;

/// <summary>
/// Response model for the Reka API videos/get endpoint (DTO)
/// </summary>
public class RekaVideoResponse
{
    [JsonPropertyName("results")]
    public List<RekaVideoDto> Results { get; set; } = new();
}