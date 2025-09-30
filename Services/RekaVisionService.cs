using LearningTool.Domain;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LearningTool.Services;

public class RekaVisionService : IRekaVisionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RekaVisionService> _logger;
    private readonly string _rekaAPIKey;
    private readonly JsonSerializerOptions _jsonOptions;

    // API base endpoint
    private const string BaseEndpoint = "https://vision-agent.api.reka.ai";

    public RekaVisionService(HttpClient httpClient, ILogger<RekaVisionService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Try to get RekaAPIKey from environment variable first, then fall back to appsettings
        _rekaAPIKey = Environment.GetEnvironmentVariable("REKA_API_KEY")
                   ?? configuration["RekaAPIKey"]
                   ?? throw new ArgumentException("RekaAPIKey configuration is required. Set either REKA_API_KEY environment variable or RekaAPIKey in appsettings.json");

        // Configure JSON serialization options
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Retrieves a list of videos from the Reka Vision service
    /// </summary>
    /// <returns>A list of Video objects</returns>
    public async Task<List<Video>> GetAllVideos()
    {
        _logger.LogInformation("Fetching videos from Reka Vision API");

        var request = CreateRequest(HttpMethod.Post, $"{BaseEndpoint}/videos/get");
        request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        var responseContent = await SendRequestAsync(request, "fetch videos");

        // Save response to file
        SaveResponseToFile(responseContent);

        try
        {
            // Deserialize the response
            var rekaResponse = JsonSerializer.Deserialize<RekaVideoResponse>(responseContent, _jsonOptions);

            if (rekaResponse?.Results == null)
            {
                _logger.LogWarning("No videos found in response or response format unexpected");
                return new List<Video>();
            }

            // Convert to domain models
            var videos = rekaResponse.Results.Select(ConvertToVideo).ToList();

            _logger.LogInformation("Successfully retrieved {Count} videos", videos.Count);
            return videos;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error deserializing response from Reka Vision API");
            throw new InvalidOperationException("Failed to parse response from Reka Vision API", ex);
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
        _logger.LogInformation("Uploading video {VideoName} from URL {VideoUrl}", videoName, videoUrl);

        var request = CreateRequest(HttpMethod.Post, $"{BaseEndpoint}/videos/upload");

        // Create form data content
        var formData = new MultipartFormDataContent();
        formData.Add(new StringContent(videoUrl), "video_url");
        formData.Add(new StringContent(videoName), "video_name");
        formData.Add(new StringContent(index.ToString().ToLowerInvariant()), "index");

        request.Content = formData;

        var responseContent = await SendRequestAsync(request, $"upload video {videoName}");

        try
        {
            // Deserialize the response
            var rekaResponse = JsonSerializer.Deserialize<RekaVideoUploadResponse>(responseContent, _jsonOptions);

            if (rekaResponse?.Video == null)
            {
                _logger.LogWarning("No video found in upload response or response format unexpected");
                throw new InvalidOperationException("Failed to parse upload response from Reka Vision API");
            }

            // Convert to domain model
            var video = ConvertToVideo(rekaResponse.Video);

            _logger.LogInformation("Successfully uploaded video {VideoName} with ID {VideoId}", videoName, video.VideoId);
            return video;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error deserializing upload response from Reka Vision API for video {VideoName}", videoName);
            throw new InvalidOperationException($"Failed to parse upload response for video {videoName} from Reka Vision API", ex);
        }
    }

    /// <summary>
    /// Deletes videos from the Reka Vision service
    /// </summary>
    /// <param name="videoIds">The IDs of the videos to delete</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task DeleteVideos(IEnumerable<Guid> videoIds)
    {
        _logger.LogInformation("Deleting videos with IDs: {VideoIds}", string.Join(", ", videoIds));

        var request = CreateRequest(HttpMethod.Delete, $"{BaseEndpoint}/videos/delete");
        request.Headers.Add("Content-Type", "application/json");

        // Create the request body
        var requestBody = new
        {
            video_ids = videoIds.Select(id => id.ToString()).ToArray()
        };
        var jsonContent = JsonSerializer.Serialize(requestBody);
        request.Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

        await SendRequestAsync(request, "delete videos");

        _logger.LogInformation("Successfully deleted videos with IDs: {VideoIds}", string.Join(", ", videoIds));
    }

    /// <summary>
    /// Creates an HTTP request message with the API key header
    /// </summary>
    /// <param name="method">The HTTP method</param>
    /// <param name="endpoint">The API endpoint URL</param>
    /// <returns>The configured HttpRequestMessage</returns>
    private HttpRequestMessage CreateRequest(HttpMethod method, string endpoint)
    {
        var request = new HttpRequestMessage(method, endpoint);
        request.Headers.Add("X-Api-Key", _rekaAPIKey);
        return request;
    }

    /// <summary>
    /// Sends an HTTP request and returns the response content as a string
    /// </summary>
    /// <param name="request">The HTTP request message</param>
    /// <param name="operationName">The name of the operation for logging</param>
    /// <returns>The response content as a string</returns>
    private async Task<string> SendRequestAsync(HttpRequestMessage request, string operationName)
    {
        try
        {
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while {Operation}", operationName);
            throw new InvalidOperationException($"Failed to {operationName} from Reka Vision API", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while {Operation}", operationName);
            throw;
        }
    }

    /// <summary>
    /// Converts a RekaVideoDto to a Video domain model
    /// </summary>
    /// <param name="dto">The DTO to convert</param>
    /// <returns>The converted Video object</returns>
    private static Video ConvertToVideo(RekaVideoDto dto)
    {
        return new Video
        {
            VideoId = Guid.TryParse(dto.VideoId, out var guid) ? guid : Guid.NewGuid(),
            Url = dto.Url,
            IndexingStatus = ParseIndexingStatus(dto.IndexingStatus),
            Metadata = dto.Metadata,
            IndexingType = dto.IndexingType
        };
    }

    /// <summary>
    /// Saves response content to a timestamped file in the wwwroot directory
    /// </summary>
    /// <param name="responseContent">The response content to save</param>
    private void SaveResponseToFile(string responseContent)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmm");
        var responseFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", $"videos_response_{timestamp}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(responseFilePath)!);
        File.WriteAllText(responseFilePath, responseContent);
        _logger.LogInformation("Response saved to: {FilePath}", responseFilePath);
    }

    private static IndexingStatus ParseIndexingStatus(string status)
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