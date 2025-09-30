using LearningTool.Domain;

namespace LearningTool.Services;

public interface IRekaVisionService
{
    /// <summary>
    /// Retrieves a list of videos from the Reka Vision service
    /// </summary>
    /// <returns>A list of Video objects</returns>
    Task<List<Video>> GetAllVideos();

    /// <summary>
    /// Uploads a video to the Reka Vision service
    /// </summary>
    /// <param name="videoUrl">The URL of the video to upload</param>
    /// <param name="videoName">The name for the video</param>
    /// <param name="index">Whether to index the video</param>
    /// <returns>The uploaded video information</returns>
    Task<Video> AddVideo(string videoUrl, string videoName);

    /// <summary>
    /// Deletes videos from the Reka Vision service
    /// </summary>
    /// <param name="videoIds">The IDs of the videos to delete</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task DeleteVideos(IEnumerable<Guid> videoIds);

    /// <summary>
    /// Searches for video segments matching the query
    /// </summary>
    /// <param name="query">The search query</param>
    /// <returns>A list of search results with timestamps</returns>
    Task<List<SearchResult>> Search(string query);

    /// <summary>
    /// Asks a question about a specific video
    /// </summary>
    /// <param name="videoId">The ID of the video to ask about</param>
    /// <param name="question">The question to ask</param>
    /// <returns>The answer to the question</returns>
    Task<QAAnswer> AskQuestion(string videoId, string question);
}