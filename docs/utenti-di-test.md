# Utenti di test

Utenti creati in automatico all'avvio del backend in **sviluppo**
(`ASPNETCORE_ENVIRONMENT=Development`), per i test manuali. In produzione non vengono mai creati.

**Password per tutti: `Test1234!`**

Per accedere usa il nome utente (oppure l'email) e la password.

| Nome utente | Email | Tipo di profilo | Ruolo | Cosa serve a provare |
|---|---|---|---|---|
| `test.admin` | testadmin@test.cardexchange.local | Collezionista | SuperAdmin, Admin | Amministrazione: `/admin/users` (cambio tipo di profilo, sospensioni), moderazione, metriche, sponsor |
| `test.moderatore` | testmoderatore@test.cardexchange.local | Collezionista | Moderator | Moderazione e segnalazioni di sicurezza; **non** ha accesso a `/admin/users` |
| `test.collezionista` | testcollezionista@test.cardexchange.local | Collezionista | Utente | Utente base: collezione, wishlist, scambi, chat. Ha "Negozio Test" come negozio di casa |
| `test.collezionista2` | testcollezionista2@test.cardexchange.local | Collezionista | Utente | Controparte per scambi, chat e incontri con `test.collezionista` |
| `test.minorenne` | testminorenne@test.cardexchange.local | Collezionista (15 anni) | Utente | Regole di sicurezza con i minorenni: chat solo dentro uno scambio, orari, incontri solo in negozio partner tra le 9 e le 20, niente promozioni profilate |
| `test.venditore` | testvenditore@test.cardexchange.local | Venditore | Utente | Funzioni da venditore: vetrina (Banco), avvisi "Sono qui" |
| `test.negozio` | testnegozio@test.cardexchange.local | Negozio | Utente | Titolare di **Negozio Test** (verificato e partner): QR, risultati, fedeltà, lista acquisti, prenotazioni, domanda locale, tornei |
| `test.organizzatore` | testorganizzatore@test.cardexchange.local | Organizzatore | Utente | Creazione di eventi e gestione degli espositori |
| `test.senzaeta` | testsenzaeta@test.cardexchange.local | Collezionista, data di nascita mancante | Utente | Al primo accesso l'app chiede la data di nascita prima di continuare |
| `test.nonverificato` | testnonverificato@test.cardexchange.local | Collezionista, email da confermare | Utente | Avviso "Conferma la tua email" e reinvio del link |
| `test.daqr` | testdaqr@test.cardexchange.local | Collezionista | Utente | Utente iscritto dal QR del negozio: segue Negozio Test, che è il suo negozio di casa, e compare nei risultati del negozio |

## Negozio di prova

- **Negozio Test**, Via Torino 40, Milano. È verificato e partner, ed è un punto d'incontro sicuro.
- Il titolare è `test.negozio`.
- Link del QR: `/s/negozio-test-milano`. Aprilo da disconnesso per provare l'iscrizione dal QR, oppure da connesso per provare "segui" e "negozio di casa".

## Se l'accesso dice "Credenziali non valide"

1. **Verifica di avere l'ultima versione.** Fai il pull del branch e ricompila la soluzione: in Visual Studio Compila → Ricompila soluzione.
2. **Controlla il log all'avvio del backend.** Deve comparire questa riga:
   `Utenti di prova: N creati, 11 presenti in totale (password Test1234!). Database: <nome>`.
   - Se la riga non compare, l'ambiente non è Development: controlla il profilo di avvio, o `ASPNETCORE_ENVIRONMENT`.
   - Se compare `Utenti di prova NON creati: …`, il messaggio spiega il motivo.
   - Il nome del database ti dice su quale database sono stati creati: deve essere lo stesso che usa il frontend.
3. **Creali a mano da Swagger.** Apri `/swagger`, sezione **Dev**, e lancia **POST /api/dev/test-users**. La risposta elenca gli utenti presenti e il database usato. In caso di problemi mostra l'errore completo. Funziona solo in sviluppo.

## Da sapere

- Gli utenti si trovano in zone diverse di Milano e dintorni, con raggio di ricerca di 100 km, così le distanze tra loro sono realistiche.
- Il seeding crea solo ciò che manca. Un utente già presente non viene modificato, quindi le modifiche fatte durante i test restano anche dopo il riavvio.
- Per riportare un utente allo stato iniziale, cancellalo dal database e riavvia il backend. Con SQLite in sviluppo basta anche cancellare il file del database.
- Gli utenti partono senza carte. Per riempire le collezioni con carte vere usa `python tools/seed_demo.py` con il backend avviato. Lo script crea anche gli utenti dimostrativi (`demo`, `giulia.tcg`, …) con password `Demo1234!`.
- Per non creare gli utenti di test, imposta `"Seed": { "TestUsers": false }` in `CardExchange.API/appsettings.Development.json`.
- Il codice è in `CardExchange.API/Services/Seeding/TestUsersSeeder.cs`. Se cambi gli utenti lì, aggiorna anche questo file.
