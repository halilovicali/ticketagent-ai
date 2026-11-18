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
│   ├── Hubs               SignalR emit rezultata runnera
│   └── wwwroot            lokalni dashboard za korisnika i recenzenta
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
| Learn | Aktivira novi model, veže korištene review ID-jeve i troši samo korekcije iz snapshota. |

## Preduslovi

- .NET 10 SDK
- Nisu potrebni vanjski DB server ni Python.
- SQLite baza, seed dataset i prvi ML model kreiraju se automatski.

## Pokretanje

```bash
dotnet restore AiAgents.SupportTicketTriage.sln
dotnet run --project src/AiAgents.SupportTicketAgent.Web
```

Web dashboard i API se prema launch profilu pokreću na `http://localhost:5080`.

Provjera:

```bash
curl http://localhost:5080/health
curl http://localhost:5080/api/settings
```

Za ručni demo otvorite `http://localhost:5080` u browseru. Stranica omogućava slanje ticketa, automatsko praćenje statusa, pregled odluke i unos gold korekcije.

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

Defaultni prag je 5 novih gold labela. Kada se dostigne, `RetrainAgentRunner` kreira snapshot korekcija, trenira i aktivira novu verziju modela. Counter umanjuje samo za korekcije iz snapshota; korekcije koje stignu tokom treninga ostaju za sljedeću verziju. Verzije se vide na:

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
| `GET` | `/api/models/{id}/reviews` | Tačan skup korekcija korištenih za verziju. |
| `GET` | `/health` | Health check. |
| SignalR | `/hubs/agent-events` | Eventi `triageCompleted` i `modelRetrained`. |

## Pouzdanost

- `Queued → Processing` koristi conditional update, pa dva workera ne mogu preuzeti isti ticket.
- Prediction i finalni status snimaju se jednim `SaveChanges` pozivom, koji EF izvršava transakcijski.
- Neuspjelo čitanje nakon claima vraća ticket u `Queued` uz maksimalno tri pokušaja.
- Neuspjeli završni prediction upis završava kao `Failed` i isti insert se ne ponavlja.
- Prekinuti `Processing` ticketi se pri restartu oporavljaju uz isti attempt limit.
- Svaka verzija modela čuva cutoff i audit vezu sa korištenim review ID-jevima.
- Prazan queue vraća `null` bez exceptiona i worker radi backoff.
- Tick ne sadrži `Delay`, HTTP, SignalR ni host logiku.
- Worker kreira novi DI scope za svaki tick i poštuje cancellation token.
- Dataset split je determinističan: 80% train / 20% test.

## Testovi

```bash
dotnet test AiAgents.SupportTicketTriage.sln
```

Testovi provjeravaju threshold policy, no-work izlaz, obradu samo jednog ticketa, oba recovery scenarija, validaciju enum vrijednosti, snapshot korekcija tokom treninga i stvarnu promjenu ML.NET odluke između dvije verzije.

Trenutni verifikovani rezultat: **14/14 testova prolazi**, a Release build ima **0 warninga i 0 grešaka**.

Ponovljivi learning scenario i snimljeni rezultat opisani su u [docs/LEARNING-DEMO.md](docs/LEARNING-DEMO.md).

## Podaci i modeli

- SQLite: `data/support-ticket-agent.db`
- Modeli: `data/models/model-vN/`
- Runtime datoteke su ignorisane u Git-u i automatski se ponovo kreiraju.
- Seed sadrži neutralne sintetičke support primjere; ne sadrži lične podatke.

## Sigurnost i opseg demoa

Ova verzija je namjerno ograničena na localhost kroz `LocalDemoOnlyMiddleware`. Nije namijenjena vanjskom deploymentu bez autentifikacije. Za vanjski pristup potrebno je dodati stvarne korisničke sesije i role te identitet recenzenta uzimati iz authenticated session claima, ne iz request body-ja.

## LLM korištenje

Inženjerski zapis stvarnog korištenja LLM alata nalazi se u [docs/LLM-USAGE.md](docs/LLM-USAGE.md).
