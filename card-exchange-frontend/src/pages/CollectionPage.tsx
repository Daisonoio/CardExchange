import { useState, useEffect, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router-dom';
import { useFormat } from '../i18n/format';
import { Plus, Library, Search, TrendingUp, ChevronDown, Loader2, FileUp, CheckSquare, X, ScanLine } from 'lucide-react';
import { cards, priceTracking, scryfall } from '../api';
import { useAuth } from '../context/AuthContext';
import { useGame } from '../context/GameContext';
import type { Card, GameCard, CardCondition, CardFinish, PriceSpike, ScryfallCard } from '../types';
import { CONDITION_LABELS } from '../types';
import CardGridItem from '../components/cards/CardGridItem';
import EditCardSheet from '../components/cards/EditCardSheet';
import LanguageFinishPicker from '../components/cards/LanguageFinishPicker';
import ImportCollectionSheet from '../components/cards/ImportCollectionSheet';
import { readLastLanguage, saveLastLanguage } from '../utils/lastLanguage';
import GameCardSearch, { importGameCard, searchGameCards } from '../components/cards/GameCardSearch';
import CardScanner from '../components/cards/CardScanner';
import Button from '../components/ui/Button';
import EmptyState from '../components/ui/EmptyState';
import BottomSheet from '../components/ui/BottomSheet';

export default function CollectionPage() {
  const { t } = useTranslation('collection');
  const { euro } = useFormat();
  const { user } = useAuth();
  const { selectedGameId, selectedGame } = useGame();
  const [myCards, setMyCards] = useState<Card[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  // ?add=1 (dal "+" dell'anteprima): apre subito la finestra di aggiunta
  const [searchParams, setSearchParams] = useSearchParams();
  const [showAddModal, setShowAddModal] = useState(() => searchParams.get('add') === '1');
  useEffect(() => {
    if (searchParams.get('add') === '1') setSearchParams({}, { replace: true });
  }, [searchParams, setSearchParams]);
  const [showScanner, setShowScanner] = useState(false);
  const [editCard, setEditCard] = useState<Card | null>(null);
  const [selectedCard, setSelectedCard] = useState<GameCard | null>(null);
  const [addForm, setAddForm] = useState({
    condition: 2 as CardCondition,
    quantity: 1,
    isAvailableForTrade: true,
    notes: '',
    language: readLastLanguage(),
    finish: 1 as CardFinish,
    askingPrice: '',
  });
  const [printings, setPrintings] = useState<ScryfallCard[]>([]);
  const [loadingPrintings, setLoadingPrintings] = useState(false);
  const [showPrintings, setShowPrintings] = useState(false);
  const isMagic = !selectedGame || selectedGame.name.toLowerCase().includes('magic');
  // Import CSV per tutti i giochi con un catalogo (Magic, Pokémon, Yu-Gi-Oh!, One Piece, Lorcana…)
  const canImport = isMagic || !!selectedGame?.catalogKey;
  const [isSaving, setIsSaving] = useState(false);
  const [showImport, setShowImport] = useState(false);
  // Modalità selezione per le azioni di massa
  const [selectMode, setSelectMode] = useState(false);
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [bulkBusy, setBulkBusy] = useState(false);
  const [bulkError, setBulkError] = useState<string | null>(null);
  const [filterText, setFilterText] = useState('');
  const [filterTrade, setFilterTrade] = useState<'all' | 'trade' | 'keep'>('all');
  const [spikeCards, setSpikeCards] = useState<PriceSpike[]>([]);
  const [spikeCardIds, setSpikeCardIds] = useState<Set<number>>(new Set());
  const [showSpikesOnly, setShowSpikesOnly] = useState(false);

  const loadCards = useCallback(async () => {
    if (!user) return;
    setIsLoading(true);
    try {
      const { data } = await cards.getByUser(user.id, selectedGameId);
      setMyCards(Array.isArray(data) ? data : (data as any).cards ?? []);
    } catch (err) {
      console.error('Errore caricamento carte:', err);
    } finally {
      setIsLoading(false);
    }
  }, [user, selectedGameId]);

  const loadSpikes = useCallback(async () => {
    try {
      const { data } = await priceTracking.getSpikes(selectedGameId);
      const spikes = Array.isArray(data) ? data : [];
      setSpikeCards(spikes);
      setSpikeCardIds(new Set(spikes.map((s) => s.cardId)));
    } catch {
      // silently fail - spikes are non-essential
    }
  }, [selectedGameId]);

  useEffect(() => { loadCards(); loadSpikes(); }, [loadCards, loadSpikes]);

  const loadPrintings = useCallback(async (cardName: string) => {
    setLoadingPrintings(true);
    try {
      const { data } = await scryfall.search(`!"${cardName}" unique:prints`);
      const list = data.data ?? data.cards ?? [];
      setPrintings(Array.isArray(list) ? list : []);
    } catch {
      setPrintings([]);
    } finally {
      setLoadingPrintings(false);
    }
  }, []);

  const handleSelectCard = (card: GameCard) => {
    setSelectedCard(card);
    setShowPrintings(false);
    if (isMagic) loadPrintings(card.name);
  };

  const handleSelectPrinting = (card: ScryfallCard) => {
    setSelectedCard({
      externalId: card.scryfallId || card.id,
      name: card.name,
      setName: card.set_name || card.setName,
      rarity: card.rarity,
      imageSmall: card.image_uris?.small || card.images?.small || card.card_faces?.[0]?.image_uris?.small || '',
      imageLarge: card.image_uris?.normal || card.images?.normal || card.image_uris?.large || card.images?.large || card.card_faces?.[0]?.image_uris?.normal || '',
      priceEur: card.prices?.eur,
      subtitle: card.type_line || card.typeLine,
    });
    setShowPrintings(false);
  };

  const getCardId = (card: ScryfallCard) => card.scryfallId || card.id;
  const getCardImageSmall = (card: ScryfallCard) =>
    card.image_uris?.small || card.images?.small || card.card_faces?.[0]?.image_uris?.small || '';
  const getSetName = (card: ScryfallCard) => card.set_name || card.setName || '';
  const getSetCode = (card: ScryfallCard) => card.setCode || card.set || '';

  const handleAddCard = async () => {
    if (!selectedCard) return;
    setIsSaving(true);
    try {
      const cardInfoId = await importGameCard(selectedGame?.name, selectedCard.externalId, selectedGame?.catalogKey);

      await cards.create(user!.id, {
        cardInfoId,
        condition: addForm.condition,
        quantity: addForm.quantity,
        language: addForm.language || undefined,
        finish: addForm.finish,
        askingPrice: addForm.askingPrice.trim() ? Math.max(0, Number(addForm.askingPrice.replace(',', '.'))) : undefined,
        isAvailableForTrade: addForm.isAvailableForTrade,
        notes: addForm.notes || undefined,
      });
      saveLastLanguage(addForm.language);

      setShowAddModal(false);
      setSelectedCard(null);
      setPrintings([]);
      setShowPrintings(false);
      // Mantiene la lingua scelta: di solito si inseriscono più carte nella stessa lingua
      setAddForm((f) => ({ condition: 2, quantity: 1, isAvailableForTrade: true, notes: '', language: f.language, finish: 1, askingPrice: '' }));
      loadCards();
    } catch (err) {
      console.error('Errore aggiunta carta:', err);
    } finally {
      setIsSaving(false);
    }
  };

  const handleCardDeleted = (card: Card) => {
    setMyCards((prev) => prev.filter((c) => c.id !== card.id));
  };

  const filtered = myCards.filter((c) => {
    const matchText = !filterText ||
      (c.cardName || c.cardInfo?.name || '').toLowerCase().includes(filterText.toLowerCase());
    const matchTrade = filterTrade === 'all' ||
      (filterTrade === 'trade' && c.isAvailableForTrade) ||
      (filterTrade === 'keep' && !c.isAvailableForTrade);
    const matchSpike = !showSpikesOnly || spikeCardIds.has(c.id);
    return matchText && matchTrade && matchSpike;
  });

  const exitSelectMode = () => { setSelectMode(false); setSelectedIds(new Set()); setBulkError(null); };
  const toggleSelected = (card: Card) =>
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(card.id)) next.delete(card.id); else next.add(card.id);
      return next;
    });
  const allFilteredSelected = filtered.length > 0 && filtered.every((c) => selectedIds.has(c.id));

  const runBulk = async (action: { isAvailableForTrade?: boolean; delete?: boolean }) => {
    if (selectedIds.size === 0) return;
    if (action.delete && !confirm(t('bulk.confirmDelete', { count: selectedIds.size }))) return;
    setBulkBusy(true);
    setBulkError(null);
    try {
      await cards.bulk({ cardIds: [...selectedIds], ...action });
      exitSelectMode();
      loadCards();
    } catch {
      setBulkError(t('bulk.error'));
    } finally {
      setBulkBusy(false);
    }
  };

  const totalValue = myCards.reduce(
    (sum, c) => sum + (c.marketValue ?? c.estimatedValue ?? c.cardInfo?.priceEur ?? 0) * c.quantity, 0
  );

  return (
    <div>
      {/* Header */}
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold text-text">{t('header.title')}</h1>
          <p className="text-sm text-text-secondary">
            {t('header.cardCount', { count: myCards.length })} &middot; {t('header.value', { value: euro(totalValue) })}
          </p>
        </div>
        <div className="flex items-center gap-1.5">
          {myCards.length > 0 && !selectMode && (
            <Button onClick={() => setSelectMode(true)} size="sm" variant="ghost" aria-label={t('header.selectAria')}>
              <CheckSquare size={16} />
              <span className="hidden sm:inline">{t('header.select')}</span>
            </Button>
          )}
          {canImport && (
            <Button onClick={() => setShowImport(true)} size="sm" variant="outline" aria-label={t('header.importAria')}>
              <FileUp size={16} />
              <span className="hidden sm:inline">{t('header.import')}</span>
            </Button>
          )}
          <Button onClick={() => setShowAddModal(true)} size="sm">
            <Plus size={16} />
            {t('header.add')}
          </Button>
        </div>
      </div>

      {/* Barra azioni di massa */}
      {selectMode && (
        <div className="sticky top-14 md:top-16 z-30 mb-3 p-2.5 rounded-xl bg-primary text-white shadow-lg space-y-2">
          <div className="flex items-center gap-2 text-sm">
            <button onClick={exitSelectMode} aria-label={t('bulk.exit')} className="p-1 rounded hover:bg-white/10">
              <X size={16} />
            </button>
            <span className="font-semibold flex-1">{t('bulk.selected', { count: selectedIds.size })}</span>
            <button
              onClick={() => setSelectedIds(allFilteredSelected ? new Set() : new Set(filtered.map((c) => c.id)))}
              className="text-xs underline"
            >
              {allFilteredSelected ? t('bulk.deselectAll') : t('bulk.selectAll')}
            </button>
            <button
              onClick={() => setSelectedIds(new Set(myCards.filter((c) => c.quantity > 1).map((c) => c.id)))}
              className="text-xs underline"
              title={t('bulk.duplicatesTitle')}
            >
              {t('bulk.duplicates')}
            </button>
          </div>
          <div className="grid grid-cols-3 gap-1.5">
            {[
              { key: 'toTrade', action: { isAvailableForTrade: true } },
              { key: 'keep', action: { isAvailableForTrade: false } },
              { key: 'delete', action: { delete: true } },
            ].map(({ key, action }) => (
              <button
                key={key}
                disabled={bulkBusy || selectedIds.size === 0}
                onClick={() => runBulk(action)}
                className={`py-1.5 rounded-lg text-xs font-semibold disabled:opacity-50 ${key === 'delete' ? 'bg-red-500/90 hover:bg-red-500' : 'bg-white/15 hover:bg-white/25'}`}
              >
                {t(`bulk.${key}`)}
              </button>
            ))}
          </div>
          {bulkError && <p className="text-xs text-red-100">{bulkError}</p>}
        </div>
      )}

      {/* Spike alert banner */}
      {spikeCards.length > 0 && (
        <button
          onClick={() => setShowSpikesOnly(!showSpikesOnly)}
          className={`w-full mb-3 flex items-center gap-2 px-4 py-2.5 rounded-xl border-2 transition-all ${
            showSpikesOnly
              ? 'border-amber-400 bg-amber-50 text-amber-800'
              : 'border-amber-300 bg-amber-50/60 text-amber-700 hover:bg-amber-50'
          }`}
        >
          <TrendingUp size={16} className="text-amber-500" />
          <span className="text-sm font-medium flex-1 text-left">
            {t('spikes.banner', { count: spikeCards.length })}
          </span>
          <span className={`text-xs px-2 py-0.5 rounded-full font-semibold ${
            showSpikesOnly ? 'bg-amber-200 text-amber-800' : 'bg-amber-100 text-amber-600'
          }`}>
            {showSpikesOnly ? t('spikes.showAll') : t('spikes.show')}
          </span>
        </button>
      )}

      {/* Search & filters */}
      <div className="flex gap-2 mb-4">
        <div className="flex-1 relative">
          <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" />
          <input
            type="text"
            placeholder={t('filters.placeholder')}
            value={filterText}
            onChange={(e) => setFilterText(e.target.value)}
            className="w-full pl-9 pr-4 py-2.5 rounded-xl border border-border bg-white text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
          />
        </div>
        <div className="flex rounded-xl border border-border overflow-hidden bg-white">
          {(['all', 'trade', 'keep'] as const).map((f) => (
            <button
              key={f}
              onClick={() => setFilterTrade(f)}
              className={`px-3 py-2 text-xs font-medium transition-colors ${
                filterTrade === f ? 'bg-primary text-white' : 'text-text-secondary hover:bg-surface-dark'
              }`}
            >
              {t(`filters.${f}`)}
            </button>
          ))}
        </div>
      </div>

      {/* Cards grid */}
      {isLoading ? (
        <div className="grid grid-cols-3 ">
          {[...Array(9)].map((_, i) => (
            <div key={i} className="aspect-[6/7] bg-white rounded-xl animate-pulse" />
          ))}
        </div>
      ) : filtered.length === 0 ? (
        <EmptyState
          icon={Library}
          title={myCards.length === 0 ? t('empty.noCards') : t('empty.noResults')}
          description={myCards.length === 0
            ? t('empty.noCardsDescription')
            : t('empty.noResultsDescription')}
          action={myCards.length === 0 ? (
            <div className="flex flex-col sm:flex-row gap-2 items-center">
              <Button onClick={() => setShowAddModal(true)} size="sm">
                <Plus size={16} /> {t('empty.addCard')}
              </Button>
              {canImport && (
                <Button onClick={() => setShowImport(true)} size="sm" variant="outline">
                  <FileUp size={16} /> {t('empty.importCsv')}
                </Button>
              )}
            </div>
          ) : undefined}
        />
      ) : (
        <div className="grid grid-cols-3 sm:grid-cols-4 gap-2">
          {filtered.map((card) => (
            <CardGridItem
              key={card.id}
              card={card}
              onClick={selectMode ? toggleSelected : setEditCard}
              selected={selectMode && selectedIds.has(card.id)}
              hasPriceSpike={spikeCardIds.has(card.id)}
            />
          ))}
        </div>
      )}

      <ImportCollectionSheet open={showImport} onClose={() => setShowImport(false)} onImported={loadCards} />

      {/* Edit Card Sheet */}
      <EditCardSheet
        card={editCard}
        onClose={() => setEditCard(null)}
        onUpdated={loadCards}
        onDeleted={handleCardDeleted}
        spikeInfo={editCard ? spikeCards.find((s) => s.cardId === editCard.id) ?? null : null}
      />

      <CardScanner
        open={showScanner}
        onClose={() => setShowScanner(false)}
        gameKey={selectedGame?.catalogKey}
        onSearch={(q) => searchGameCards(selectedGame?.name, selectedGame?.catalogKey, q)}
        onSelect={(card) => { setShowScanner(false); handleSelectCard(card); setShowAddModal(true); }}
      />

      {/* Add Card Bottom Sheet */}
      <BottomSheet
        open={showAddModal}
        onClose={() => { setShowAddModal(false); setSelectedCard(null); setPrintings([]); setShowPrintings(false); }}
        title={t('addSheet.title')}
      >
        {!selectedCard ? (
          <div>
            <p className="text-sm text-text-secondary mb-3">
              {t('addSheet.searchPrompt')}
            </p>
            <GameCardSearch onSelect={handleSelectCard} />
            <Button variant="outline" className="w-full mt-3" onClick={() => { setShowAddModal(false); setShowScanner(true); }}>
              <ScanLine size={16} /> {t('addSheet.scan')}
            </Button>
          </div>
        ) : (
          <div className="space-y-4">
            {/* Selected card preview */}
            <div className="flex gap-4 p-3 bg-surface-dark rounded-xl">
              {selectedCard.imageSmall && (
                <img
                  src={selectedCard.imageSmall}
                  alt={selectedCard.name}
                  className="w-16 h-22 rounded-lg object-cover"
                />
              )}
              <div className="min-w-0 flex-1">
                <p className="font-semibold text-sm">{selectedCard.name}</p>

                {/* Edition selector (Magic only) */}
                {isMagic ? (
                  <button
                    onClick={() => setShowPrintings(!showPrintings)}
                    className="mt-0.5 flex items-center gap-1.5 text-sm text-primary hover:underline"
                  >
                    <span className="uppercase font-semibold text-xs bg-primary/10 px-1.5 py-0.5 rounded truncate max-w-[180px]">
                      {selectedCard.setName}
                    </span>
                    <ChevronDown size={14} className={`transition-transform shrink-0 ${showPrintings ? 'rotate-180' : ''}`} />
                  </button>
                ) : selectedCard.setName ? (
                  <p className="mt-0.5 text-xs text-text-secondary">{selectedCard.setName}</p>
                ) : null}

                {selectedCard.subtitle && <p className="text-xs text-text-muted mt-0.5">{selectedCard.subtitle}</p>}
                {selectedCard.priceEur && (
                  <p className="text-sm font-bold text-accent mt-1">{euro(Number(selectedCard.priceEur))}</p>
                )}
              </div>
            </div>

            {/* Printings dropdown (Magic only) */}
            {isMagic && showPrintings && (
              <div className="max-h-48 overflow-y-auto border border-border rounded-xl bg-surface-dark">
                {loadingPrintings ? (
                  <div className="flex justify-center py-4">
                    <Loader2 size={20} className="animate-spin text-primary" />
                  </div>
                ) : (
                  <>
                    <div className="px-3 py-2 text-xs text-text-muted border-b border-border/50">
                      {t('addSheet.editions', { count: printings.length })}
                    </div>
                    {printings.map((p) => {
                      const isSelected = getCardId(p) === selectedCard.externalId;
                      return (
                        <button
                          key={getCardId(p)}
                          onClick={() => handleSelectPrinting(p)}
                          className={`w-full flex items-center gap-3 px-3 py-2.5 text-left hover:bg-white transition-colors border-b border-border/30 last:border-0 ${
                            isSelected ? 'bg-primary/5' : ''
                          }`}
                        >
                          {getCardImageSmall(p) && (
                            <img src={getCardImageSmall(p)} alt="" className="w-8 h-11 rounded object-cover shrink-0" />
                          )}
                          <div className="min-w-0 flex-1">
                            <p className="text-xs font-semibold truncate">
                              <span className="uppercase text-text-muted">{getSetCode(p)}</span>
                              {' '}{getSetName(p)}
                            </p>
                            <p className="text-xs text-text-muted">
                              {p.rarity}{p.prices?.eur ? ` · ${euro(Number(p.prices.eur))}` : ''}
                            </p>
                          </div>
                          {isSelected && (
                            <span className="text-xs font-semibold text-primary shrink-0">{t('addSheet.selected')}</span>
                          )}
                        </button>
                      );
                    })}
                  </>
                )}
              </div>
            )}

            <button
              onClick={() => { setSelectedCard(null); setPrintings([]); setShowPrintings(false); }}
              className="text-sm text-primary hover:underline"
            >
              {t('addSheet.changeCard')}
            </button>

            {/* Condition select */}
            <div>
              <label className="text-xs font-semibold text-text-secondary block mb-1.5">{t('cards:form.condition')}</label>
              <div className="grid grid-cols-2 gap-1.5">
                {([1,2,3,4,5,6,7,8] as CardCondition[]).map((c) => (
                  <button
                    key={c}
                    onClick={() => setAddForm({ ...addForm, condition: c })}
                    className={`px-3 py-2 rounded-xl text-xs font-medium border transition-colors ${
                      addForm.condition === c
                        ? 'border-primary bg-primary/10 text-primary'
                        : 'border-border text-text-secondary hover:bg-surface-dark'
                    }`}
                  >
                    {CONDITION_LABELS[c]}
                  </button>
                ))}
              </div>
            </div>

            <LanguageFinishPicker
              language={addForm.language}
              onLanguageChange={(language) => setAddForm({ ...addForm, language })}
              finish={addForm.finish}
              onFinishChange={(finish) => setAddForm({ ...addForm, finish: finish ?? 1 })}
            />

            {/* Quantity */}
            <div>
              <label className="text-xs font-semibold text-text-secondary block mb-1.5">{t('cards:form.quantity')}</label>
              <div className="flex items-center gap-3">
                <button
                  onClick={() => setAddForm({ ...addForm, quantity: Math.max(1, addForm.quantity - 1) })}
                  className="w-10 h-10 rounded-xl border border-border flex items-center justify-center text-lg hover:bg-surface-dark"
                >
                  -
                </button>
                <span className="text-lg font-bold w-8 text-center">{addForm.quantity}</span>
                <button
                  onClick={() => setAddForm({ ...addForm, quantity: addForm.quantity + 1 })}
                  className="w-10 h-10 rounded-xl border border-border flex items-center justify-center text-lg hover:bg-surface-dark"
                >
                  +
                </button>
              </div>
            </div>

            {/* Prezzo indicato (facoltativo) */}
            <div>
              <label htmlFor="add-asking-price" className="text-xs font-semibold text-text-secondary block mb-1.5">
                {t('cards:form.askingPrice')}
              </label>
              <input
                id="add-asking-price"
                type="number"
                inputMode="decimal"
                min="0"
                step="0.01"
                value={addForm.askingPrice}
                onChange={(e) => setAddForm({ ...addForm, askingPrice: e.target.value })}
                placeholder={t('addSheet.askingPricePlaceholder')}
                className="w-full px-3 py-2 rounded-xl border border-border text-sm"
              />
              <p className="text-[10px] text-text-muted mt-1">{t('addSheet.askingPriceHint')}</p>
            </div>

            {/* Trade toggle */}
            <div className="flex items-center justify-between p-3 bg-surface-dark rounded-xl">
              <div>
                <p className="text-sm font-medium">{t('cards:form.availableForTrade')}</p>
                <p className="text-xs text-text-muted">{t('cards:form.availableForTradeHint')}</p>
              </div>
              <button
                onClick={() => setAddForm({ ...addForm, isAvailableForTrade: !addForm.isAvailableForTrade })}
                className={`w-12 h-7 rounded-full transition-colors relative ${
                  addForm.isAvailableForTrade ? 'bg-primary' : 'bg-gray-300'
                }`}
              >
                <div className={`w-5 h-5 bg-white rounded-full absolute top-1 transition-transform ${
                  addForm.isAvailableForTrade ? 'translate-x-6' : 'translate-x-1'
                }`} />
              </button>
            </div>

            {/* Notes */}
            <div>
              <label className="text-xs font-semibold text-text-secondary block mb-1.5">{t('cards:form.notesOptional')}</label>
              <textarea
                value={addForm.notes}
                onChange={(e) => setAddForm({ ...addForm, notes: e.target.value })}
                placeholder={t('cards:form.notesPlaceholder')}
                className="w-full px-4 py-2.5 rounded-xl border border-border text-sm resize-none h-20 focus:outline-none focus:ring-2 focus:ring-primary/30"
              />
            </div>

            <Button onClick={handleAddCard} isLoading={isSaving} className="w-full" size="lg">
              {t('addSheet.submit')}
            </Button>
          </div>
        )}
      </BottomSheet>
    </div>
  );
}
