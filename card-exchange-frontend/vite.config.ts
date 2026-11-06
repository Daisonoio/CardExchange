import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  build: {
    rollupOptions: {
      output: {
        // Librerie pesanti in file separati: si scaricano solo dalle pagine che le usano e restano in cache tra i rilasci
        manualChunks(id) {
          if (!id.includes('node_modules')) return undefined;
          if (id.includes('recharts') || id.includes('d3-') || id.includes('victory-vendor')) return 'charts';
          if (id.includes('leaflet')) return 'maps';
          if (id.includes('tesseract')) return 'scanner';
          if (id.includes('@microsoft/signalr')) return 'realtime';
          if (id.includes('react-dom') || id.includes('react-router') || /node_modules\/react\//.test(id) || id.includes('scheduler')) return 'react';
          if (id.includes('i18next')) return 'i18n';
          return undefined;
        },
      },
    },
  },
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:8080',
        changeOrigin: true,
        secure: false,
      },
      '/hubs': {
        target: 'http://localhost:8080',
        changeOrigin: true,
        secure: false,
        ws: true,
      },
    },
  },
})
