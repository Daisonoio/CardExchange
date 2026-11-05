using System.Text.Json;
using System.Text.Json.Nodes;

namespace CardExchange.API.Localization
{
    /// <summary>
    /// Traduce i testi delle risposte JSON delle API nella lingua dell'interfaccia (header Accept-Language).
    /// Si toccano solo le chiavi di testo (message, error, errors, warnings, title, body) e solo se la frase
    /// è nel dizionario: i contenuti scritti dagli utenti restano com'erano.
    /// </summary>
    public class LocalizeResponsesMiddleware
    {
        private const int MaxBytes = 512 * 1024;
        private static readonly HashSet<string> TextKeys = new(StringComparer.OrdinalIgnoreCase) { "message", "error", "errors", "warnings", "title", "body", "headline" };
        private readonly RequestDelegate _next;

        public LocalizeResponsesMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            var lang = ServerText.Normalize(context.Request.Headers.AcceptLanguage.ToString());
            if (lang == ServerText.Default || !context.Request.Path.StartsWithSegments("/api"))
            {
                await _next(context);
                return;
            }

            var original = context.Response.Body;
            await using var buffer = new MemoryStream();
            context.Response.Body = buffer;
            try
            {
                await _next(context);
            }
            finally
            {
                context.Response.Body = original;
            }

            buffer.Position = 0;
            var isJson = context.Response.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true;
            if (isJson && buffer.Length > 0 && buffer.Length <= MaxBytes)
            {
                try
                {
                    var node = JsonNode.Parse(buffer);
                    if (node != null && Translate(node, lang, inText: false))
                    {
                        var bytes = JsonSerializer.SerializeToUtf8Bytes(node);
                        context.Response.ContentLength = bytes.Length;
                        await original.WriteAsync(bytes);
                        return;
                    }
                }
                catch (JsonException)
                {
                    // Non è JSON valido: si restituisce così com'è
                }
                buffer.Position = 0;
            }
            await buffer.CopyToAsync(original);
        }

        /// <summary>
        /// Traduce in place; true se qualcosa è cambiato. Sotto una chiave di testo (es. "errors": { "Password": [...] })
        /// tutte le stringhe sono testo; altrove si traducono solo i valori delle chiavi di testo.
        /// </summary>
        private static bool Translate(JsonNode node, string lang, bool inText)
        {
            var changed = false;
            switch (node)
            {
                case JsonObject obj:
                    foreach (var (key, value) in obj.ToList())
                    {
                        if (value == null) continue;
                        var childInText = inText || TextKeys.Contains(key);
                        if (value is JsonValue v)
                        {
                            if (childInText && v.TryGetValue<string>(out var s))
                            {
                                var t = ServerText.T(s, lang);
                                if (t != s) { obj[key] = t; changed = true; }
                            }
                        }
                        else changed |= Translate(value, lang, childInText);
                    }
                    break;
                case JsonArray arr:
                    for (var i = 0; i < arr.Count; i++)
                    {
                        var item = arr[i];
                        if (item is JsonValue v)
                        {
                            if (inText && v.TryGetValue<string>(out var s))
                            {
                                var t = ServerText.T(s, lang);
                                if (t != s) { arr[i] = t; changed = true; }
                            }
                        }
                        else if (item != null) changed |= Translate(item, lang, inText);
                    }
                    break;
            }
            return changed;
        }
    }
}
