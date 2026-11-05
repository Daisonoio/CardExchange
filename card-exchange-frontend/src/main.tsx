import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
// Font servito dal nostro dominio: nessuna richiesta a Google Fonts (privacy)
import '@fontsource-variable/inter'
import './index.css'
import './i18n'
import App from './App.tsx'
import { captureReferralFromUrl } from './utils/referral'

captureReferralFromUrl()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)

// Service worker: rende l'app installabile e riceve le notifiche push
if ('serviceWorker' in navigator) {
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/sw.js').catch((err) => console.warn('Service worker non registrato', err));
  });
}
