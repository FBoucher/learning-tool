using System.Text.Json.Serialization;

namespace LearningTool.Services;

/// <summary>
/// Response model for the Reka API videos/upload endpoint (DTO)
/// </summary>
public class RekaVideoUploadResponse
{
    [JsonPropertyName("video_id")]
    public string VideoId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}