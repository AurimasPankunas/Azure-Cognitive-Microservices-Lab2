namespace Shared.Contracts;

/// <summary>
/// Message sent from the image analysis service to the history service.
/// Both projects reference this contract, so their JSON shape stays in sync.
/// </summary>
public record ImageAnalyzedEvent(
    Guid Id,
    string FileName,
    string Text,
    DateTimeOffset AnalyzedAt);
