import {
  ArrowLeftRight,
  Check,
  CheckCheck,
  Heart,
  MessageSquare,
  AlertTriangle,
  X,
  Bell,
  TrendingUp,
  CalendarClock,
  Mail,
  MapPin,
  UserPlus,
  Store,
  type LucideIcon,
} from 'lucide-react';
import i18n from '../../i18n';

export interface NotificationTypeConfig {
  icon: LucideIcon;
  color: string;
  label: string;
  category: string;
}

// Le etichette sono getter: vengono tradotte nella lingua corrente a ogni lettura
export const NOTIFICATION_CATEGORIES: Record<string, string> = {
  get trade() { return i18n.t('notifications:categories.trade'); },
  get collection() { return i18n.t('notifications:categories.collection'); },
  get social() { return i18n.t('notifications:categories.social'); },
  get system() { return i18n.t('notifications:categories.system'); },
};

export const notificationRegistry: Record<string, NotificationTypeConfig> = {
  // === Scambi ===
  TradeOfferReceived: {
    icon: ArrowLeftRight,
    color: 'text-blue-500 bg-blue-50',
    get label() { return i18n.t('notifications:types.TradeOfferReceived'); },
    category: 'trade',
  },
  TradeOfferAccepted: {
    icon: Check,
    color: 'text-green-500 bg-green-50',
    get label() { return i18n.t('notifications:types.TradeOfferAccepted'); },
    category: 'trade',
  },
  TradeOfferRejected: {
    icon: X,
    color: 'text-red-500 bg-red-50',
    get label() { return i18n.t('notifications:types.TradeOfferRejected'); },
    category: 'trade',
  },
  TradeCompleted: {
    icon: CheckCheck,
    color: 'text-emerald-500 bg-emerald-50',
    get label() { return i18n.t('notifications:types.TradeCompleted'); },
    category: 'trade',
  },
  // === Seguiti e "Sono qui" ===
  FollowedNearby: {
    icon: MapPin,
    color: 'text-primary bg-primary/10',
    get label() { return i18n.t('notifications:types.FollowedNearby'); },
    category: 'social',
  },
  StoreOffer: {
    icon: Store,
    color: 'text-primary bg-primary/10',
    get label() { return i18n.t('notifications:types.StoreOffer'); },
    category: 'collection',
  },
  EventUpdate: {
    icon: CalendarClock,
    color: 'text-primary bg-primary/10',
    get label() { return i18n.t('notifications:types.EventUpdate'); },
    category: 'social',
  },
  StoreActivity: {
    icon: Store,
    color: 'text-primary bg-primary/10',
    get label() { return i18n.t('notifications:types.StoreActivity'); },
    category: 'social',
  },
  NewFollower: {
    icon: UserPlus,
    color: 'text-primary bg-primary/10',
    get label() { return i18n.t('notifications:types.NewFollower'); },
    category: 'social',
  },
  WeeklyDigest: {
    icon: Mail,
    color: 'text-sky-600 bg-sky-50',
    get label() { return i18n.t('notifications:types.WeeklyDigest'); },
    category: 'system',
  },
  MeetupUpdate: {
    icon: CalendarClock,
    color: 'text-indigo-500 bg-indigo-50',
    get label() { return i18n.t('notifications:types.MeetupUpdate'); },
    category: 'trade',
  },
  CounterOfferReceived: {
    icon: ArrowLeftRight,
    color: 'text-purple-500 bg-purple-50',
    get label() { return i18n.t('notifications:types.CounterOfferReceived'); },
    category: 'trade',
  },

  // === Collezione ===
  FavoritePriceChanged: {
    icon: Heart,
    color: 'text-amber-500 bg-amber-50',
    get label() { return i18n.t('notifications:types.FavoritePriceChanged'); },
    category: 'collection',
  },
  FavoriteCardTraded: {
    icon: Heart,
    color: 'text-red-500 bg-red-50',
    get label() { return i18n.t('notifications:types.FavoriteCardTraded'); },
    category: 'collection',
  },
  WishlistMatch: {
    icon: Heart,
    color: 'text-pink-500 bg-pink-50',
    get label() { return i18n.t('notifications:types.WishlistMatch'); },
    category: 'collection',
  },

  // === Social ===
  NewMessage: {
    icon: MessageSquare,
    color: 'text-indigo-500 bg-indigo-50',
    get label() { return i18n.t('notifications:types.NewMessage'); },
    category: 'social',
  },
  NewReview: {
    icon: Check,
    color: 'text-teal-500 bg-teal-50',
    get label() { return i18n.t('notifications:types.NewReview'); },
    category: 'social',
  },

  // === Prezzi ===
  PriceSpike: {
    icon: TrendingUp,
    color: 'text-amber-500 bg-amber-50',
    get label() { return i18n.t('notifications:types.PriceSpike'); },
    category: 'collection',
  },

  // === Sistema ===
  SubscriptionExpiring: {
    icon: AlertTriangle,
    color: 'text-orange-500 bg-orange-50',
    get label() { return i18n.t('notifications:types.SubscriptionExpiring'); },
    category: 'system',
  },
  SubscriptionExpired: {
    icon: AlertTriangle,
    color: 'text-red-500 bg-red-50',
    get label() { return i18n.t('notifications:types.SubscriptionExpired'); },
    category: 'system',
  },
  SystemAnnouncement: {
    icon: AlertTriangle,
    color: 'text-gray-500 bg-gray-50',
    get label() { return i18n.t('notifications:types.SystemAnnouncement'); },
    category: 'system',
  },
};

const defaultConfig: NotificationTypeConfig = {
  icon: Bell,
  color: 'text-gray-500 bg-gray-50',
  get label() { return i18n.t('notifications:types.default'); },
  category: 'system',
};

export function getNotificationConfig(type: string): NotificationTypeConfig {
  return notificationRegistry[type] ?? defaultConfig;
}
