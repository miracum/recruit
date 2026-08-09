import { fileURLToPath, URL } from "node:url";

import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
  server: {
    port: 8080,
  },
  build: {
    // Buefy 3.0.8's Steps component ships an invalid `var(...)-1px` media query
    // (fixed upstream in an unreleased version); lightningcss (Vite's default
    // CSS minifier) rejects it outright, esbuild tolerates it like a browser does.
    cssMinify: "esbuild",
  },
  test: {
    globals: true,
    environment: "jsdom",
    include: ["tests/unit/**/*.spec.js"],
    coverage: {
      provider: "v8",
      reporter: ["text", "lcov", "clover"],
      include: ["src/**/*.{js,vue}", "server/**/*.js"],
    },
  },
});
