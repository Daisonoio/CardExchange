import { useState, useRef, useCallback, useEffect } from 'react';

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
/*  Voyage-style 3D carousel – no arrows, click side cards to nav     */
/* ------------------------------------------------------------------ */

const CARD_W = 'min(52vw, 250px)';
const CARD_H = 'min(72.5vw, 350px)';
const TRANSITION_MS = 460;
const DRAG_THRESHOLD = 8; // px – below this it's a tap, not a drag
const SWIPE_DISTANCE = 42;
const SWIPE_VELOCITY = 0.28;
const SNAP_BACK_MS = 420;

export default function CardCarousel({ cards, onCardClick }: CardCarouselProps) {
  const [current, setCurrent] = useState(0);
  const [isAnimating, setIsAnimating] = useState(false);
  const [isSnapBack, setIsSnapBack] = useState(false);

  // drag / swipe
  const [dragX, setDragX] = useState(0);
  const [isDragging, setIsDragging] = useState(false);
  const dragStartX = useRef(0);
  const dragStartY = useRef(0);
  const dragStartTime = useRef(0);
  const sliderRef = useRef<HTMLDivElement>(null);
  const cardRefs = useRef<Map<number, HTMLDivElement>>(new Map());

  const total = cards.length;
  if (total === 0) return null;

  /* ---- navigate ---- */
  const goTo = useCallback(
    (index: number) => {
      if (isAnimating || index < 0 || index >= total || index === current) return;
      setCurrent(index);
      setIsAnimating(true);
      setTimeout(() => setIsAnimating(false), TRANSITION_MS);
    },
    [total, isAnimating, current],
  );

  const go = useCallback(
    (dir: number) => goTo(current + dir),
    [current, goTo],
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

  /* ---- find which card was tapped based on pointer coordinates ---- */
  const findTappedCardIndex = (clientX: number, clientY: number): number => {
    // Check cards from highest z-index to lowest (center first, then sides)
    const candidates: { index: number; zIndex: number }[] = [];
    cardRefs.current.forEach((el, index) => {
      const rect = el.getBoundingClientRect();
      if (
        clientX >= rect.left &&
        clientX <= rect.right &&
        clientY >= rect.top &&
        clientY <= rect.bottom
      ) {
        const z = parseInt(el.style.zIndex || '0', 10);
        candidates.push({ index, zIndex: z });
      }
    });
    if (candidates.length === 0) return -1;
    candidates.sort((a, b) => b.zIndex - a.zIndex);
    return candidates[0].index;
  };

  /* ---- pointer events (touch + mouse) ---- */
  const onPointerDown = (e: React.PointerEvent) => {
    if (isAnimating) return;
    setIsSnapBack(false);
    setIsDragging(true);
    dragStartX.current = e.clientX;
    dragStartY.current = e.clientY;
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
    const dy = e.clientY - dragStartY.current;
    const dist = Math.sqrt(dx * dx + dy * dy);
    const dt = Date.now() - dragStartTime.current;
    const vel = Math.abs(dx) / dt;

    if (Math.abs(dx) > SWIPE_DISTANCE || vel > SWIPE_VELOCITY) {
      // Requested behavior:
      // swipe right -> show card on the left (previous)
      // swipe left  -> show card on the right (next)
      go(dx > 0 ? -1 : 1);
    } else if (dist < DRAG_THRESHOLD && dt < 500) {
      // Tap → figure out which card was tapped
      const tappedIndex = findTappedCardIndex(e.clientX, e.clientY);
      if (tappedIndex >= 0) {
        const offset = tappedIndex - current;
        if (Math.abs(offset) === 0) {
          // Center card → open
          onCardClick?.(cards[tappedIndex]);
        } else if (Math.abs(offset) === 1) {
          // Side card → navigate to it
          goTo(tappedIndex);
        }
      }
    } else {
      // Elastic snap-back when release is not enough for a page switch.
      setIsSnapBack(true);
      window.setTimeout(() => setIsSnapBack(false), SNAP_BACK_MS);
    }
    setDragX(0);
  };

  /* ---- card styles ---- */
  const containerW = sliderRef.current?.offsetWidth || 400;
  const dragFrac = isDragging ? dragX / containerW : 0;

  const getCardStyle = (index: number): React.CSSProperties => {
    const raw = index - current;
    // Keep drag direction visually aligned with the finger/mouse movement.
    const adj = raw + dragFrac * 1.8;
    const absAdj = Math.abs(adj);

    const txPct = adj * 110;
    const rotY = -adj * 25;
    const scale = absAdj < 0.3 ? 1.16 : Math.max(0.58, 0.9 - (absAdj - 1) * 0.14);
    const tyPx = Math.min(18, absAdj * 8);
    const overlayOpacity = absAdj < 0.3 ? 0.15 : Math.min(0.75, 0.6 + (absAdj - 1) * 0.1);

    const opacity = absAdj > 2.5 ? 0 : 1;
    const zIndex = absAdj < 0.3 ? 50 : absAdj < 1.3 ? 30 : 10;

    const transition = isDragging
      ? 'none'
      : isSnapBack
      ? `transform ${SNAP_BACK_MS}ms cubic-bezier(0.18, 1.35, 0.32, 1), opacity ${SNAP_BACK_MS}ms cubic-bezier(0.18, 1.2, 0.32, 1), filter ${SNAP_BACK_MS}ms cubic-bezier(0.18, 1.2, 0.32, 1)`
      : `transform ${TRANSITION_MS}ms cubic-bezier(0.22, 1, 0.36, 1), opacity ${TRANSITION_MS}ms cubic-bezier(0.22, 1, 0.36, 1), filter ${TRANSITION_MS}ms cubic-bezier(0.22, 1, 0.36, 1)`;

    const isCurrent = absAdj < 0.5;
    const isSide = absAdj >= 0.5 && absAdj < 1.5;
    const cardShadow = isCurrent
      ? '0 30px 56px rgba(15, 23, 42, 0.28), 0 12px 20px rgba(15, 23, 42, 0.2)'
      : '0 14px 30px rgba(15, 23, 42, 0.18)';

    return {
      position: 'absolute',
      left: '50%',
      top: '50%',
      width: CARD_W,
      height: CARD_H,
      marginLeft: `calc(${CARD_W} / -2)`,
      marginTop: `calc(${CARD_H} / -2)`,
      transform: `translateX(${txPct}%) translateY(${tyPx}px) rotateY(${rotY}deg) scale(${scale})`,
      transition,
      opacity,
      zIndex,
      cursor: (isCurrent || isSide) && !isDragging ? 'pointer' : 'default',
      willChange: 'transform, opacity',
      filter: absAdj < 0.45 ? 'saturate(1.08) brightness(1.02)' : 'saturate(0.9) brightness(0.92) blur(0.35px)',
      boxShadow: cardShadow,
      border: '1px solid rgba(255, 255, 255, 0.45)',
      animation: !isDragging && !isSnapBack && isCurrent ? 'cardCarouselBreath 3.8s ease-in-out infinite' : undefined,
      '--overlay-opacity': `${overlayOpacity}`,
    } as React.CSSProperties;
  };

  const currentCard = cards[current];
  const renderRange = 3;

  return (
    <div className="w-full select-none">
      <style>{`
        @keyframes cardCarouselBreath {
          0%, 100% { box-shadow: 0 30px 56px rgba(15, 23, 42, 0.28), 0 12px 20px rgba(15, 23, 42, 0.2); }
          50% { box-shadow: 0 34px 64px rgba(15, 23, 42, 0.32), 0 14px 24px rgba(15, 23, 42, 0.24); }
        }
      `}</style>
      <div className="relative w-full overflow-hidden rounded-2xl bg-white">
        {/* Slider area with perspective */}
        <div
          ref={sliderRef}
          className="relative w-full touch-pan-y"
          style={{
            height: `calc(${CARD_H} * 1.25 + 40px)`,
            perspective: '1000px',
          }}
          onPointerDown={onPointerDown}
          onPointerMove={onPointerMove}
          onPointerUp={onPointerUp}
          onPointerCancel={onPointerUp}
        >
          {/* Cards */}
          <div className="relative w-full h-full" style={{ transformStyle: 'preserve-3d' }}>
            {cards.map((card, i) => {
              if (Math.abs(i - current) > renderRange) return null;
              return (
                <div
                  key={card.cardId}
                  ref={(el) => {
                    if (el) cardRefs.current.set(i, el);
                    else cardRefs.current.delete(i);
                  }}
                  style={getCardStyle(i)}
                  className="rounded-xl overflow-hidden"
                >
                  {/* Fallback background (always present for clickable area) */}
                  <div className="absolute inset-0 bg-gradient-to-br from-gray-700 to-gray-900 flex items-center justify-center p-4">
                    <span className="text-white text-center text-sm font-bold leading-tight">
                      {card.cardName}
                    </span>
                  </div>
                  {/* Image (on top of fallback) */}
                  {(card.imageLarge || card.imageUrl) && (
                    <img
                      src={card.imageLarge || card.imageUrl}
                      alt={card.cardName}
                      className="absolute inset-0 w-full h-full object-cover"
                      draggable={false}
                      loading={Math.abs(i - current) <= 1 ? 'eager' : 'lazy'}
                    />
                  )}

                  {/* Dark overlay */}
                  <div
                    className="absolute inset-0 bg-black pointer-events-none"
                    style={{
                      opacity: `var(--overlay-opacity, 0)`,
                      transition: `opacity ${TRANSITION_MS}ms ease`,
                    }}
                  />

                  {/* Light sheen */}
                  <div className="absolute inset-0 pointer-events-none bg-[linear-gradient(130deg,rgba(255,255,255,0.22)_0%,rgba(255,255,255,0.02)_36%,rgba(255,255,255,0)_65%)]" />
                </div>
              );
            })}
          </div>
        </div>

        {/* Info section */}
        <div className="px-4 pb-2">
          <div className="max-w-lg mx-auto rounded-2xl bg-white/75 backdrop-blur-md border border-white/70 shadow-[0_14px_34px_rgba(15,23,42,0.12)] px-4 py-3 text-center">
            <h3 className="text-lg sm:text-xl font-bold text-text uppercase tracking-[0.08em] truncate">
              {currentCard.cardName}
            </h3>
            <div className="flex items-center justify-center gap-2 mt-1">
              <span className="w-5 h-[2px] bg-border inline-block" />
              <span className="text-sm text-text-secondary font-semibold uppercase tracking-wide truncate max-w-[70%]">
                {currentCard.cardSetName}
              </span>
              <span className="w-5 h-[2px] bg-border inline-block" />
            </div>
            <div className="flex items-center justify-center gap-3 mt-2 flex-wrap">
              <span className="text-base font-bold text-amber-600 bg-amber-50 border border-amber-200/70 px-2.5 py-1 rounded-full">
                €{currentCard.priceEur.toFixed(2)}
              </span>
              <span className="text-xs text-text-muted bg-white/70 px-2.5 py-1 rounded-full border border-border/60">
                {currentCard.distanceKm} km · @{currentCard.ownerUsername}
              </span>
            </div>
          </div>
        </div>

        {/* Dots */}
        {total > 1 && total <= 20 && (
          <div className="flex items-center justify-center gap-1.5 pt-3 pb-4">
            {cards.map((_, i) => (
              <button
                key={i}
                onClick={() => { if (!isAnimating) goTo(i); }}
                className={`rounded-full transition-all duration-500 border ${
                  i === current
                    ? 'w-7 h-2 bg-primary border-primary/70 shadow-[0_0_0_3px_rgba(59,130,246,0.18)]'
                    : 'w-2 h-2 bg-white/85 border-gray-300/80 hover:bg-gray-100'
                }`}
              />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
