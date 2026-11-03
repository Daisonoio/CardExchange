import type { ReactNode } from 'react';
import { Layers } from 'lucide-react';

/** Contenitore grafico delle pagine pubbliche di autenticazione (login, reset password, verifica email). */
export default function AuthShell({ subtitle, children, footer }: { subtitle: string; children: ReactNode; footer?: ReactNode }) {
  return (
    <div className="min-h-screen flex flex-col items-center justify-center px-4" style={{ background: 'linear-gradient(160deg, #1a3461 0%, #2a4d8f 60%, #eef2f7 100%)' }}>
      <div className="w-full max-w-sm">
        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-16 h-16 rounded-2xl mb-4" style={{ background: '#f5b800' }}>
            <Layers size={32} style={{ color: '#1a3461' }} />
          </div>
          <h1 className="text-2xl font-bold text-white">CardExchange</h1>
          <p className="text-sm text-white/70 mt-1">{subtitle}</p>
        </div>
        <div className="bg-white rounded-2xl p-6 shadow-xl space-y-4">{children}</div>
        {footer && <div className="text-center text-sm text-white/70 mt-6">{footer}</div>}
      </div>
    </div>
  );
}
