using CardExchange.API.Authorization;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CardExchange.API.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ApplicationDbContext _context;
        private readonly ISubscriptionService _subscriptionService;
        private readonly CardExchange.API.Services.Moderation.IUserBlockService _blocks;
        private readonly CardExchange.API.Services.Push.IPushQueue _pushQueue;
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(ApplicationDbContext context, ISubscriptionService subscriptionService, CardExchange.API.Services.Moderation.IUserBlockService blocks, CardExchange.API.Services.Push.IPushQueue pushQueue, ILogger<ChatHub> logger)
        {
            _context = context;
            _subscriptionService = subscriptionService;
            _blocks = blocks;
            _pushQueue = pushQueue;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();
            if (userId > 0)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"chat_user_{userId}");
                _logger.LogInformation("Utente {UserId} connesso a ChatHub", userId);
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = GetUserId();
            if (userId > 0)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat_user_{userId}");
            }
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Entra nel gruppo di una conversazione specifica per ricevere messaggi in tempo reale
        /// </summary>
        public async Task JoinConversation(int conversationId)
        {
            var userId = GetUserId();
            var conversation = await _context.Conversations
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == conversationId && (c.User1Id == userId || c.User2Id == userId));

            if (conversation == null) return;

            await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");
        }

        public async Task LeaveConversation(int conversationId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");
        }

        /// <summary>
        /// Invocato dal client per inviare un messaggio. Persiste in DB e fa broadcast real-time.
        /// </summary>
        public async Task<object?> SendMessage(int recipientId, string content, int? tradeOfferId = null)
        {
            var userId = GetUserId();
            if (userId == 0 || userId == recipientId || string.IsNullOrWhiteSpace(content)) return null;

            content = content.Trim();
            if (content.Length > 2000) content = content[..2000];

            if (await _blocks.IsBlockedAsync(userId, recipientId))
                throw new HubException("Non puoi inviare messaggi a questo utente.");

            // Stesso limite giornaliero dell'endpoint REST
            var plan = await _subscriptionService.GetUserActivePlanAsync(userId);
            var dailyLimit = plan?.MaxDailyMessages ?? 10;
            var todayCount = await _context.Messages
                .CountAsync(m => m.SenderId == userId && m.CreatedAt >= DateTime.UtcNow.Date);
            if (todayCount >= dailyLimit)
                throw new HubException($"Hai raggiunto il limite di {dailyLimit} messaggi giornalieri");

            Conversation conversation = null!;
            Message message = null!;

            try
            {
                (conversation, message) = await _context.InTransactionAsync(async () =>
                {
                    var minId = Math.Min(userId, recipientId);
                    var maxId = Math.Max(userId, recipientId);

                    conversation = await _context.Conversations
                        .FirstOrDefaultAsync(c => c.User1Id == minId && c.User2Id == maxId);

                    if (conversation == null)
                    {
                        conversation = new Conversation
                        {
                            User1Id = minId,
                            User2Id = maxId,
                            TradeOfferId = tradeOfferId
                        };
                        _context.Conversations.Add(conversation);
                        await _context.SaveChangesAsync();
                    }

                    conversation.LastMessageAt = DateTime.UtcNow;

                    message = new Message
                    {
                        ConversationId = conversation.Id,
                        SenderId = userId,
                        Content = content
                    };
                    _context.Messages.Add(message);

                    _context.Notifications.Add(new Notification
                    {
                        UserId = recipientId,
                        Type = NotificationType.NewMessage,
                        Title = "Nuovo messaggio",
                        Body = content.Length > 80 ? content[..80] + "..." : content,
                        ReferenceId = conversation.Id,
                        ReferenceType = "Conversation"
                    });

                    await _context.SaveChangesAsync();
                    return (conversation, message);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'invio del messaggio da {SenderId} a {RecipientId}", userId, recipientId);
                throw new HubException("Impossibile inviare il messaggio. Riprova.");
            }

            var senderUser = await _context.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.Username })
                .FirstOrDefaultAsync();

            var msgPayload = await BroadcastNewMessageAsync(Clients, conversation.Id, message, userId, senderUser?.Username ?? "", recipientId);

            if (!await _context.NotificationPreferences.AnyAsync(p => p.UserId == recipientId && p.Type == NotificationType.NewMessage && !p.IsEnabled))
            {
                _pushQueue.Enqueue(new CardExchange.API.Services.Push.PushJob(
                    recipientId,
                    $"Messaggio da {senderUser?.Username}",
                    content.Length > 120 ? content[..120] + "…" : content,
                    $"/chat?conversationId={conversation.Id}",
                    Tag: $"chat-{conversation.Id}"));
            }

            return msgPayload;
        }

        /// <summary>
        /// Recapita un nuovo messaggio in tempo reale a entrambi i partecipanti.
        /// Usa i gruppi personali (chat_user_{id}), a cui ogni connessione è iscritta alla connessione:
        /// così il messaggio arriva anche se la pagina non ha ancora fatto JoinConversation
        /// (conversazione appena creata, riconnessione, connessione ancora in avvio).
        /// Usato sia dall'hub sia dall'endpoint REST.
        /// </summary>
        public static async Task<object> BroadcastNewMessageAsync(
            IHubClients<IClientProxy> clients, int conversationId, Message message, int senderId, string senderUsername, int recipientId)
        {
            var payload = new
            {
                id = message.Id,
                conversationId,
                senderId,
                senderUsername,
                content = message.Content,
                sentAt = message.CreatedAt,
                isRead = false
            };

            await clients.Groups($"chat_user_{senderId}", $"chat_user_{recipientId}").SendAsync("ReceiveMessage", payload);

            await clients.Group($"chat_user_{recipientId}").SendAsync("NewMessageAlert", new
            {
                conversationId,
                senderUsername,
                preview = message.Content.Length > 60 ? message.Content[..60] + "..." : message.Content
            });

            return payload;
        }

        /// <summary>
        /// Segna i messaggi come letti
        /// </summary>
        public async Task MarkAsRead(int conversationId)
        {
            var userId = GetUserId();
            var unread = await _context.Messages
                .Where(m => m.ConversationId == conversationId && m.SenderId != userId && !m.IsRead)
                .ToListAsync();

            if (unread.Count == 0) return;

            var now = DateTime.UtcNow;
            foreach (var m in unread)
            {
                m.IsRead = true;
                m.ReadAt = now;
            }
            await _context.SaveChangesAsync();

            // Notifica il mittente che i messaggi sono stati letti
            var senderId = unread[0].SenderId;
            await Clients.Group($"chat_user_{senderId}").SendAsync("MessagesRead", new
            {
                conversationId,
                readBy = userId
            });
        }

        private int GetUserId()
        {
            var claim = Context.User?.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
        }
    }
}
