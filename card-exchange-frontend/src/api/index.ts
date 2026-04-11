import client from './client';
import type {
  LoginRequest,
  RegisterRequest,
  AuthResponse,
  Card,
  CreateCardRequest,
  WishlistItem,
  CreateWishlistRequest,
  PagedResult,
  CardEvent,
  ScryfallAutocomplete,
  ScryfallCard,
  User,
  TradeOffer,
  CreateTradeOfferRequest,
  TradeOfferStatus,
  Notification,
  NotificationPreference,
} from '../types';

// === Auth ===
export const auth = {
  login: (data: LoginRequest) =>
    client.post<AuthResponse>('/auth/login', data),
  register: (data: RegisterRequest) =>
    client.post<AuthResponse>('/auth/register', data),
  me: () => client.get<User>('/auth/me'),
  logout: () => client.post('/auth/logout'),
};

// === Cards ===
export const cards = {
  getAll: (gameId?: number | null) =>
    client.get<Card[]>('/cards', { params: gameId ? { gameId } : undefined }),
  getByUser: (userId: number, gameId?: number | null) =>
    client.get<Card[]>(`/cards/user/${userId}`, { params: gameId ? { gameId } : undefined }),
  getById: (id: number) => client.get<Card>(`/cards/${id}`),
  create: (userId: number, data: CreateCardRequest) =>
    client.post<Card>(`/cards/user/${userId}`, data),
  update: (id: number, data: Partial<CreateCardRequest>) =>
    client.put<Card>(`/cards/${id}`, data),
  delete: (id: number) => client.delete(`/cards/${id}`),
  search: (term: string) =>
    client.get<Card[]>('/cards/search', { params: { term } }),
  nearby: (userId: number, radiusKm: number, coords?: { latitude: number; longitude: number }, gameId?: number | null) =>
    client.get<Card[]>(`/cards/nearby/${userId}`, {
      params: { radiusKm, ...coords, ...(gameId ? { gameId } : {}) },
    }),
};

// === Card Photos ===
export const cardPhotos = {
  getByCard: (cardId: number) =>
    client.get<{ cardId: number; count: number; photos: { id: number; cardId: number; contentType: string; fileSizeBytes: number; downloadUrl: string; createdAt: string }[] }>(`/cards/${cardId}/photos`),
  upload: (cardId: number, base64Image: string, contentType: string) =>
    client.post(`/cards/${cardId}/photos`, { base64Image, contentType }),
  delete: (cardId: number, photoId: number) =>
    client.delete(`/cards/${cardId}/photos/${photoId}`),
};

// === Wishlist ===
export const wishlist = {
  getByUser: (userId: number, gameId?: number | null) =>
    client.get<WishlistItem[]>(`/wishlist/user/${userId}`, { params: gameId ? { gameId } : undefined }),
  getById: (id: number) => client.get<WishlistItem>(`/wishlist/${id}`),
  create: (userId: number, data: CreateWishlistRequest) =>
    client.post<WishlistItem>(`/wishlist/user/${userId}`, data),
  update: (id: number, data: Partial<CreateWishlistRequest>) =>
    client.put<WishlistItem>(`/wishlist/${id}`, data),
  delete: (id: number) => client.delete(`/wishlist/${id}`),
  getMatches: (userId: number) =>
    client.get(`/wishlist/user/${userId}/all-matches`),
  topNearbyMatches: (userId: number, params?: Record<string, string | number>) =>
    client.get(`/wishlist/user/${userId}/top-nearby-matches`, { params }),
};

// === Scryfall ===
export const scryfall = {
  autocomplete: (q: string) =>
    client.get<ScryfallAutocomplete>('/scryfall/autocomplete', {
      params: { q },
    }),
  search: (q: string, page = 1) =>
    client.get<{ data: ScryfallCard[]; has_more: boolean; total_cards: number }>(
      '/scryfall/search',
      { params: { q, page } }
    ),
  getByName: (name: string) =>
    client.get<ScryfallCard>('/scryfall/cards/named', { params: { exact: name } }),
  importCard: (scryfallId: string) =>
    client.post('/scryfall/import', { scryfallId }),
};

