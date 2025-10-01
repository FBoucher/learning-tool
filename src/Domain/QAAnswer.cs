namespace LearningTool.Domain;

public class QAAnswer
{
    public string Answer { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public Guid VideoId { get; set; }
    public string Question { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}