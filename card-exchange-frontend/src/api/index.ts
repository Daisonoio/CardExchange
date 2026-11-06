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
  setBirthDate: (birthDate: string) => client.post<User>('/auth/birth-date', { birthDate }),
  logout: () => client.post('/auth/logout'),
  forgotPassword: (email: string) =>
    client.post<{ message: string }>('/auth/forgot-password', { email }),
  resetPassword: (data: { token: string; newPassword: string; confirmNewPassword: string }) =>
    client.post<{ message: string }>('/auth/reset-password', data),
  verifyEmail: (token: string) =>
    client.get<{ message: string }>('/auth/verify-email', { params: { token } }),
  resendVerification: () =>
    client.post<{ message: string }>('/auth/resend-verification'),
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
  bulk: (data: { cardIds: number[]; isAvailableForTrade?: boolean; delete?: boolean }) =>
    client.post<{ updated: number; ignored: number }>('/cards/bulk', data),
  search: (term: string) =>
    client.get<Card[]>('/cards/search', { params: { term } }),
  nearby: (userId: number, radiusKm: number, coords?: { latitude: number; longitude: number }, gameId?: number | null, searchTerm?: string) =>
    client.get<Card[]>(`/cards/nearby/${userId}`, {
      params: { radiusKm, ...coords, ...(gameId ? { gameId } : {}), ...(searchTerm ? { searchTerm } : {}) },
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
    client.get<{ data: ScryfallCard[]; cards?: ScryfallCard[]; has_more: boolean; total_cards: number }>(
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
      '/onepiecetcg/search',
      { params: { q, page } }
    ),
  importCard: (code: string) =>
    client.post<{ cardInfoId: number; isNew: boolean }>('/onepiecetcg/import', { code }),
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
  /** Senza coordinate il backend usa la posizione del profilo. */
  getNearby: (lat?: number, lon?: number, radiusKm = 50) =>
    client.get<CardEvent[]>('/events/nearby', {
      params: { latitude: lat, longitude: lon, radiusKm },
    }),
  getById: (id: number) => client.get<import('../types').CardEventDetail>(`/events/${id}`),
  byStore: (storeId: number) => client.get<CardEvent[]>(`/events/store/${storeId}`),
  create: (data: {
    title: string; description?: string; type: number; startDate: string; endDate?: string;
    address: string; city: string; province?: string; postalCode?: string; country: string;
    latitude?: number; longitude?: number; maxParticipants?: number; isPublic: boolean;
    supportedGameIds?: string; storeId?: number;
    entryFeeEur?: number; registrationDeadline?: string; format?: string; gameId?: number;
  }) => client.post<CardEvent>('/events', data),
  confirmSpot: (id: number) => client.post<{ message: string }>(`/events/${id}/confirm-spot`),
  attendees: (id: number) => client.get<import('../types').EventAttendee[]>(`/events/${id}/attendees`),
  markPresent: (id: number, userId: number, present: boolean) =>
    client.post<{ userId: number; checkedInAt?: string | null }>(`/events/${id}/attendees/${userId}/checkin`, null, { params: { present } }),
  cancel: (id: number) => client.delete(`/events/${id}`),
  checkIn: (id: number) => client.post<{ message: string }>(`/events/${id}/checkin`),
  checkOut: (id: number) => client.post<{ message: string }>(`/events/${id}/checkout`),
  live: (id: number) => client.get<import('../types').EventLive>(`/events/${id}/live`),
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
  reviewsByUser: (userId: number, page = 1) =>
    client.get<import('../types').UserReviewsPage>(`/tradeoffers/reviews/user/${userId}`, { params: { page, pageSize: 20 } }),
  evaluate: (offeredCards: { cardId: number; quantity: number }[], requestedCards: { cardId: number; quantity: number }[]) =>
    client.post<import('../types').TradeEvaluation>('/tradeoffers/evaluate', { offeredCards, requestedCards }),
  suggest: (receiverId: number, requestedCards: { cardId: number; quantity: number }[]) =>
    client.post<import('../types').TradeSuggestion>('/tradeoffers/suggest', { receiverId, requestedCards }),
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
  getMatches: (params?: { radiusKm?: number; latitude?: number; longitude?: number; gameId?: number | null; limit?: number; mutualOnly?: boolean }) => {
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

// === Import collezione (CSV, Magic) ===
export const collectionImport = {
  preview: (csv: string, catalogKey?: string) =>
    client.post<import('../types').ImportPreview>('/import/collection/preview', { csv, catalogKey }, { timeout: 300_000 }),
  commit: (rows: import('../types').ImportCommitRow[], isAvailableForTrade: boolean) =>
    client.post<import('../types').ImportCommitResult>('/import/collection/commit', { rows, isAvailableForTrade }, { timeout: 300_000 }),
};

// === Sicurezza: blocchi, segnalazioni, moderazione ===
export const safety = {
  block: (userId: number) => client.post<{ message: string }>(`/users/${userId}/block`),
  unblock: (userId: number) => client.delete<{ message: string }>(`/users/${userId}/block`),
  blocked: () => client.get<{ userId: number; username: string; blockedAt: string }[]>('/users/blocked'),
  report: (data: { reportedUserId: number; reason: number; details?: string; tradeOfferId?: number }) =>
    client.post<{ message: string }>('/reports', data),
};

export const moderation = {
  reports: (status: 'open' | 'closed' | 'all' = 'open', page = 1) =>
    client.get<{ total: number; page: number; pageSize: number; items: import('../types').ModerationReport[] }>(
      '/moderation/reports', { params: { status, page } }),
  resolve: (id: number, data: { outcome: 'resolved' | 'dismissed'; note?: string; suspendUser?: boolean }) =>
    client.post<{ message: string }>(`/moderation/reports/${id}/resolve`, data),
};

// === Appuntamenti di scambio ===
export const meetups = {
  list: (tradeOfferId: number) =>
    client.get<import('../types').TradeMeetup[]>(`/tradeoffers/${tradeOfferId}/meetups`),
  suggestions: (tradeOfferId: number) =>
    client.get<MeetupSpot[]>(`/tradeoffers/${tradeOfferId}/meetups/suggestions`),
  propose: (tradeOfferId: number, data: { scheduledAt: string; locationName: string; address?: string; latitude?: number; longitude?: number; notes?: string; storeId?: number }) =>
    client.post<import('../types').TradeMeetup>(`/tradeoffers/${tradeOfferId}/meetups`, data),
  accept: (tradeOfferId: number, meetupId: number) =>
    client.post<import('../types').TradeMeetup>(`/tradeoffers/${tradeOfferId}/meetups/${meetupId}/accept`),
  decline: (tradeOfferId: number, meetupId: number) =>
    client.post<import('../types').TradeMeetup>(`/tradeoffers/${tradeOfferId}/meetups/${meetupId}/decline`),
  cancel: (tradeOfferId: number, meetupId: number) =>
    client.post<import('../types').TradeMeetup>(`/tradeoffers/${tradeOfferId}/meetups/${meetupId}/cancel`),
};

// === Notifiche push (Web Push) ===
export const pushApi = {
  publicKey: () => client.get<{ enabled: boolean; publicKey?: string }>('/push/public-key'),
  subscribe: (data: { endpoint: string; keys: { p256dh: string; auth: string } }) =>
    client.post<{ message: string }>('/push/subscribe', data),
  unsubscribe: (endpoint: string) => client.post<{ message: string }>('/push/unsubscribe', { endpoint }),
  test: () => client.post<{ message: string }>('/push/test'),
};

// === Negozi ===
export const buyList = {
  list: (storeId: number) => client.get<import('../types').StoreBuyOrder[]>(`/stores/${storeId}/buy-orders`),
  create: (storeId: number, data: import('../types').BuyOrderInput) => client.post<{ id: number }>(`/stores/${storeId}/buy-orders`, data),
  update: (storeId: number, orderId: number, data: import('../types').BuyOrderInput) => client.put<{ id: number }>(`/stores/${storeId}/buy-orders/${orderId}`, data),
  accept: (offerId: number, note?: string) => client.post(`/buy-offers/${offerId}/accept`, { note }),
  reject: (offerId: number, note?: string) => client.post(`/buy-offers/${offerId}/reject`, { note }),
  complete: (offerId: number) => client.post(`/buy-offers/${offerId}/complete`),
  forMe: () => client.get<import('../types').BuyOrderForMe[]>('/buy-orders/for-me'),
  propose: (orderId: number, cardId: number, quantity: number) => client.post(`/buy-orders/${orderId}/offers`, { cardId, quantity }),
  withdraw: (offerId: number) => client.post(`/buy-offers/${offerId}/withdraw`),
};

export const reservations = {
  storeCards: (storeId: number, params?: { q?: string; gameId?: number; page?: number }) =>
    client.get<import('../types').StoreCardsPage>(`/stores/${storeId}/cards`, { params }),
  create: (storeId: number, cardId: number, quantity = 1) =>
    client.post<import('../types').Reservation>(`/stores/${storeId}/reservations`, { cardId, quantity }),
  mine: () => client.get<import('../types').Reservation[]>('/reservations/mine'),
  forStore: (storeId: number) => client.get<import('../types').Reservation[]>(`/stores/${storeId}/reservations`),
  confirm: (id: number) => client.post<import('../types').Reservation>(`/reservations/${id}/confirm`),
  pickup: (id: number, code: string) => client.post<import('../types').Reservation>(`/reservations/${id}/pickup`, { code }),
  cancel: (id: number) => client.post<import('../types').Reservation>(`/reservations/${id}/cancel`),
  noShow: (id: number) => client.post<import('../types').Reservation>(`/reservations/${id}/no-show`),
};

export const stores = {
  list: (params?: { latitude?: number; longitude?: number; radiusKm?: number; gameId?: number; meetupOnly?: boolean; q?: string }) =>
    client.get<import('../types').Store[]>('/stores', { params }),
  get: (id: number) => client.get<import('../types').Store>(`/stores/${id}`),
  mine: () => client.get<import('../types').Store[]>('/stores/mine'),
  bySlug: (slug: string) => client.get<{ id: number; name: string; city: string; slug: string }>(`/stores/by-slug/${encodeURIComponent(slug)}`),
  attributions: (id: number) => client.get<{ total: number; last30Days: number }>(`/stores/${id}/attributions`),
  reports: (id: number) => client.get<import('../types').StoreReports>(`/stores/${id}/reports`),
  demand: (id: number) => client.get<import('../types').StoreDemand>(`/stores/${id}/demand`),
  notifyInterested: (id: number, cardInfoId: number) => client.post<{ sent: number; skipped: number }>(`/stores/${id}/demand/notify`, { cardInfoId }),
  meetups: (id: number) => client.get<{ upcoming: { id: number; scheduledAt: string }[]; completedLast30Days: number }>(`/stores/${id}/meetups`),
  create: (data: import('../types').StoreInput) => client.post<import('../types').Store>('/stores', data),
  update: (id: number, data: import('../types').StoreInput) => client.put<import('../types').Store>(`/stores/${id}`, data),
  pending: (status: 'pending' | 'verified' | 'rejected' | 'suspended' = 'pending') =>
    client.get<{ ownerUsername: string; createdAt: string; store: import('../types').Store }[]>('/moderation/stores', { params: { status } }),
  review: (id: number, decision: 'verify' | 'reject' | 'suspend', note?: string) =>
    client.post<{ message: string }>(`/moderation/stores/${id}/review`, { decision, note }),
};

// === Inviti ===
export const referrals = {
  mine: () => client.get<{
    code: string; link: string; invitedCount: number;
    invited: { username: string; joinedAt: string; completedTrades: number; active?: boolean }[];
    reward?: { qualified: number; towardsNext: number; perReward: number; monthsEarned: number; monthsThisYear: number; maxMonthsPerYear: number };
  }>('/referrals/me'),
  lookup: (code: string) => client.get<{ username: string }>(`/referrals/${code}`),
};


// === Abbonamenti alla piattaforma ===
export const commissions = {
  forStore: (storeId: number) => client.get<{
    rate: number; maturingEur: number; availableEur: number; creditedEur: number; thisMonthEur: number; thisMonthCount: number;
    recent: { paidAt: string; amountEur: number; status: string }[];
    payouts: { id: number; month: string; amountEur: number; method: string; status: string }[];
  }>(`/stores/${storeId}/commissions`),
};

export const purchases = {
  products: () => client.get<{ enabled: boolean; products: { code: string; type: string; name: string; quantity: number; priceEur: number }[] }>('/purchases/products'),
  checkout: (productCode: string, refId?: number) => client.post<{ url: string; purchaseId: number }>('/purchases/checkout', { productCode, refId }),
  mine: () => client.get<{ credits: number; purchases: { id: number; productCode: string; type: string; quantity: number; amountEur: number; status: string; createdAt: string; paidAt?: string | null; refundedAt?: string | null }[] }>('/purchases/mine'),
};

export const me = {
  entitlements: () => client.get<import('../types').Entitlements>('/me/entitlements'),
};

export const billing = {
  plans: () => client.get<import('../types').SubscriptionPlan[]>('/subscriptions/plans'),
  options: () => client.get<import('../types').BillingOptions>('/subscriptions/billing-options'),
  current: () => client.get<import('../types').SubscriptionState>('/subscriptions/current'),
  history: () => client.get<import('../types').UserSubscription[]>('/subscriptions/history'),
  bankTransfer: (planId: number) =>
    client.post<import('../types').BankTransferInstructions>('/subscriptions/bank-transfer', { planId }),
  cancelBankTransfer: (id: number) => client.delete<{ message: string }>(`/subscriptions/bank-transfer/${id}`),
  cancelRenewal: () => client.post<{ message: string; activeUntil: string }>('/subscriptions/cancel', {}),
  checkout: (planId: number) => client.post<{ url: string }>('/billing/stripe/checkout', { planId }),
  portal: () => client.post<{ url: string }>('/billing/stripe/portal'),
};

export const adminBilling = {
  list: (params: { status?: string; q?: string; page?: number }) =>
    client.get<{ total: number; page: number; items: import('../types').AdminSubscriptionRow[] }>('/admin/billing/subscriptions', { params }),
  confirm: (id: number, data: { amountPaid?: number; note?: string }) =>
    client.post<{ message: string }>(`/admin/billing/subscriptions/${id}/confirm`, data),
  revoke: (id: number, note?: string) =>
    client.post<{ message: string }>(`/admin/billing/subscriptions/${id}/revoke`, { note }),
  grant: (data: { user: string; planId: number; amountPaid: number; endDate?: string; paymentReference?: string; note?: string }) =>
    client.post<{ message: string }>('/admin/billing/grant', data),
};

// === Statistiche, export e ricerca avanzata (Premium) ===
export const statistics = {
  basic: () => client.get<import('../types').BasicStats>('/statistics/collection/basic'),
  insights: (days: number) => client.get<import('../types').CollectionInsights>('/statistics/collection/insights', { params: { days } }),
};

/** Scarica un file dall'API (con il token) e lo salva con il nome indicato dal server. */
export async function downloadExport(path: 'collection/csv' | 'collection/json' | 'wishlist/csv') {
  const res = await client.get<Blob>(`/export/${path}`, { responseType: 'blob' });
  const disposition = String(res.headers['content-disposition'] ?? '');
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
  const name = match ? decodeURIComponent(match[1]) : `cardexchange_${path.replace('/', '.')}`;
  const url = URL.createObjectURL(res.data);
  const a = document.createElement('a');
  a.href = url;
  a.download = name;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

export const advancedSearch = {
  search: (params: import('../types').AdvancedSearchParams) =>
    client.post<import('../types').AdvancedSearchResponse>('/search/advanced', params),
};

// === Catalogo comune ai giochi gestiti da provider (es. Lorcana) ===
export interface CatalogCardDto {
  externalId: string;
  name: string;
  subtitle?: string;
  setCode?: string;
  setName?: string;
  number?: string;
  rarity?: string;
  imageSmall?: string;
  imageLarge?: string;
  priceEur?: number;
  priceEurFoil?: number;
  priceConverted?: boolean;
}

export const catalog = {
  providers: () => client.get<{ key: string; gameName: string; importHint: string }[]>('/catalog/providers'),
  search: (key: string, q: string) => client.get<{ cards: CatalogCardDto[] }>(`/catalog/${key}/search`, { params: { q } }),
  importCard: (key: string, externalId: string) =>
    client.post<{ cardInfoId: number; isNew: boolean }>(`/catalog/${key}/import`, { externalId }),
};

// === "Segui" e avvisi "Sono qui" ===
export interface FollowStatus { following: boolean; followers: number }

export interface LiveBroadcast {
  id: number;
  author: { id: number; username: string; avatarUrl?: string };
  store?: { id: number; name: string } | null;
  event?: { id: number; title: string } | null;
  placeName: string;
  spotDetail?: string | null;
  message?: string | null;
  latitude: number;
  longitude: number;
  startsAt: string;
  endsAt: string;
  isLive: boolean;
  isMine: boolean;
  distanceKm?: number | null;
  recipientCount?: number | null;
}

export interface FollowingList {
  users: { id: number; username: string; avatarUrl?: string; city?: string; live?: { id: number; placeName: string } | null }[];
  stores: { id: number; name: string; city: string; live?: { id: number; placeName: string } | null }[];
  followers: number;
}

export interface CreateBroadcastPayload {
  storeId?: number;
  eventId?: number;
  placeName: string;
  spotDetail?: string;
  message?: string;
  latitude?: number;
  longitude?: number;
  durationMinutes: number;
}

export const follows = {
  list: () => client.get<FollowingList>('/follows'),
  userStatus: (id: number) => client.get<FollowStatus>(`/follows/users/${id}`),
  followUser: (id: number) => client.post<FollowStatus>(`/follows/users/${id}`),
  unfollowUser: (id: number) => client.delete<FollowStatus>(`/follows/users/${id}`),
  storeStatus: (id: number) => client.get<FollowStatus>(`/follows/stores/${id}`),
  followStore: (id: number) => client.post<FollowStatus>(`/follows/stores/${id}`),
  unfollowStore: (id: number) => client.delete<FollowStatus>(`/follows/stores/${id}`),
};

export const broadcasts = {
  create: (payload: CreateBroadcastPayload) => client.post<LiveBroadcast>('/broadcasts', payload),
  live: (coords?: { latitude: number; longitude: number }) => client.get<LiveBroadcast[]>('/broadcasts/live', { params: coords }),
  mine: () => client.get<{ items: LiveBroadcast[]; remainingToday: number; maxPerDay: number; followers: number }>('/broadcasts/mine'),
  get: (id: number) => client.get<LiveBroadcast>(`/broadcasts/${id}`),
  end: (id: number) => client.post<LiveBroadcast>(`/broadcasts/${id}/end`),
};

/** Negozio suggerito come punto di scambio sicuro per un appuntamento. */
export interface MeetupSpot {
  storeId: number;
  name: string;
  address: string;
  city: string;
  latitude: number;
  longitude: number;
  openingHours?: string | null;
  isPartner: boolean;
  distanceFromMeKm?: number | null;
  distanceFromOtherKm?: number | null;
}

// === Account e privacy (GDPR) ===
export const account = {
  /** Tutti i dati personali in un file JSON. */
  exportData: () => client.get<Blob>('/account/export-data', { responseType: 'blob' }),
  /** Elimina l'account: serve lo username come conferma. */
  deleteAccount: (confirmUsername: string) => client.delete('/account/delete-account', { params: { confirmUsername } }),
  /** Fuso orario e lingua del dispositivo, per orari e testi di notifiche ed email. */
  setLocale: (data: { timeZone?: string; language?: string }) => client.put('/account/locale', data),
};

// === Monitoraggio (amministratori) ===
export interface MonitoringOverview {
  errors: { id: number; source: 'Server' | 'Client'; category: string; message: string; detail?: string | null; path?: string | null; count: number; firstSeenAt: string; lastSeenAt: string; isResolved: boolean }[];
  openGroups: number;
  seenLast24h: number;
  catalogs: { key: string; lastCheckAt?: string | null; lastOkAt?: string | null; lastFailAt?: string | null; consecutiveFailures: number; lastError?: string | null; isUp: boolean }[];
  localCopy?: { cards: number; sets: number; lastMirroredAt?: string | null };
}

export interface RateMetric { eligible: number; count: number; rate?: number | null }
export interface PlanMetricsResult {
  city: string | null;
  matchWithin48h: RateMetric;
  weeklyActiveUsers: number;
  tradesCompletedLastWeek: number;
  retentionAfter4Weeks: RateMetric;
  activeStores: number;
  broadcastsLastWeek: number;
  totalUsers: number;
  weeks: { from: string; activeUsers: number; newUsers: number; tradesCompleted: number; broadcasts: number }[];
  cities: { city: string; users: number }[];
}

export const planMetrics = {
  get: (city?: string) => client.get<PlanMetricsResult>('/admin/metrics', { params: { city: city || undefined } }),
};

export const monitoring = {
  overview: (includeResolved = false) => client.get<MonitoringOverview>('/admin/monitoring', { params: { includeResolved } }),
  resolve: (id: number) => client.post(`/admin/monitoring/errors/${id}/resolve`),
};

export type LoyaltyInfo = {
  active: boolean; hasPlan?: boolean; configured?: boolean; stampsRequired?: number; rewardDescription?: string; isActive?: boolean;
  myStamps?: number;
  stats?: { customers: number; withTwoOrMore: number; rewardsReady: number; redeemedThisMonth: number; stampsThisMonth: number } | null;
};
export type LoyaltyCardInfo = {
  storeId: number; storeName: string; storeSlug?: string; city: string; stamps: number; stampsRequired: number;
  reward: string; rewardReady: boolean; rewardsRedeemed: number; lastStampAt?: string;
};
export type LoyaltyScanResult = { message: string; stamped?: boolean; username?: string; stamps: number; stampsRequired: number; rewardReady: boolean };

export const loyalty = {
  forStore: (storeId: number) => client.get<LoyaltyInfo>(`/stores/${storeId}/loyalty`),
  configure: (storeId: number, body: { stampsRequired: number; rewardDescription: string; isActive: boolean }) =>
    client.put(`/stores/${storeId}/loyalty`, body),
  stamp: (storeId: number, token: string) => client.post<LoyaltyScanResult>(`/stores/${storeId}/loyalty/stamp`, { token }),
  redeem: (storeId: number, token: string) => client.post<LoyaltyScanResult>(`/stores/${storeId}/loyalty/redeem`, { token }),
  mine: () => client.get<LoyaltyCardInfo[]>('/me/loyalty'),
  pass: () => client.get<{ token: string; expiresAt: string }>('/me/loyalty/pass'),
};
