using CardExchange.Infrastructure.Configuration;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Ignora i cicli di riferimento
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;

        // Opzionale: Ignora le proprietà null nelle risposte
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

        // Opzionale: Formattazione più leggibile (solo per development)
        options.JsonSerializerOptions.WriteIndented = true;
    });

builder.Services.AddEndpointsApiExplorer();

// Configurazione Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Card Exchange API",
        Version = "v1",
        Description = "API per la piattaforma di scambio carte collezionabili"
    });
});

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
app.UseAuthorization();
app.MapControllers();

app.Run();