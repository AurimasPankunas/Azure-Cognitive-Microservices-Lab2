# Lab 2 – Azure Cognitive Services klientinė programa

## Struktūra

```text
Lab2/
├─ CognitiveClient/       # Lab 2-1 klientinė programa
├─ .env                   # LOKALŪS sekretai (ignoruojamas Git)
├─ .env.example           # šablonas, keliamas į Git
├─ .gitignore
└─ README.md
```

## Pasiruošimas

1. Nusikopijuok šabloną į lokalių nustatymų failą:

```powershell
Copy-Item .env.example .env
```

2. Atidaryk `.env` ir įrašyk savo Azure reikšmes:

```env
AZURE_VISION_ENDPOINT=https://tavo-resursas.cognitiveservices.azure.com/
AZURE_VISION_KEY=tavo-rakto-reiksme
```

3. Paleisk programą:

```powershell
dotnet run --project CognitiveClient
```

## Kaip naudoti programą

Paleidus programą, ji paprašys:

```text
1 - OCR / Read
2 - Tags
3 - Objects + Tags
4 - Caption (jei regionas palaiko)
Pasirinkimas (1/2/3/4):
```

- **1** – teksto atpažinimas (OCR). Tinka nuotraukoms su tekstu, dokumentų nuotraukoms, ekrano kopijoms.
- **2** – nuotraukos žymos (tags).
- **3** – objektų atpažinimas ir žymos.
- **4** – nuotraukos aprašymas (caption). **Caption palaikomas ne visuose Azure regionuose** – jei gauni `400 The feature 'Caption' is not supported in this region`, naudok 1–3 variantus arba pasirink kitą regioną.

Tada įvesk failo kelią. Jei tik paspausi **Enter**, bus naudojamas numatytasis pavyzdys:

- pasirinkus **1** → `samples/ocr-test.png` (paveikslėlis su tekstu),
- pasirinkus **2, 3 arba 4** → `samples/image-analysis-sample.jpg` (nuotrauka su objektais).

Pavyzdys:

```text
Pasirinkimas (1/2/3/4): 1
Failo kelias (Enter - numatytasis pavyzdys):
(Naudoju numatytąjį failą: ...\CognitiveClient\samples\ocr-test.png)
Siunčiama į Azure...

--- Atpažintas tekstas ---
LAB 2 OCR TEST
Kaunas University of Technology
Azure Cognitive Services
Image Analysis - Read API
Date: 2026-11-09
This text should be recognized by Azure.
```

Tags pavyzdys su `samples/image-analysis-sample.jpg`:

```text
Pasirinkimas (1/2/3/4): 2
Failo kelias (Enter - samples/ocr-test.png): samples/image-analysis-sample.jpg
Tag: furniture (98.7%)
Tag: clothing (97.9%)
Tag: person (94.3%)
...
```

### Testiniai failai

- `samples/ocr-test.png` – dirbtinai sukurtas paveikslėlis su tekstu, skirtas OCR testui.
- `samples/image-analysis-sample.jpg` – Microsoft pavyzdinė nuotrauka, skirta Caption / Tags / Objects testams.

### Svarbu dėl PDF

Naujasis **Image Analysis 4.0 Read** SDK skirtas paveikslėliams (JPG, PNG, BMP, GIF, TIFF, WEBP). Jei nori analizuoti **PDF**, naudok senąjį **Read API 3.2** (`Microsoft.Azure.CognitiveServices.Vision.ComputerVision`) arba pirmiausia konvertuok PDF puslapį į PNG/JPG.

## Saugumas

- `.env` yra **ignoruojamas** `.gitignore` ir į GitHub/GitLab nepatenka.
- Į repo keliamas tik `.env.example` su placeholderiais.
- Niekada neįkelk Azure rakto į viešą repo. Jei raktas netyčia pateko į Git istoriją – iš karto jį regeneruok Azure portale.

## Naudingos komandos

```powershell
# Patikrinti, ar .env tikrai ignoruojamas
git check-ignore -v .env

# Patikrinti, kokie failai bus įkelti
git status --short

# Build
dotnet build
```
