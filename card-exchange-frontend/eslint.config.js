import js from '@eslint/js'
import globals from 'globals'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import tseslint from 'typescript-eslint'
import { defineConfig, globalIgnores } from 'eslint/config'

export default defineConfig([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommended,
      reactHooks.configs.flat.recommended,
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      ecmaVersion: 2020,
      globals: globals.browser,
    },
    rules: {
      // Restano visibili come avvisi ma non bloccano la CI: sono debito di tipizzazione o consigli di stile,
      // non errori. Le regole che segnalano bug veri (rules-of-hooks, refs) restano errori.
      '@typescript-eslint/no-explicit-any': 'warn',
      'react-hooks/set-state-in-effect': 'warn',
      'react-refresh/only-export-components': 'warn',
    },
  },
  {
    // Mai spazi sponsorizzati o promozioni in chat, proposte di scambio, conferme e appuntamenti (regola di prodotto F13)
    files: [
      'src/pages/ChatPage.tsx', 'src/pages/TradesPage.tsx', 'src/pages/TradeRequestPage.tsx',
      'src/components/chat/**', 'src/components/trades/**', 'src/components/meetups/**',
    ],
    rules: {
      'no-restricted-imports': ['error', {
        patterns: [{ group: ['**/promotions/*', '**/SponsoredSlotCard', '**/SponsoredEvents'], message: 'Niente contenuti sponsorizzati in chat, scambi e appuntamenti.' }],
      }],
    },
  },
])
