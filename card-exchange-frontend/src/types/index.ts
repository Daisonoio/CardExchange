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
  emailConfirmed?: boolean;
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

// Lingue di stampa (stessi codici del backend / Scryfall). Vuoto = non specificata.
export const CARD_LANGUAGES: { code: string; label: string; short: string }[] = [
  { code: 'en', label: 'Inglese', short: 'EN' },
  { code: 'it', label: 'Italiano', short: 'IT' },
  { code: 'fr', label: 'Francese', short: 'FR' },
  { code: 'de', label: 'Tedesco', short: 'DE' },
  { code: 'es', label: 'Spagnolo', short: 'ES' },
  { code: 'pt', label: 'Portoghese', short: 'PT' },
  { code: 'ja', label: 'Giapponese', short: 'JP' },
  { code: 'ko', label: 'Coreano', short: 'KO' },
  { code: 'zhs', label: 'Cinese semplificato', short: 'CS' },
  { code: 'zht', label: 'Cinese tradizionale', short: 'CT' },
  { code: 'ru', label: 'Russo', short: 'RU' },
];

export const languageLabel = (code?: string | null) =>
  CARD_LANGUAGES.find((l) => l.code === code)?.label;
export const languageShort = (code?: string | null) =>
  CARD_LANGUAGES.find((l) => l.code === code)?.short;

// Finitura: 1=Normale, 2=Foil, 3=Reverse Holo, 4=Etched. Il backend restituisce il nome ("Foil").
export type CardFinish = 1 | 2 | 3 | 4;
export const FINISH_LABELS: Record<CardFinish, string> = {
  1: 'Normale',
  2: 'Foil',
  3: 'Reverse Holo',
  4: 'Etched',
};
const FINISH_BY_NAME: Record<string, CardFinish> = { Normal: 1, Foil: 2, ReverseHolo: 3, Etched: 4 };
export const finishValue = (finish?: CardFinish | string | null): CardFinish | undefined =>
  finish == null ? undefined : typeof finish === 'number' ? finish : FINISH_BY_NAME[finish];

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
  language?: string | null;
  finish?: CardFinish | string;
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
  /** Codice lingua; nell'aggiornamento '' rimuove la lingua. */
  language?: string;
  finish?: CardFinish;
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
  preferredLanguage?: string | null;
  preferredFinish?: CardFinish | string | null;
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
  /** Nell'aggiornamento 0 = qualsiasi condizione. */
  preferredCondition?: CardCondition | 0;
  /** Codice lingua; nell'aggiornamento '' = qualsiasi. */
  preferredLanguage?: string;
  /** Nell'aggiornamento 0 = qualsiasi finitura. */
  preferredFinish?: CardFinish | 0;
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
  suggestions?: string[];
}

export interface ScryfallSearchResult {
  data: ScryfallCard[];
  has_more: boolean;
  total_cards: number;
}

// === Generic card search result (normalized across all games) ===
export interface GameCard {
  /** The external API identifier used for import */
  externalId: string;
  name: string;
  setName?: string;
  rarity?: string;
  imageSmall?: string;
  imageLarge?: string;
  priceEur?: string;
  /** Extra info line (type, HP, etc.) */
  subtitle?: string;
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
  supportedGameIds?: string;
  storeId?: number;
  storeName?: string;
  organizer: {
    id: number;
    username: string;
    avatarUrl?: string;
    reputationScore: number;
  };
  createdAt: string;
}

export interface CardEventDetail extends CardEvent {
  participants: { userId: number; username: string; avatarUrl?: string; status: string; joinedAt: string }[];
}

export const EVENT_TYPE_LABELS: Record<string, string> = {
  Trademeetup: 'Serata di scambio',
  Tournament: 'Torneo',
  CardShow: 'Fiera',
  DraftEvent: 'Draft',
  SealedEvent: 'Sealed',
  CasualPlay: 'Gioco libero',
  Other: 'Altro',
};
export const EVENT_TYPE_VALUES: Record<string, number> = {
  Trademeetup: 1, Tournament: 2, CardShow: 3, DraftEvent: 4, SealedEvent: 5, CasualPlay: 6, Other: 99,
};

// === Trade Offers ===
export type TradeOfferStatus = 'Pending' | 'Accepted' | 'Rejected' | 'Cancelled' | 'Completed' | 'CounterOffer' | 'Expired';

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
  /** Utenti che hanno già recensito questo scambio. */
  reviewerIds?: number[];
  senderConfirmedAt?: string;
  receiverConfirmedAt?: string;
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

// === Chat / Messaging ===
export interface ConversationPreview {
  conversationId: number;
  otherUser: { id: number; username: string; avatarUrl?: string };
  lastMessage?: { content: string; sentAt: string; isFromMe: boolean };
  unreadCount: number;
  tradeOfferId?: number;
}

