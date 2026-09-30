import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// Dev proxy: frontend -> backend (so API calls can use /api)
// You can override the backend target with VITE_PROXY_TARGET in .env
const target = process.env.VITE_PROXY_TARGET || "http://127.0.0.1:5282";

export default defineConfig({
  plugins: [react()],
  server: {
    host: "127.0.0.1",
    port: 5182,
    strictPort: true,
    proxy: {
      "/api": {
        target,
        changeOrigin: true,
      },
    },
  },
});
