import type { ReactNode } from 'react';
import BrandLogo from '../brand/BrandLogo';
import LegalLinks from './LegalLinks';

/** Contenitore grafico delle pagine pubbliche di autenticazione (login, reset password, verifica email). */
export default function AuthShell({ subtitle, children, footer }: { subtitle: string; children: ReactNode; footer?: ReactNode }) {
  return (
    <div className="min-h-screen flex flex-col items-center justify-center px-4" style={{ background: 'linear-gradient(160deg, #1a3461 0%, #2a4d8f 60%, #eef2f7 100%)' }}>
      <div className="w-full max-w-sm">
        <div className="text-center mb-8">
          <h1 className="flex justify-center mb-2">
            <BrandLogo layout="stacked" theme="dark" height={150} />
          </h1>
          <p className="text-sm text-white/70 mt-1">{subtitle}</p>
        </div>
        <div className="bg-white rounded-2xl p-6 shadow-xl space-y-4">{children}</div>
        {footer && <div className="text-center text-sm text-white/70 mt-6">{footer}</div>}
        <LegalLinks light />
      </div>
    </div>
  );
}
