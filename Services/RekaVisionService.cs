using LearningTool.Domain;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LearningTool.Services;

// Response model for the Reka API


public class RekaVisionService : IRekaVisionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RekaVisionService> _logger;
    private readonly string _rekaAPIKey;

    public RekaVisionService(HttpClient httpClient, ILogger<RekaVisionService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Try to get RekaAPIKey from environment variable first, then fall back to appsettings
        _rekaAPIKey = Environment.GetEnvironmentVariable("REKA_API_KEY")
                   ?? configuration["RekaAPIKey"]
                   ?? throw new ArgumentException("RekaAPIKey configuration is required. Set either REKA_API_KEY environment variable or RekaAPIKey in appsettings.json");
    }

    /// <summary>
    /// Retrieves a list of videos from the Reka Vision service
    /// </summary>
    /// <returns>A list of Video objects</returns>
    public async Task<List<Video>> GetAllVideos()
    {
        try
        {
            _logger.LogInformation("Fetching videos from Reka Vision API");

            // Configure the HTTP request
            var request = new HttpRequestMessage(HttpMethod.Post, "https://vision-agent.api.reka.ai/videos/get");
            request.Headers.Add("X-Api-Key", _rekaAPIKey);
            
            // Add empty JSON content with proper Content-Type header
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

            // Make the HTTP call asynchronously
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            // Read the response content
            var responseContent = await response.Content.ReadAsStringAsync();

            // Save response to file
            var responseFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "videos_response.json");
            Directory.CreateDirectory(Path.GetDirectoryName(responseFilePath)!);
            File.WriteAllText(responseFilePath, responseContent);
            _logger.LogInformation("Response saved to: {FilePath}", responseFilePath);

            // Deserialize the response
            var rekaResponse = JsonSerializer.Deserialize<RekaVideoResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (rekaResponse?.Results == null)
            {
                _logger.LogWarning("No videos found in response or response format unexpected");
                return new List<Video>();
            }

            // Convert to domain models
            var videos = rekaResponse.Results.Select(dto => new Video
            {
                VideoId = Guid.TryParse(dto.VideoId, out var guid) ? guid : Guid.NewGuid(),
                Url = dto.Url,
                IndexingStatus = ParseIndexingStatus(dto.IndexingStatus),
                Metadata = dto.Metadata,
                IndexingType = dto.IndexingType
            }).ToList();

            _logger.LogInformation("Successfully retrieved {Count} videos", videos.Count);
            return videos;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while fetching videos");
            throw new InvalidOperationException("Failed to fetch videos from Reka Vision API", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error deserializing response from Reka Vision API");
            throw new InvalidOperationException("Failed to parse response from Reka Vision API", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while fetching videos");
            throw;
        }
    }

    /// <summary>
    /// Uploads a video to the Reka Vision service
    /// </summary>
    /// <param name="videoUrl">The URL of the video to upload</param>
    /// <param name="videoName">The name for the video</param>
    /// <param name="index">Whether to index the video</param>
    /// <returns>The uploaded video information</returns>
    public async Task<Video> AddVideo(string videoUrl, string videoName, bool index = true)
    {
        try
        {
            _logger.LogInformation("Uploading video {VideoName} from URL {VideoUrl}", videoName, videoUrl);

            // Configure the HTTP request
            var request = new HttpRequestMessage(HttpMethod.Post, "https://vision-agent.api.reka.ai/videos/upload");
            request.Headers.Add("X-Api-Key", _rekaAPIKey);
            
            // Create form data content
            var formData = new MultipartFormDataContent();
            formData.Add(new StringContent(videoUrl), "video_url");
            formData.Add(new StringContent(videoName), "video_name");
            formData.Add(new StringContent(index.ToString().ToLowerInvariant()), "index");
            
            request.Content = formData;

            // Make the HTTP call asynchronously
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            // Read the response content
            var responseContent = await response.Content.ReadAsStringAsync();

            // Deserialize the response
            var rekaResponse = JsonSerializer.Deserialize<RekaVideoUploadResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (rekaResponse?.Video == null)
            {
                _logger.LogWarning("No video found in upload response or response format unexpected");
                throw new InvalidOperationException("Failed to parse upload response from Reka Vision API");
            }

            // Convert to domain model
            var video = new Video
            {
                VideoId = Guid.TryParse(rekaResponse.Video.VideoId, out var guid) ? guid : Guid.NewGuid(),
                Url = rekaResponse.Video.Url,
                IndexingStatus = ParseIndexingStatus(rekaResponse.Video.IndexingStatus),
                Metadata = rekaResponse.Video.Metadata,
                IndexingType = rekaResponse.Video.IndexingType
            };

            _logger.LogInformation("Successfully uploaded video {VideoName} with ID {VideoId}", videoName, video.VideoId);
            return video;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while uploading video {VideoName}", videoName);
            throw new InvalidOperationException($"Failed to upload video {videoName} to Reka Vision API", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error deserializing upload response from Reka Vision API for video {VideoName}", videoName);
            throw new InvalidOperationException($"Failed to parse upload response for video {videoName} from Reka Vision API", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while uploading video {VideoName}", videoName);
            throw;
        }
    }

    private IndexingStatus ParseIndexingStatus(string status)
    {
        return status?.ToLowerInvariant() switch
        {
            "pending" => IndexingStatus.Pending,
            "processing" or "indexing" => IndexingStatus.Indexing,
            "completed" or "indexed" => IndexingStatus.Indexed,
            "failed" => IndexingStatus.Failed,
            _ => IndexingStatus.Pending
        };
    }
}




public class RekaVideoResponse
{
    [JsonPropertyName("results")]
    public List<RekaVideoDto> Results { get; set; } = new();
}

public class RekaVideoUploadResponse
{
    [JsonPropertyName("video")]
    public RekaVideoDto Video { get; set; } = new();
}

public class RekaVideoDto
{
    [JsonPropertyName("video_id")]
    public string VideoId { get; set; } = string.Empty;
    
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
    
    [JsonPropertyName("indexing_status")]
    public string IndexingStatus { get; set; } = string.Empty;
    
    [JsonPropertyName("metadata")]
    public VideoMetadata? Metadata { get; set; }
    
    [JsonPropertyName("indexing_type")]
    public string IndexingType { get; set; } = string.Empty;
}