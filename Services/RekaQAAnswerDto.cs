namespace LearningTool.Services;

public class RekaQAAnswerDto
{
    public string answer { get; set; } = string.Empty;
    public double confidence { get; set; }
    public string video_id { get; set; } = string.Empty;
    public string question { get; set; } = string.Empty;
    public long timestamp { get; set; }
}