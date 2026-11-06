import i18n from '../i18n';
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
  /** Data di nascita aaaa-mm-gg (età minima 14 anni). */
  birthDate: string;
  /** Codice invito facoltativo. */
  referralCode?: string;
  /** Accettazione di termini e presa visione dell'informativa (obbligatoria) */
  acceptTerms: boolean;
  /** Negozio dal cui QR arriva la registrazione */
  storeSlug?: string;
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
export type PlanBadge = 'Premium' | 'Store';

export interface User {
  planBadge?: PlanBadge;
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
  /** Account creato prima del controllo dell'età: va chiesta la data di nascita. */
  birthDateRequired?: boolean;
  /** Meno di 18 anni. */
  isMinor?: boolean;
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


// Etichette tradotte: oggetti con getter, si leggono come prima (CONDITION_LABELS[2]) ma rispettano la lingua corrente
function translatedLabels<K extends string | number>(group: string, keys: readonly K[]): Record<K, string> {
  const out = {} as Record<K, string>;
  for (const k of keys) Object.defineProperty(out, k, { enumerable: true, get: () => i18n.t(`labels:${group}.${k}`) });
  return out;
}

export const CONDITION_LABELS = translatedLabels<CardCondition>('condition', [1, 2, 3, 4, 5, 6, 7, 8]);

// Lingue di stampa (stessi codici del backend / Scryfall). Vuoto = non specificata.
export const CARD_LANGUAGES: { code: string; label: string; short: string }[] = [
  { code: 'en', short: 'EN', get label() { return i18n.t('labels:cardLanguage.en'); } },
  { code: 'it', short: 'IT', get label() { return i18n.t('labels:cardLanguage.it'); } },
  { code: 'fr', short: 'FR', get label() { return i18n.t('labels:cardLanguage.fr'); } },
  { code: 'de', short: 'DE', get label() { return i18n.t('labels:cardLanguage.de'); } },
  { code: 'es', short: 'ES', get label() { return i18n.t('labels:cardLanguage.es'); } },
  { code: 'pt', short: 'PT', get label() { return i18n.t('labels:cardLanguage.pt'); } },
  { code: 'ja', short: 'JP', get label() { return i18n.t('labels:cardLanguage.ja'); } },
  { code: 'ko', short: 'KO', get label() { return i18n.t('labels:cardLanguage.ko'); } },
  { code: 'zhs', short: 'CS', get label() { return i18n.t('labels:cardLanguage.zhs'); } },
  { code: 'zht', short: 'CT', get label() { return i18n.t('labels:cardLanguage.zht'); } },
  { code: 'ru', short: 'RU', get label() { return i18n.t('labels:cardLanguage.ru'); } },
];

export const languageLabel = (code?: string | null) =>
  CARD_LANGUAGES.find((l) => l.code === code)?.label;
export const languageShort = (code?: string | null) =>
  CARD_LANGUAGES.find((l) => l.code === code)?.short;

// Finitura: 1=Normale, 2=Foil, 3=Reverse Holo, 4=Etched. Il backend restituisce il nome ("Foil").
export type CardFinish = 1 | 2 | 3 | 4;
export const FINISH_LABELS = translatedLabels<CardFinish>('finish', [1, 2, 3, 4]);
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
  /** Valore di mercato della copia (riferimento). */
  marketValue?: number;
  /** Prezzo indicato dal proprietario: cifra a cui la cederebbe, indicativa e trattabile. */
  askingPrice?: number;
  /** Prezzo mostrato: indicato dal proprietario, altrimenti di mercato. */
  price?: number;
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
  askingPrice?: number;
  /** Solo aggiornamento: rimuove il prezzo indicato. */
  clearAskingPrice?: boolean;
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
  /** Quota indicativa, pagata di persona all'evento. */
  entryFeeEur?: number | null;
  registrationDeadline?: string | null;
  format?: string | null;
  gameId?: number | null;
  registrationOpen?: boolean;
  /** Posti rimasti (assente se senza limite). */
  spotsLeft?: number | null;
  waitlistCount?: number;
  myStatus?: 'Registered' | 'Confirmed' | 'Attended' | 'Waitlisted' | 'Offered' | null;
  myWaitlistPosition?: number | null;
  myOfferExpiresAt?: string | null;
}

export interface EventAttendee {
  userId: number;
  username: string;
  status: string;
  joinedAt: string;
  checkedInAt?: string | null;
  waitlistPosition?: number | null;
}

