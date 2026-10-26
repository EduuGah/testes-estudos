import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

// Proxy de desenvolvimento: o navegador fala só com http://localhost:5173 e o Vite
// encaminha /api para o ASP.NET Core. Assim o código usa caminhos relativos e
// não depende de CORS em desenvolvimento.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/api": { target: "https://localhost:7001", changeOrigin: true, secure: false },
    },
  },
  build: { outDir: "dist" },
  test: { environment: "jsdom" },
});
