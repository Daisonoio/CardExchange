import { useId, useMemo } from 'react';
import { LOGO_DATA, type LogoLayout, type LogoTheme } from './logoData';

interface BrandLogoProps {
  /** mark = solo simbolo; horizontal = simbolo + scritta affiancati; stacked = simbolo sopra la scritta */
  layout?: LogoLayout;
  /** light = su sfondo chiaro; dark = su sfondo blu */
  theme?: LogoTheme;
  /** Altezza in px: la larghezza segue le proporzioni. */
  height: number;
  className?: string;
  /** Testo alternativo; vuoto se il logo è accanto a un testo che dice già "CardExchange". */
  title?: string;
}

/**
 * Logo vettoriale di CardExchange, nitido a qualsiasi dimensione.
 * Il disegno è generato da tools/brand/build_logo.py (stessi file in public/brand/).
 */
export default function BrandLogo({ layout = 'horizontal', theme = 'light', height, className, title = 'CardExchange' }: BrandLogoProps) {
  // Id univoci per i gradienti: più loghi nella stessa pagina non si disturbano
  const id = useId().replace(/[^a-zA-Z0-9_-]/g, '');
  const data = LOGO_DATA[layout][theme];
  // Contenuto statico generato da noi (nessun input dell'utente)
  const markup = useMemo(() => data.svg.replaceAll('__ID__', `ce${id}-`), [data, id]);

  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      viewBox={`0 0 ${data.w} ${data.h}`}
      height={height}
      width={(height * data.w) / data.h}
      className={className}
      role={title ? 'img' : undefined}
      aria-label={title || undefined}
      aria-hidden={title ? undefined : true}
      focusable="false"
      dangerouslySetInnerHTML={{ __html: markup }}
    />
  );
}
