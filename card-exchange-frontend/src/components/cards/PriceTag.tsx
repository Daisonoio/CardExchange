import { useTranslation } from 'react-i18next';
import { useFormat } from '../../i18n/format';

/** Prezzo di una copia: "prezzo indicato" dal proprietario oppure "valore di mercato" come riferimento. */
export default function PriceTag({ askingPrice, marketValue, compact = false }: {
  askingPrice?: number | null;
  marketValue?: number | null;
  compact?: boolean;
}) {
  const { t } = useTranslation('cards');
  const { euro: eur } = useFormat();
  if (askingPrice != null) {
    return (
      <span className={`inline-flex items-baseline gap-1 flex-wrap ${compact ? 'text-[10px]' : 'text-xs'}`}>
        <span className="font-bold text-accent">{eur(askingPrice)}</span>
        <span className="text-text-muted">{compact ? t('priceTag.askingShort') : t('priceTag.asking')}</span>
        {marketValue != null && marketValue !== askingPrice && (
          <span className="text-text-muted">{t('priceTag.marketInline', { value: eur(marketValue) })}</span>
        )}
      </span>
    );
  }
  if (marketValue != null && marketValue > 0) {
    return (
      <span className={`inline-flex items-baseline gap-1 ${compact ? 'text-[10px]' : 'text-xs'}`}>
        <span className="font-bold text-accent">{eur(marketValue)}</span>
        <span className="text-text-muted">{compact ? t('priceTag.marketShort') : t('priceTag.market')}</span>
      </span>
    );
  }
  return null;
}
