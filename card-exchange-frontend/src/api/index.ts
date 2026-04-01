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
  getAll: () => client.get<Card[]>('/cards'),
  getByUser: (userId: number) => client.get<Card[]>(`/cards/user/${userId}`),
  getById: (id: number) => client.get<Card>(`/cards/${id}`),
  create: (userId: number, data: CreateCardRequest) =>
    client.post<Card>(`/cards/user/${userId}`, data),
  update: (id: number, data: Partial<CreateCardRequest>) =>
    client.put<Card>(`/cards/${id}`, data),
  delete: (id: number) => client.delete(`/cards/${id}`),
  search: (term: string) =>
    client.get<Card[]>('/cards/search', { params: { term } }),
  nearby: (userId: number, radiusKm: number, coords?: { latitude: number; longitude: number }) =>
    client.get<Card[]>(`/cards/nearby/${userId}`, {
      params: { radiusKm, ...coords },
    }),
};

// === Wishlist ===
export const wishlist = {
  getByUser: (userId: number) =>
    client.get<WishlistItem[]>(`/wishlist/user/${userId}`),
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

// === Users ===
export const users = {
  getProfile: (id: number) => client.get<User>(`/users/${id}`),
  getByUsername: (username: string) =>
    client.get<User>(`/users/by-username/${username}`),
  update: (id: number, data: { firstName?: string; lastName?: string; bio?: string }) =>
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
};
