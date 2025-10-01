using System;

namespace LearningTool.Domain;

/// <summary>
/// Search result for video search queries (Domain)
/// </summary>
public class SearchResult
{
    /// <summary>
    /// Unique identifier for the video chunk
    /// </summary>
    public Guid VideoChunkId { get; set; }

    /// <summary>
    /// Unique identifier for the video
    /// </summary>
    public Guid VideoId { get; set; }

    /// <summary>
    /// Relevance score of the search result
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Start timestamp of the matching segment
    /// </summary>
    public double StartTimestamp { get; set; }

    /// <summary>
    /// End timestamp of the matching segment
    /// </summary>
    public double EndTimestamp { get; set; }

    /// <summary>
    /// Presigned S3 URL for accessing the video segment
    /// </summary>
    public string S3PresignedUrl { get; set; } = string.Empty;

    /// <summary>
    /// Plain text caption of the video segment
    /// </summary>
    public string PlainTextCaption { get; set; } = string.Empty;

    /// <summary>
    /// Title of the video
    /// </summary>
    public string Title { get; set; } = string.Empty;
}