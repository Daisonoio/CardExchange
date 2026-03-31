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
    setCurrent((c) => {
      const next = c + dir;
      if (next < 0) return 0;
      if (next >= total) return total - 1;
      return next;
    });
  }, [total]);

  // Keyboard navigation
  useEffect(() => {
    const h = (e: KeyboardEvent) => {
      if (e.key === 'ArrowLeft') go(-1);
      if (e.key === 'ArrowRight') go(1);
    };
    window.addEventListener('keydown', h);
    return () => window.removeEventListener('keydown', h);
  }, [go]);

  // Pointer events for smooth drag
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

    if (Math.abs(dx) > 40 || velocity > 0.3) {
      go(dx < 0 ? 1 : -1);
    }
    setDragX(0);
  };

  const getOffset = (index: number) => {
    return index - current;
  };

  const containerWidth = containerRef.current?.offsetWidth || 400;
  const dragFraction = isDragging ? dragX / containerWidth : 0;

  // Card sizing: on mobile ~55vw, on desktop capped at 280px
  // Space between cards ~30% of card width
  const getSlideStyle = (offset: number): React.CSSProperties => {
    const adj = offset - dragFraction * 2;
    const absAdj = Math.abs(adj);

    // Horizontal spacing: percentage of container width
    const tx = adj * 38;
    // Scale: center card = 1, shrinks with distance
    const scale = Math.max(0.5, 1 - absAdj * 0.12);
    // 3D rotation
    const rotY = Math.max(-60, Math.min(60, -adj * 25));
    // Depth
    const z = -absAdj * 120;
    // Darken cards further from center
    const brightness = Math.max(0.3, 1 - absAdj * 0.25);
    // Fade out cards very far from center
    const opacity = absAdj > 6 ? 0 : Math.max(0.2, 1 - absAdj * 0.12);

    return {
      position: 'absolute',
      left: '50%',
      top: '50%',
      width: 'min(55vw, 280px)',
      aspectRatio: '488 / 680',
      marginLeft: 'calc(min(55vw, 280px) / -2)',
      marginTop: 'calc(min(55vw, 280px) * 680 / 488 / -2)',
      transform: `perspective(1200px) translateX(${tx}%) translateZ(${z}px) rotateY(${rotY}deg) scale(${scale})`,
      transition: isDragging
        ? 'none'
        : 'transform 0.5s cubic-bezier(0.4, 0, 0.2, 1), filter 0.5s ease, opacity 0.4s ease',
      filter: `brightness(${brightness})`,
      opacity,
      zIndex: 100 - Math.round(absAdj * 10),
      cursor: absAdj < 0.5 && !isDragging ? 'pointer' : 'grab',
      pointerEvents: (absAdj < 0.5 ? 'auto' : 'none') as React.CSSProperties['pointerEvents'],
      willChange: 'transform',
    };
  };

  const currentCard = cards[current];

  // Show ALL cards, no filtering — they fade naturally with distance
  const maxVisible = 10; // render at most 10 each side for perf

  return (
    <div className="w-full select-none">
      {/* Dark background section */}
      <div className="relative w-full rounded-2xl overflow-hidden"
        style={{ background: 'linear-gradient(180deg,#fff 0%, #e8e9e9 60%, #efefef 100%)' }}
      >
        {/* Slider area */}
        <div
          ref={containerRef}
          className="relative w-full touch-pan-y"
          style={{ height: 'calc(min(55vw, 280px) * 680 / 488 + 80px)', paddingTop: '20px' }}
          onPointerDown={onPointerDown}
          onPointerMove={onPointerMove}
          onPointerUp={onPointerUp}
          onPointerCancel={onPointerUp}
        >
          {/* Prev arrow — at left edge */}
          {total > 1 && (
            <button
              onClick={(e) => { e.stopPropagation(); go(-1); }}
              className="absolute left-3 top-1/2 -translate-y-1/2 z-[200] w-10 h-10 rounded-full bg-white/10 backdrop-blur-md flex items-center justify-center text-white/80 hover:bg-white/25 transition-colors"
            >
              <ChevronLeft size={22} />
            </button>
          )}

          {/* Cards */}
          {cards.map((card, index) => {
            const offset = getOffset(index);
            // Skip cards too far for performance
            if (Math.abs(offset) > maxVisible) return null;

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
                    loading={Math.abs(offset) <= 2 ? 'eager' : 'lazy'}
                  />
                ) : (
                  <div className="w-full h-full bg-gradient-to-br from-gray-700 to-gray-900 flex items-center justify-center p-3">
                    <span className="text-white text-center text-sm font-bold">{card.cardName}</span>
                  </div>
                )}

                {/* Overlay info on the center card only */}
                {Math.abs(offset) < 0.5 && (
                  <div className="absolute bottom-0 left-0 right-0 bg-gradient-to-t from-black/80 via-black/40 to-transparent p-4 pt-10">
                    <h3 className="text-base font-bold text-white truncate">
                      
                    </h3>
                   
                  </div>
                )}
              </div>
            );
          })}

          {/* Next arrow — at right edge */}
          {total > 1 && (
            <button
              onClick={(e) => { e.stopPropagation(); go(1); }}
              className="absolute right-3 top-1/2 -translate-y-1/2 z-[200] w-10 h-10 rounded-full bg-white/10 backdrop-blur-md flex items-center justify-center text-white/80 hover:bg-white/25 transition-colors"
            >
              <ChevronRight size={22} />
            </button>
          )}
        </div>

        {/* Card details below carousel */}
        <div className="text-center px-4 pb-5">
          <div >
                    <h3 className="text-xl font-bold text-black-400">
                      {currentCard.cardName}
                    </h3>
                   
                  </div>
          <div className="items-center justify-center gap-3"> 
              <p className="text-m text-black/60 mt-0.5">
                  {currentCard.cardSetName}
              </p>
            <span className="text-xl font-bold text-amber-400">
                €{currentCard.priceEur.toFixed(2)}<br />
            <span className="text-xs text-black/50">
                {currentCard.distanceKm} km · @{currentCard.ownerUsername}
            </span>
</span>
          </div>
        </div>

        {/* Dot indicators */}
        {total > 1 && total <= 20 && (
          <div className="flex items-center justify-center gap-1 pb-4">
            {cards.map((_, i) => (
              <button
                key={i}
                onClick={() => setCurrent(i)}
                className={`rounded-full transition-all duration-300 ${
                  i === current
                    ? 'w-5 h-1.5 bg-white'
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
