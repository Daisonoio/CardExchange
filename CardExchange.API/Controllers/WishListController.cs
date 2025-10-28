using CardExchange.API.DTOs.Requests;
using CardExchange.API.DTOs.Responses;
using CardExchange.Core.Entities;
using CardExchange.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using CardExchange.API.Authorization;
using Microsoft.AspNetCore.Authorization;


namespace CardExchange.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class WishlistController : ControllerBase
    {
        private readonly IWishlistRepository _wishlistRepository;
        private readonly IUserRepository _userRepository;
        private readonly ICardInfoRepository _cardInfoRepository;
        private readonly ICardRepository _cardRepository;
        private readonly IBaseRepository<CardSet> _cardSetRepository;
        private readonly IBaseRepository<Game> _gameRepository;
        private readonly ILogger<WishlistController> _logger;

        public WishlistController(
            IWishlistRepository wishlistRepository,
            IUserRepository userRepository,
            ICardInfoRepository cardInfoRepository,
            ICardRepository cardRepository,
            IBaseRepository<CardSet> cardSetRepository,
            IBaseRepository<Game> gameRepository,
            ILogger<WishlistController> logger)
        {
            _wishlistRepository = wishlistRepository;
            _userRepository = userRepository;
            _cardInfoRepository = cardInfoRepository;
            _cardRepository = cardRepository;
            _cardSetRepository = cardSetRepository;
            _gameRepository = gameRepository;
            _logger = logger;
        }

        /// <summary>
        /// Ottiene la wishlist completa di un utente
        /// </summary>
        [HttpGet("user/{userId}")]
        [RequirePermission("WISHLIST.READ.OWN")]
        public async Task<ActionResult<IEnumerable<WishlistItemDto>>> GetUserWishlist(int userId)
        {
            try
            {
                var userExists = await _userRepository.GetByIdAsync(userId);
                if (userExists == null)
                {
                    return NotFound(new { message = $"Utente con ID {userId} non trovato" });
                }

                var wishlistItems = await _wishlistRepository.GetUserWishlistAsync(userId);

                // Carica le informazioni necessarie per i DTOs
                var cardInfoIds = wishlistItems.Select(wi => wi.CardInfoId).Distinct();
                var cardSetIds = new HashSet<int>();
                var cardInfoDict = new Dictionary<int, CardInfo>();

                foreach (var cardInfoId in cardInfoIds)
                {
                    var cardInfo = await _cardInfoRepository.GetByIdAsync(cardInfoId);
                    if (cardInfo != null)
                    {
                        cardInfoDict[cardInfoId] = cardInfo;
                        cardSetIds.Add(cardInfo.CardSetId);
                    }
                }

                // Carica CardSets e Games
                var cardSets = new Dictionary<int, (string Name, string Code, int GameId)>();
                var games = new Dictionary<int, string>();

                foreach (var cardSetId in cardSetIds)
                {
                    var cardSet = await _cardSetRepository.GetByIdAsync(cardSetId);
                    if (cardSet != null)
                    {
                        cardSets[cardSetId] = (cardSet.Name, cardSet.Code, cardSet.GameId);

                        if (!games.ContainsKey(cardSet.GameId))
                        {
                            var game = await _gameRepository.GetByIdAsync(cardSet.GameId);
                            if (game != null)
                            {
                                games[cardSet.GameId] = game.Name;
                            }
                        }
                    }
                }

                var wishlistDtos = await Task.WhenAll(
                    wishlistItems.Select(async wi => await MapToDto(wi, cardInfoDict, cardSets, games))
                );

                return Ok(new
                {
                    userId,
                    username = userExists.Username,
                    count = wishlistDtos.Length,
                    items = wishlistDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero della wishlist per l'utente {UserId}", userId);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Ottiene un singolo item della wishlist per ID
        /// </summary>
        [HttpGet("{id}")]
        [RequirePermission("WISHLIST.CREATE.OWN")]
        public async Task<ActionResult<WishlistItemDto>> GetWishlistItemById(int id)
        {
            try
            {
                var wishlistItem = await _wishlistRepository.GetByIdAsync(id);

                if (wishlistItem == null)
                {
                    return NotFound(new { message = $"Item wishlist con ID {id} non trovato" });
                }

                var cardInfo = await _cardInfoRepository.GetByIdAsync(wishlistItem.CardInfoId);
                if (cardInfo == null)
                {
                    return NotFound(new { message = "CardInfo associata non trovata" });
                }

                var cardSet = await _cardSetRepository.GetByIdAsync(cardInfo.CardSetId);
                var game = cardSet != null ? await _gameRepository.GetByIdAsync(cardSet.GameId) : null;

                var cardInfoDict = new Dictionary<int, CardInfo> { [cardInfo.Id] = cardInfo };
                var cardSets = new Dictionary<int, (string, string, int)>
                {
                    [cardInfo.CardSetId] = (cardSet?.Name ?? "Unknown", cardSet?.Code ?? "", cardSet?.GameId ?? 0)
                };
                var games = new Dictionary<int, string>();
                if (game != null && cardSet != null)
                {
                    games[cardSet.GameId] = game.Name;
                }

                var dto = await MapToDto(wishlistItem, cardInfoDict, cardSets, games);
                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero dell'item wishlist {ItemId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Ottiene gli item della wishlist per priorità
        /// </summary>
        [HttpGet("user/{userId}/priority/{priority}")]
        [RequirePermission("WISHLIST.READ.OWN")]
        public async Task<ActionResult<IEnumerable<WishlistItemDto>>> GetWishlistByPriority(int userId, int priority)
        {
            try
            {
                if (priority < 1 || priority > 3)
                {
                    return BadRequest(new { message = "Priorità non valida. Valori ammessi: 1 (Alta), 2 (Media), 3 (Bassa)" });
                }

                var userExists = await _userRepository.GetByIdAsync(userId);
                if (userExists == null)
                {
                    return NotFound(new { message = $"Utente con ID {userId} non trovato" });
                }

                var wishlistItems = await _wishlistRepository.GetWishlistByPriorityAsync(userId, priority);

                // Carica le informazioni necessarie (stesso codice del GetUserWishlist)
                var cardInfoIds = wishlistItems.Select(wi => wi.CardInfoId).Distinct();
                var cardSetIds = new HashSet<int>();
                var cardInfoDict = new Dictionary<int, CardInfo>();

                foreach (var cardInfoId in cardInfoIds)
                {
                    var cardInfo = await _cardInfoRepository.GetByIdAsync(cardInfoId);
                    if (cardInfo != null)
                    {
                        cardInfoDict[cardInfoId] = cardInfo;
                        cardSetIds.Add(cardInfo.CardSetId);
                    }
                }

                var cardSets = new Dictionary<int, (string Name, string Code, int GameId)>();
                var games = new Dictionary<int, string>();

                foreach (var cardSetId in cardSetIds)
                {
                    var cardSet = await _cardSetRepository.GetByIdAsync(cardSetId);
                    if (cardSet != null)
                    {
                        cardSets[cardSetId] = (cardSet.Name, cardSet.Code, cardSet.GameId);

                        if (!games.ContainsKey(cardSet.GameId))
                        {
                            var game = await _gameRepository.GetByIdAsync(cardSet.GameId);
                            if (game != null)
                            {
                                games[cardSet.GameId] = game.Name;
                            }
                        }
                    }
                }

                var wishlistDtos = await Task.WhenAll(
                    wishlistItems.Select(async wi => await MapToDto(wi, cardInfoDict, cardSets, games))
                );

                string priorityLabel = priority switch
                {
                    1 => "Alta",
                    2 => "Media",
                    3 => "Bassa",
                    _ => "Sconosciuta"
                };

                return Ok(new
                {
                    userId,
                    priority,
                    priorityLabel,
                    count = wishlistDtos.Length,
                    items = wishlistDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero della wishlist per priorità");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Aggiunge una carta alla wishlist di un utente
        /// </summary>
        [HttpPost("user/{userId}")]
        [RequirePermission("WISHLIST.UPDATE.OWN")]
        public async Task<ActionResult<WishlistItemDto>> AddToWishlist(int userId, [FromBody] CreateWishlistItemRequest request)
        {
            try
            {
                // Verifica che l'utente esista
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    return NotFound(new { message = $"Utente con ID {userId} non trovato" });
                }

                // Verifica che la CardInfo esista
                var cardInfo = await _cardInfoRepository.GetByIdAsync(request.CardInfoId);
                if (cardInfo == null)
                {
                    return NotFound(new { message = $"CardInfo con ID {request.CardInfoId} non trovata" });
                }

                // Verifica che non esista già nella wishlist
                var existing = await _wishlistRepository.GetUserWishlistItemAsync(userId, request.CardInfoId);
                if (existing != null)
                {
                    return BadRequest(new { message = "Questa carta è già presente nella tua wishlist" });
                }

                var wishlistItem = new WishlistItem
                {
                    UserId = userId,
                    CardInfoId = request.CardInfoId,
                    PreferredCondition = request.PreferredCondition.HasValue
                        ? (CardCondition)request.PreferredCondition.Value
                        : null,
                    MaxPrice = request.MaxPrice,
                    Notes = request.Notes,
                    Priority = request.Priority
                };

                await _wishlistRepository.AddAsync(wishlistItem);
                await _wishlistRepository.SaveChangesAsync();

                _logger.LogInformation("Carta {CardInfoId} aggiunta alla wishlist dell'utente {UserId}", request.CardInfoId, userId);

                // Ricarica con le relazioni per il DTO
                var createdItem = await _wishlistRepository.GetUserWishlistItemAsync(userId, request.CardInfoId);

                var cardSet = await _cardSetRepository.GetByIdAsync(cardInfo.CardSetId);
                var game = cardSet != null ? await _gameRepository.GetByIdAsync(cardSet.GameId) : null;

                var cardInfoDict = new Dictionary<int, CardInfo> { [cardInfo.Id] = cardInfo };
                var cardSets = new Dictionary<int, (string, string, int)>
                {
                    [cardInfo.CardSetId] = (cardSet?.Name ?? "Unknown", cardSet?.Code ?? "", cardSet?.GameId ?? 0)
                };
                var games = new Dictionary<int, string>();
                if (game != null && cardSet != null)
                {
                    games[cardSet.GameId] = game.Name;
                }

                var dto = await MapToDto(createdItem!, cardInfoDict, cardSets, games);
                return CreatedAtAction(nameof(GetWishlistItemById), new { id = wishlistItem.Id }, dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'aggiunta alla wishlist");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Aggiorna un item della wishlist
        /// </summary>
        [HttpPut("{id}")]
        [RequirePermission("WISHLIST.UPDATE.OWN")]
        public async Task<ActionResult<WishlistItemDto>> UpdateWishlistItem(int id, [FromBody] UpdateWishlistItemRequest request)
        {
            try
            {
                var wishlistItem = await _wishlistRepository.GetByIdAsync(id);

                if (wishlistItem == null)
                {
                    return NotFound(new { message = $"Item wishlist con ID {id} non trovato" });
                }

                // Aggiorna solo i campi forniti
                if (request.PreferredCondition.HasValue)
                {
                    if (!Enum.IsDefined(typeof(CardCondition), request.PreferredCondition.Value))
                    {
                        return BadRequest(new { message = "Condizione non valida" });
                    }
                    wishlistItem.PreferredCondition = (CardCondition)request.PreferredCondition.Value;
                }

                if (request.MaxPrice.HasValue)
                {
                    wishlistItem.MaxPrice = request.MaxPrice.Value;
                }

                if (request.Notes != null)
                {
                    wishlistItem.Notes = request.Notes;
                }

                if (request.Priority.HasValue)
                {
                    if (request.Priority.Value < 1 || request.Priority.Value > 3)
                    {
                        return BadRequest(new { message = "La priorità deve essere 1 (Alta), 2 (Media) o 3 (Bassa)" });
                    }
                    wishlistItem.Priority = request.Priority.Value;
                }

                _wishlistRepository.Update(wishlistItem);
                await _wishlistRepository.SaveChangesAsync();

                _logger.LogInformation("Item wishlist {ItemId} aggiornato", id);

                // Ricarica per il DTO
                var updatedItem = await _wishlistRepository.GetByIdAsync(id);
                var cardInfo = await _cardInfoRepository.GetByIdAsync(updatedItem!.CardInfoId);

                if (cardInfo == null)
                {
                    return NotFound(new { message = "CardInfo associata non trovata" });
                }

                var cardSet = await _cardSetRepository.GetByIdAsync(cardInfo.CardSetId);
                var game = cardSet != null ? await _gameRepository.GetByIdAsync(cardSet.GameId) : null;

                var cardInfoDict = new Dictionary<int, CardInfo> { [cardInfo.Id] = cardInfo };
                var cardSets = new Dictionary<int, (string, string, int)>
                {
                    [cardInfo.CardSetId] = (cardSet?.Name ?? "Unknown", cardSet?.Code ?? "", cardSet?.GameId ?? 0)
                };
                var games = new Dictionary<int, string>();
                if (game != null && cardSet != null)
                {
                    games[cardSet.GameId] = game.Name;
                }

                var dto = await MapToDto(updatedItem, cardInfoDict, cardSets, games);
                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'aggiornamento dell'item wishlist {ItemId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Rimuove una carta dalla wishlist
        /// </summary>
        [HttpDelete("{id}")]
        [RequirePermission("WISHLIST.DELETE.OWN")]
        public async Task<IActionResult> DeleteWishlistItem(int id)
        {
            try
            {
                var wishlistItem = await _wishlistRepository.GetByIdAsync(id);

                if (wishlistItem == null)
                {
                    return NotFound(new { message = $"Item wishlist con ID {id} non trovato" });
                }

                _wishlistRepository.Delete(wishlistItem);
                await _wishlistRepository.SaveChangesAsync();

                _logger.LogInformation("Item wishlist {ItemId} eliminato", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'eliminazione dell'item wishlist {ItemId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Ottiene i dettagli di un item con tutte le carte disponibili che matchano
        /// </summary>
        [HttpGet("{id}/matches")]
        public async Task<ActionResult<WishlistItemDetailDto>> GetWishlistItemWithMatches(int id)
        {
            try
            {
                var wishlistItem = await _wishlistRepository.GetByIdAsync(id);

                if (wishlistItem == null)
                {
                    return NotFound(new { message = $"Item wishlist con ID {id} non trovato" });
                }

                var cardInfo = await _cardInfoRepository.GetByIdAsync(wishlistItem.CardInfoId);
                if (cardInfo == null)
                {
                    return NotFound(new { message = "CardInfo associata non trovata" });
                }

                var cardSet = await _cardSetRepository.GetByIdAsync(cardInfo.CardSetId);
                var game = cardSet != null ? await _gameRepository.GetByIdAsync(cardSet.GameId) : null;

                // Trova tutte le carte disponibili che matchano
                var matchingCards = await FindMatchingCards(wishlistItem);

                // Carica le location degli utenti per calcolare le distanze
                var user = await _userRepository.GetWithLocationAsync(wishlistItem.UserId);

                var matchingCardDtos = await Task.WhenAll(
                    matchingCards.Select(async card => await MapToMatchingCardDto(card, user))
                );

                var detailDto = new WishlistItemDetailDto
                {
                    Id = wishlistItem.Id,
                    UserId = wishlistItem.UserId,
                    CardInfoId = wishlistItem.CardInfoId,
                    CardName = cardInfo.Name,
                    CardSetName = cardSet?.Name ?? "Unknown",
                    CardSetCode = cardSet?.Code ?? "",
                    GameName = game?.Name ?? "Unknown",
                    CardNumber = cardInfo.CardNumber,
                    Rarity = cardInfo.Rarity,
                    CardType = cardInfo.Type,
                    CardDescription = cardInfo.Description,
                    CardImageUrl = cardInfo.ImageUrl,
                    PreferredCondition = wishlistItem.PreferredCondition?.ToString(),
                    MaxPrice = wishlistItem.MaxPrice,
                    Notes = wishlistItem.Notes,
                    Priority = wishlistItem.Priority,
                    PriorityLabel = GetPriorityLabel(wishlistItem.Priority),
                    CreatedAt = wishlistItem.CreatedAt,
                    AvailableMatches = matchingCardDtos.Length,
                    MatchingCards = matchingCardDtos
                };

                return Ok(detailDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero dei match per l'item {ItemId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Trova tutte le carte disponibili che matchano gli item della wishlist di un utente
        /// </summary>
        [HttpGet("user/{userId}/all-matches")]
        public async Task<ActionResult> GetAllMatchesForUser(int userId)
        {
            try
            {
                var userExists = await _userRepository.GetByIdAsync(userId);
                if (userExists == null)
                {
                    return NotFound(new { message = $"Utente con ID {userId} non trovato" });
                }

                var wishlistItems = await _wishlistRepository.GetUserWishlistAsync(userId);

                var allMatches = new List<object>();

                foreach (var item in wishlistItems)
                {
                    var matchingCards = await FindMatchingCards(item);

                    if (matchingCards.Any())
                    {
                        var cardInfo = await _cardInfoRepository.GetByIdAsync(item.CardInfoId);
                        var cardSet = cardInfo != null ? await _cardSetRepository.GetByIdAsync(cardInfo.CardSetId) : null;
                        var game = cardSet != null ? await _gameRepository.GetByIdAsync(cardSet.GameId) : null;

                        var user = await _userRepository.GetWithLocationAsync(userId);
                        var matchingCardDtos = await Task.WhenAll(
                            matchingCards.Select(async card => await MapToMatchingCardDto(card, user))
                        );

                        allMatches.Add(new
                        {
                            wishlistItemId = item.Id,
                            cardName = cardInfo?.Name ?? "Unknown",
                            cardSetName = cardSet?.Name ?? "Unknown",
                            gameName = game?.Name ?? "Unknown",
                            priority = item.Priority,
                            priorityLabel = GetPriorityLabel(item.Priority),
                            matchCount = matchingCardDtos.Length,
                            matches = matchingCardDtos
                        });
                    }
                }

                return Ok(new
                {
                    userId,
                    username = userExists.Username,
                    totalWishlistItems = wishlistItems.Count(),
                    itemsWithMatches = allMatches.Count,
                    matches = allMatches
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero di tutti i match per l'utente {UserId}", userId);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Trova utenti che hanno carte della mia wishlist disponibili in un raggio specifico
        /// </summary>
        [HttpGet("user/{userId}/nearby-matches")]
        public async Task<ActionResult> GetNearbyWishlistMatches(
            int userId,
            [FromQuery] int radiusKm = 50,
            [FromQuery] int? priority = null)
        {
            try
            {
                var user = await _userRepository.GetWithLocationAsync(userId);

                if (user == null)
                {
                    return NotFound(new { message = $"Utente con ID {userId} non trovato" });
                }

                if (user.Location == null || !user.Location.Latitude.HasValue || !user.Location.Longitude.HasValue)
                {
                    return BadRequest(new { message = "Devi configurare la tua location per usare questa funzionalità" });
                }

                if (radiusKm < 1 || radiusKm > 1000)
                {
                    return BadRequest(new { message = "Il raggio deve essere tra 1 e 1000 km" });
                }

                // Ottieni la wishlist dell'utente
                var wishlistItems = await _wishlistRepository.GetUserWishlistAsync(userId);

                // Filtra per priorità se specificata
                if (priority.HasValue)
                {
                    if (priority.Value < 1 || priority.Value > 3)
                    {
                        return BadRequest(new { message = "Priorità non valida" });
                    }
                    wishlistItems = wishlistItems.Where(wi => wi.Priority == priority.Value);
                }

                var results = new List<object>();

                foreach (var wishlistItem in wishlistItems)
                {
                    // Trova carte che matchano
                    var matchingCards = await FindMatchingCards(wishlistItem);

                    if (!matchingCards.Any())
                        continue;

                    // Filtra per distanza
                    var cardsInRadius = new List<(Card card, double distance)>();

                    foreach (var card in matchingCards)
                    {
                        var cardOwner = await _userRepository.GetWithLocationAsync(card.UserId);

                        if (cardOwner?.Location != null &&
                            cardOwner.Location.Latitude.HasValue &&
                            cardOwner.Location.Longitude.HasValue)
                        {
                            var distance = CalculateDistance(
                                (double)user.Location.Latitude.Value,
                                (double)user.Location.Longitude.Value,
                                (double)cardOwner.Location.Latitude.Value,
                                (double)cardOwner.Location.Longitude.Value
                            );

                            if (distance <= radiusKm)
                            {
                                cardsInRadius.Add((card, distance));
                            }
                        }
                    }

                    if (cardsInRadius.Any())
                    {
                        var cardInfo = await _cardInfoRepository.GetByIdAsync(wishlistItem.CardInfoId);
                        var cardSet = cardInfo != null ? await _cardSetRepository.GetByIdAsync(cardInfo.CardSetId) : null;
                        var game = cardSet != null ? await _gameRepository.GetByIdAsync(cardSet.GameId) : null;

                        // Raggruppa per utente
                        var userGroups = cardsInRadius
                            .GroupBy(x => x.card.UserId)
                            .OrderBy(g => cardsInRadius.First(c => c.card.UserId == g.Key).distance);

                        var owners = new List<object>();

                        foreach (var group in userGroups)
                        {
                            var owner = await _userRepository.GetWithLocationAsync(group.Key);
                            var firstCard = cardsInRadius.First(c => c.card.UserId == group.Key);

                            owners.Add(new
                            {
                                userId = owner!.Id,
                                username = owner.Username,
                                distanceKm = Math.Round(firstCard.distance, 2),
                                location = new
                                {
                                    city = owner.Location?.City,
                                    province = owner.Location?.Province,
                                    country = owner.Location?.Country
                                },
                                availableQuantity = group.Sum(x => x.card.Quantity),
                                cards = group.Select(x => new
                                {
                                    cardId = x.card.Id,
                                    condition = x.card.Condition.ToString(),
                                    quantity = x.card.Quantity,
                                    estimatedValue = x.card.EstimatedValue
                                })
                            });
                        }

                        results.Add(new
                        {
                            wishlistItemId = wishlistItem.Id,
                            cardName = cardInfo?.Name ?? "Unknown",
                            cardSetName = cardSet?.Name ?? "Unknown",
                            gameName = game?.Name ?? "Unknown",
                            priority = wishlistItem.Priority,
                            priorityLabel = GetPriorityLabel(wishlistItem.Priority),
                            preferredCondition = wishlistItem.PreferredCondition?.ToString(),
                            maxPrice = wishlistItem.MaxPrice,
                            matchesCount = owners.Count,
                            totalQuantityAvailable = cardsInRadius.Sum(x => x.card.Quantity),
                            ownersWithCards = owners
                        });
                    }
                }

                return Ok(new
                {
                    userId,
                    username = user.Username,
                    userLocation = new
                    {
                        city = user.Location.City,
                        province = user.Location.Province,
                        country = user.Location.Country,
                        latitude = user.Location.Latitude,
                        longitude = user.Location.Longitude
                    },
                    radiusKm,
                    priority,
                    priorityLabel = priority.HasValue ? GetPriorityLabel(priority.Value) : null,
                    wishlistItemsChecked = wishlistItems.Count(),
                    itemsWithNearbyMatches = results.Count,
                    matches = results
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la ricerca di match nelle vicinanze per l'utente {UserId}", userId);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }



        // Helper methods per i match
        private async Task<IEnumerable<Card>> FindMatchingCards(WishlistItem wishlistItem)
        {
            var availableCards = await _cardRepository.GetCardsByCardInfoAsync(wishlistItem.CardInfoId);

            // Filtra per condizione se specificata
            if (wishlistItem.PreferredCondition.HasValue)
            {
                availableCards = availableCards.Where(c => c.Condition >= wishlistItem.PreferredCondition.Value);
            }

            // Filtra per prezzo se specificato
            if (wishlistItem.MaxPrice.HasValue)
            {
                availableCards = availableCards.Where(c => !c.EstimatedValue.HasValue || c.EstimatedValue <= wishlistItem.MaxPrice);
            }

            // Non includere le proprie carte
            availableCards = availableCards.Where(c => c.UserId != wishlistItem.UserId);

            return availableCards.ToList();
        }

        private async Task<MatchingCardDto> MapToMatchingCardDto(Card card, User? requestingUser)
        {
            var cardOwner = await _userRepository.GetWithLocationAsync(card.UserId);

            double? distanceKm = null;
            if (requestingUser?.Location != null &&
                cardOwner?.Location != null &&
                requestingUser.Location.Latitude.HasValue &&
                requestingUser.Location.Longitude.HasValue &&
                cardOwner.Location.Latitude.HasValue &&
                cardOwner.Location.Longitude.HasValue)
            {
                distanceKm = CalculateDistance(
                    (double)requestingUser.Location.Latitude.Value,
                    (double)requestingUser.Location.Longitude.Value,
                    (double)cardOwner.Location.Latitude.Value,
                    (double)cardOwner.Location.Longitude.Value
                );
            }

            return new MatchingCardDto
            {
                CardId = card.Id,
                UserId = card.UserId,
                Username = cardOwner?.Username ?? "Unknown",
                Condition = card.Condition.ToString(),
                Quantity = card.Quantity,
                EstimatedValue = card.EstimatedValue,
                DistanceKm = distanceKm,
                UserLocation = cardOwner?.Location != null ? new UserLocationDto
                {
                    City = cardOwner.Location.City,
                    Province = cardOwner.Location.Province,
                    Country = cardOwner.Location.Country,
                    PostalCode = cardOwner.Location.PostalCode,
                    Latitude = cardOwner.Location.Latitude,
                    Longitude = cardOwner.Location.Longitude,
                    MaxDistanceKm = cardOwner.Location.MaxDistanceKm
                } : null
            };
        }

        // Formula di Haversine per calcolare la distanza tra due punti GPS
        private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double earthRadiusKm = 6371;

            var dLat = DegreesToRadians(lat2 - lat1);
            var dLon = DegreesToRadians(lon2 - lon1);

            lat1 = DegreesToRadians(lat1);
            lat2 = DegreesToRadians(lat2);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2) * Math.Cos(lat1) * Math.Cos(lat2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return earthRadiusKm * c;
        }

        private static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180;
        }



        // Helper methods
        private async Task<WishlistItemDto> MapToDto(
            WishlistItem wishlistItem,
            Dictionary<int, CardInfo> cardInfos,
            Dictionary<int, (string Name, string Code, int GameId)> cardSets,
            Dictionary<int, string> games)
        {
            var cardInfo = cardInfos.GetValueOrDefault(wishlistItem.CardInfoId);
            var cardSetInfo = cardInfo != null && cardSets.TryGetValue(cardInfo.CardSetId, out var cs)
                ? cs
                : (Name: "Unknown", Code: "", GameId: 0);
            var gameName = games.GetValueOrDefault(cardSetInfo.GameId, "Unknown");

            // Conta le carte disponibili che matchano
            var availableMatches = await CountAvailableMatches(wishlistItem);

            return new WishlistItemDto
            {
                Id = wishlistItem.Id,
                UserId = wishlistItem.UserId,
                CardInfoId = wishlistItem.CardInfoId,
                CardName = cardInfo?.Name ?? "Unknown",
                CardSetName = cardSetInfo.Name,
                CardSetCode = cardSetInfo.Code,
                GameName = gameName,
                CardNumber = cardInfo?.CardNumber,
                Rarity = cardInfo?.Rarity,
                PreferredCondition = wishlistItem.PreferredCondition?.ToString(),
                MaxPrice = wishlistItem.MaxPrice,
                Notes = wishlistItem.Notes,
                Priority = wishlistItem.Priority,
                PriorityLabel = GetPriorityLabel(wishlistItem.Priority),
                CreatedAt = wishlistItem.CreatedAt,
                AvailableMatches = availableMatches
            };
        }

        private async Task<int> CountAvailableMatches(WishlistItem wishlistItem)
        {
            var availableCards = await _cardRepository.GetCardsByCardInfoAsync(wishlistItem.CardInfoId);

            // Filtra per condizione se specificata
            if (wishlistItem.PreferredCondition.HasValue)
            {
                availableCards = availableCards.Where(c => c.Condition >= wishlistItem.PreferredCondition.Value);
            }

            // Filtra per prezzo se specificato
            if (wishlistItem.MaxPrice.HasValue)
            {
                availableCards = availableCards.Where(c => !c.EstimatedValue.HasValue || c.EstimatedValue <= wishlistItem.MaxPrice);
            }

            // Non contare le proprie carte
            availableCards = availableCards.Where(c => c.UserId != wishlistItem.UserId);

            return availableCards.Count();
        }

        private static string GetPriorityLabel(int priority)
        {
            return priority switch
            {
                1 => "Alta",
                2 => "Media",
                3 => "Bassa",
                _ => "Sconosciuta"
            };
        }
    }
}