// === Pokémon TCG ===
export const pokemontcg = {
  search: (q: string, page = 1) =>
    client.get<{ source: string; cards: any[]; totalCount: number }>(
      '/pokemontcg/search',
      { params: { q, page } }
    ),
  importCard: (pokemonTcgId: string) =>
    client.post<{ cardInfoId: number; isNew: boolean }>('/pokemontcg/import', { pokemonTcgId }),
};

// === Yu-Gi-Oh! ===
export const yugioh = {
  search: (q: string, page = 1) =>
    client.get<{ source: string; cards: any[]; totalCount: number }>(
      '/yugioh/search',
      { params: { q, page } }
    ),
  importCard: (yuGiOhId: number) =>
    client.post<{ cardInfoId: number; isNew: boolean }>('/yugioh/import', { yuGiOhId }),
};

// === One Piece TCG ===
export const onepiece = {
  search: (q: string, page = 1) =>
    client.get<{ source: string; cards: any[]; totalCount: number }>(
      '/onepiece/search',
      { params: { q, page } }
    ),
  importCard: (code: string) =>
    client.post<{ cardInfoId: number; isNew: boolean }>('/onepiece/import', { code }),
};

// === Users ===
export const users = {
  getProfile: (id: number) => client.get<User>(`/users/${id}`),
  getByUsername: (username: string) =>
    client.get<User>(`/users/by-username/${username}`),
  update: (id: number, data: {
    firstName?: string;
    lastName?: string;
    bio?: string;
    paypalUsername?: string;
    satispayUsername?: string;
    paymentQrCodeUrl?: string;
  }) =>
    client.put<User>(`/users/${id}`, data),
  updateLocation: (id: number, data: {
    city: string;
    province: string;
    country: string;
    postalCode?: string;
    latitude?: number;
    longitude?: number;
    maxDistanceKm?: number;
  }) => client.put<User>(`/users/${id}/location`, data),
  nearby: (lat: number, lon: number, radiusKm: number) =>
    client.get<User[]>('/users/nearby', {
      params: { latitude: lat, longitude: lon, radiusKm },
    }),
  byLocation: (params: Record<string, string | number>) =>
    client.get<User[]>('/users/by-location', { params }),
};

// === Events ===
export const events = {
  getUpcoming: (page = 1, pageSize = 20) =>
    client.get<PagedResult<CardEvent>>('/events', {
      params: { page, pageSize },
    }),
  getByCity: (city: string, page = 1) =>
    client.get<PagedResult<CardEvent>>(`/events/city/${city}`, {
      params: { page },
    }),
  getNearby: (lat: number, lon: number, radiusKm = 50) =>
    client.get<CardEvent[]>('/events/nearby', {
      params: { latitude: lat, longitude: lon, radiusKm },
    }),
  getById: (id: number) => client.get<CardEvent>(`/events/${id}`),
  join: (id: number) => client.post(`/events/${id}/join`),
  leave: (id: number) => client.post(`/events/${id}/leave`),
};

// === CardInfos ===
export const cardInfos = {
  search: (name: string) =>
    client.get<{ id: number; name: string; cardSet?: { name: string } }[]>(
      '/cardinfos/search',
      { params: { name } }
    ),
  getById: (id: number) => client.get(`/cardinfos/${id}`),
};

// === Trade Offers ===
export const tradeOffers = {
  getMine: (params?: { page?: number; pageSize?: number; status?: TradeOfferStatus }) =>
    client.get<{ items: TradeOffer[]; totalCount: number }>('/tradeoffers', { params }),
  getById: (id: number) =>
    client.get<TradeOffer>(`/tradeoffers/${id}`),
  create: (data: CreateTradeOfferRequest) =>
    client.post<TradeOffer>('/tradeoffers', data),
  accept: (id: number) =>
    client.post<TradeOffer>(`/tradeoffers/${id}/accept`),
  reject: (id: number) =>
    client.post<TradeOffer>(`/tradeoffers/${id}/reject`),
  cancel: (id: number) =>
    client.post<TradeOffer>(`/tradeoffers/${id}/cancel`),
  counter: (id: number, data: Omit<CreateTradeOfferRequest, 'receiverId'>) =>
    client.post<TradeOffer>(`/tradeoffers/${id}/counter`, data),
  complete: (id: number) =>
    client.post<TradeOffer>(`/tradeoffers/${id}/complete`),
  review: (id: number, data: { rating: number; comment?: string; cardAsDescribed: boolean; timelyShipping: boolean; goodCommunication: boolean }) =>
    client.post(`/tradeoffers/${id}/review`, data),
};

