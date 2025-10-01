using System.Text.Json.Serialization;

namespace LearningTool.Services;

/// <summary>
/// Response model for the Reka API videos/search endpoint (DTO)
/// </summary>
public class RekaSearchResponse
{
    [JsonPropertyName("results")]
    public List<RekaSearchResultDto> Results { get; set; } = new();

    [JsonPropertyName("total_results")]
    public int TotalResults { get; set; }
}