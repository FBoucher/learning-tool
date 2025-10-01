namespace LearningTool.Services;

public class RekaQAAnswerDto
{
    public string chat_response { get; set; } = string.Empty;
    public string? system_message { get; set; }
    public string? error { get; set; }
    public string status { get; set; } = string.Empty;
    public object? debug_chunks { get; set; }
    public string debug_predicted_start_time { get; set; } = string.Empty;
    public string debug_predicted_end_time { get; set; } = string.Empty;
}