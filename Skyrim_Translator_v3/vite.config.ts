import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// https://vitejs.dev/config/
export default defineConfig(async () => ({
  plugins: [react()],

  // Vite options tailored for Tauri development and only applied in `tauri dev` or `tauri build`
  //
  // 1. prevent vite from obscuring rust errors
  clearScreen: false,
  // 2. tauri expects a fixed port, fail if that port is not available
  server: {
    port: 3000,
    strictPort: false,
    host: false,
    open: false, // 브라우저 자동 열기 비활성화
    watch: {
      // 3. tell vite to ignore watching `src-tauri`
      ignored: ["**/src-tauri/**"],
    },
    hmr: {
      overlay: true
    }
  },
  // 캐시 비활성화
  optimizeDeps: {
    force: true,
    exclude: ['@tauri-apps/api', '@tauri-apps/plugin-dialog']
  },
  build: {
    target: 'es2020',
    minify: 'esbuild',
    esbuild: {
      drop: ['console', 'debugger']
    },
    rollupOptions: {
      output: {
        // 매번 다른 파일명으로 빌드
        entryFileNames: '[name]-[hash].js',
        chunkFileNames: '[name]-[hash].js',
        assetFileNames: '[name]-[hash].[ext]',
        manualChunks: {
          'react-vendor': ['react', 'react-dom'],
          'ui-vendor': ['lucide-react', 'react-hot-toast'],
          'state-vendor': ['zustand'],
          'virtual-vendor': ['@tanstack/react-virtual']
        }
      }
    }
  },
  // 4. Resolve aliases for cleaner imports
  resolve: {
    alias: {
      "@": "/src",
      "@components": "/src/components",
      "@services": "/src/services",
      "@types": "/src/types",
      "@hooks": "/src/hooks"
    }
  }
}));