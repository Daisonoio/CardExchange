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
const TRANSITION_MS = 600;
const DRAG_THRESHOLD = 8; // px – below this it's a tap, not a drag

export default function CardCarousel({ cards, onCardClick }: CardCarouselProps) {
  const [current, setCurrent] = useState(0);
  const [isAnimating, setIsAnimating] = useState(false);

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

    if (Math.abs(dx) > 50 || vel > 0.35) {
      // Swipe → navigate
      go(dx < 0 ? 1 : -1);
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
    }
    setDragX(0);
  };

  /* ---- card styles ---- */
  const containerW = sliderRef.current?.offsetWidth || 400;
  const dragFrac = isDragging ? dragX / containerW : 0;

  const getCardStyle = (index: number): React.CSSProperties => {
    const raw = index - current;
    const adj = raw - dragFrac * 1.8;
    const absAdj = Math.abs(adj);

    const txPct = adj * 110;
    const rotY = -adj * 25;
    const scale = absAdj < 0.3 ? 1.2 : Math.max(0.55, 0.9 - (absAdj - 1) * 0.15);
    const overlayOpacity = absAdj < 0.3 ? 0.15 : Math.min(0.75, 0.6 + (absAdj - 1) * 0.1);

    const opacity = absAdj > 2.5 ? 0 : 1;
    const zIndex = absAdj < 0.3 ? 50 : absAdj < 1.3 ? 30 : 10;

    const transition = isDragging
      ? 'none'
      : `transform ${TRANSITION_MS}ms ease, opacity ${TRANSITION_MS}ms ease`;

    const isCurrent = absAdj < 0.5;
    const isSide = absAdj >= 0.5 && absAdj < 1.5;

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
      cursor: (isCurrent || isSide) && !isDragging ? 'pointer' : 'default',
      willChange: 'transform, opacity',
      '--overlay-opacity': `${overlayOpacity}`,
    } as React.CSSProperties;
  };

  const currentCard = cards[current];
  const renderRange = 3;

  return (
    <div className="w-full select-none">
      <div
        className="relative w-full overflow-hidden rounded-2xl"
        style={{ background: 'linear-gradient(180deg, #f8fafc 0%, #f1f5f9 50%, #f8fafc 100%)' }}
      >
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
                  className="rounded-xl overflow-hidden shadow-2xl"
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
                </div>
              );
            })}
          </div>
        </div>

        {/* Info section */}
        <div className="text-center px-4 pb-1">
          <h3 className="text-lg sm:text-xl font-bold text-text uppercase tracking-wider truncate">
            {currentCard.cardName}
          </h3>
          <div className="flex items-center justify-center gap-2 mt-1">
            <span className="w-5 h-[2px] bg-border inline-block" />
            <span className="text-sm text-text-secondary font-semibold uppercase tracking-wide">
              {currentCard.cardSetName}
            </span>
            <span className="w-5 h-[2px] bg-border inline-block" />
          </div>
          <div className="flex items-center justify-center gap-3 mt-2">
            <span className="text-base font-bold text-amber-600">
              €{currentCard.priceEur.toFixed(2)}
            </span>
            <span className="text-xs text-text-muted">
              {currentCard.distanceKm} km · @{currentCard.ownerUsername}
            </span>
          </div>
        </div>

        {/* Dots */}
        {total > 1 && total <= 20 && (
          <div className="flex items-center justify-center gap-1 pt-3 pb-4">
            {cards.map((_, i) => (
              <button
                key={i}
                onClick={() => { if (!isAnimating) goTo(i); }}
                className={`rounded-full transition-all duration-500 ${
                  i === current
                    ? 'w-6 h-1.5 bg-primary'
                    : 'w-1.5 h-1.5 bg-gray-300 hover:bg-gray-400'
                }`}
              />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