// === Messages / Chat ===
export const messages = {
  getConversations: () =>
    client.get<import('../types').ConversationPreview[]>('/messages/conversations'),
  getMessages: (conversationId: number, page = 1, pageSize = 50) =>
    client.get<{ totalCount: number; page: number; pageSize: number; messages: import('../types').ChatMessage[] }>(
      `/messages/conversations/${conversationId}`, { params: { page, pageSize } }),
  send: (recipientId: number, content: string, tradeOfferId?: number) =>
    client.post<{ messageId: number; conversationId: number; sentAt: string }>(
      '/messages/send', { recipientId, content, tradeOfferId }),
  getUnreadCount: () =>
    client.get<{ unreadCount: number }>('/messages/unread-count'),
};

// === Notifications ===
export const notifications = {
  getAll: (params?: { unreadOnly?: boolean; page?: number; pageSize?: number }) =>
    client.get<{ totalCount: number; page: number; pageSize: number; notifications: Notification[] }>('/notifications', { params }),
  markAsRead: (id: number) =>
    client.put(`/notifications/${id}/read`),
  markAllAsRead: () =>
    client.put('/notifications/read-all'),
  getUnreadCount: () =>
    client.get<{ unreadCount: number }>('/notifications/unread-count'),
  delete: (id: number) =>
    client.delete(`/notifications/${id}`),
  getPreferences: () =>
    client.get<{ preferences: NotificationPreference[] }>('/notifications/preferences'),
  updatePreferences: (preferences: { typeId: number; isEnabled: boolean }[]) =>
    client.put('/notifications/preferences', { preferences }),
};

// === Favorites ===
export const favorites = {
  getAll: () =>
    client.get<{ count: number; favorites: any[] }>('/favorites'),
  add: (cardId: number) =>
    client.post(`/favorites/${cardId}`),
  remove: (cardId: number) =>
    client.delete(`/favorites/${cardId}`),
  check: (cardId: number) =>
    client.get<{ isFavorite: boolean }>(`/favorites/check/${cardId}`),
  getCardIds: () =>
    client.get<{ cardIds: number[] }>('/favorites/card-ids'),
};

// === Games ===
export const games = {
  getAll: () => client.get<{ id: number; name: string; description?: string; publisher: string; isActive: boolean }[]>('/games'),
};

// === Matchmaking ===
export const matchmaking = {
  getMatches: (params?: { radiusKm?: number; latitude?: number; longitude?: number; gameId?: number | null }) => {
    const { gameId, ...rest } = params ?? {};
    return client.get<import('../types').MatchmakingResponse>('/matchmaking', {
      params: { ...rest, ...(gameId ? { gameId } : {}) },
    });
  },
};

// === Price Tracking ===
export const priceTracking = {
  portfolio: () => client.get('/pricetracking/portfolio'),
  movers: (limit = 10) =>
    client.get('/pricetracking/portfolio/movers', { params: { limit } }),
  analyzeTrade: (offeredCardIds: number[], requestedCardIds: number[]) =>
    client.post('/pricetracking/analyze-trade', {
      offeredCardIds,
      requestedCardIds,
    }),
  history: (cardInfoId: number, days = 90) =>
    client.get<import('../types').PriceHistoryData>(`/pricetracking/history/${cardInfoId}`, { params: { days } }),
  getSpikes: (gameId?: number | null) =>
    client.get<import('../types').PriceSpike[]>('/pricetracking/spikes', { params: gameId ? { gameId } : undefined }),
  checkSpikes: () =>
    client.post<{ spikeCount: number; message: string }>('/pricetracking/spikes/check'),
  getSpikeSettings: () =>
    client.get<import('../types').SpikeSettings>('/pricetracking/spike-settings'),
  updateSpikeSettings: (thresholdPercentage: number) =>
    client.put('/pricetracking/spike-settings', { thresholdPercentage }),
};
