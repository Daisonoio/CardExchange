import { useState, useRef, useEffect, useCallback } from 'react';
import { Search, X, Loader2 } from 'lucide-react';
import { scryfall, pokemontcg, yugioh, onepiece } from '../../api';
import { useGame } from '../../context/GameContext';
import type { GameCard } from '../../types';

interface GameCardSearchProps {
  onSelect: (card: GameCard) => void;
  placeholder?: string;
}

// Normalize Scryfall (Magic) results into GameCard
function normalizeScryfallCards(cards: any[]): GameCard[] {
  return cards.map((c) => ({
    externalId: c.scryfallId || c.id,
    name: c.name,
    setName: c.set_name || c.setName,
    rarity: c.rarity,
    imageSmall: c.image_uris?.small || c.images?.small || c.card_faces?.[0]?.image_uris?.small || '',
    imageLarge: c.image_uris?.normal || c.images?.normal || c.image_uris?.large || c.images?.large || c.card_faces?.[0]?.image_uris?.normal || '',
    priceEur: c.prices?.eur,
    subtitle: c.type_line || c.typeLine,
  }));
}

// Normalize Pokémon TCG results into GameCard
function normalizePokemonCards(cards: any[]): GameCard[] {
  return cards.map((c) => ({
    externalId: c.pokemonTcgId || c.id,
    name: c.name,
    setName: c.setName,
    rarity: c.rarity,
    imageSmall: c.images?.small || '',
    imageLarge: c.images?.large || c.images?.small || '',
    priceEur: c.prices?.eur != null ? String(c.prices.eur) : c.prices?.cardmarketAvg != null ? String(c.prices.cardmarketAvg) : undefined,
    subtitle: [c.supertype, ...(c.subtypes || [])].filter(Boolean).join(' — ') || (c.hp ? `HP ${c.hp}` : undefined),
  }));
}

// Normalize Yu-Gi-Oh! results into GameCard
function normalizeYuGiOhCards(cards: any[]): GameCard[] {
  return cards.map((c) => ({
    externalId: String(c.yugiohId || c.id),
    name: c.name,
    setName: c.setName,
    rarity: c.rarity,
    imageSmall: c.images?.small || c.images?.url || '',
    imageLarge: c.images?.url || c.images?.small || '',
    priceEur: c.prices?.eur != null ? String(c.prices.eur) : c.prices?.cardmarket != null ? String(c.prices.cardmarket) : undefined,
    subtitle: c.type || c.race,
  }));
}

// Normalize One Piece TCG results into GameCard
function normalizeOnePieceCards(cards: any[]): GameCard[] {
  return cards.map((c) => ({
    externalId: c.code || c.id,
    name: c.name,
    setName: c.setName,
    rarity: c.rarity,
    imageSmall: c.images?.small || c.images?.large || '',
    imageLarge: c.images?.large || c.images?.small || '',
    priceEur: undefined,
    subtitle: [c.type, c.color, c.cost != null ? `Cost ${c.cost}` : null].filter(Boolean).join(' — '),
  }));
}

type GameKey = 'magic' | 'pokemon' | 'yugioh' | 'onepiece' | 'generic';

function getGameKey(gameName: string | undefined): GameKey {
  if (!gameName) return 'generic';
  const lower = gameName.toLowerCase();
  if (lower.includes('magic')) return 'magic';
  if (lower.includes('pokémon') || lower.includes('pokemon')) return 'pokemon';
  if (lower.includes('yu-gi-oh') || lower.includes('yugioh')) return 'yugioh';
  if (lower.includes('one piece') || lower.includes('onepiece')) return 'onepiece';
  return 'generic';
}