export interface CardEventDetail extends CardEvent {
  participants: { userId: number; username: string; avatarUrl?: string; status: string; joinedAt: string }[];
}

export const EVENT_TYPE_LABELS: Record<string, string> = translatedLabels('eventType', ['Trademeetup', 'Tournament', 'CardShow', 'DraftEvent', 'SealedEvent', 'CasualPlay', 'Other']);
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
  /** 'Premium' o 'Store' se l'utente ha un piano con badge. */
  planBadge?: PlanBadge;
  theyHaveIWant: MatchCard[];
  iHaveTheyWant: MatchCard[];
}

export interface MatchmakingResponse {
  hasMore?: boolean;
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
    catalogKey?: string;
    externalId?: string;
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
  scryfallId?: string;
  catalogKey?: string;
  externalId?: string;
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
  /** Valore di mercato unitario. */
  unitValue?: number | null;
  askingPrice?: number | null;
  /** Prezzo indicato o, in mancanza, di mercato. */
  unitPrice?: number | null;
}

export interface TradeEvaluation {
  /** Totali a valore di mercato. */
  offeredTotal: number;
  requestedTotal: number;
  /** Totali ai prezzi indicati dai proprietari (o di mercato se non indicati). */
  offeredPriceTotal: number;
  requestedPriceTotal: number;
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
export const REPORT_REASONS: { value: number; label: string }[] = [1, 2, 3, 4, 5, 6].map((value) => ({
  value,
  get label() { return i18n.t(`labels:reportReasonLong.${value}`); },
}));

export const REPORT_REASON_LABELS: Record<string, string> = translatedLabels('reportReason', ['Scam', 'NoShow', 'Harassment', 'CardsNotAsDescribed', 'Spam', 'Other']);

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
  storeId?: number | null;
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
  /** Il proprietario ha il piano Negozio attivo. */
  isFeatured?: boolean;
  /** Identificativo per link e QR (/s/{slug}) */
  slug?: string;
}

export type StoreInput = Omit<Store, 'id' | 'status' | 'reviewNote' | 'isOwner' | 'distanceKm' | 'upcomingEventsCount' | 'isFeatured' | 'slug'>;

// === Serata di scambio ===
export interface LiveMatchCard {
  cardId: number;
  name: string;
  condition: string;
  language?: string;
  finish: string;
  priceEur?: number;
  askingPrice?: number;
}

export interface EventLive {
  isOpen: boolean;
  checkedIn: boolean;
  opensAt: string;
  closesAt: string;
  presentCount: number;
  attendees: {
    userId: number;
    username: string;
    reputationScore: number;
    checkedInAt: string;
    isMutual: boolean;
    score: number;
    theyHave: LiveMatchCard[];
    theyHaveCount: number;
    iHave: LiveMatchCard[];
    iHaveCount: number;
  }[];
}


// === Abbonamenti alla piattaforma (Premium, Negozio) ===
export type SubscriptionTier = 'Free' | 'Premium' | 'Store' | 'Seller' | 'StorePro';
export const isStoreTier = (t?: string | null) => t === 'Store' || t === 'StorePro';

export interface SubscriptionPlan {
  id: number;
  name: string;
  description?: string;
  tier: SubscriptionTier;
  cycle: 'Monthly' | 'Annual';
  price: number;
  currency: string;
  canSearchAdvanced: boolean;
  canSearchGeographic: boolean;
  canExportCollection: boolean;
  canViewStatistics: boolean;
  canSaveSearches: boolean;
  hasPriorityListing: boolean;
  hasVerifiedBadge: boolean;
  hasWishlistAlerts: boolean;
  sortOrder?: number;
  maxRadiusKm?: number;
  maxBroadcastsPerDay?: number;
  maxActiveReservations?: number;
  monthlyPromotionCredits?: number;
  maxWishlistItems?: number;
  /** Nomi delle funzioni incluse (Capability lato server). */
  capabilities?: string[];
}

/** Diritti effettivi dell'utente (GET /api/me/entitlements). */
export interface Entitlements {
  tier: SubscriptionTier;
  planName: string;
  isPaid: boolean;
  capabilities: string[];
  /** null = illimitato */
  limits: Record<string, number | null>;
}

