using CardExchange.API.Authorization;
using CardExchange.API.Configuration;
using CardExchange.API.Middleware;
using CardExchange.API.Services;
using CardExchange.API.Services.Notifications;
using CardExchange.API.Services.Notifications.Handlers;
using CardExchange.Infrastructure.Configuration;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;

// Utility: genera una coppia di chiavi VAPID per le notifiche push ed esce
//   dotnet run --project CardExchange.API -- --generate-vapid-keys
if (args.Contains("--generate-vapid-keys"))
{
    var (vapidPublic, vapidPrivate) = CardExchange.API.Services.Push.WebPushOptions.GenerateKeys();
    Console.WriteLine($"WEBPUSH_PUBLIC_KEY={vapidPublic}");
    Console.WriteLine($"WEBPUSH_PRIVATE_KEY={vapidPrivate}");
    return;
}

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// Validazione configurazione critica all'avvio
// ============================================================
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();
if (jwtSettings == null || string.IsNullOrWhiteSpace(jwtSettings.SecretKey))
{
    throw new InvalidOperationException("JwtSettings:SecretKey non configurato. Impostare la chiave JWT.");
}
if (Encoding.UTF8.GetByteCount(jwtSettings.SecretKey) < 32)
{
    throw new InvalidOperationException("JwtSettings:SecretKey deve essere almeno 256 bit (32 bytes).");
}

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));

// ============================================================
// Controllers + JSON + FluentValidation
// ============================================================
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;

        if (builder.Environment.IsDevelopment())
        {
            options.JsonSerializerOptions.WriteIndented = true;
        }
    });

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddEndpointsApiExplorer();

// ============================================================
// Swagger con supporto JWT
// ============================================================
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Card Exchange API",
        Version = "v1",
        Description = "API per la piattaforma di scambio carte collezionabili"
    });

    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Inserisci 'Bearer' seguito dal token.",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ============================================================
// JWT Authentication
// ============================================================
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
        ClockSkew = TimeSpan.FromSeconds(30)
    };

    // SignalR: WebSocket e Server-Sent Events non possono inviare l'header Authorization,
    // quindi il client passa il token nella query string (access_token) solo per gli hub.
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// ============================================================
// Authorization (RBAC dinamico)
// ============================================================
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization();

// ============================================================
// SignalR (notifiche real-time)
// ============================================================
builder.Services.AddSignalR();

// ============================================================
// Services & Repositories
// ============================================================
builder.Services.AddScoped<ITokenService, TokenService>();

// Email: SMTP se configurato, altrimenti solo log (sviluppo)
builder.Services.Configure<CardExchange.API.Configuration.EmailSettings>(builder.Configuration.GetSection("Email"));
if (!string.IsNullOrWhiteSpace(builder.Configuration["Email:Smtp:Host"]))
    builder.Services.AddScoped<CardExchange.API.Services.Email.IEmailSender, CardExchange.API.Services.Email.SmtpEmailSender>();
else
    builder.Services.AddScoped<CardExchange.API.Services.Email.IEmailSender, CardExchange.API.Services.Email.LogOnlyEmailSender>();
// Limiti del piano gratuito (PlanLimits:FreeMaxCards vuoto = collezione illimitata)
builder.Services.Configure<CardExchange.API.Configuration.PlanLimitsOptions>(builder.Configuration.GetSection("PlanLimits"));
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
// Diritti dei piani: unico servizio per tutti i controlli; cache invalidata a ogni modifica di un abbonamento
builder.Services.AddScoped<CardExchange.API.Services.Entitlements.IEntitlementService, CardExchange.API.Services.Entitlements.EntitlementService>();
builder.Services.AddSingleton<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor, CardExchange.API.Services.Entitlements.EntitlementCacheInterceptor>();
// Abbonamenti alla piattaforma: carta (Stripe) e bonifico confermato dall'amministratore
builder.Services.Configure<CardExchange.API.Configuration.BillingOptions>(builder.Configuration.GetSection("Billing"));
builder.Services.AddScoped<CardExchange.API.Services.Billing.IBillingService, CardExchange.API.Services.Billing.BillingService>();
builder.Services.AddScoped<CardExchange.API.Services.Billing.IStripeBilling, CardExchange.API.Services.Billing.StripeBilling>();

