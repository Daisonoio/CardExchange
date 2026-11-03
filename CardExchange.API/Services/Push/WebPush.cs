using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using LibPushSubscription = Lib.Net.Http.WebPush.PushSubscription;

namespace CardExchange.API.Services.Push
{
    /// <summary>Chiavi VAPID (sezione "WebPush"). Senza chiavi le notifiche push sono disattivate.</summary>
    public class WebPushOptions
    {
        public string? PublicKey { get; set; }
        public string? PrivateKey { get; set; }
        /// <summary>Contatto del gestore richiesto dai push service, es. "mailto:supporto@dominio.it".</summary>
        public string Subject { get; set; } = "mailto:admin@cardexchange.local";

        public bool IsConfigured => !string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(PrivateKey);

        /// <summary>Genera una coppia di chiavi VAPID (P-256) in Base64 URL-safe.</summary>
        public static (string PublicKey, string PrivateKey) GenerateKeys()
        {
            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var p = ecdsa.ExportParameters(includePrivateParameters: true);
            var publicKey = new byte[65];
            publicKey[0] = 0x04;
            p.Q.X!.CopyTo(publicKey, 1);
            p.Q.Y!.CopyTo(publicKey, 33);
            return (Base64Url(publicKey), Base64Url(p.D!));
        }

        private static string Base64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public record PushJob(int UserId, string Title, string? Body, string Url, string? Tag);

    /// <summary>Coda in memoria: chi genera una notifica non aspetta i push service esterni.</summary>
    public interface IPushQueue
    {
        void Enqueue(PushJob job);
        ChannelReader<PushJob> Reader { get; }
    }

    public class PushQueue : IPushQueue
    {
        // Coda limitata: in caso di picchi si scartano i messaggi più vecchi invece di consumare memoria
        private readonly Channel<PushJob> _channel = Channel.CreateBounded<PushJob>(
            new BoundedChannelOptions(5000) { FullMode = BoundedChannelFullMode.DropOldest });
        private readonly bool _enabled;

        public PushQueue(IOptions<WebPushOptions> options)
        {
            _enabled = options.Value.IsConfigured;
        }

        public void Enqueue(PushJob job)
        {
            if (_enabled) _channel.Writer.TryWrite(job);
        }

        public ChannelReader<PushJob> Reader => _channel.Reader;

        /// <summary>Pagina da aprire toccando la notifica, in base al riferimento.</summary>
        public static string UrlFor(string? referenceType, int? referenceId) => referenceType switch
        {
            "TradeOffer" => "/trades",
            "Conversation" => referenceId.HasValue ? $"/chat?conversationId={referenceId}" : "/chat",
            "Card" or "PriceSpike" or "PriceAlert" => "/collection",
            "Wishlist" => "/wishlist",
            _ => "/home"
        };
    }

    /// <summary>Consuma la coda e invia i messaggi a tutti i dispositivi dell'utente; rimuove le sottoscrizioni scadute.</summary>
    public class PushSenderHostedService : BackgroundService
    {
        private readonly IPushQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly WebPushOptions _options;
        private readonly ILogger<PushSenderHostedService> _logger;
        private readonly PushServiceClient _client;

        public PushSenderHostedService(IPushQueue queue, IServiceScopeFactory scopeFactory, IOptions<WebPushOptions> options,
            IHttpClientFactory httpClientFactory, ILogger<PushSenderHostedService> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
            _client = new PushServiceClient(httpClientFactory.CreateClient("WebPush"));
            if (_options.IsConfigured)
            {
                _client.DefaultAuthentication = new VapidAuthentication(_options.PublicKey, _options.PrivateKey) { Subject = _options.Subject };
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.IsConfigured)
            {
                _logger.LogInformation("Web Push non configurato (WebPush:PublicKey/PrivateKey): notifiche push disattivate");
                return;
            }

            try
            {
                await foreach (var job in _queue.Reader.ReadAllAsync(stoppingToken))
                {
                    try
                    {
                        await SendAsync(job, stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "Invio push fallito per utente {UserId}", job.UserId);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // arresto dell'applicazione
            }
        }

        private async Task SendAsync(PushJob job, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var subscriptions = await context.PushSubscriptions.Where(s => s.UserId == job.UserId).ToListAsync(ct);
            if (subscriptions.Count == 0) return;

            var payload = JsonSerializer.Serialize(new { title = job.Title, body = job.Body, url = job.Url, tag = job.Tag },
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var message = new PushMessage(payload) { TimeToLive = 60 * 60 * 24, Urgency = PushMessageUrgency.Normal };

            foreach (var sub in subscriptions)
            {
                var target = new LibPushSubscription { Endpoint = sub.Endpoint };
                target.SetKey(PushEncryptionKeyName.P256DH, sub.P256dh);
                target.SetKey(PushEncryptionKeyName.Auth, sub.Auth);
                try
                {
                    await _client.RequestPushMessageDeliveryAsync(target, message, ct);
                    sub.LastSuccessAt = DateTime.UtcNow;
                }
                catch (PushServiceClientException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
                {
                    // Il browser ha revocato la sottoscrizione
                    context.PushSubscriptions.Remove(sub);
                }
                catch (PushServiceClientException ex)
                {
                    _logger.LogWarning("Push service ha risposto {Status} per l'utente {UserId}: {Message}", ex.StatusCode, job.UserId, ex.Message);
                }
            }
            await context.SaveChangesAsync(ct);
        }
    }
}