export interface ChatMessage {
  id: number;
  senderId: number;
  senderUsername: string;
  content: string;
  isRead: boolean;
  sentAt: string;
  isFromMe: boolean;
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

// === Matchmaking ===
export interface MatchCard {
  cardId: number;
  cardInfoId: number;
  name: string;
  setName?: string;
  imageSmall?: string;
  condition: string;
  quantity: number;
  priceEur?: number;
  wishlistPriority: number;
}

export interface MatchResult {
  userId: number;
  username: string;
  avatarUrl?: string;
  reputationScore: number;
  totalTradesCompleted: number;
  city?: string;
  province?: string;
  distanceKm?: number;
  score: number;
  mutualCardCount: number;
  isMutual: boolean;
  theyHaveIWant: MatchCard[];
  iHaveTheyWant: MatchCard[];
}

export interface MatchmakingResponse {
  totalMatches: number;
  mutualMatches: number;
  matches: MatchResult[];
}

// === Price Tracking ===
export interface PricePoint {
  date: string;
  priceUsd?: number;
  priceUsdFoil?: number;
  priceEur?: number;
  priceEurFoil?: number;
}

export interface PriceHistoryData {
  cardInfoId: number;
  cardName: string;
  setName?: string;
  dataPoints: PricePoint[];
}

export interface PriceDayPoint {
  date: string;
  priceEur?: number;
}

export interface PriceSpike {
  cardId: number;
  cardInfoId: number;
  name: string;
  setName?: string;
  imageSmall?: string;
  currentPriceEur: number;
  oldPriceEur: number;
  changePercentage: number;
  changeAmount: number;
  last5Days: PriceDayPoint[];
}

export interface SpikeSettings {
  thresholdPercentage: number;
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

// === Import collezione ===
export interface ImportPreviewRow {
  lineNumber: number;
  inputName?: string;
  inputSet?: string;
  inputNumber?: string;
  quantity: number;
  condition: CardCondition;
  language?: string;
  finish: CardFinish;
  status: 'ready' | 'error';
  error?: string;
  warnings: string[];
  match?: {
    scryfallId: string;
    name: string;
    setCode: string;
    setName: string;
    collectorNumber?: string;
    imageSmall?: string;
    priceEur?: number;
  };
}

export interface ImportPreview {
  detectedFormat: string;
  mapping: Record<string, string>;
  ignoredColumns: string[];
  totalRows: number;
  readyCount: number;
  errorCount: number;
  totalQuantity: number;
  rows: ImportPreviewRow[];
}

export interface ImportCommitRow {
  scryfallId: string;
  quantity: number;
  condition: CardCondition;
  language?: string;
  finish: CardFinish;
}

export interface ImportCommitResult {
  createdCopies: number;
  mergedIntoExisting: number;
  totalQuantity: number;
  skippedByPlanLimit: number;
  planCardLimit?: number;
  failed: string[];
}

// === Equilibrio scambio ===
export interface TradeItemValue {
  cardId: number;
  quantity: number;
  unitValue?: number | null;
}

export interface TradeEvaluation {
  offeredTotal: number;
  requestedTotal: number;
  /** Offerto meno richiesto (positivo = dai di più). */
  difference: number;
  tolerance: number;
  verdict: 'fair' | 'favorsReceiver' | 'favorsProposer';
  missingPriceCount: number;
  items: TradeItemValue[];
}

export interface TradeSuggestion {
  offeredCards: TradeItemValue[];
  evaluation: TradeEvaluation;
  candidatesCount: number;
  message?: string;
}

// === Recensioni ===
export interface TradeReview {
  id: number;
  tradeOfferId: number;
  reviewerId: number;
  reviewerUsername: string;
  reviewedUserId: number;
  rating: number;
  comment?: string;
  cardAsDescribed: boolean;
  timelyShipping: boolean;
  goodCommunication: boolean;
  createdAt: string;
}

export interface UserReviewsPage {
  userId: number;
  username: string;
  reputationScore: number;
  totalReviews: number;
  reviews: { items: TradeReview[]; totalCount: number; page: number; pageSize: number };
}

// === Segnalazioni ===
export const REPORT_REASONS: { value: number; label: string }[] = [
  { value: 1, label: 'Truffa o tentata truffa' },
  { value: 2, label: "Non si è presentato all'appuntamento" },
  { value: 3, label: 'Comportamento offensivo o molesto' },
  { value: 4, label: 'Carte false o non come descritte' },
  { value: 5, label: 'Spam' },
  { value: 6, label: 'Altro' },
];

export const REPORT_REASON_LABELS: Record<string, string> = {
  Scam: 'Truffa', NoShow: 'Non presentato', Harassment: 'Comportamento offensivo',
  CardsNotAsDescribed: 'Carte non conformi', Spam: 'Spam', Other: 'Altro',
};

export interface ModerationReport {
  id: number;
  reason: string;
  details?: string;
  status: 'Open' | 'Resolved' | 'Dismissed';
  tradeOfferId?: number;
  createdAt: string;
  resolvedAt?: string;
  resolutionNote?: string;
  userSuspended: boolean;
  reporter: { id: number; username: string };
  reportedUser: { id: number; username: string; isActive: boolean; reputationScore: number; totalTradesCompleted: number; openReports: number };
}

// === Appuntamenti ===
export interface TradeMeetup {
  id: number;
  tradeOfferId: number;
  proposedById: number;
  proposedByUsername: string;
  /** ISO UTC */
  scheduledAt: string;
  locationName: string;
  address?: string;
  latitude?: number;
  longitude?: number;
  notes?: string;
  status: 'Proposed' | 'Accepted' | 'Declined' | 'Cancelled' | 'Superseded';
  createdAt: string;
  respondedAt?: string;
}

// === Negozi ===
export interface Store {
  id: number;
  name: string;
  description?: string;
  address: string;
  city: string;
  province?: string;
  postalCode?: string;
  country: string;
  latitude: number;
  longitude: number;
  phone?: string;
  website?: string;
  instagram?: string;
  openingHours?: string;
  gameIds: number[];
  isSafeMeetupSpot: boolean;
  status: 'Pending' | 'Verified' | 'Rejected' | 'Suspended';
  reviewNote?: string;
  isOwner: boolean;
  distanceKm?: number;
  upcomingEventsCount: number;
}

export type StoreInput = Omit<Store, 'id' | 'status' | 'reviewNote' | 'isOwner' | 'distanceKm' | 'upcomingEventsCount'>;