// Catalogo multi-gioco: un provider per gioco. Per aggiungere un gioco: implementare ICatalogProvider e registrarlo qui.
builder.Services.AddHttpClient<CardExchange.API.Services.Catalog.IExchangeRates, CardExchange.API.Services.Catalog.EcbExchangeRates>(c =>
    c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddScoped<CardExchange.API.Services.Catalog.CatalogStore>();
builder.Services.AddHttpClient<CardExchange.API.Services.Catalog.LorcanaProvider>(c =>
{
    c.BaseAddress = new Uri(CardExchange.API.Services.Catalog.LorcanaProvider.BaseUrl);
    c.Timeout = TimeSpan.FromSeconds(15);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("CardExchange/1.0");
});
builder.Services.AddScoped<CardExchange.API.Services.Catalog.PokemonCatalogProvider>();
builder.Services.AddScoped<CardExchange.API.Services.Catalog.YuGiOhCatalogProvider>();
builder.Services.AddScoped<CardExchange.API.Services.Catalog.OnePieceCatalogProvider>();
builder.Services.AddScoped<CardExchange.API.Services.Catalog.ICatalogProvider>(sp => sp.GetRequiredService<CardExchange.API.Services.Catalog.PokemonCatalogProvider>());
builder.Services.AddScoped<CardExchange.API.Services.Catalog.ICatalogProvider>(sp => sp.GetRequiredService<CardExchange.API.Services.Catalog.YuGiOhCatalogProvider>());
builder.Services.AddScoped<CardExchange.API.Services.Catalog.ICatalogProvider>(sp => sp.GetRequiredService<CardExchange.API.Services.Catalog.OnePieceCatalogProvider>());
builder.Services.AddScoped<CardExchange.API.Services.Catalog.ICatalogProvider>(sp => sp.GetRequiredService<CardExchange.API.Services.Catalog.LorcanaProvider>());
builder.Services.AddScoped<CardExchange.API.Services.Catalog.ICatalogRegistry, CardExchange.API.Services.Catalog.CatalogRegistry>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

// Notification Handlers (modulari - aggiungere qui nuovi handler)
builder.Services.AddScoped<INotificationHandler, TradeNotificationHandler>();
builder.Services.AddScoped<INotificationHandler, WishlistNotificationHandler>();
builder.Services.AddScoped<INotificationHandler, FavoriteNotificationHandler>();
builder.Services.AddScoped<INotificationHandler, MessageNotificationHandler>();
builder.Services.AddScoped<INotificationHandler, SystemNotificationHandler>();

builder.Services.AddScoped<IPriceTrackingService, PriceTrackingService>();
builder.Services.AddScoped<IMatchmakingService, MatchmakingService>();
builder.Services.AddScoped<CardSearchService>();
builder.Services.AddScoped<CardExchange.API.Services.Social.BroadcastService>();
builder.Services.AddSingleton<CardExchange.API.Services.Security.LoginAttemptGuard>();
builder.Services.AddSingleton<CardExchange.API.Services.Stores.IStoreActivityRecorder, CardExchange.API.Services.Stores.StoreActivityRecorder>();
builder.Services.AddHostedService<CardExchange.API.Services.Stores.StoreActivityWriter>();
builder.Services.AddScoped<CardExchange.API.Services.Stores.StoreStatsService>();
builder.Services.AddScoped<CardExchange.API.Services.Stores.StoreMonthlyReports>();
builder.Services.AddSingleton<CardExchange.API.Services.Metrics.ActiveDayRecorder>();
builder.Services.AddHostedService<CardExchange.API.Services.Metrics.ActiveDayWriter>();
builder.Services.AddScoped<CardExchange.API.Services.Metrics.ActivationCheck>();
builder.Services.AddScoped<CardExchange.API.Services.Metrics.PlanMetrics>();
builder.Services.AddScoped<CardExchange.API.Services.NearbyWishlistMatches>();
builder.Services.AddScoped<CardExchange.API.Services.Events.EventRegistrations>();
builder.Services.AddScoped<CardExchange.API.Services.Stores.StoreDemandService>();
builder.Services.AddScoped<CardExchange.API.Services.ReferralRewards>();
builder.Services.AddScoped<CardExchange.API.Services.Stores.ReservationService>();
builder.Services.AddScoped<CardExchange.API.Services.Stores.BuyListService>();
builder.Services.AddScoped<CardExchange.API.Services.Catalog.PokemonCatalogMirror>();
builder.Services.AddScoped<CardExchange.API.Services.WishlistDigest>();
builder.Services.AddScoped<CardExchange.API.Services.Billing.PurchaseService>();
builder.Services.AddScoped<CardExchange.API.Services.Billing.StoreCommissions>();

// Monitoraggio: errori (anche quelli solo registrati nei log) e stato dei cataloghi esterni
builder.Services.AddSingleton<CardExchange.API.Services.Monitoring.IErrorTracker, CardExchange.API.Services.Monitoring.ErrorTracker>();
builder.Services.AddSingleton<ILoggerProvider>(sp => new CardExchange.API.Services.Monitoring.ErrorTrackingLoggerProvider(
    sp.GetRequiredService<CardExchange.API.Services.Monitoring.IErrorTracker>()));
builder.Services.AddScoped<CardExchange.API.Services.Monitoring.MonitoringAlerts>();
builder.Services.AddScoped<CardExchange.API.Services.Monitoring.CatalogHealthChecker>();
builder.Services.AddHostedService<CardExchange.API.Services.Monitoring.ErrorTrackingHostedService>();
builder.Services.AddHostedService<CardExchange.API.Services.Monitoring.CatalogHealthHostedService>();
builder.Services.AddScoped<IWishlistAlertService, WishlistAlertService>();
builder.Services.AddScoped<CardExchange.API.Services.Moderation.IUserBlockService, CardExchange.API.Services.Moderation.UserBlockService>();
builder.Services.AddScoped<CardExchange.API.Services.Import.IMagicCatalogService, CardExchange.API.Services.Import.MagicCatalogService>();
builder.Services.AddScoped<CardExchange.API.Services.Import.ICollectionImportService, CardExchange.API.Services.Import.CollectionImportService>();
builder.Services.AddScoped<CardExchange.API.Services.Prices.ICatalogPriceRefresher, CardExchange.API.Services.Prices.CatalogPriceRefresher>();
// Lavori notturni: scadenza offerte, prezzi, storico, allarmi (sezione Jobs:DailyMaintenance)
builder.Services.AddScoped<CardExchange.API.Services.Prices.DailyMaintenance>();
builder.Services.AddScoped<WeeklyDigest>();
builder.Services.AddHostedService<CardExchange.API.Services.Prices.DailyMaintenanceHostedService>();
builder.Services.AddScoped<MeetupReminders>();
builder.Services.AddHostedService<MeetupRemindersHostedService>();

// Web Push (PWA). In sviluppo, senza chiavi configurate, ne genera una coppia temporanea a ogni avvio.
builder.Services.Configure<CardExchange.API.Services.Push.WebPushOptions>(options =>
{
    builder.Configuration.GetSection("WebPush").Bind(options);
    if (!options.IsConfigured && builder.Environment.IsDevelopment())
    {
        (options.PublicKey, options.PrivateKey) = CardExchange.API.Services.Push.WebPushOptions.GenerateKeys();
    }
});
builder.Services.AddHttpClient("WebPush");
builder.Services.AddSingleton<CardExchange.API.Services.Push.IPushQueue, CardExchange.API.Services.Push.PushQueue>();
builder.Services.AddHostedService<CardExchange.API.Services.Push.PushSenderHostedService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IScryfallService, ScryfallService>(client =>
{
    client.BaseAddress = new Uri("https://api.scryfall.com");
    client.DefaultRequestHeaders.Add("User-Agent", "CardExchange/1.0");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddHttpClient<IPokemonTcgService, PokemonTcgService>(client =>
{
    client.BaseAddress = new Uri("https://api.pokemontcg.io");
    client.DefaultRequestHeaders.Add("User-Agent", "CardExchange/1.0");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddHttpClient<IYuGiOhService, YuGiOhService>(client =>
{
    client.BaseAddress = new Uri("https://db.ygoprodeck.com");
    client.DefaultRequestHeaders.Add("User-Agent", "CardExchange/1.0");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddHttpClient<IOnePieceTcgService, OnePieceTcgService>(client =>
{
    client.BaseAddress = new Uri(OnePieceTcgService.BaseUrl);
    client.DefaultRequestHeaders.Add("User-Agent", "CardExchange/1.0");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddRepositories();

// ============================================================
// Response Caching
// ============================================================
builder.Services.AddResponseCaching();
builder.Services.AddOutputCache(options =>
{
    // Cache per endpoint ad alta lettura (giochi, set di carte)
    options.AddBasePolicy(b => b.NoCache());
    options.AddPolicy("CatalogCache", b =>
        b.Expire(TimeSpan.FromMinutes(5))
         .Tag("catalog"));
    options.AddPolicy("StatisticsCache", b =>
        b.Expire(TimeSpan.FromMinutes(15))
         .Tag("statistics"));
});

// ============================================================
// Compressione delle risposte (JSON: 5-10 volte più leggero su rete mobile)
// ============================================================
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);

// ============================================================
// Rate Limiting
// ============================================================
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // I limiti sono per indirizzo IP: AddFixedWindowLimiter creerebbe un unico contatore condiviso da
    // tutti gli utenti (con 20 login in 15 minuti in tutta l'app, gli altri resterebbero bloccati).
    // Dietro un reverse proxy configurare ForwardedHeaders, altrimenti l'IP è quello del proxy.
    static string ClientIp(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    // Policy globale: 100 richieste/minuto per IP
    options.AddPolicy("GlobalPolicy", ctx => RateLimitPartition.GetFixedWindowLimiter(ClientIp(ctx), _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = 100,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0
    }));

    // Accesso e recupero password: 60 richieste/15 minuti per IP. Il limite è largo perché più persone
    // possono condividere la stessa rete (es. Wi-Fi di un negozio); contro i tentativi di indovinare la
    // password c'è il blocco per account (LoginAttemptGuard: 5 errori in 15 minuti).
    options.AddPolicy("AuthPolicy", ctx => RateLimitPartition.GetFixedWindowLimiter(ClientIp(ctx), _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = 60,
        Window = TimeSpan.FromMinutes(15),
        QueueLimit = 0
    }));

    // Registrazioni: 100/ora per IP, così una serata di scambio in negozio può iscriversi tutta dal QR
    options.AddPolicy("SignupPolicy", ctx => RateLimitPartition.GetFixedWindowLimiter(ClientIp(ctx), _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = 100,
        Window = TimeSpan.FromHours(1),
        QueueLimit = 0
    }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            status = 429,
            message = "Troppe richieste. Riprova più tardi."
        }, cancellationToken);
    };
});

// ============================================================
// CORS
// ============================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevelopmentPolicy", policy =>
    {
        //policy.AllowAnyOrigin()
        policy.WithOrigins("http://localhost:5173")
             .AllowAnyMethod()
             .AllowCredentials()
              .AllowAnyHeader()
              .WithExposedHeaders("Content-Disposition");
    });

    options.AddPolicy("ProductionPolicy", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                             ?? Array.Empty<string>();

        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .WithMethods("GET", "POST", "PUT", "DELETE")
                  .WithHeaders("Authorization", "Content-Type")
                  .WithExposedHeaders("Content-Disposition") // nome dei file esportati
                  .AllowCredentials();
        }
    });
});

