using Azure;
using Azure.AI.Vision.ImageAnalysis;
using DotNetEnv;

// Nuskaitome .env failą iš einamojo arba aukštesnių aplankų.
// Ten laikomi Azure endpoint ir raktas, kurių negalima kelti į Git.
Env.TraversePath().Load();

// Paimame prisijungimo duomenis iš aplinkos kintamųjų.
var endpoint = Environment.GetEnvironmentVariable("AZURE_VISION_ENDPOINT");
var key = Environment.GetEnvironmentVariable("AZURE_VISION_KEY");

// Jei trūksta bent vienos reikšmės, toliau dirbti nėra prasmės.
if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key))
{
    Console.WriteLine("Klaida: trūksta Azure endpoint arba rakto.");
    Console.WriteLine("Užpildyk .env failą (AZURE_VISION_ENDPOINT ir AZURE_VISION_KEY).");
    return;
}

// Sukuriame Azure Image Analysis klientą su endpoint adresu ir raktu.
var client = new ImageAnalysisClient(
    new Uri(endpoint),
    new AzureKeyCredential(key));

// Parodome pasirinkimų meniu vartotojui.
Console.WriteLine("1 - OCR / Read");
Console.WriteLine("2 - Tags");
Console.WriteLine("3 - Objects + Tags");
Console.WriteLine("4 - Caption (jei regionas palaiko)");
Console.Write("Pasirinkimas (1/2/3/4): ");
var choice = Console.ReadLine();

// Klausiame failo kelio; jei vartotojas nieko neįveda, naudosime pavyzdį.
Console.Write("Failo kelias (Enter - numatytasis pavyzdys): ");
var filePath = Console.ReadLine()?.Trim().Trim('"');

// Jei kelias neįvestas, parenkame numatytąjį pavyzdinį failą.
if (string.IsNullOrWhiteSpace(filePath))
{
    // OCR naudoja paveikslėlį su tekstu, kiti režimai – nuotrauką su objektais.
    var defaultSample = choice == "1"
        ? "ocr-test.png"
        : "image-analysis-sample.jpg";

    // Tikriname kelias galimas vietas, nes programa gali būti paleista
    // iš skirtingų aplankų (projekto šaknies, bin katalogo ir pan.).
    var candidates = new[]
    {
        Path.Combine(Directory.GetCurrentDirectory(), "samples", defaultSample),
        Path.Combine(Directory.GetCurrentDirectory(), "CognitiveClient", "samples", defaultSample),
        Path.Combine(AppContext.BaseDirectory, "samples", defaultSample),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "samples", defaultSample)
    };

    // Imame pirmą egzistuojantį kelią; jei nė vieno nėra – pirmą variantą klaidai parodyti.
    filePath = candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    Console.WriteLine($"(Naudoju numatytąjį failą: {Path.GetFullPath(filePath)})");
}

// Patikriname, ar failas tikrai egzistuoja prieš siųsdami į Azure.
if (!File.Exists(filePath))
{
    Console.WriteLine($"Klaida: failas nerastas: {filePath}");
    return;
}

// Pagal vartotojo pasirinkimą nustatome, kokias Azure funkcijas naudosime.
var features = choice switch
{
    "1" => VisualFeatures.Read,                              // teksto atpažinimas
    "2" => VisualFeatures.Tags,                              // paveikslėlio žymos
    "3" => VisualFeatures.Objects | VisualFeatures.Tags,     // objektai ir žymos
    "4" => VisualFeatures.Caption,                           // paveikslėlio aprašymas
    _ => VisualFeatures.Read                                 // neteisingas pasirinkimas -> OCR
};

try
{
    // Atidarome failą skaitymui; using automatiškai jį uždarys.
    using var stream = File.OpenRead(filePath);

    Console.WriteLine("Siunčiama į Azure...");

    // Azure SDK priima BinaryData, todėl srautą aprašome be rankinio krovimo.
    var imageData = BinaryData.FromStream(stream);

    // Siunčiame paveikslėlį ir pasirinktas funkcijas į Azure.
    var response = await client.AnalyzeAsync(imageData, features);
    var result = response.Value;

    // 1 pasirinkimas – spausdiname atpažintą tekstą po eilutę.
    if (choice == "1")
    {
        Console.WriteLine("\n--- Atpažintas tekstas ---");

        // Pasitaiko paveikslėlių be teksto, todėl tikriname, ar blokų yra.
        if (result.Read is null || result.Read.Blocks.Count == 0)
        {
            Console.WriteLine("(teksto nerasta)");
        }
        else
        {
            // Azure grąžina tekstą blokais, o blokuose – eilutes.
            foreach (var block in result.Read.Blocks)
                foreach (var line in block.Lines)
                    Console.WriteLine(line.Text);
        }
    }
    else
    {
        // 4 pasirinkimas – parodome aprašymą, jei Azure jį grąžino.
        if (result.Caption is not null)
            Console.WriteLine($"Caption: {result.Caption.Text} ({result.Caption.Confidence:P1})");

        // 3 pasirinkimas – parodome aptiktus objektus su jų ribomis.
        if (result.Objects is not null)
        {
            Console.WriteLine("\n--- Objektai ---");

            // Jei objektų nėra, aiškiai tai pranešame.
            if (result.Objects.Values.Count == 0)
            {
                Console.WriteLine("(objektų nerasta)");
            }
            else
            {
                foreach (var detectedObject in result.Objects.Values)
                {
                    // Imame pirmą (tiksliausią) objekto žymą.
                    var tag = detectedObject.Tags.First();
                    Console.WriteLine(
                        $"Objektas: {tag.Name} ({tag.Confidence:P1}), " +
                        $"ribos: {detectedObject.BoundingBox}");
                }
            }
        }

        // 2 ir 3 pasirinkimai – parodome visas paveikslėlio žymas.
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
    // Azure specifinė klaida: neteisingas raktas, regionas arba failo formatas.
    Console.WriteLine($"\nAzure klaida (HTTP {ex.Status}): {ex.Message}");
    Console.WriteLine("Patikrink .env endpoint/raktą, regioną ir failo formatą.");
}
catch (Exception ex)
{
    // Visos kitos klaidos (pvz., failo skaitymo problema).
    Console.WriteLine($"\nKlaida: {ex.Message}");
}
