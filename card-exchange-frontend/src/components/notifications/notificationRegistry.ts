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
  type LucideIcon,
} from 'lucide-react';

export interface NotificationTypeConfig {
  icon: LucideIcon;
  color: string;
  label: string;
  category: string;
}

export const NOTIFICATION_CATEGORIES: Record<string, string> = {
  trade: 'Scambi',
  collection: 'Collezione',
  social: 'Social',
  system: 'Sistema',
};

export const notificationRegistry: Record<string, NotificationTypeConfig> = {
  // === Scambi ===
  TradeOfferReceived: {
    icon: ArrowLeftRight,
    color: 'text-blue-500 bg-blue-50',
    label: 'Offerta di scambio ricevuta',
    category: 'trade',
  },
  TradeOfferAccepted: {
    icon: Check,
    color: 'text-green-500 bg-green-50',
    label: 'Offerta accettata',
    category: 'trade',
  },
  TradeOfferRejected: {
    icon: X,
    color: 'text-red-500 bg-red-50',
    label: 'Offerta rifiutata',
    category: 'trade',
  },
  TradeCompleted: {
    icon: CheckCheck,
    color: 'text-emerald-500 bg-emerald-50',
    label: 'Scambio completato',
    category: 'trade',
  },
  CounterOfferReceived: {
    icon: ArrowLeftRight,
    color: 'text-purple-500 bg-purple-50',
    label: 'Controproposta ricevuta',
    category: 'trade',
  },

  // === Collezione ===
  FavoritePriceChanged: {
    icon: Heart,
    color: 'text-amber-500 bg-amber-50',
    label: 'Prezzo preferito aggiornato',
    category: 'collection',
  },
  FavoriteCardTraded: {
    icon: Heart,
    color: 'text-red-500 bg-red-50',
    label: 'Carta preferita scambiata',
    category: 'collection',
  },
  WishlistMatch: {
    icon: Heart,
    color: 'text-pink-500 bg-pink-50',
    label: 'Carta trovata in wishlist',
    category: 'collection',
  },

  // === Social ===
  NewMessage: {
    icon: MessageSquare,
    color: 'text-indigo-500 bg-indigo-50',
    label: 'Nuovo messaggio',
    category: 'social',
  },
  NewReview: {
    icon: Check,
    color: 'text-teal-500 bg-teal-50',
    label: 'Nuova recensione',
    category: 'social',
  },

  // === Prezzi ===
  PriceSpike: {
    icon: TrendingUp,
    color: 'text-amber-500 bg-amber-50',
    label: 'Carte in aumento di prezzo',
    category: 'collection',
  },

  // === Sistema ===
  SubscriptionExpiring: {
    icon: AlertTriangle,
    color: 'text-orange-500 bg-orange-50',
    label: 'Abbonamento in scadenza',
    category: 'system',
  },
  SubscriptionExpired: {
    icon: AlertTriangle,
    color: 'text-red-500 bg-red-50',
    label: 'Abbonamento scaduto',
    category: 'system',
  },
  SystemAnnouncement: {
    icon: AlertTriangle,
    color: 'text-gray-500 bg-gray-50',
    label: 'Annuncio di sistema',
    category: 'system',
  },
};

const defaultConfig: NotificationTypeConfig = {
  icon: Bell,
  color: 'text-gray-500 bg-gray-50',
  label: 'Notifica',
  category: 'system',
};

export function getNotificationConfig(type: string): NotificationTypeConfig {
  return notificationRegistry[type] ?? defaultConfig;
}
