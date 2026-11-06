import { Loader2 } from 'lucide-react';

/** Attesa mentre arriva il codice di una pagina. */
export default function PageLoader() {
  return (
    <div className="flex justify-center py-16" role="status" aria-live="polite">
      <Loader2 className="animate-spin text-primary" />
    </div>
  );
}
