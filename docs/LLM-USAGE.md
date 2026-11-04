# Evidencija korištenja LLM alata

Ovaj dokument služi kao polazna evidencija za dio ocjene koji se odnosi na inženjerski pristup korištenju LLM alata. Student treba razumjeti, provjeriti i moći objasniti svaku stavku prije predaje.

## Cilj

LLM je korišten kao razvojni saradnik za pretvaranje zahtjeva profesora u provjerljivu .NET arhitekturu Support Ticket Triage agenta.

## Primjeri zadataka datih LLM-u

1. Analizirati arhitektonske zahtjeve `Sense → Think → Act → Learn` i Clean Architecture.
2. Predložiti temu drugačiju od Spam Agenta.
3. Implementirati .NET rješenje u kojem Web ostaje tanki host.
4. Napraviti siguran queue claim i konzistentan upis prediction/status podataka.
5. Odvojiti ML.NET iza aplikacijskog interfejsa.
6. Napisati testove za agent tick, no-work izlaz i retrain pravilo.
7. Pokrenuti build, testove i stvarni API/worker integracijski tok.

## Odluke koje je student/razvojni proces eksplicitno provjerio

- Izabrana su dva obavezna runnera: triage i retrain.
- Endpoint samo enqueue-a ticket; klasifikacija nije unutar HTTP requesta.
- Confidence i retrain pravila nisu u Web projektu.
- ML model je verzioniran, a aktivna verzija je pohranjena u bazi.
- Gold label dolazi isključivo kroz ljudski review.
- Queue koristi atomsku promjenu statusa radi zaštite od duple obrade.
- Restart oporavlja nedovršene `Processing` tickete.

## Pronađene i ispravljene greške

- Prvi EF test je otkrio stale tracked entity nakon `ExecuteUpdateAsync`. Nakon atomskog claim-a uveden je `ChangeTracker.Clear()` prije ponovnog učitavanja ticketa.
- Test projekt je u prvom buildu imao nedostajući globalni `Xunit` import; dodat je i kompletan test suite je ponovo izvršen.
- SDK okruženje nije imalo ICU runtime; korišten je projektni launcher s lokalnom ICU bibliotekom umjesto nesigurne sistemske izmjene.

## Verifikacija

- Release build: bez warninga i grešaka.
- Automatizovani testovi: 6/6 prolazi.
- Stvarni integracijski test: API je vratio `Queued`, background runner je zatim klasifikovao primjer kao `Technical/Urgent`, postavio `Routed`, dodijelio `Technical Support` i sačuvao model version ID.

## Šta dopuniti prije predaje

- Sačuvati dodatne vlastite promptove i kratko opisati zašto su korišteni.
- Navesti barem jedan prijedlog LLM-a koji je odbijen ili izmijenjen i razlog.
- Zapisati rezultate vlastitog code review-a.
- Ne tvrditi da je kod razumljiv dok se ne može usmeno objasniti runner, transakcija, threshold i retraining tok.

