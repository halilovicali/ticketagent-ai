# Evidencija stvarnog korištenja LLM alata

Ovo nije generički opis. Ispod je stvarni redoslijed važnijih upita, sažetak odgovora i odluka koje su nakon toga provjerene u kodu.

## 1. Razumijevanje zadatka i izbor agenta — 04.11.2026.

**Upit:** Profesorove upute su proslijeđene uz pitanje: *"What exactly do I have to do or make so I can submit this and attend the exam?"*

**Odgovor/rezultat:** LLM je zahtjeve preveo u checklistu: autonomni background runner, tanki Web host, shared logika, Sense–Think–Act–Learn, siguran queue, review i retraining. Posebno je izdvojeno da API ne smije raditi klasifikaciju unutar HTTP requesta.

**Moja odluka:** Tražio sam temu različitu od Spam Agenta. Među prijedlozima sam izabrao **Support Ticket Triage Agent** zato što prirodno ima percept (ticket), odluku (kategorija/prioritet), akciju (routing) i učenje (gold korekcije).

## 2. Razrada STAL toka

**Upit:** *"Do this — Support Ticket Triage Agent."*

**Odgovor/rezultat:** Razrađena su dva ciklusa:

- `TriageAgentRunner`: Sense = claim jednog ticketa; Think = ML prediction + threshold policy; Act = prediction + status + tim.
- `RetrainAgentRunner`: Sense = settings/model/counter; Think = retrain uslov; Act = trening i verzioniranje; Learn = aktivacija novog modela i potrošnja korištenih korekcija.

**Provjera:** Runneri se nalaze u shared projektu. `BackgroundService` klase u Web projektu samo kreiraju scope, pozivaju jedan tick, rade delay i emituju SignalR događaj.

## 3. Izbor tehnologije i odbijeni prijedlog

**Prvobitni prijedlog LLM-a:** Pošto .NET SDK nije bio instaliran, predložena je zero-dependency Python implementacija.

**Moja korekcija:** Odgovorio sam: *"It has to be in .NET."* Python početak je uklonjen, instaliran je .NET SDK i rješenje je implementirano kao ASP.NET Core + EF Core/SQLite + ML.NET. Kasnije je cijelo rješenje prebačeno na .NET 10 i ponovo testirano.

Ovo je primjer prijedloga koji nije prihvaćen jer nije odgovarao ograničenju projekta.

## 4. Implementacija i proširenja koja su razmotrena

**Upiti:** Traženi su implementacija, pokretanje primjera, objašnjenje funkcija i priprema za ispit.

**Rezultat:** Implementirani su queue, dvije vrste runnera, review, settings, SignalR, model versioning i testovi. Razmotrena su sljedeća proširenja:

- grafički dashboard umjesto samo API-ja;
- autentifikacija i role za vanjski deployment;
- state-machine trening za veliki dataset;
- trajniji DB server umjesto SQLite-a.

Dashboard je sada implementiran. Autentifikacija nije lažno simulirana: ova verzija je namjerno ograničena na localhost, a dokumentovano je da vanjski deployment mora koristiti stvarne sesije i role.

## 5. Izmjena nakon code review-a

**Problem koji je pronađen testom:** `ExecuteUpdateAsync` je promijenio status u bazi, ali je EF ChangeTracker zadržao staru `Queued` instancu. Završni upis je zato vidio nekonzistentan status.

**Izmjena:** Nakon atomskog claima uveden je `ChangeTracker.Clear()` i ticket se ponovo učitava. Dodani su testovi koji potvrđuju obradu samo jednog ticketa i konzistentan status.

Ovo je konkretna stvar izmijenjena nakon pregleda izvršavanja koda, a ne samo nakon generisanja teksta.

## 6. Izmjene nakon profesorovog pregleda — 18.11.2026.

**Ulaz:** Profesorov mail sa sedam numerisanih dorada.

**LLM analiza i urađene promjene:**

1. Dodan je lokalni web dashboard za ticket, status, rezultat i gold korekciju.
2. Napravljen je stvarni, deterministični ML.NET learning test: isti tekst prelazi sa `General/Low` (model 1) na `Billing/Urgent` (model 2), a aktivni model opstaje kroz novi DB kontekst.
3. Trening sada uzima snapshot review ID-jeva. Model ima audit vezu prema svakoj korištenoj korekciji. Korekcija koja stigne tokom treninga ostaje u counteru.
4. Razdvojeni su recovery nakon claim/read greške i recovery nakon završnog DB upisa. Neuspjeli prediction insert se ne ponavlja.
5. Nullable web contract + `EnumDataType` i application `Enum.IsDefined` odbijaju izostavljene i `999` enum vrijednosti.
6. Ovaj dokument je zamijenio raniju generičku evidenciju stvarnim slijedom upita i odluka.
7. Aplikacija je eksplicitno localhost-only. Za vanjski pristup dokumentovan je zahtjev za ASP.NET Identity/SSO sesiju i role.

## 7. Način verifikacije LLM rezultata

LLM odgovor nije prihvaćen kao dokaz sam po sebi. Svaka važna promjena je provjerena kroz:

- kompajliranje cijelog solutiona sa warnings-as-errors;
- automatizovane unit/integration testove;
- stvarni ML.NET trening, ne samo fake klasifikator;
- HTTP pozive prema lokalnom ASP.NET hostu;
- pregled DB stanja prije i poslije simuliranog restarta.

Konačnu arhitekturu i ograničenja moram moći objasniti bez oslanjanja na ovaj dokument.
