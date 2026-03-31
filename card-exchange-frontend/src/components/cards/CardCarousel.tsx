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

export default function CardCarousel({ cards, onCardClick }: CardCarouselProps) {
  const [current, setCurrent] = useState(0);
  const [dragX, setDragX] = useState(0);
  const [isDragging, setIsDragging] = useState(false);
  const dragStartX = useRef(0);
  const dragStartTime = useRef(0);
  const containerRef = useRef<HTMLDivElement>(null);

  const total = cards.length;
  if (total === 0) return null;

  const go = useCallback((dir: number) => {
    setCurrent((c) => (c + dir + total) % total);
  }, [total]);

  // Keyboard
  useEffect(() => {
    const h = (e: KeyboardEvent) => {
      if (e.key === 'ArrowLeft') go(-1);
      if (e.key === 'ArrowRight') go(1);
    };
    window.addEventListener('keydown', h);
    return () => window.removeEventListener('keydown', h);
  }, [go]);

  // Pointer events for smooth drag (mouse + touch)
  const onPointerDown = (e: React.PointerEvent) => {
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
    const velocity = Math.abs(dx) / dt;

    // Swipe threshold: 40px or fast flick
    if (Math.abs(dx) > 40 || velocity > 0.3) {
      go(dx < 0 ? 1 : -1);
    }
    setDragX(0);
  };

  const getOffset = (index: number) => {
    let diff = index - current;
    if (diff > total / 2) diff -= total;
    if (diff < -total / 2) diff += total;
    return diff;
  };

  // Compute drag-based fractional offset for smooth feel
  const containerWidth = containerRef.current?.offsetWidth || 400;
  const dragFraction = isDragging ? dragX / containerWidth : 0;

  const getSlideStyle = (offset: number): React.CSSProperties => {
    // Apply drag fraction to make slides follow finger
    const adjustedOffset = offset - dragFraction * 1.5;

    const tx = adjustedOffset * 55; // % translation
    const absOff = Math.abs(adjustedOffset);
    const scale = Math.max(0.65, 1 - absOff * 0.18);
    const rotY = -adjustedOffset * 35; // degrees
    const z = -absOff * 100;
    const brightness = Math.max(0.35, 1 - absOff * 0.45);
    const opacity = absOff > 2.5 ? 0 : 1;

    return {
      position: 'absolute',
      left: '50%',
      top: '50%',
      width: 'min(60vw, 260px)',
      aspectRatio: '488 / 680',
      marginLeft: 'calc(min(60vw, 260px) / -2)',
      marginTop: 'calc(min(60vw, 260px) * 680 / 488 / -2)',
      transform: `perspective(1200px) translateX(${tx}%) translateZ(${z}px) rotateY(${rotY}deg) scale(${scale})`,
      transition: isDragging ? 'none' : 'transform 0.5s cubic-bezier(0.4, 0, 0.2, 1), filter 0.5s ease, opacity 0.4s ease',
      filter: `brightness(${brightness})`,
      opacity,
      zIndex: 100 - Math.round(absOff * 10),
      cursor: offset === 0 && !isDragging ? 'pointer' : 'grab',
      pointerEvents: (absOff < 0.5 ? 'auto' : 'none') as React.CSSProperties['pointerEvents'],
      willChange: 'transform',
    };
  };

  const currentCard = cards[current];

  return (
    <div className="w-full select-none touch-pan-y">
      {/* Slider area */}
      <div
        ref={containerRef}
        className="relative w-full overflow-hidden"
        style={{ height: 'calc(min(60vw, 260px) * 680 / 488 + 24px)' }}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={onPointerUp}
        onPointerCancel={onPointerUp}
      >
        {/* Prev button */}
        {total > 1 && (
          <button
            onClick={(e) => { e.stopPropagation(); go(-1); }}
            className="absolute left-2 top-1/2 -translate-y-1/2 z-[200] w-9 h-9 rounded-full bg-black/10 backdrop-blur-sm flex items-center justify-center text-text-secondary hover:bg-black/20 transition"
          >
            <ChevronLeft size={20} />
          </button>
        )}

        {/* Cards */}
        {cards.map((card, index) => {
          const offset = getOffset(index);
          if (Math.abs(offset) > 3) return null;

          return (
            <div
              key={card.cardId}
              style={getSlideStyle(offset)}
              className="rounded-xl overflow-hidden shadow-2xl"
              onClick={() => {
                if (Math.abs(offset) < 0.5 && !isDragging) onCardClick?.(card);
              }}
            >
              {card.imageUrl ? (
                <img
                  src={card.imageLarge || card.imageUrl}
                  alt={card.cardName}
                  className="w-full h-full object-cover"
                  draggable={false}
                  loading={Math.abs(offset) <= 1 ? 'eager' : 'lazy'}
                />
              ) : (
                <div className="w-full h-full bg-gradient-to-br from-gray-700 to-gray-900 flex items-center justify-center p-3">
                  <span className="text-white text-center text-sm font-bold">{card.cardName}</span>
                </div>
              )}
            </div>
          );
        })}

        {/* Next button */}
        {total > 1 && (
          <button
            onClick={(e) => { e.stopPropagation(); go(1); }}
            className="absolute right-2 top-1/2 -translate-y-1/2 z-[200] w-9 h-9 rounded-full bg-black/10 backdrop-blur-sm flex items-center justify-center text-text-secondary hover:bg-black/20 transition"
          >
            <ChevronRight size={20} />
          </button>
        )}
      </div>

      {/* Card info below */}
      <div className="mt-3 text-center px-4">
        <h3 className="text-base font-bold text-text truncate">
          {currentCard.cardName}
        </h3>
        <p className="text-sm text-text-secondary mt-0.5">
          {currentCard.cardSetName}
        </p>
        <div className="flex items-center justify-center gap-2 mt-1.5">
          <span className="text-sm font-bold text-amber-600">
            {currentCard.priceEur.toFixed(2)} EUR
          </span>
          <span className="text-xs text-text-muted">
            {currentCard.distanceKm} km - @{currentCard.ownerUsername}
          </span>
        </div>
      </div>

      {/* Dots */}
      {total > 1 && total <= 20 && (
        <div className="flex items-center justify-center gap-1.5 mt-3">
          {cards.map((_, i) => (
            <button
              key={i}
              onClick={() => setCurrent(i)}
              className={`rounded-full transition-all duration-300 ${
                i === current
                  ? 'w-5 h-1.5 bg-primary'
                  : 'w-1.5 h-1.5 bg-gray-300'
              }`}
            />
          ))}
        </div>
      )}
    </div>
  );
}
