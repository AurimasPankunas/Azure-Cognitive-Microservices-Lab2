using Azure;
using Azure.AI.Vision.ImageAnalysis;
using DotNetEnv;
using Microsoft.Extensions.Configuration;

// Nuskaito .env failą. TraversePath() ieško .env aukštyn per katalogus,
// todėl veikia ir paleidus iš Lab2, ir iš CognitiveClient katalogo.
Env.TraversePath().Load();

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var endpoint = Environment.GetEnvironmentVariable("AZURE_VISION_ENDPOINT")
    ?? config["Azure:Endpoint"];
var key = Environment.GetEnvironmentVariable("AZURE_VISION_KEY")
    ?? config["Azure:Key"];

if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key))
{
    Console.WriteLine("Klaida: trūksta Azure endpoint arba rakto.");
    Console.WriteLine("Užpildyk .env failą (AZURE_VISION_ENDPOINT ir AZURE_VISION_KEY).");
    return;
}

var client = new ImageAnalysisClient(
    new Uri(endpoint),
    new AzureKeyCredential(key));

Console.WriteLine("1 - OCR / Read");
Console.WriteLine("2 - Tags");
Console.WriteLine("3 - Objects + Tags");
Console.WriteLine("4 - Caption (jei regionas palaiko)");
Console.Write("Pasirinkimas (1/2/3/4): ");
var choice = Console.ReadLine();

Console.Write("Failo kelias (Enter - numatytasis pavyzdys): ");
var filePath = Console.ReadLine()?.Trim().Trim('"');

if (string.IsNullOrWhiteSpace(filePath))
{
    var defaultSample = choice == "1"
        ? "ocr-test.png"
        : "image-analysis-sample.jpg";

    var candidates = new[]
    {
        Path.Combine(Directory.GetCurrentDirectory(), "samples", defaultSample),
        Path.Combine(Directory.GetCurrentDirectory(), "CognitiveClient", "samples", defaultSample),
        Path.Combine(AppContext.BaseDirectory, "samples", defaultSample),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "samples", defaultSample)
    };

    filePath = candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    Console.WriteLine($"(Naudoju numatytąjį failą: {Path.GetFullPath(filePath)})");
}

if (!File.Exists(filePath))
{
    Console.WriteLine($"Klaida: failas nerastas: {filePath}");
    return;
}

var features = choice switch
{
    "1" => VisualFeatures.Read,
    "2" => VisualFeatures.Tags,
    "3" => VisualFeatures.Objects | VisualFeatures.Tags,
    "4" => VisualFeatures.Caption,
    _ => VisualFeatures.Read
};

try
{
    using var stream = File.OpenRead(filePath);

    Console.WriteLine("Siunčiama į Azure...");
    var imageData = BinaryData.FromStream(stream);
    var response = await client.AnalyzeAsync(imageData, features);
    var result = response.Value;

    if (choice == "1")
    {
        Console.WriteLine("\n--- Atpažintas tekstas ---");

        if (result.Read is null || result.Read.Blocks.Count == 0)
        {
            Console.WriteLine("(teksto nerasta)");
        }
        else
        {
            foreach (var block in result.Read.Blocks)
                foreach (var line in block.Lines)
                    Console.WriteLine(line.Text);
        }
    }
    else
    {
        if (result.Caption is not null)
            Console.WriteLine($"Caption: {result.Caption.Text} ({result.Caption.Confidence:P1})");

        if (result.Objects is not null)
        {
            Console.WriteLine("\n--- Objektai ---");

            if (result.Objects.Values.Count == 0)
            {
                Console.WriteLine("(objektų nerasta)");
            }
            else
            {
                foreach (var detectedObject in result.Objects.Values)
                {
                    var tag = detectedObject.Tags.First();
                    Console.WriteLine(
                        $"Objektas: {tag.Name} ({tag.Confidence:P1}), " +
                        $"ribos: {detectedObject.BoundingBox}");
                }
            }
        }

        if (result.Tags is not null)
        {
            Console.WriteLine("\n--- Žymos (Tags) ---");
            foreach (var tag in result.Tags.Values)
                Console.WriteLine($"Tag: {tag.Name} ({tag.Confidence:P1})");
        }
    }
}
catch (RequestFailedException ex)
{
    Console.WriteLine($"\nAzure klaida (HTTP {ex.Status}): {ex.Message}");
    Console.WriteLine("Patikrink .env endpoint/raktą, regioną ir failo formatą.");
}
catch (Exception ex)
{
    Console.WriteLine($"\nKlaida: {ex.Message}");
}