export interface UserSubscription {
  id: number;
  planName: string;
  tier: SubscriptionTier;
  status: 'Active' | 'Expired' | 'Cancelled' | 'PendingPayment';
  startDate: string;
  endDate: string;
  autoRenew: boolean;
  amountPaid: number;
  currency: string;
  paymentMethod: 'Stripe' | 'BankTransfer' | 'Manual' | 'Simulated';
  paymentReference?: string;
  createdAt: string;
  plan?: SubscriptionPlan;
}

export interface SubscriptionState {
  tier: SubscriptionTier;
  active?: UserSubscription;
  queued: UserSubscription[];
  pending: UserSubscription[];
}

export interface BankDetails {
  accountHolder: string;
  iban: string;
  bic?: string;
  bankName?: string;
}

export interface BillingOptions {
  cardEnabled: boolean;
  bankTransfer?: BankDetails & { enabled: boolean };
}

export interface BankTransferInstructions extends BankDetails {
  request: UserSubscription;
  amount: number;
  currency: string;
  reference: string;
  message: string;
}

export interface AdminSubscriptionRow {
  id: number;
  userId: number;
  username: string;
  email: string;
  planId: number;
  planName: string;
  tier: SubscriptionTier;
  price: number;
  status: UserSubscription['status'];
  paymentMethod: UserSubscription['paymentMethod'];
  paymentReference?: string;
  amountPaid: number;
  currency: string;
  startDate: string;
  endDate: string;
  autoRenew: boolean;
  adminNote?: string;
  createdAt: string;
}

// === Statistiche della collezione ===
export interface BasicStats {
  totalCards: number;
  uniqueCards: number;
  availableForTrade: number;
  marketValue: number;
  wishlistItems: number;
  pendingTrades: number;
}

export interface StatsCardLine {
  cardId: number;
  cardInfoId: number;
  name: string;
  setName?: string;
  imageUrl?: string;
  finish: string;
  condition?: string;
  quantity: number;
  unitValue: number;
  totalValue?: number;
  unitChange?: number;
  totalChange?: number;
  percent?: number;
}

export interface ValueChange { amount: number; percent?: number | null }

export interface CollectionInsights {
  totals: {
    totalCards: number;
    uniqueCards: number;
    marketValue: number;
    availableForTrade: number;
    tradeableValue: number;
    unpricedCards: number;
  };
  change7d: ValueChange;
  change30d: ValueChange;
  series: { date: string; value: number }[];
  topCards: StatsCardLine[];
  gainers: StatsCardLine[];
  losers: StatsCardLine[];
  byGame: { game: string; count: number; value: number }[];
  byCondition: { condition: string; count: number; value: number }[];
  trading: { completedTrades: number };
}

// === Ricerca avanzata ===
export interface AdvancedSearchParams {
  searchTerm?: string;
  gameId?: number;
  rarity?: string;
  language?: string;
  finish?: CardFinish;
  /** Condizione peggiore accettata (1 = Mint … 8 = Damaged). */
  maxCondition?: number;
  minPrice?: number;
  maxPrice?: number;
  radiusKm?: number;
  sortBy?: 'distance' | 'price' | 'newest';
  page?: number;
  pageSize?: number;
}

export interface AdvancedSearchCard {
  id: number;
  cardInfoId: number;
  cardName: string;
  cardNumber?: string;
  cardSet: string;
  cardSetCode?: string;
  game: string;
  rarity?: string;
  condition: string;
  language?: string;
  finish: string;
  quantity: number;
  price?: number;
  marketValue?: number;
  askingPrice?: number;
  imageUrl?: string;
  distanceKm?: number;
  owner: { id: number; username: string; reputationScore: number; totalTradesCompleted: number; planBadge?: PlanBadge };
  location?: { city: string; province?: string };
}

export interface AdvancedSearchResponse {
  totalCount: number;
  truncated: boolean;
  page: number;
  pageSize: number;
  hasLocation: boolean;
  sortBy: string;
  cards: AdvancedSearchCard[];
}

/** Valori di un negozio in un periodo (scambi e presenze portati da CardExchange). */
export interface StoreStats {
  peopleBroughtIn: number;
  profileViews: number;
  qrScans: number;
  qrSignups: number;
  newFollowers: number;
  meetupsScheduled: number;
  tradesCompletedAtStore: number;
  eventRegistrations: number;
  eventCheckIns: number;
}

