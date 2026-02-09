# CardExchange Platform

Piattaforma API per lo scambio di carte collezionabili con modello freemium.

## Stack Tecnologico

- **.NET 9.0** / C# 12+ - ASP.NET Core Web API
- **Entity Framework Core 9.0** + SQL Server
- **JWT Authentication** + RBAC (ruoli e permessi)
- **FluentValidation** - Validazione input
- **BCrypt** - Hashing password
- **Docker** + Docker Compose

## Quick Start

### Con Docker (consigliato)

```bash
# Clona il repository
git clone <url-repository>
cd CardExchange

# Copia e configura le variabili d'ambiente
cp .env.example .env
# Modifica .env con le tue credenziali

# Avvia i servizi
docker compose up -d

# L'API sarà disponibile su http://localhost:8080
# Swagger UI: http://localhost:8080/swagger (solo in Development)
```

### Sviluppo Locale

**Prerequisiti:**
- .NET 9.0 SDK
- SQL Server (o LocalDB)

```bash
# Ripristina dipendenze
dotnet restore

# Configura la stringa di connessione in appsettings.Development.json

# Applica le migrations
dotnet ef database update --project CardExchange.Infrastructure --startup-project CardExchange.API

# Avvia l'applicazione
dotnet run --project CardExchange.API

# Swagger UI: https://localhost:7xxx/swagger
```

## Architettura

```
CardExchange.API/            # Web API Layer
├── Controllers/             # 16 API Controllers
├── DTOs/                    # Request & Response DTOs
├── Authorization/           # JWT + Permission-based auth
├── Middleware/               # Error handling, Security headers, Logging
├── Services/                # Business logic services
└── Validators/              # FluentValidation validators

CardExchange.Core/           # Domain Layer
├── Entities/                # 21 Domain entities
├── Interfaces/              # Repository interfaces
└── Constants/               # App constants, permissions

CardExchange.Infrastructure/ # Data Access Layer
├── Data/                    # DbContext, RBAC Seeder
├── Repositories/            # Repository implementations
└── Configuration/           # DI setup
```

## API Endpoints

### Autenticazione
| Metodo | Endpoint | Descrizione |
|--------|----------|-------------|
| POST | `/api/auth/register` | Registrazione utente |
| POST | `/api/auth/login` | Login |
| POST | `/api/auth/refresh-token` | Rinnovo token |

### Carte
| Metodo | Endpoint | Descrizione |
|--------|----------|-------------|
| GET | `/api/cards` | Lista carte disponibili |
| GET | `/api/cards/{id}` | Dettaglio carta |
| POST | `/api/cards/user/{userId}` | Aggiungi carta alla collezione |
| GET | `/api/cards/search?searchTerm=` | Cerca carte |
| GET | `/api/cards/nearby/{userId}` | Carte nelle vicinanze |

### Scambi
| Metodo | Endpoint | Descrizione |
|--------|----------|-------------|
| GET | `/api/tradeoffers` | Le mie offerte (paginato) |
| POST | `/api/tradeoffers` | Crea offerta di scambio |
| POST | `/api/tradeoffers/{id}/accept` | Accetta offerta |
| POST | `/api/tradeoffers/{id}/reject` | Rifiuta offerta |
| POST | `/api/tradeoffers/{id}/counter` | Contro-offerta |
| POST | `/api/tradeoffers/{id}/complete` | Completa scambio |
| POST | `/api/tradeoffers/{id}/review` | Recensisci scambio |

### Messaggi
| Metodo | Endpoint | Descrizione |
|--------|----------|-------------|
| GET | `/api/messages/conversations` | Le mie conversazioni |
| GET | `/api/messages/conversations/{id}` | Messaggi conversazione |
| POST | `/api/messages/send` | Invia messaggio |

### Abbonamenti (Freemium)
| Metodo | Endpoint | Descrizione |
|--------|----------|-------------|
| GET | `/api/subscriptions/plans` | Piani disponibili |
| GET | `/api/subscriptions/current` | Abbonamento corrente |
| POST | `/api/subscriptions/subscribe` | Sottoscrivi piano |

### Account / GDPR
| Metodo | Endpoint | Descrizione |
|--------|----------|-------------|
| GET | `/api/account/export-data` | Esporta dati personali |
| DELETE | `/api/account/delete-account` | Elimina account |
| GET | `/api/account/privacy-summary` | Riepilogo privacy |

## Modello Freemium

### Piano Free
- Max 50 carte in collezione
- Max 20 elementi in wishlist
- Max 5 offerte di scambio attive
- Max 10 messaggi al giorno
- Ricerca base

### Piano Premium
- Carte e wishlist illimitati
- Offerte di scambio illimitate
- Messaggi illimitati
- Ricerca avanzata con filtri
- Ricerca geografica per raggio
- Esportazione collezione (CSV/JSON)
- Statistiche dettagliate
- Ricerche salvate con alert
- Badge verificato

## Variabili d'Ambiente

| Variabile | Descrizione | Default |
|-----------|-------------|---------|
| `SA_PASSWORD` | Password SQL Server | - |
| `JWT_SECRET_KEY` | Chiave segreta JWT (min 32 byte) | - |
| `CORS_ORIGIN` | Origine CORS consentita | `http://localhost:3000` |

## Test

```bash
dotnet test
```

## Licenza

Proprietario - Tutti i diritti riservati.
