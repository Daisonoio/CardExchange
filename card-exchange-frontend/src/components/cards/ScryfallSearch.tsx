import { useState, useRef, useEffect, useCallback } from 'react';
import { Search, X, Loader2 } from 'lucide-react';
import { scryfall } from '../../api';
import type { ScryfallCard } from '../../types';

interface ScryfallSearchProps {
  onSelect: (card: ScryfallCard) => void;
  placeholder?: string;
}

export default function ScryfallSearch({ onSelect, placeholder = 'Cerca una carta...' }: ScryfallSearchProps) {
  const [query, setQuery] = useState('');
  const [suggestions, setSuggestions] = useState<string[]>([]);
  const [results, setResults] = useState<ScryfallCard[]>([]);
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
      const { data } = await scryfall.autocomplete(q);
      const list = data.data ?? data.suggestions ?? [];
      setSuggestions(Array.isArray(list) ? list.slice(0, 8) : []);
      setMode('autocomplete');
      setIsOpen(true);
    } catch {
      setSuggestions([]);
    }
  }, []);

  const handleSearch = async (name: string) => {
    skipAutocompleteOnceRef.current = true;
    setQuery(name);
    setIsSearching(true);
    setMode('results');
    setIsOpen(true);
    setSuggestions([]);
    try {
      const { data } = await scryfall.search(name);
      setResults(data.data ?? data.cards ?? []);
      setIsOpen(true);
    } catch {
      setResults([]);
    } finally {
      setIsSearching(false);
    }
  };

  const handleSelect = (card: ScryfallCard) => {
    onSelect(card);
    setQuery('');
    setResults([]);
    setSuggestions([]);
    setIsOpen(false);
  };

  useEffect(() => {
    if (skipAutocompleteOnceRef.current) {
      skipAutocompleteOnceRef.current = false;
      return;
    }
    if (mode === 'results') return;
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => fetchAutocomplete(query), 300);
    return () => { if (debounceRef.current) clearTimeout(debounceRef.current); };
  }, [query, fetchAutocomplete, mode]);

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

  const getCardImage = (card: ScryfallCard) =>
    card.image_uris?.small || card.images?.small || card.card_faces?.[0]?.image_uris?.small || '';

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
                <li key={card.id}>
                  <button
                    onClick={() => handleSelect(card)}
                    className="w-full text-left px-4 py-3 hover:bg-surface-dark transition-colors flex items-center gap-3 border-b border-border/50 last:border-0"
                  >
                    {getCardImage(card) && (
                      <img
                        src={getCardImage(card)}
                        alt={card.name}
                        className="w-10 h-14 rounded object-cover shrink-0"
                        loading="lazy"
                      />
                    )}
                    <div className="min-w-0">
                      <p className="text-sm font-medium text-text truncate">{card.name}</p>
                      <p className="text-xs text-text-secondary">
                        {card.set_name || card.setName} &middot; {card.rarity}
                        {card.prices?.eur && (
                          <span className="ml-2 font-semibold text-accent">
                            {card.prices.eur}
                          </span>
                        )}
                      </p>
                      {(card.type_line || card.typeLine) && (
                        <p className="text-xs text-text-muted truncate">{card.type_line || card.typeLine}</p>
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