// ============================================================
// Health Checks
// ============================================================
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>(name: "database");

var app = builder.Build();

// ============================================================
// Seed RBAC
// ============================================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        // Per SQLite in sviluppo, crea il database dal modello
        if (context.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            await context.Database.EnsureCreatedAsync();
            // Database già esistente: aggiunge tabelle, colonne e indici nuovi senza perdere i dati
            var sync = await SqliteSchemaSync.ApplyAsync(context);
            if (sync.TablesCreated.Count > 0 || sync.ColumnsAdded.Count > 0)
                app.Logger.LogInformation("Schema SQLite aggiornato: tabelle {Tables}, colonne {Columns}",
                    string.Join(", ", sync.TablesCreated), string.Join(", ", sync.ColumnsAdded));
            foreach (var w in sync.Warnings) app.Logger.LogWarning("Schema SQLite: {Warning}", w);
        }
        else
        {
            // Applica le migration pendenti automaticamente
            await context.Database.MigrateAsync();
        }

        await RBACSeeder.SeedRolesAndPermissions(context);
        await RBACSeeder.SeedSubscriptionPlans(context);
        await GameSeeder.SeedGames(context);

        // Negozi creati prima dei link e QR: assegna lo slug mancante
        var withoutSlug = await context.Stores.IgnoreQueryFilters().Where(s => s.Slug == null).ToListAsync();
        if (withoutSlug.Count > 0)
        {
            var used = (await context.Stores.IgnoreQueryFilters().Where(s => s.Slug != null).Select(s => s.Slug!).ToListAsync()).ToHashSet();
            foreach (var store in withoutSlug)
            {
                store.Slug = CardExchange.Core.Rules.Slugs.Unique(CardExchange.Core.Rules.Slugs.From($"{store.Name} {store.City}"), used.Contains);
                used.Add(store.Slug);
            }
            await context.SaveChangesAsync();
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Errore durante il seed dei dati RBAC/Giochi");
    }
}

