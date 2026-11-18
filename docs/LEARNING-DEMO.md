# Ponovljiv primjer učenja

Ovaj scenario koristi stvarni `MlNetSupportTicketClassifier`, deterministični seed `42`, svježu SQLite bazu i isti tekst prije i poslije retreninga.

## Pokretanje

```bash
dotnet test tests/AiAgents.SupportTicketAgent.Tests \
  -c Release \
  --filter "Category=LearningDemo" \
  --logger "console;verbosity=detailed"
```

## Koraci scenarija

1. Seeder trenira i aktivira model 1.
2. Tekst `ZORB ledger synchronization ZORB ledger synchronization needs a decision today` šalje se starom modelu.
3. Zabilježe se DB ID, verzija i automatska odluka.
4. Unese se 12 gold korekcija sa istim tekstom i drugačijom ispravnom oznakom.
5. Trening snapshot pamti review ID-jeve `1–12`.
6. Trenira se i aktivira model 2 uz iste postavke.
7. Isti tekst se ponovo klasifikuje.
8. Kreira se novi EF `DbContext`, što simulira restart procesa, i provjerava se da je model 2 još aktivan.

## Snimljeni rezultat

```text
BEFORE modelDbId=1, version=1: General/Low
AFTER  modelDbId=2, version=2: Billing/Urgent
Used corrections=12, cutoffReviewId=12
Passed: 1, Failed: 0
```

Test ne provjerava samo da postoji nova verzija. Provjerava da se odluka promijenila, da su ID-jevi različiti, da su korištene korekcije vezane za model i da aktivna verzija opstaje poslije restarta.

Za ručnu demonstraciju isti tok se može izvesti kroz web stranicu na `http://localhost:5080`: poslati početni ticket, kreirati slične tickete, unijeti gold korekcije i posmatrati sekciju **Historija učenja**.
