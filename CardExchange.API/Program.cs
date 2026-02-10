using CardExchange.API.Authorization;
using CardExchange.API.Configuration;
using CardExchange.API.Middleware;
using CardExchange.API.Services;
using CardExchange.Infrastructure.Configuration;
using CardExchange.Infrastructure.Data;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;

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
});

// ============================================================
// Authorization (RBAC dinamico)
// ============================================================
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization();

// ============================================================
// Services & Repositories
// ============================================================
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IPriceTrackingService, PriceTrackingService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IScryfallService, ScryfallService>(client =>
{
    client.BaseAddress = new Uri("https://api.scryfall.com");
    client.DefaultRequestHeaders.Add("User-Agent", "CardExchange/1.0");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(15);
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
// Rate Limiting
// ============================================================
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Policy globale: 100 richieste/minuto per IP
    options.AddFixedWindowLimiter("GlobalPolicy", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 5;
    });

    // Policy stringente per autenticazione: 10 richieste/15 minuti per IP
    options.AddFixedWindowLimiter("AuthPolicy", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(15);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

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
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
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
        await RBACSeeder.SeedRolesAndPermissions(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Errore durante il seed dei dati RBAC");
    }
}

// ============================================================
// Middleware Pipeline
// ============================================================

// 1. Global exception handler (primo nel pipeline)
app.UseMiddleware<GlobalExceptionHandler>();

// 2. Request logging con correlation ID
app.UseMiddleware<RequestLoggingMiddleware>();

// 3. Security headers
app.UseMiddleware<SecurityHeadersMiddleware>();

// 4. HTTPS redirect
app.UseHttpsRedirection();

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

// 9. Endpoints
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
