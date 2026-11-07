# Support Ticket Triage Agent

Autonomni softverski agent za klasifikaciju, prioritizaciju i usmjeravanje korisničkih zahtjeva. Projekat je implementiran za predmet **Umjetna inteligencija** prema arhitekturi **Sense → Think → Act → Learn** i principima Clean Architecture.

Agent nije obična request/response ML aplikacija. HTTP API samo prima zahtjev i stavlja ga u red, dok dva pozadinska runnera samostalno izvršavaju kratke korake:

- `TriageAgentRunner` obrađuje tačno jedan ticket po koraku.
- `RetrainAgentRunner` provjerava gold-label prag i po potrebi trenira novu verziju modela.

## Arhitektura

```text
AiAgents.SupportTicketTriage.sln
├── src/AiAgents.Core
│   └── generičke agent apstrakcije bez EF-a, ML.NET-a i domenskog znanja
├── src/AiAgents.SupportTicketAgent
│   ├── Domain             entiteti, enum-i, invarijante i TriagePolicy
│   ├── Application
│   │   ├── Abstractions   portovi prema bazi, modelu i file storage-u
│   │   ├── Services       use-case servisi
│   │   └── Runners        agentički Sense/Think/Act/Learn ciklusi
│   ├── Infrastructure     EF Core, SQLite, seeder, queue i model paths
│   └── ML                 ML.NET klasifikator kao crna kutija
├── src/AiAgents.SupportTicketAgent.Web
│   ├── Controllers        tanki transport i DTO mapping
│   ├── Workers            scheduling, scope, delay i cancellation
│   └── Hubs               SignalR emit rezultata runnera
└── tests/AiAgents.SupportTicketAgent.Tests
```

### Triage tick

| Faza | Odgovornost |
|---|---|
| Sense | Atomski preuzme jedan `Queued` ticket i učita settings + aktivni model. |
| Think | ML.NET predviđa kategoriju/prioritet; `TriagePolicy` primjenjuje confidence threshold. |
| Act | U istoj DB promjeni upisuje `Prediction` i postavlja `Routed` ili `PendingReview`. |
| Output | Vraća `TriageTickResult`, koji Web worker može logirati i emitovati preko SignalR-a. |

### Retrain tick

| Faza | Odgovornost |
|---|---|
| Sense | Čita `SystemSettings`, gold counter i aktivni model. |
| Think | Provjerava `RetrainingEnabled` i `NewGoldSinceLastTrain >= RetrainGoldThreshold`. |
| Act | Trenira kategorijski i prioritetni ML.NET model te kreira novu `ModelVersion`. |
| Learn | Aktivira novi model i resetuje gold counter. |

## Preduslovi

- .NET 10 SDK
- Nisu potrebni vanjski DB server ni Python.
- SQLite baza, seed dataset i prvi ML model kreiraju se automatski.

## Pokretanje

```bash
dotnet restore AiAgents.SupportTicketTriage.sln
dotnet run --project src/AiAgents.SupportTicketAgent.Web
```

API se prema launch profilu pokreće na `http://localhost:5080`.

Provjera:

```bash
curl http://localhost:5080/health
curl http://localhost:5080/api/settings
```

## Demonstracija kompletnog toka

### 1. Predaja ticketa

```bash
curl -X POST http://localhost:5080/api/tickets \
  -H 'Content-Type: application/json' \
  -d '{
    "subject": "Production API is down",
    "description": "Every customer receives server errors and cannot place orders",
    "requesterEmail": "ops@example.com"
  }'
```

API odmah vraća HTTP `202` i status `Queued`. Ne poziva klasifikator.

### 2. Provjera rezultata pozadinskog runnera

```bash
curl http://localhost:5080/api/tickets/1
```

Nakon ticka rezultat sadrži predviđenu kategoriju/prioritet, confidence, tim, finalni status i ID modela.

### 3. Gold-label review

```bash
curl -X POST http://localhost:5080/api/tickets/1/reviews \
  -H 'Content-Type: application/json' \
  -d '{
    "category": "Technical",
    "priority": "Urgent",
    "reviewer": "student"
  }'
```

Review ispravlja routing ako je potrebno i povećava `NewGoldSinceLastTrain`. Jedan ticket može imati samo jedan gold label.

### 4. Retraining

Defaultni prag je 5 novih gold labela. Kada se dostigne, `RetrainAgentRunner` kreira i aktivira novu verziju modela, a zatim resetuje counter. Verzije se vide na:

```bash
curl http://localhost:5080/api/models
```

Za bržu demonstraciju prag se može smanjiti:

```bash
curl -X PUT http://localhost:5080/api/settings \
  -H 'Content-Type: application/json' \
  -d '{
    "autoRouteThreshold": 0.60,
    "retrainGoldThreshold": 1,
    "retrainingEnabled": true
  }'
```

## API

| Metoda | Ruta | Namjena |
|---|---|---|
| `POST` | `/api/tickets` | Validira i enqueue-a ticket; vraća `202`. |
| `GET` | `/api/tickets?limit=50` | Lista ticketa. |
| `GET` | `/api/tickets/{id}` | Ticket sa prediction i review podacima. |
| `POST` | `/api/tickets/{id}/reviews` | Upis ljudskog gold labela. |
| `GET` | `/api/settings` | Threshold, retrain prag i counter. |
| `PUT` | `/api/settings` | Ažuriranje postavki. |
| `GET` | `/api/models` | Historija i aktivna verzija modela. |
| `GET` | `/health` | Health check. |
| SignalR | `/hubs/agent-events` | Eventi `triageCompleted` i `modelRetrained`. |

## Pouzdanost

- `Queued → Processing` koristi conditional update, pa dva workera ne mogu preuzeti isti ticket.
- Prediction i finalni status snimaju se jednim `SaveChanges` pozivom, koji EF izvršava transakcijski.
- Prekinuti `Processing` ticketi se pri restartu vraćaju u `Queued`.
- Prazan queue vraća `null` bez exceptiona i worker radi backoff.
- Tick ne sadrži `Delay`, HTTP, SignalR ni host logiku.
- Worker kreira novi DI scope za svaki tick i poštuje cancellation token.
- Dataset split je determinističan: 80% train / 20% test.

## Testovi

```bash
dotnet test AiAgents.SupportTicketTriage.sln
```

Testovi provjeravaju threshold policy, no-work izlaz, obradu samo jednog ticketa, prediction persistence, retrain uslov, aktivaciju modela i reset countera.

## Podaci i modeli

- SQLite: `data/support-ticket-agent.db`
- Modeli: `data/models/model-vN/`
- Runtime datoteke su ignorisane u Git-u i automatski se ponovo kreiraju.
- Seed sadrži neutralne sintetičke support primjere; ne sadrži lične podatke.

## LLM korištenje

Inženjerski zapis korištenja LLM alata nalazi se u [docs/LLM-USAGE.md](docs/LLM-USAGE.md). Prije predaje ga dopuniti vlastitim promptovima, odlukama i provjerama.
