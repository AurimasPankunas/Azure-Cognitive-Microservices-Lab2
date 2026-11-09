namespace AnalysisHistory.Api.Data;

/// <summary>
/// Database row representing one completed image analysis.
/// </summary>
public class AnalysisRecord
{
    /// <summary>Unique identifier of the analysis (same as the image id).</summary>
    public Guid Id { get; set; }

    /// <summary>Original file name as uploaded by the client.</summary>
    public string FileName { get; set; } = "";

    /// <summary>Full text extracted by Azure Read/OCR.</summary>
    public string Text { get; set; } = "";

    /// <summary>Total number of words in the extracted text.</summary>
    public int WordCount { get; set; }

    /// <summary>Number of distinct words in the extracted text.</summary>
    public int UniqueWordCount { get; set; }

    /// <summary>Top keywords serialized as JSON so SQLite can store them easily.</summary>
    public string TopKeywordsJson { get; set; } = "[]";

    /// <summary>Timestamp when the analysis was performed.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
