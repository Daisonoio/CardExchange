import client from '../api/client';
import type { CardFinish } from '../types';

export interface ExploreCard {
  id: number;
  cardInfoId: number;
  name: string;
  setName?: string;
  number?: string;
  rarity?: string;
  game?: string;
  imageUrl?: string;
  condition: string;
  finish: string;
  language?: string;
  price?: number;
  priceKind: 'asking' | 'market';
  approxKm?: number;
  isWanted: boolean;
  owner: { id: number; username: string; reputationScore: number; planBadge?: 'Premium' | 'Store' };
}

export interface ExploreArea {
  lat: number;
  lng: number;
  count: number;
  approxKm?: number;
  preview: { id: number; name: string; imageUrl?: string; price?: number }[];
}

export interface ExploreResponse {
  total: number;
  page: number;
  hasMore: boolean;
  hasLocation: boolean;
  isPremium: boolean;
  radiusKm: number;
  origin?: { lat: number; lng: number };
  areas: ExploreArea[];
  cards: ExploreCard[];
}

export interface ExploreFilters {
  q?: string;
  gameId?: number;
  radiusKm?: number;
  sort?: 'distance' | 'price' | 'newest';
  minPrice?: number;
  maxPrice?: number;
  maxCondition?: number;
  finish?: CardFinish;
  page?: number;
  pageSize?: number;
}

export interface ExploreCardDetail extends Omit<ExploreCard, 'owner'> {
  setCode?: string;
  quantity: number;
  notes?: string;
  marketValue?: number;
  askingPrice?: number;
  isAvailableForTrade: boolean;
  isOwn: boolean;
  ownerWantsFromMe: number;
  owner: {
    id: number;
    username: string;
    avatarUrl?: string;
    reputationScore: number;
    totalReviewsReceived: number;
    totalTradesCompleted: number;
    memberSince: string;
    city?: string;
    planBadge?: 'Premium' | 'Store';
  };
}

export const explore = {
  cards: (f: ExploreFilters) => client.get<ExploreResponse>('/explore/cards', { params: f }),
  card: (id: number) => client.get<ExploreCardDetail>(`/explore/cards/${id}`),
};
