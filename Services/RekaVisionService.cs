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
        // SaveResponseToFile(responseContent);

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
    public async Task<Video> AddVideo(string videoUrl, string videoName)
    {
        _logger.LogInformation("Uploading video {VideoName} from URL {VideoUrl}", videoName, videoUrl);

        var request = CreateRequest(HttpMethod.Post, $"{BaseEndpoint}/videos/upload");

        var formData = new MultipartFormDataContent();
        formData.Add(new StringContent(videoUrl), "video_url");
        formData.Add(new StringContent(videoName), "video_name");
        formData.Add(new StringContent("true"), "index");

        request.Content = formData;

        var responseContent = await SendRequestAsync(request, $"upload video {videoName}");

        try
        {
            // SaveResponseToFile(responseContent);

            var rekaResponse = JsonSerializer.Deserialize<RekaVideoUploadResponse>(responseContent, _jsonOptions);

            if (string.IsNullOrEmpty(rekaResponse?.VideoId))
            {
                _logger.LogWarning("No video ID found in upload response or response format unexpected");
                throw new InvalidOperationException("Failed to parse upload response from Reka Vision API");
            }

            var video = new Video
            {
                VideoId = Guid.Parse(rekaResponse.VideoId),
                Url = videoUrl,
                IndexingStatus = ParseIndexingStatus(rekaResponse.Status)
            };

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
    /// Searches for video segments matching the query
    /// </summary>
    /// <param name="query">The search query</param>
    /// <returns>A list of search results with timestamps</returns>
    public async Task<List<SearchResult>> Search(string query)
    {
        _logger.LogInformation("Searching videos with query: {Query}", query);

        var request = CreateRequest(HttpMethod.Post, $"{BaseEndpoint}/search/hybrid");

        var requestBody = new
        {
            query = query,
            max_results = 3,
            threshold = 0.5
        };
        var jsonContent = JsonSerializer.Serialize(requestBody);
        request.Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

        var responseContent = await SendRequestAsync(request, $"search videos with query {query}");

        try
        {

            // SaveResponseToFile(responseContent);

            var results = JsonSerializer.Deserialize<List<RekaSearchResultDto>>(responseContent, _jsonOptions);

            if (results == null)
            {
                _logger.LogWarning("No search results found in response or response format unexpected");
                return new List<SearchResult>();
            }

            var domainResults = results.Select(r => new SearchResult
            {
                VideoChunkId = Guid.Parse(r.VideoChunkId),
                VideoId = Guid.Parse(r.VideoId),
                Score = r.Score,
                StartTimestamp = r.StartTimestamp,
                EndTimestamp = r.EndTimestamp,
                S3PresignedUrl = r.S3PresignedUrl,
                PlainTextCaption = r.PlainTextCaption
            }).ToList();

            _logger.LogInformation("Successfully retrieved {Count} search results", domainResults.Count);
            return domainResults;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error deserializing search response from Reka Vision API");
            throw new InvalidOperationException("Failed to parse search response from Reka Vision API", ex);
        }
    }

    /// <summary>
    /// Asks a question about a specific video
    /// </summary>
    /// <param name="videoId">The ID of the video to ask about</param>
    /// <param name="question">The question to ask</param>
    /// <returns>The answer to the question</returns>
    public async Task<QAAnswer> AskQuestion(string videoId, string question)
    {
        _logger.LogInformation("Asking question about video {VideoId}: {Question}", videoId, question);

        var request = CreateRequest(HttpMethod.Post, $"{BaseEndpoint}/qa/chat");

        var requestBody = new
        {
            video_id = videoId,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = question
                }
            }
        };
        var jsonContent = JsonSerializer.Serialize(requestBody);
        request.Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

        var responseContent = await SendRequestAsync(request, $"ask question about video {videoId}");

        try
        {
            SaveResponseToFile(responseContent);

            var rekaResponse = JsonSerializer.Deserialize<RekaQAAnswerDto>(responseContent, _jsonOptions);

            if (rekaResponse == null)
            {
                _logger.LogWarning("No answer found in response or response format unexpected");
                throw new InvalidOperationException("Failed to parse QA response from Reka Vision API");
            }

            if (rekaResponse.status != "success")
            {
                _logger.LogError("QA request failed with status: {Status}, error: {Error}", rekaResponse.status, rekaResponse.error);
                throw new InvalidOperationException($"QA request failed: {rekaResponse.error ?? "Unknown error"}");
            }

            var answer = new QAAnswer
            {
                Answer = rekaResponse.chat_response,
                Confidence = 1.0, // API does not provide confidence
                VideoId = Guid.Parse(videoId),
                Question = question,
                Timestamp = DateTime.Now
            };

            _logger.LogInformation("Successfully retrieved answer for question about video {VideoId}", videoId);
            return answer;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error deserializing QA response from Reka Vision API");
            throw new InvalidOperationException("Failed to parse QA response from Reka Vision API", ex);
        }
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
            "pending" or "download_initiated" => IndexingStatus.Pending,
            "processing" or "indexing" => IndexingStatus.Indexing,
            "completed" or "indexed" => IndexingStatus.Indexed,
            "failed" => IndexingStatus.Failed,
            _ => IndexingStatus.Pending
        };
    }
}