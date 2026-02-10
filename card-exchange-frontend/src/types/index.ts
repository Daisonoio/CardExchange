// === Auth ===
export interface LoginRequest {
  emailOrUsername: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  username: string;
  firstName: string;
  lastName: string;
  password: string;
}

export interface AuthResponse {
  token: string;
  refreshToken: string;
  user: User;
}

// === User ===
export interface User {
  id: number;
  email: string;
  username: string;
  firstName: string;
  lastName: string;
  bio?: string;
  avatarUrl?: string;
  isActive: boolean;
  reputationScore: number;
  totalTradesCompleted: number;
  totalReviewsReceived: number;
  location?: UserLocation;
}

export interface UserLocation {
  city: string;
  province: string;
  country: string;
  postalCode?: string;
  latitude?: number;
  longitude?: number;
  maxDistanceKm: number;
}

// === Cards ===
export interface CardInfo {
  id: number;
  cardSetId: number;
  name: string;
  cardNumber?: string;
  rarity?: string;
  type?: string;
  description?: string;
  imageUrl?: string;
  scryfallId?: string;
  manaCost?: string;
  cmc?: number;
  typeLine?: string;
  oracleText?: string;
  colors?: string;
  power?: string;
  toughness?: string;
  artist?: string;
  imageSmall?: string;
  imageNormal?: string;
  imageLarge?: string;
  priceUsd?: number;
  priceEur?: number;
  priceUsdFoil?: number;
  priceEurFoil?: number;
  cardSet?: CardSet;
}

export interface CardSet {
  id: number;
  gameId: number;
  name: string;
  code: string;
  releaseDate?: string;
  iconSvgUri?: string;
  game?: Game;
}

export interface Game {
  id: number;
  name: string;
  description?: string;
  isActive: boolean;
}

export type CardCondition = 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8;

export const CONDITION_LABELS: Record<CardCondition, string> = {
  1: 'Mint',
  2: 'Near Mint',
  3: 'Excellent',
  4: 'Good',
  5: 'Lightly Played',
  6: 'Moderately Played',
  7: 'Heavily Played',
  8: 'Damaged',
};

export interface Card {
  id: number;
  userId: number;
  cardInfoId: number;
  condition: CardCondition;
  quantity: number;
  notes?: string;
  isAvailableForTrade: boolean;
  estimatedValue?: number;
  cardInfo?: CardInfo;
  user?: User;
}

export interface CreateCardRequest {
  cardInfoId: number;
  condition: CardCondition;
  quantity: number;
  notes?: string;
  isAvailableForTrade: boolean;
  estimatedValue?: number;
}

// === Wishlist ===
export interface WishlistItem {
  id: number;
  userId: number;
  cardInfoId: number;
  preferredCondition?: CardCondition;
  maxPrice?: number;
  priority: 1 | 2 | 3;
  notes?: string;
  cardInfo?: CardInfo;
  availableMatchesCount?: number;
}

export interface CreateWishlistRequest {
  cardInfoId: number;
  preferredCondition?: CardCondition;
  maxPrice?: number;
  priority: 1 | 2 | 3;
  notes?: string;
}

// === Scryfall ===
export interface ScryfallCard {
  id: string;
  name: string;
  set: string;
  set_name: string;
  collector_number: string;
  rarity: string;
  mana_cost?: string;
  cmc?: number;
  type_line?: string;
  oracle_text?: string;
  colors?: string[];
  power?: string;
  toughness?: string;
  artist?: string;
  image_uris?: {
    small: string;
    normal: string;
    large: string;
    art_crop: string;
  };
  card_faces?: Array<{
    image_uris?: {
      small: string;
      normal: string;
      large: string;
    };
  }>;
  prices?: {
    usd?: string;
    usd_foil?: string;
    eur?: string;
    eur_foil?: string;
  };
}

export interface ScryfallAutocomplete {
  data: string[];
}

export interface ScryfallSearchResult {
  data: ScryfallCard[];
  has_more: boolean;
  total_cards: number;
}

// === Paged ===
export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages?: number;
}

// === Events ===
export interface CardEvent {
  id: number;
  title: string;
  description?: string;
  type: string;
  status: string;
  startDate: string;
  endDate?: string;
  address: string;
  city: string;
  province?: string;
  country: string;
  latitude?: number;
  longitude?: number;
  maxParticipants?: number;
  isPublic: boolean;
  imageUrl?: string;
  participantCount: number;
  isParticipating: boolean;
  organizer: {
    id: number;
    username: string;
    avatarUrl?: string;
    reputationScore: number;
  };
  createdAt: string;
}
