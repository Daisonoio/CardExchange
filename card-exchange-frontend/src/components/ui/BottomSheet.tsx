import { useEffect, useRef, useState } from 'react';
import { X } from 'lucide-react';

interface BottomSheetProps {
  open: boolean;
  onClose: () => void;
  title: string;
  children: React.ReactNode;
}

/**
 * Mobile-first bottom sheet that slides up from the bottom.
 * On desktop (md+) renders as a centered modal dialog.
 * Uses z-[60] to sit above the BottomNav (z-50).
 */
export default function BottomSheet({ open, onClose, title, children }: BottomSheetProps) {
  const [visible, setVisible] = useState(false);
  const [animating, setAnimating] = useState(false);
  const sheetRef = useRef<HTMLDivElement>(null);

  // Open animation
  useEffect(() => {
    if (open) {
      setVisible(true);
      requestAnimationFrame(() => {
        requestAnimationFrame(() => setAnimating(true));
      });
    } else {
      setAnimating(false);
      const t = setTimeout(() => setVisible(false), 300);
      return () => clearTimeout(t);
    }
  }, [open]);

  // Lock body scroll when open
  useEffect(() => {
    if (open) {
      document.body.style.overflow = 'hidden';
      return () => { document.body.style.overflow = ''; };
    }
  }, [open]);

  // Close on backdrop click
  const handleBackdropClick = (e: React.MouseEvent) => {
    if (e.target === e.currentTarget) onClose();
  };

  // Close on Escape
  useEffect(() => {
    if (!open) return;
    const h = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', h);
    return () => window.removeEventListener('keydown', h);
  }, [open, onClose]);

  if (!visible) return null;

  return (
    <div
      className={`fixed inset-0 z-[60] transition-colors duration-300 ${
        animating ? 'bg-black/40 backdrop-blur-sm' : 'bg-transparent'
      }`}
      onClick={handleBackdropClick}
    >
      {/* Mobile: bottom sheet | Desktop: centered dialog */}
      <div
        ref={sheetRef}
        className={`
          absolute inset-x-0 bottom-0
          md:static md:absolute md:inset-0 md:m-auto md:max-w-lg md:h-fit md:max-h-[85vh]
          bg-white rounded-t-2xl md:rounded-2xl
          shadow-2xl
          flex flex-col
          max-h-[92vh]
          transition-transform duration-300 ease-out
          ${animating
            ? 'translate-y-0'
            : 'translate-y-full md:translate-y-4 md:opacity-0'
          }
        `}
        style={{ willChange: 'transform' }}
      >
        {/* Drag handle (mobile only) */}
        <div className="flex justify-center pt-3 pb-1 md:hidden">
          <div className="w-10 h-1 rounded-full bg-gray-300" />
        </div>

        {/* Header */}
        <div className="flex items-center justify-between px-5 py-3 border-b border-border/50 shrink-0">
          <h2 className="text-lg font-bold text-text">{title}</h2>
          <button
            onClick={onClose}
            className="w-8 h-8 rounded-full flex items-center justify-center hover:bg-surface-dark transition-colors"
          >
            <X size={20} className="text-text-muted" />
          </button>
        </div>

        {/* Content — scrollable, with bottom padding for mobile nav */}
        <div className="flex-1 overflow-y-auto px-5 py-4 pb-24 md:pb-6">
          {children}
        </div>
      </div>
    </div>
  );
}
