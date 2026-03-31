import { useState, useRef, useCallback, useEffect } from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';

export interface CarouselCard {
  cardId: number;
  cardInfoId: number;
  cardName: string;
  cardSetName: string;
  imageUrl?: string;
  imageLarge?: string;
  priceEur: number;
  condition: string;
  quantity: number;
  ownerId: number;
  ownerUsername: string;
  ownerCity?: string;
  ownerCountry?: string;
  distanceKm: number;
}

interface CardCarouselProps {
  cards: CarouselCard[];
  onCardClick?: (card: CarouselCard) => void;
}

/* ------------------------------------------------------------------ */
/*  Voyage Slider – faithful React adaptation                         */
/*  3 visible cards (prev / current / next) with CSS transitions,     */
/*  perspective, rotateY, dark overlay, blurred bg, swipe support.    */
/*  All cards are in the DOM; only ±1 offset are styled visible,      */
/*  the rest stay off-screen until they rotate in.                    */
/* ------------------------------------------------------------------ */

const CARD_W = 'min(52vw, 250px)';
const CARD_H = 'min(72.5vw, 350px)';
const TRANSITION_MS = 800;

export default function CardCarousel({ cards, onCardClick }: CardCarouselProps) {
  const [current, setCurrent] = useState(0);
  const [isAnimating, setIsAnimating] = useState(false);

  // drag / swipe
  const [dragX, setDragX] = useState(0);
  const [isDragging, setIsDragging] = useState(false);
  const dragStartX = useRef(0);
  const dragStartTime = useRef(0);
  const sliderRef = useRef<HTMLDivElement>(null);

  const total = cards.length;
  if (total === 0) return null;

  /* ---- navigate ---- */
  const go = useCallback(
    (dir: number) => {
      if (isAnimating) return;
      setCurrent((c) => {
        const n = c + dir;
        if (n < 0 || n >= total) return c;
        return n;
      });
      setIsAnimating(true);
      setTimeout(() => setIsAnimating(false), TRANSITION_MS);
    },
    [total, isAnimating],
  );

  /* keyboard */
  useEffect(() => {
    const h = (e: KeyboardEvent) => {
      if (e.key === 'ArrowLeft') go(-1);
      if (e.key === 'ArrowRight') go(1);
    };
    window.addEventListener('keydown', h);
    return () => window.removeEventListener('keydown', h);
  }, [go]);

  /* ---- pointer events (touch + mouse) ---- */
  const onPointerDown = (e: React.PointerEvent) => {
    if (isAnimating) return;
    setIsDragging(true);
    dragStartX.current = e.clientX;
    dragStartTime.current = Date.now();
    setDragX(0);
    (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
  };
  const onPointerMove = (e: React.PointerEvent) => {
    if (!isDragging) return;
    setDragX(e.clientX - dragStartX.current);
  };
  const onPointerUp = (e: React.PointerEvent) => {
    if (!isDragging) return;
    setIsDragging(false);
    const dx = e.clientX - dragStartX.current;
    const dt = Date.now() - dragStartTime.current;
    const vel = Math.abs(dx) / dt;
    if (Math.abs(dx) > 50 || vel > 0.35) {
      go(dx < 0 ? 1 : -1);
    }
    setDragX(0);
  };

  /* ---- card styles (Voyage Slider logic) ---- */
  const containerW = sliderRef.current?.offsetWidth || 400;
  const dragFrac = isDragging ? dragX / containerW : 0;

  const getCardStyle = (index: number): React.CSSProperties => {
    const raw = index - current;
    const adj = raw - dragFrac * 1.8;
    const absAdj = Math.abs(adj);

    // Voyage values:
    //   current  → translateX(0)  rotateY(0)    scale(1.2)  overlay 20%
    //   ±1       → ±110%          ∓25deg        scale(0.9)  overlay 60%
    //   ±2+      → keep stacking outwards, fully dark, hidden
    const txPct = adj * 110;
    const rotY = -adj * 25;
    const scale = absAdj < 0.3 ? 1.2 : Math.max(0.55, 0.9 - (absAdj - 1) * 0.15);
    const overlayOpacity = absAdj < 0.3 ? 0.15 : Math.min(0.75, 0.6 + (absAdj - 1) * 0.1);

    // Cards beyond ±2 are invisible
    const opacity = absAdj > 2.5 ? 0 : 1;
    const zIndex = absAdj < 0.3 ? 50 : absAdj < 1.3 ? 30 : 10;

    const transition = isDragging
      ? 'none'
      : `transform ${TRANSITION_MS}ms ease, opacity ${TRANSITION_MS}ms ease`;

    return {
      position: 'absolute',
      left: '50%',
      top: '50%',
      width: CARD_W,
      height: CARD_H,
      marginLeft: `calc(${CARD_W} / -2)`,
      marginTop: `calc(${CARD_H} / -2)`,
      transform: `translateX(${txPct}%) rotateY(${rotY}deg) scale(${scale})`,
      transition,
      opacity,
      zIndex,
      cursor: absAdj < 0.5 && !isDragging ? 'pointer' : 'default',
      pointerEvents: absAdj < 0.5 ? 'auto' : 'none',
      willChange: 'transform, opacity',
      '--overlay-opacity': `${overlayOpacity}`,
    } as React.CSSProperties;
  };

  const currentCard = cards[current];
  const bgImg = currentCard.imageLarge || currentCard.imageUrl;

  // Render ±3 cards for smooth transition (prev, current, next + one extra each side)
  const renderRange = 3;

  return (
    <div className="w-full select-none">
      {/* Container with dark bg + blurred card image */}
      <div
        className="relative w-full overflow-hidden rounded-2xl"
        style={{ background: '#0a0a14' }}
      >
        {/* Blurred background – current card image */}
        {bgImg && (
          <div
            key={current}
            className="absolute inset-0 z-0"
            style={{
              backgroundImage: `url(${bgImg})`,
              backgroundSize: 'cover',
              backgroundPosition: 'center',
              filter: 'blur(16px) brightness(0.25)',
              transform: 'scale(1.3)',
            }}
          />
        )}
        <div className="absolute inset-0 z-[1] bg-black/60" />

        {/* Slider area with perspective */}
        <div
          ref={sliderRef}
          className="relative z-[2] w-full touch-pan-y"
          style={{
            height: `calc(${CARD_H} * 1.25 + 40px)`,
            perspective: '1000px',
          }}
          onPointerDown={onPointerDown}
          onPointerMove={onPointerMove}
          onPointerUp={onPointerUp}
          onPointerCancel={onPointerUp}
        >
          {/* Left arrow – page edge */}
          {total > 1 && (
            <button
              onClick={(e) => { e.stopPropagation(); go(-1); }}
              disabled={current === 0}
              className="absolute left-3 sm:left-6 top-1/2 -translate-y-1/2 z-[200] w-9 h-9 sm:w-11 sm:h-11 rounded-full border border-white/20 flex items-center justify-center text-white transition-all duration-200 hover:bg-white/10 disabled:opacity-20"
            >
              <ChevronLeft size={20} />
            </button>
          )}

          {/* Cards */}
          <div className="relative w-full h-full" style={{ transformStyle: 'preserve-3d' }}>
            {cards.map((card, i) => {
              if (Math.abs(i - current) > renderRange) return null;
              const offset = i - current;
              return (
                <div
                  key={card.cardId}
                  style={getCardStyle(i)}
                  className="rounded-xl overflow-hidden shadow-2xl"
                  onClick={() => {
                    if (Math.abs(offset) < 0.5 && !isDragging && !isAnimating)
                      onCardClick?.(card);
                  }}
                >
                  {/* Image */}
                  {card.imageUrl ? (
                    <img
                      src={card.imageLarge || card.imageUrl}
                      alt={card.cardName}
                      className="absolute inset-0 w-full h-full object-cover"
                      draggable={false}
                      loading={Math.abs(offset) <= 1 ? 'eager' : 'lazy'}
                    />
                  ) : (
                    <div className="absolute inset-0 bg-gradient-to-br from-gray-700 to-gray-900 flex items-center justify-center p-4">
                      <span className="text-white text-center text-sm font-bold leading-tight">
                        {card.cardName}
                      </span>
                    </div>
                  )}

                  {/* Dark overlay (Voyage style ::before) */}
                  <div
                    className="absolute inset-0 bg-black pointer-events-none"
                    style={{
                      opacity: `var(--overlay-opacity, 0)`,
                      transition: `opacity ${TRANSITION_MS}ms ease`,
                    }}
                  />
                </div>
              );
            })}
          </div>

          {/* Right arrow – page edge */}
          {total > 1 && (
            <button
              onClick={(e) => { e.stopPropagation(); go(1); }}
              disabled={current === total - 1}
              className="absolute right-3 sm:right-6 top-1/2 -translate-y-1/2 z-[200] w-9 h-9 sm:w-11 sm:h-11 rounded-full border border-white/20 flex items-center justify-center text-white transition-all duration-200 hover:bg-white/10 disabled:opacity-20"
            >
              <ChevronRight size={20} />
            </button>
          )}
        </div>

        {/* Info – Voyage style (name + location separator) */}
        <div className="relative z-[2] text-center px-4 pb-1">
          <h3 className="text-lg sm:text-xl font-bold text-white uppercase tracking-wider truncate">
            {currentCard.cardName}
          </h3>
          <div className="flex items-center justify-center gap-2 mt-1">
            <span className="w-5 h-[2px] bg-white/30 inline-block" />
            <span className="text-sm text-white/60 font-semibold uppercase tracking-wide">
              {currentCard.cardSetName}
            </span>
            <span className="w-5 h-[2px] bg-white/30 inline-block" />
          </div>
          <div className="flex items-center justify-center gap-3 mt-2">
            <span className="text-base font-bold text-amber-400">
              €{currentCard.priceEur.toFixed(2)}
            </span>
            <span className="text-xs text-white/40">
              {currentCard.distanceKm} km · @{currentCard.ownerUsername}
            </span>
          </div>
        </div>

        {/* Dots */}
        {total > 1 && total <= 20 && (
          <div className="relative z-[2] flex items-center justify-center gap-1 pt-3 pb-4">
            {cards.map((_, i) => (
              <button
                key={i}
                onClick={() => { if (!isAnimating) setCurrent(i); }}
                className={`rounded-full transition-all duration-500 ${
                  i === current
                    ? 'w-6 h-1.5 bg-white'
                    : 'w-1.5 h-1.5 bg-white/25 hover:bg-white/40'
                }`}
              />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
