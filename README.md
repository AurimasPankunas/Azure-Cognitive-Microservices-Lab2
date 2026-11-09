# Lab 2 – Azure Cognitive Services klientinė programa

## Struktūra

```text
Lab2/
├─ CognitiveClient/            # Lab 2-1 klientinė programa
├─ src/
│  ├─ ImageAnalysis.Api/       # Mikroservisas 1: Azure OCR + RabbitMQ publisher
│  ├─ AnalysisHistory.Api/     # Mikroservisas 2: RabbitMQ consumer + SQLite istorija
│  └─ Shared.Contracts/        # Bendra žinutės sutartis tarp servisų
├─ docker-compose.yml          # RabbitMQ + abu mikroservisai
├─ Lab2.slnx                   # Sprendimo failas
├─ .env                        # LOKALŪS sekretai (ignoruojamas Git)
├─ .env.example                # šablonas, keliamas į Git
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

## Lab 2-2 – mikroservisai su Docker Compose

### Architektūra

```text
Klientas (curl / Swagger)
        │  POST /api/analysis/upload
        ▼
┌─────────────────────────┐
│ ImageAnalysis.Api        │  1. priima paveikslėlį
│ (Azure AI Vision Read)   │  2. ištraukia tekstą per Azure OCR
└───────────┬─────────────┘
            │ ImageAnalyzedEvent (JSON)
            ▼
     ┌─────────────┐
     │  RabbitMQ   │  eilė: analysis-history
     └──────┬──────┘
            ▼
┌─────────────────────────┐
│ AnalysisHistory.Api      │  3. perskaito žinutę
│ (consumer + SQLite)      │  4. suskaičiuoja statistiką
└─────────────────────────┘  5. išsaugo istoriją
            │
            ▼
   GET /api/history/all
   GET /api/history/{id}
   GET /api/history/{id}/report
```

Kodėl du servisai:

- **ImageAnalysis.Api** atsakingas tik už Azure kvietimus ir rezultato publikavimą.
- **AnalysisHistory.Api** atsakingas tik už duomenų saugojimą ir ataskaitas.
- Servisai nebendrauja tiesiogiai – juos sieja RabbitMQ eilė, todėl istorijos servisas gali būti išjungtas, o žinutės laukia eilėje.

### Paleidimas

1. Įsitikink, kad `.env` turi ir RabbitMQ kintamuosius:

```env
AZURE_VISION_ENDPOINT=https://tavo-resursas.cognitiveservices.azure.com/
AZURE_VISION_KEY=tavo-rakto-reiksme
RABBITMQ_USER=lab2
RABBITMQ_PASS=lab2pass
```

2. Paleisk visus servisus:

```powershell
docker compose up -d --build
```

3. Patikrink būseną:

```powershell
docker compose ps
```

Turi būti trys konteineriai: `lab2-rabbitmq-1`, `lab2-image-analysis-api-1`, `lab2-analysis-history-api-1`.

### Prieigos taškai

| Servisas | URL | Paskirtis |
|---|---|---|
| Image Analysis API | `http://localhost:8081/swagger` | Swagger UI, OCR įkėlimas |
| Image Analysis health | `http://localhost:8081/health` | Būsenos patikra |
| Analysis History API | `http://localhost:8082/swagger` | Swagger UI, istorija |
| Analysis History health | `http://localhost:8082/health` | Būsenos patikra |
| RabbitMQ UI | `http://localhost:15672` | Eilių stebėjimas |

### Testavimas

Įkelti paveikslėlį ir ištraukti tekstą:

```powershell
curl.exe -s -X POST http://localhost:8081/api/analysis/upload `
  -F "files=@CognitiveClient/samples/ocr-test.png"
```

Peržiūrėti visą istoriją (naujausi įrašai pirmi):

```powershell
Invoke-RestMethod http://localhost:8082/api/history/all | ConvertTo-Json -Depth 5
```

Peržiūrėti vieną įrašą:

```powershell
Invoke-RestMethod http://localhost:8082/api/history/<id> | ConvertTo-Json -Depth 5
```

Atsisiųsti tekstinę ataskaitą:

```powershell
curl.exe -O http://localhost:8082/api/history/<id>/report
```

### Asinchroniškumo patikra

1. Sustabdyk istorijos servisą:

```powershell
docker compose stop analysis-history-api
```

2. Įkelk paveikslėlį – jis vis tiek bus apdorotas Azure ir žinutė pateks į eilę.
3. Patikrink eilę:

```powershell
docker exec lab2-rabbitmq-1 rabbitmqctl list_queues name messages
```

4. Vėl paleisk servisą:

```powershell
docker compose start analysis-history-api
```

Žinutė bus automatiškai perskaityta ir įrašyta į SQLite.

### Sustabdymas

```powershell
docker compose down
```

Duomenis ištrinti kartu su volume:

```powershell
docker compose down -v
```

### Screenshot sąrašas ataskaitai

- [ ] `docker compose ps` – visi trys konteineriai `Up` / `healthy`.
- [ ] `POST /api/analysis/upload` Swagger arba curl – grąžintas atpažintas tekstas.
- [ ] RabbitMQ UI `Queues → analysis-history` – matoma eilė ir pranešimų skaičius.
- [ ] `GET /api/history/all` – įrašas su žodžių skaičiumi.
- [ ] `GET /api/history/{id}` – pilnas tekstas ir raktažodžiai.
- [ ] `GET /api/history/{id}/report` – atsisiųsta `.txt` ataskaita.
- [ ] Sustabdyto istorijos serviso testas – žinutė laukia eilėje, po paleidimo suvartojama.

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
