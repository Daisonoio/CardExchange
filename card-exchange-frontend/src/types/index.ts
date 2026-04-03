// === Auth ===
export interface LoginRequest {
  usernameOrEmail: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  username: string;
  firstName: string;
  lastName: string;
  password: string;
  confirmPassword: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiration: string;
  refreshTokenExpiration: string;
  user: User;
  message?: string;
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
  paypalUsername?: string;
  satispayUsername?: string;
  paymentQrCodeUrl?: string;
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
  userUsername?: string;
  cardInfoId: number;
  // Flat fields from backend CardDto
  cardName?: string;
  cardSetName?: string;
  gameName?: string;
  cardNumber?: string;
  rarity?: string;
  condition: CardCondition | string;
  quantity: number;
  notes?: string;
  isAvailableForTrade: boolean;
  estimatedValue?: number;
  imageSmall?: string;
  imageNormal?: string;
  imageLarge?: string;
  createdAt?: string;
  userLocation?: UserLocation;
  hasUserPhotos?: boolean;
  userPhotoCount?: number;
  // Legacy nested field (not returned by backend, but kept for compatibility)
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
  cardName?: string;
  cardSetName?: string;
  cardSetCode?: string;
  gameName?: string;
  cardNumber?: string;
  rarity?: string;
  preferredCondition?: CardCondition | string;
  maxPrice?: number;
  priority: number;
  priorityLabel?: string;
  notes?: string;
  imageSmall?: string;
  imageNormal?: string;
  createdAt?: string;
  availableMatches?: number;
  // Legacy/nested fields
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
// Supports both direct Scryfall format (snake_case) and backend proxy format (camelCase)
export interface ScryfallCard {
  id: string;
  name: string;
  set?: string;
  set_name?: string;
  setName?: string;
  setCode?: string;
  collector_number?: string;
  collectorNumber?: string;
  rarity: string;
  mana_cost?: string;
  manaCost?: string;
  cmc?: number;
  type_line?: string;
  typeLine?: string;
  oracle_text?: string;
  oracleText?: string;
  colors?: string[];
  power?: string;
  toughness?: string;
  artist?: string;
  image_uris?: {
    small: string;
    normal: string;
    large: string;
    art_crop?: string;
  };
  images?: {
    small: string;
    normal: string;
    large: string;
    artCrop?: string;
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
    eurFoil?: string;
    usdFoil?: string;
  };
  scryfallId?: string;
  scryfallUri?: string;
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

// === Trade Offers ===
export type TradeOfferStatus = 'Pending' | 'Accepted' | 'Rejected' | 'Cancelled' | 'Completed' | 'CounterOffer';

export interface TradeOfferItem {
  id: number;
  cardId: number;
  cardName: string;
  cardSetName: string;
  condition: string;
  quantity: number;
  ownerUsername: string;
  imageSmall?: string;
  imageNormal?: string;
}

export interface TradeOffer {
  id: number;
  senderId: number;
  senderUsername: string;
  receiverId: number;
  receiverUsername: string;
  status: TradeOfferStatus;
  message?: string;
  createdAt: string;
  responseDate?: string;
  completedDate?: string;
  expiresAt?: string;
  parentOfferId?: number;
  offeredCards: TradeOfferItem[];
  requestedCards: TradeOfferItem[];
}

export interface CreateTradeOfferRequest {
  receiverId: number;
  message?: string;
  offeredCards: { cardId: number; quantity: number }[];
  requestedCards: { cardId: number; quantity: number }[];
}

// === Notifications ===
export interface Notification {
  id: number;
  type: string;
  title: string;
  body?: string;
  isRead: boolean;
  readAt?: string;
  referenceId?: number;
  referenceType?: string;
  createdAt: string;
}

export interface NotificationPreference {
  type: string;
  typeId: number;
  isEnabled: boolean;
}

// === Favorites ===
export interface FavoriteCard {
  favoriteId: number;
  cardId: number;
  createdAt: string;
  card: Card;
}

export interface TradeReview {
  id: number;
  tradeOfferId: number;
  reviewerId: number;
  reviewerUsername: string;
  reviewedUserId: number;
  reviewedUserUsername: string;
  rating: number;
  comment?: string;
  cardAsDescribed: boolean;
  timelyShipping: boolean;
  goodCommunication: boolean;
  createdAt: string;
}
