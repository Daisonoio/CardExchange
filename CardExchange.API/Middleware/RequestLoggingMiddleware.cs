using System.Diagnostics;

namespace CardExchange.API.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Genera o leggi correlation ID
            var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                                ?? Guid.NewGuid().ToString("N")[..12];

            context.Items["CorrelationId"] = correlationId;
            context.Response.Headers["X-Correlation-ID"] = correlationId;

            var stopwatch = Stopwatch.StartNew();
            var method = context.Request.Method;
            var path = context.Request.Path;

            try
            {
                await _next(context);
                stopwatch.Stop();

                var statusCode = context.Response.StatusCode;
                var elapsed = stopwatch.ElapsedMilliseconds;

                if (elapsed > 1000)
                {
                    _logger.LogWarning("[{CorrelationId}] {Method} {Path} -> {StatusCode} ({Elapsed}ms) [SLOW]",
                        correlationId, method, path, statusCode, elapsed);
                }
                else
                {
                    _logger.LogInformation("[{CorrelationId}] {Method} {Path} -> {StatusCode} ({Elapsed}ms)",
                        correlationId, method, path, statusCode, elapsed);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "[{CorrelationId}] {Method} {Path} -> ERRORE ({Elapsed}ms)",
                    correlationId, method, path, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }
}
