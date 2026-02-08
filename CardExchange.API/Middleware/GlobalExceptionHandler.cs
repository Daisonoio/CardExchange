using System.Net;
using System.Text.Json;

namespace CardExchange.API.Middleware
{
    public class GlobalExceptionHandler
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandler> _logger;
        private readonly IHostEnvironment _environment;

        public GlobalExceptionHandler(
            RequestDelegate next,
            ILogger<GlobalExceptionHandler> logger,
            IHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Eccezione non gestita per la richiesta {Method} {Path}",
                    context.Request.Method, context.Request.Path);

                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            var (statusCode, message) = exception switch
            {
                ArgumentNullException => (HttpStatusCode.BadRequest, "Parametro mancante o non valido"),
                ArgumentException => (HttpStatusCode.BadRequest, "Parametro non valido"),
                UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Accesso non autorizzato"),
                KeyNotFoundException => (HttpStatusCode.NotFound, "Risorsa non trovata"),
                InvalidOperationException => (HttpStatusCode.Conflict, "Operazione non valida"),
                _ => (HttpStatusCode.InternalServerError, "Errore interno del server")
            };

            context.Response.StatusCode = (int)statusCode;

            var response = new
            {
                status = (int)statusCode,
                message,
                detail = _environment.IsDevelopment() ? exception.Message : null,
                traceId = context.TraceIdentifier
            };

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
        }
    }
}