export interface DemandCard {
  cardInfoId: number;
  name: string;
  setName: string;
  imageUrl?: string | null;
  users: number;
  avgMaxPriceEur?: number | null;
  marketPriceEur?: number | null;
  copiesInStock: number;
}

export interface StoreDemand {
  computedAt: string;
  radiusKm: number;
  activeUsersInRadius: number;
  interestedUsers: number;
  inStock: DemandCard[];
  missing: DemandCard[];
}

export interface StoreReports {
  store: { id: number; name: string };
  current: { month: string; stats: StoreStats };
  months: { month: string; stats: StoreStats | null; previous: StoreStats | null }[];
}

// === Prenota e ritira ===
export interface StoreCardItem {
  id: number;
  cardInfoId: number;
  name: string;
  setName?: string | null;
  imageUrl?: string | null;
  condition: string;
  finish: string;
  language?: string | null;
  available: number;
  priceEur?: number | null;
}

export interface StoreCardsPage {
  total: number;
  page: number;
  pageSize: number;
  acceptsReservations: boolean;
  items: StoreCardItem[];
}

export type ReservationStatus = 'Pending' | 'Confirmed' | 'PickedUp' | 'Cancelled' | 'NoShow' | 'Expired';

export interface Reservation {
  id: number;
  storeId: number;
  storeName: string;
  storeAddress: string;
  storeCity: string;
  cardId: number;
  cardName: string;
  setName?: string | null;
  imageUrl?: string | null;
  condition: string;
  language?: string | null;
  finish: string;
  quantity: number;
  priceEur?: number | null;
  status: ReservationStatus;
  expiresAt: string;
  createdAt: string;
  confirmedAt?: string | null;
  closedAt?: string | null;
  /** Solo per chi ha prenotato. */
  pickupCode?: string | null;
  /** Solo per il negozio. */
  username?: string | null;
}

const CONDITION_BY_NAME: Record<string, CardCondition> = {
  Mint: 1, NearMint: 2, Excellent: 3, Good: 4, LightlyPlayed: 5, ModeratelyPlayed: 6, HeavilyPlayed: 7, Damaged: 8,
} as Record<string, CardCondition>;

/** Etichetta tradotta di una condizione restituita per nome dal server ("NearMint" → "Near Mint"/"Quasi perfetta"). */
export const conditionLabel = (name: string) => (CONDITION_BY_NAME[name] ? CONDITION_LABELS[CONDITION_BY_NAME[name]] : name);

// === Lista acquisti dei negozi ===
export interface BuyOfferForStore {
  id: number;
  userId: number;
  username: string;
  quantity: number;
  status: 'Proposed' | 'Accepted' | 'Rejected' | 'Completed' | 'Expired' | 'Withdrawn';
  condition: CardCondition;
  language?: string | null;
  finish: CardFinish;
  dropOffBy?: string | null;
  createdAt: string;
}

export interface StoreBuyOrder {
  id: number;
  cardInfoId: number;
  cardName: string;
  setName?: string | null;
  imageUrl?: string | null;
  minCondition: CardCondition;
  finish?: CardFinish | null;
  language?: string | null;
  priceCashEur?: number | null;
  priceCreditEur?: number | null;
  quantityWanted: number;
  quantityFilled: number;
  quantityPending: number;
  expiresAt?: string | null;
  isActive: boolean;
  offers: BuyOfferForStore[];
}

export interface BuyOrderInput {
  cardInfoId: number;
  minCondition: number;
  finish?: number | null;
  language?: string | null;
  priceCashEur?: number | null;
  priceCreditEur?: number | null;
  quantityWanted: number;
  expiresAt?: string | null;
  isActive?: boolean;
}

export interface BuyOrderForMe {
  orderId: number;
  storeId: number;
  storeName: string;
  storeCity: string;
  storeOwnerId: number;
  distanceKm: number;
  cardInfoId: number;
  cardName: string;
  setName?: string | null;
  imageUrl?: string | null;
  minCondition: CardCondition;
  finish?: CardFinish | null;
  language?: string | null;
  priceCashEur?: number | null;
  priceCreditEur?: number | null;
  remaining: number;
  myCards: { id: number; condition: CardCondition; language?: string | null; finish: CardFinish; available: number }[];
  myOffer?: { id: number; quantity: number; status: BuyOfferForStore['status']; dropOffBy?: string | null; storeNote?: string | null } | null;
}
