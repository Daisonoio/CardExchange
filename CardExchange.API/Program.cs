using CardExchange.API.Authorization;
using CardExchange.API.Configuration;
using CardExchange.API.Services;
using CardExchange.Infrastructure.Configuration;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configurazione JWT Settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;

        if (builder.Environment.IsDevelopment())
        {
            options.JsonSerializerOptions.WriteIndented = true;
        }
    });

builder.Services.AddEndpointsApiExplorer();

// Configurazione Swagger con supporto JWT
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
        Description = "JWT Authorization header usando lo schema Bearer. Inserisci 'Bearer' seguito da uno spazio e poi il token. Esempio: 'Bearer 12345abcdef'",
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

// Configurazione JWT Authentication
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
        ValidIssuer = jwtSettings?.Issuer,
        ValidAudience = jwtSettings?.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings?.SecretKey ?? "")),
        ClockSkew = TimeSpan.Zero
    };
});

// ✅ CONFIGURAZIONE AUTHORIZATION CORRETTA
// 1. PRIMA il policy provider
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

// 2. POI l'handler
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// 3. INFINE Authorization (opzionale)
builder.Services.AddAuthorization(options =>
{
    // Le policy vengono create dinamicamente dal PermissionPolicyProvider
});

// Registra il TokenService
builder.Services.AddScoped<ITokenService, TokenService>();

// Add Database
builder.Services.AddDatabase(builder.Configuration);

// Add Repositories
builder.Services.AddRepositories();

// Configurazione CORS per sviluppo
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevelopmentPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Seed dei ruoli e permessi (PRIMA di app.Run)
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

// Configure the HTTP request pipeline.
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

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();