async function searchCards(gameKey: GameKey, query: string): Promise<GameCard[]> {
  switch (gameKey) {
    case 'pokemon': {
      const { data } = await pokemontcg.search(query);
      return normalizePokemonCards(data.cards ?? []);
    }
    case 'yugioh': {
      const { data } = await yugioh.search(query);
      return normalizeYuGiOhCards(data.cards ?? []);
    }
    case 'onepiece': {
      const { data } = await onepiece.search(query);
      return normalizeOnePieceCards(data.cards ?? []);
    }
    case 'magic':
    case 'generic':
    default: {
      const { data } = await scryfall.search(query);
      return normalizeScryfallCards(data.data ?? data.cards ?? []);
    }
  }
}

async function autocompleteCards(gameKey: GameKey, query: string): Promise<string[]> {
  // Only Scryfall has a dedicated autocomplete endpoint; for other games we skip autocomplete
  if (gameKey === 'magic' || gameKey === 'generic') {
    const { data } = await scryfall.autocomplete(query);
    const list = data.data ?? data.suggestions ?? [];
    return Array.isArray(list) ? list.slice(0, 8) : [];
  }
  return [];
}

export default function GameCardSearch({ onSelect, placeholder = 'Cerca una carta...' }: GameCardSearchProps) {
  const { selectedGame } = useGame();
  const gameKey = getGameKey(selectedGame?.name);

  const [query, setQuery] = useState('');
  const [suggestions, setSuggestions] = useState<string[]>([]);
  const [results, setResults] = useState<GameCard[]>([]);
  const [isOpen, setIsOpen] = useState(false);
  const [isSearching, setIsSearching] = useState(false);
  const [mode, setMode] = useState<'autocomplete' | 'results'>('autocomplete');
  const inputRef = useRef<HTMLInputElement>(null);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const skipAutocompleteOnceRef = useRef(false);

  const fetchAutocomplete = useCallback(async (q: string) => {
    if (q.length < 2) {
      setSuggestions([]);
      return;
    }
    try {
      const list = await autocompleteCards(gameKey, q);
      setSuggestions(list);
      setMode('autocomplete');
      setIsOpen(true);
    } catch {
      setSuggestions([]);
    }
  }, [gameKey]);

  const handleSearch = async (name: string) => {
    skipAutocompleteOnceRef.current = true;
    setQuery(name);
    setIsSearching(true);
    setMode('results');
    setIsOpen(true);
    setSuggestions([]);
    try {
      const cards = await searchCards(gameKey, name);
      setResults(cards);
      setIsOpen(true);
    } catch {
      setResults([]);
    } finally {
      setIsSearching(false);
    }
  };

  const handleSelect = (card: GameCard) => {
    onSelect(card);
    setQuery('');
    setResults([]);
    setSuggestions([]);
    setIsOpen(false);
  };

  // Reset search when game changes
  useEffect(() => {
    setQuery('');
    setResults([]);
    setSuggestions([]);
    setIsOpen(false);
  }, [gameKey]);

  useEffect(() => {
    if (skipAutocompleteOnceRef.current) {
      skipAutocompleteOnceRef.current = false;
      return;
    }
    if (mode === 'results') return;
    if (debounceRef.current) clearTimeout(debounceRef.current);
    // For non-Magic games, trigger direct search instead of autocomplete
    if (gameKey !== 'magic' && gameKey !== 'generic') {
      debounceRef.current = setTimeout(() => {
        if (query.length >= 2) handleSearch(query);
      }, 500);
    } else {
      debounceRef.current = setTimeout(() => fetchAutocomplete(query), 300);
    }
    return () => { if (debounceRef.current) clearTimeout(debounceRef.current); };
  }, [query, fetchAutocomplete, mode, gameKey]);

  // Close dropdown on outside click
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, []);

  return (
    <div className="relative" ref={dropdownRef}>
      <div className="relative">
        <Search size={18} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" />
        <input
          ref={inputRef}
          type="text"
          value={query}
          onChange={(e) => {
            setMode('autocomplete');
            setQuery(e.target.value);
          }}
          onKeyDown={(e) => {
            if (e.key === 'Enter' && query.length >= 2) handleSearch(query);
          }}
          onFocus={() => { if (suggestions.length > 0 || results.length > 0) setIsOpen(true); }}
          placeholder={placeholder}
          className="w-full pl-10 pr-10 py-3 rounded-xl border border-border bg-white text-text placeholder:text-text-muted focus:outline-none focus:ring-2 focus:ring-primary/30 focus:border-primary transition-colors"
        />
        {query && (
          <button
            onClick={() => { setQuery(''); setSuggestions([]); setResults([]); setMode('autocomplete'); setIsOpen(false); }}
            className="absolute right-3 top-1/2 -translate-y-1/2 text-text-muted hover:text-text"
          >
            <X size={18} />
          </button>
        )}
      </div>

      <button
        type="button"
        onClick={() => { if (query.trim().length >= 2) handleSearch(query.trim()); }}
        disabled={isSearching || query.trim().length < 2}
        className="mt-2 w-full py-2.5 rounded-xl text-sm font-semibold bg-primary text-white disabled:opacity-50 disabled:cursor-not-allowed"
      >
        Cerca
      </button>

      {isOpen && (
        <div className="mt-2 bg-white rounded-xl shadow-lg border border-border max-h-96 overflow-y-auto z-50">
          {mode === 'autocomplete' && suggestions.length > 0 && (
            <ul>
              {suggestions.map((name) => (
                <li key={name}>
                  <button
                    onClick={() => handleSearch(name)}
                    className="w-full text-left px-4 py-3 hover:bg-surface-dark transition-colors text-sm border-b border-border/50 last:border-0"
                  >
                    {name}
                  </button>
                </li>
              ))}
            </ul>
          )}

          {mode === 'results' && isSearching && (
            <div className="flex items-center justify-center py-8">
              <Loader2 size={24} className="animate-spin text-primary" />
            </div>
          )}

          {mode === 'results' && !isSearching && results.length === 0 && (
            <div className="py-8 text-center text-sm text-text-muted">
              Nessun risultato trovato
            </div>
          )}

          {mode === 'results' && !isSearching && results.length > 0 && (
            <ul>
              {results.map((card) => (
                <li key={card.externalId}>
                  <button
                    onClick={() => handleSelect(card)}
                    className="w-full text-left px-4 py-3 hover:bg-surface-dark transition-colors flex items-center gap-3 border-b border-border/50 last:border-0"
                  >
                    {card.imageSmall && (
                      <img
                        src={card.imageSmall}
                        alt={card.name}
                        className="w-10 h-14 rounded object-cover shrink-0"
                        loading="lazy"
                      />
                    )}
                    <div className="min-w-0">
                      <p className="text-sm font-medium text-text truncate">{card.name}</p>
                      <p className="text-xs text-text-secondary">
                        {card.setName} &middot; {card.rarity}
                        {card.priceEur && (
                          <span className="ml-2 font-semibold text-accent">
                            {card.priceEur} EUR
                          </span>
                        )}
                      </p>
                      {card.subtitle && (
                        <p className="text-xs text-text-muted truncate">{card.subtitle}</p>
                      )}
                    </div>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  );
}

// Helper: import a card using the correct API for the current game
export async function importGameCard(gameName: string | undefined, externalId: string): Promise<number> {
  const gameKey = getGameKey(gameName);
  switch (gameKey) {
    case 'pokemon': {
      const { data } = await pokemontcg.importCard(externalId);
      return data.cardInfoId;
    }
    case 'yugioh': {
      const { data } = await yugioh.importCard(Number(externalId));
      return data.cardInfoId;
    }
    case 'onepiece': {
      const { data } = await onepiece.importCard(externalId);
      return data.cardInfoId;
    }
    case 'magic':
    case 'generic':
    default: {
      const { data } = await scryfall.importCard(externalId);
      return data.cardInfoId || data.id;
    }
  }
}
