using ImageAnalysis.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts;

namespace ImageAnalysis.Api.Controllers;

/// <summary>
/// HTTP entry point for uploading images and extracting text with Azure AI Vision.
/// </summary>
[ApiController]
[Route("api/analysis")]
public class AnalysisController : ControllerBase
{
    private readonly AzureVisionService _vision;
    private readonly RabbitMqPublisher _publisher;
    private readonly IWebHostEnvironment _env;

    /// <summary>
    /// Receives the Azure OCR service, RabbitMQ publisher and environment info via DI.
    /// </summary>
    public AnalysisController(
        AzureVisionService vision,
        RabbitMqPublisher publisher,
        IWebHostEnvironment env)
    {
        _vision = vision;
        _publisher = publisher;
        _env = env;
    }

    /// <summary>
    /// Accepts one or more image files, extracts their text and publishes an event
    /// so the history microservice can process the result asynchronously.
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Upload(
        [FromForm] List<IFormFile> files,
        CancellationToken ct)
    {
        if (files is null || files.Count == 0)
            return BadRequest("Nėra failų.");

        var results = new List<object>();

        foreach (var file in files)
        {
            if (file.Length == 0)
                continue;

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

            // Keep the allowed list aligned with the formats supported by Image Analysis 4.0.
            if (ext is not (".jpg" or ".jpeg" or ".png" or ".bmp"
                or ".gif" or ".tif" or ".tiff" or ".webp"))
                return BadRequest($"Nepalaikomas formatas: {ext}");

            var id = Guid.NewGuid();
            var dir = Path.Combine(_env.ContentRootPath, "uploads");
            Directory.CreateDirectory(dir);

            // Use a generated file name to avoid path traversal and duplicate names.
            var path = Path.Combine(dir, $"{id}{ext}");

            await using (var fs = System.IO.File.Create(path))
                await file.CopyToAsync(fs, ct);

            await using var imageStream = System.IO.File.OpenRead(path);

            var text = await _vision.ReadTextAsync(imageStream, ct);

            // Publish the analysis result; the history service consumes it independently.
            await _publisher.PublishAsync(new ImageAnalyzedEvent(
                id,
                file.FileName,
                text,
                DateTimeOffset.UtcNow), ct);

            results.Add(new
            {
                id,
                fileName = file.FileName,
                textLength = text.Length,
                text
            });
        }

        return Ok(results);
    }
}
