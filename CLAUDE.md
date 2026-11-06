# CardExchange: regole di prodotto

Regole che valgono per ogni modifica al codice. Prima di proporre una funzione, controllare di non violarne nessuna.

## Nessuna transazione tra utenti
- L'app non gestisce pagamenti tra utenti né chiude vendite: ci si accorda e si paga di persona.
- Prezzi indicati dagli utenti e valore di mercato sono solo riferimenti.
- Prenotazioni in negozio, liste acquisti dei negozi e scambi si concludono e si pagano di persona.
- Passano da Stripe solo i pagamenti verso CardExchange (abbonamenti, promozioni, pacchetti).

## Neutralità verso i negozi
I negozi sono il canale principale: l'app non deve mai metterli in competizione con i privati.
- Nella scheda di un negozio e sulle sue carte non compaiono confronti di prezzo con altri venditori
  (niente "disponibile a meno da…", né elenchi di privati con la stessa carta).
- Nei risultati di ricerca negozi e privati sono mescolati per distanza; il valore di mercato resta solo un riferimento.
- Quando due utenti fissano un incontro, i negozi partner sono proposti per primi come punto di scambio.
- I negozi vedono dati aggregati sui loro clienti, mai identità senza un'azione esplicita dell'utente.

## Privacy e minorenni
- Iscrizione dai 14 anni (`User.BirthDate`). Ai minorenni non si mostrano promozioni basate su profilazione.
- Distanze mostrate arrotondate al km (minimo 1); email e CAP degli altri utenti non vengono mai esposti.
- Dati aggregati mostrati ai negozi solo con almeno 3 utenti per valore.

## Codice
- Backend ASP.NET Core 9 + EF Core (SQL Server in produzione, migrazioni in `CardExchange.Infrastructure/Migrations`).
- Frontend React 19 + TypeScript + Tailwind in `card-exchange-frontend`; testi in `src/i18n/locales/{it,en}`.
- Testi del server (messaggi API, notifiche, email) scritti in italiano nel codice; la traduzione inglese va in `CardExchange.API/Localization/server-text.en.json` (frasi esatte o modelli con `{0}`, `{n}`, `{when}`, `{month}`).
- Piani e diritti: listino in `Core/Rules/Entitlements.cs` (`PlanCatalog`); ogni controllo di piano passa da `IEntitlementService` (`[RequireCapability]`, `HasAsync`, `LimitAsync`), mai da `Plan.Tier`.
- Transazioni sul database con `InTransactionAsync` (compatibile con la strategia di retry di SQL Server).
- Test: `dotnet test CardExchange.Tests`; frontend `npx tsc -b` e `npx eslint src`.
