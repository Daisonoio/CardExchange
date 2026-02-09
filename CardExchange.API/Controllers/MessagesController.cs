using CardExchange.API.Authorization;
using CardExchange.API.DTOs.Requests;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CardExchange.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MessagesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ISubscriptionService _subscriptionService;
        private readonly ILogger<MessagesController> _logger;

        public MessagesController(
            ApplicationDbContext context,
            ISubscriptionService subscriptionService,
            ILogger<MessagesController> logger)
        {
            _context = context;
            _subscriptionService = subscriptionService;
            _logger = logger;
        }

        /// <summary>
        /// Ottiene tutte le conversazioni dell'utente
        /// </summary>
        [HttpGet("conversations")]
        public async Task<IActionResult> GetConversations()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var conversations = await _context.Conversations
                .AsNoTracking()
                .Include(c => c.User1)
                .Include(c => c.User2)
                .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(1))
                .Where(c => c.User1Id == userId || c.User2Id == userId)
                .OrderByDescending(c => c.LastMessageAt)
                .ToListAsync();

            var conversationIds = conversations.Select(c => c.Id).ToList();
            var unreadCounts = await _context.Messages
                .Where(m => conversationIds.Contains(m.ConversationId) && m.SenderId != userId && !m.IsRead)
                .GroupBy(m => m.ConversationId)
                .Select(g => new { ConversationId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ConversationId, x => x.Count);

            var result = conversations.Select(c =>
            {
                var otherUser = c.User1Id == userId ? c.User2 : c.User1;
                var lastMessage = c.Messages.FirstOrDefault();
                unreadCounts.TryGetValue(c.Id, out var unreadCount);

                return new
                {
                    conversationId = c.Id,
                    otherUser = new { otherUser.Id, otherUser.Username, otherUser.AvatarUrl },
                    lastMessage = lastMessage != null ? new
                    {
                        content = lastMessage.Content.Length > 100
                            ? lastMessage.Content[..100] + "..."
                            : lastMessage.Content,
                        sentAt = lastMessage.CreatedAt,
                        isFromMe = lastMessage.SenderId == userId
                    } : null,
                    unreadCount,
                    tradeOfferId = c.TradeOfferId
                };
            });

            return Ok(result);
        }

        /// <summary>
        /// Ottiene i messaggi di una conversazione
        /// </summary>
        [HttpGet("conversations/{conversationId}")]
        public async Task<IActionResult> GetMessages(int conversationId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId && (c.User1Id == userId || c.User2Id == userId));

            if (conversation == null)
                return NotFound(new { message = "Conversazione non trovata" });

            // Segna come letti i messaggi ricevuti
            var unreadMessages = await _context.Messages
                .Where(m => m.ConversationId == conversationId && m.SenderId != userId && !m.IsRead)
                .ToListAsync();

            foreach (var msg in unreadMessages)
            {
                msg.IsRead = true;
                msg.ReadAt = DateTime.UtcNow;
            }
            if (unreadMessages.Count > 0)
                await _context.SaveChangesAsync();

            var totalCount = await _context.Messages.CountAsync(m => m.ConversationId == conversationId);
            var messages = await _context.Messages
                .AsNoTracking()
                .Include(m => m.Sender)
                .Where(m => m.ConversationId == conversationId)
                .OrderByDescending(m => m.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                totalCount,
                page,
                pageSize,
                messages = messages.Select(m => new
                {
                    m.Id,
                    senderId = m.SenderId,
                    senderUsername = m.Sender.Username,
                    m.Content,
                    m.IsRead,
                    sentAt = m.CreatedAt,
                    isFromMe = m.SenderId == userId
                })
            });
        }

        /// <summary>
        /// Invia un messaggio (con check limiti free tier)
        /// </summary>
        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            if (request.RecipientId == userId)
                return BadRequest(new { message = "Non puoi inviare un messaggio a te stesso" });

            // Check limite messaggi giornalieri
            var plan = await _subscriptionService.GetUserActivePlanAsync(userId);
            var todayCount = await _context.Messages
                .CountAsync(m => m.SenderId == userId && m.CreatedAt >= DateTime.UtcNow.Date);

            if (todayCount >= (plan?.MaxDailyMessages ?? 10))
            {
                return StatusCode(403, new
                {
                    message = $"Hai raggiunto il limite di {plan?.MaxDailyMessages ?? 10} messaggi giornalieri",
                    upgradeUrl = "/api/subscriptions/plans"
                });
            }

            // Trova o crea la conversazione
            var minId = Math.Min(userId, request.RecipientId);
            var maxId = Math.Max(userId, request.RecipientId);

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.User1Id == minId && c.User2Id == maxId);

            if (conversation == null)
            {
                conversation = new Conversation
                {
                    User1Id = minId,
                    User2Id = maxId,
                    TradeOfferId = request.TradeOfferId
                };
                _context.Conversations.Add(conversation);
                await _context.SaveChangesAsync();
            }

            var message = new Message
            {
                ConversationId = conversation.Id,
                SenderId = userId,
                Content = request.Content
            };
            _context.Messages.Add(message);

            conversation.LastMessageAt = DateTime.UtcNow;

            // Notifica al destinatario
            _context.Notifications.Add(new Notification
            {
                UserId = request.RecipientId,
                Type = NotificationType.NewMessage,
                Title = "Nuovo messaggio",
                Body = $"Hai ricevuto un nuovo messaggio",
                ReferenceId = conversation.Id,
                ReferenceType = "Conversation"
            });

            await _context.SaveChangesAsync();

            return Ok(new
            {
                messageId = message.Id,
                conversationId = conversation.Id,
                sentAt = message.CreatedAt
            });
        }

        /// <summary>
        /// Conteggio messaggi non letti
        /// </summary>
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var count = await _context.Messages
                .CountAsync(m => m.Conversation.User1Id == userId || m.Conversation.User2Id == userId
                    ? m.SenderId != userId && !m.IsRead
                    : false);

            return Ok(new { unreadCount = count });
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
        }
    }
}
