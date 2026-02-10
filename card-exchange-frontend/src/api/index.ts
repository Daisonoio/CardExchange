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
} from '../types';

// === Auth ===
export const auth = {
  login: (data: LoginRequest) =>
    client.post<AuthResponse>('/auth/login', data),
  register: (data: RegisterRequest) =>
    client.post<AuthResponse>('/auth/register', data),
  logout: () => client.post('/auth/logout'),
};

// === Cards ===
export const cards = {
  getAll: () => client.get<Card[]>('/cards'),
  getMine: () => client.get<Card[]>('/cards/my-cards'),
  create: (data: CreateCardRequest) => client.post<Card>('/cards', data),
  update: (id: number, data: Partial<CreateCardRequest>) =>
    client.put<Card>(`/cards/${id}`, data),
  delete: (id: number) => client.delete(`/cards/${id}`),
  search: (term: string) =>
    client.get<Card[]>('/cards/search', { params: { term } }),
  nearby: (lat: number, lon: number, radiusKm: number) =>
    client.get<Card[]>('/cards/nearby', {
      params: { latitude: lat, longitude: lon, radiusKm },
    }),
};

// === Wishlist ===
export const wishlist = {
  getMine: () => client.get<WishlistItem[]>('/wishlist'),
  create: (data: CreateWishlistRequest) =>
    client.post<WishlistItem>('/wishlist', data),
  update: (id: number, data: Partial<CreateWishlistRequest>) =>
    client.put<WishlistItem>(`/wishlist/${id}`, data),
  delete: (id: number) => client.delete(`/wishlist/${id}`),
  getMatches: () => client.get('/wishlist/matches'),
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
  getMyProfile: () => client.get<User>('/users/me'),
  search: (params: Record<string, string | number>) =>
    client.get<User[]>('/users/search', { params }),
  nearby: (lat: number, lon: number, radiusKm: number) =>
    client.get<User[]>('/users/nearby', {
      params: { latitude: lat, longitude: lon, radiusKm },
    }),
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
