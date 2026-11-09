using Azure;
using Azure.AI.Vision.ImageAnalysis;

namespace ImageAnalysis.Api.Services;

/// <summary>
/// Wraps Azure AI Vision so controllers do not depend directly on the Azure SDK.
/// </summary>
public class AzureVisionService
{
    private readonly ImageAnalysisClient _client;

    /// <summary>
    /// Builds the Azure client from configuration values supplied by environment variables.
    /// </summary>
    public AzureVisionService(IConfiguration config)
    {
        var endpoint = config["Azure:Endpoint"]
            ?? throw new InvalidOperationException("Trūksta Azure:Endpoint");

        var key = config["Azure:Key"]
            ?? throw new InvalidOperationException("Trūksta Azure:Key");

        _client = new ImageAnalysisClient(
            new Uri(endpoint),
            new AzureKeyCredential(key));
    }

    /// <summary>
    /// Sends an image stream to Azure Read/OCR and returns the recognized text as plain lines.
    /// </summary>
    public async Task<string> ReadTextAsync(
        Stream imageStream,
        CancellationToken ct)
    {
        // Azure SDK accepts BinaryData; wrap the stream without loading it manually.
        var data = BinaryData.FromStream(imageStream);

        var response = await _client.AnalyzeAsync(
            data,
            VisualFeatures.Read,
            cancellationToken: ct);

        var result = response.Value;

        if (result.Read is null || result.Read.Blocks.Count == 0)
            return string.Empty;

        // Flatten blocks -> lines into a simple newline-separated string.
        return string.Join("\n",
            result.Read.Blocks
                .SelectMany(b => b.Lines)
                .Select(l => l.Text));
    }
}
