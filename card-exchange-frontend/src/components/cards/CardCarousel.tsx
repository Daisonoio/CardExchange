import { useState, useRef, useEffect, useCallback } from 'react';
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
  const [isAnimating, setIsAnimating] = useState(false);
  const touchStartX = useRef(0);
  const containerRef = useRef<HTMLDivElement>(null);

  const total = cards.length;

  const prev = useCallback(() => {
    if (isAnimating || total < 2) return;
    setIsAnimating(true);
    setCurrent((c) => (c - 1 + total) % total);
    setTimeout(() => setIsAnimating(false), 600);
  }, [isAnimating, total]);

  const next = useCallback(() => {
    if (isAnimating || total < 2) return;
    setIsAnimating(true);
    setCurrent((c) => (c + 1) % total);
    setTimeout(() => setIsAnimating(false), 600);
  }, [isAnimating, total]);

  // Keyboard navigation
  useEffect(() => {
    const handleKey = (e: KeyboardEvent) => {
      if (e.key === 'ArrowLeft') prev();
      if (e.key === 'ArrowRight') next();
    };
    window.addEventListener('keydown', handleKey);
    return () => window.removeEventListener('keydown', handleKey);
  }, [prev, next]);

  // Touch handling
  const onTouchStart = (e: React.TouchEvent) => {
    touchStartX.current = e.touches[0].clientX;
  };

  const onTouchEnd = (e: React.TouchEvent) => {
    const diff = touchStartX.current - e.changedTouches[0].clientX;
    if (Math.abs(diff) > 50) {
      diff > 0 ? next() : prev();
    }
  };

  if (total === 0) return null;

  const getIndex = (offset: number) => (current + offset + total) % total;

  const getSlideStyle = (offset: number): React.CSSProperties => {
    const base: React.CSSProperties = {
      position: 'absolute',
      width: 'min(55vw, 220px)',
      aspectRatio: '2 / 3',
      transition: 'all 600ms ease',
      transformStyle: 'preserve-3d',
      cursor: offset === 0 ? 'pointer' : 'default',
    };

    if (offset === 0) {
      return {
        ...base,
        transform: 'perspective(1000px) translateX(0) scale(1.15) rotateY(0deg)',
        zIndex: 20,
        filter: 'brightness(1)',
      };
    }
    if (offset === 1 || (offset === -(total - 1) && total > 2)) {
      return {
        ...base,
        transform: 'perspective(1000px) translateX(calc(min(55vw, 220px) * 0.65)) scale(0.9) rotateY(-25deg)',
        zIndex: 10,
        filter: 'brightness(0.5)',
        pointerEvents: 'none',
      };
    }
    if (offset === -1 || (offset === total - 1 && total > 2)) {
      return {
        ...base,
        transform: 'perspective(1000px) translateX(calc(min(55vw, 220px) * -0.65)) scale(0.9) rotateY(25deg)',
        zIndex: 10,
        filter: 'brightness(0.5)',
        pointerEvents: 'none',
      };
    }
    return {
      ...base,
      transform: 'perspective(1000px) translateX(0) scale(0.7)',
      zIndex: 0,
      opacity: 0,
      pointerEvents: 'none',
    };
  };

  const getOffset = (index: number) => {
    let diff = index - current;
    if (diff > total / 2) diff -= total;
    if (diff < -total / 2) diff += total;
    return diff;
  };

  const currentCard = cards[current];

  return (
    <div className="w-full select-none">
      {/* Slider */}
      <div
        ref={containerRef}
        className="relative flex items-center justify-center"
        style={{ height: 'calc(min(55vw, 220px) * 1.5 + 20px)' }}
        onTouchStart={onTouchStart}
        onTouchEnd={onTouchEnd}
      >
        {/* Prev button */}
        {total > 1 && (
          <button
            onClick={prev}
            className="absolute left-0 z-30 p-2 text-white/70 hover:text-white transition-colors"
          >
            <ChevronLeft size={28} />
          </button>
        )}

        {/* Slides */}
        <div className="relative flex items-center justify-center" style={{ width: 'calc(min(55vw, 220px) * 2.5)', height: '100%' }}>
          {cards.map((card, index) => {
            const offset = getOffset(index);
            if (Math.abs(offset) > 2 && total > 4) return null;

            return (
              <div
                key={card.cardId}
                style={getSlideStyle(offset)}
                className="rounded-xl overflow-hidden shadow-2xl"
                onClick={() => offset === 0 && onCardClick?.(card)}
              >
                {card.imageUrl ? (
                  <img
                    src={card.imageLarge || card.imageUrl}
                    alt={card.cardName}
                    className="w-full h-full object-cover"
                    loading={Math.abs(offset) <= 1 ? 'eager' : 'lazy'}
                  />
                ) : (
                  <div className="w-full h-full bg-gradient-to-br from-gray-700 to-gray-900 flex items-center justify-center p-3">
                    <span className="text-white text-center text-xs font-bold">{card.cardName}</span>
                  </div>
                )}
              </div>
            );
          })}
        </div>

        {/* Next button */}
        {total > 1 && (
          <button
            onClick={next}
            className="absolute right-0 z-30 p-2 text-white/70 hover:text-white transition-colors"
          >
            <ChevronRight size={28} />
          </button>
        )}
      </div>

      {/* Card info */}
      <div className="mt-4 text-center transition-all duration-500">
        <h3 className="text-lg font-bold text-white truncate px-4">
          {currentCard.cardName}
        </h3>
        <p className="text-sm text-white/60 mt-0.5">
          {currentCard.cardSetName}
        </p>
        <div className="flex items-center justify-center gap-3 mt-2">
          <span className="text-sm font-bold text-amber-400">
            {currentCard.priceEur.toFixed(2)} EUR
          </span>
          <span className="text-xs text-white/50">
            {currentCard.distanceKm} km - @{currentCard.ownerUsername}
          </span>
        </div>
      </div>

      {/* Dots indicator */}
      {total > 1 && total <= 20 && (
        <div className="flex items-center justify-center gap-1.5 mt-3">
          {cards.map((_, i) => (
            <button
              key={i}
              onClick={() => { setCurrent(i); }}
              className={`rounded-full transition-all duration-300 ${
                i === current
                  ? 'w-6 h-1.5 bg-white'
                  : 'w-1.5 h-1.5 bg-white/30 hover:bg-white/50'
              }`}
            />
          ))}
        </div>
      )}
    </div>
  );
}
