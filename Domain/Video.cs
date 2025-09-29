using System;
using System.Text.Json.Serialization;

namespace LearningTool.Domain;

public class Video
{
    /// <summary>
    /// Unique identifier for the video
    /// </summary>
    public Guid VideoId { get; set; }

    /// <summary>
    /// Presigned S3 URL for video access
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Current indexing status of the video
    /// </summary>
    public IndexingStatus IndexingStatus { get; set; } = IndexingStatus.Pending;

    /// <summary>
    /// Video metadata including dimensions, duration, timestamps, etc.
    /// </summary>
    public VideoMetadata? Metadata { get; set; }

    /// <summary>
    /// Type of indexing applied to the video
    /// </summary>
    public string IndexingType { get; set; } = string.Empty;
}

/// <summary>
/// Video metadata structure
/// </summary>
public class VideoMetadata
{
    [JsonPropertyName("width")]
    public int Width { get; set; }
    
    [JsonPropertyName("height")]
    public int Height { get; set; }
    
    [JsonPropertyName("avg_fps")]
    public double AvgFps { get; set; }
    
    [JsonPropertyName("video_name")]
    public string VideoName { get; set; } = string.Empty;
    
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
    
    [JsonPropertyName("video_start_timestamp_utc_ms")]
    public long VideoStartTimestampUtcMs { get; set; }
    
    [JsonPropertyName("duration")]
    public double Duration { get; set; }
    
    [JsonPropertyName("thumbnail")]
    public string Thumbnail { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;
}

