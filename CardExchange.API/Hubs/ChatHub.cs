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
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(ApplicationDbContext context, ILogger<ChatHub> logger)
        {
            _context = context;
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
        public async Task SendMessage(int recipientId, string content, int? tradeOfferId = null)
        {
            var userId = GetUserId();
            if (userId == 0 || userId == recipientId || string.IsNullOrWhiteSpace(content)) return;

            content = content.Trim();
            if (content.Length > 2000) content = content[..2000];

            Conversation conversation = null!;
            Message message = null!;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
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
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Errore durante l'invio del messaggio da {SenderId} a {RecipientId}", userId, recipientId);
                return;
            }

            var senderUser = await _context.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.Username })
                .FirstOrDefaultAsync();

            var msgPayload = new
            {
                id = message.Id,
                conversationId = conversation.Id,
                senderId = userId,
                senderUsername = senderUser?.Username ?? "",
                content = message.Content,
                sentAt = message.CreatedAt,
                isRead = false
            };

            await Clients.Group($"conversation_{conversation.Id}").SendAsync("ReceiveMessage", msgPayload);

            await Clients.Group($"chat_user_{recipientId}").SendAsync("NewMessageAlert", new
            {
                conversationId = conversation.Id,
                senderUsername = senderUser?.Username ?? "",
                preview = content.Length > 60 ? content[..60] + "..." : content
            });
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
