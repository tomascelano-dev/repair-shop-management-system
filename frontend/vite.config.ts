import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

// Dev proxy: frontend -> backend (so API calls can use /api/v1 and the refresh cookie stays same-origin).
// Override the backend target with VITE_PROXY_TARGET.
const target = process.env.VITE_PROXY_TARGET || 'http://localhost:8080'

export default defineConfig(({ isSsrBuild }) => ({
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target,
        changeOrigin: true,
      },
    },
  },
  build: {
    sourcemap: false,
    chunkSizeWarningLimit: 700,
    rollupOptions: {
      // The SSR build (scripts/prerender.mjs) is a single module run by Node: no vendor chunks there.
      output: isSsrBuild
        ? {}
        : {
            manualChunks: {
              react: ['react', 'react-dom', 'react-router-dom'],
              query: ['@tanstack/react-query', 'axios'],
            },
          },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
    css: false,
  },
}))