// ============================================================
// Middleware Pipeline
// ============================================================

// 1. Global exception handler (primo nel pipeline)
app.UseMiddleware<GlobalExceptionHandler>();

// 2. Request logging con correlation ID
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseResponseCompression();
// Testi delle risposte nella lingua dell'interfaccia (Accept-Language); dentro la compressione
app.UseMiddleware<CardExchange.API.Localization.LocalizeResponsesMiddleware>();

// 3. Security headers
app.UseMiddleware<SecurityHeadersMiddleware>();

// 4. HTTPS redirect (solo in produzione)
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// 5. Rate limiting
app.UseRateLimiter();

// 6. Response caching
app.UseResponseCaching();
app.UseOutputCache();

// 7. CORS + Dev-only endpoints
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Card Exchange API v1");
        c.RoutePrefix = "swagger";
    });
    app.UseCors("DevelopmentPolicy");
}
else
{
    app.UseHsts();
    app.UseCors("ProductionPolicy");
}

// 8. Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Giorni di attività per le metriche interne (una scrittura per utente al giorno, in background)
app.Use(async (ctx, next) =>
{
    await next();
    if (ctx.User.Identity?.IsAuthenticated == true && ctx.Response.StatusCode < 400
        && int.TryParse(ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var activeUserId))
        ctx.RequestServices.GetRequiredService<CardExchange.API.Services.Metrics.ActiveDayRecorder>().Touch(activeUserId, DateTime.UtcNow);
});

// 9. Endpoints
app.MapControllers();
app.MapHub<CardExchange.API.Hubs.NotificationHub>("/hubs/notifications");
app.MapHub<CardExchange.API.Hubs.ChatHub>("/hubs/chat");
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
