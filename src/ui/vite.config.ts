import { defineConfig } from 'vite';
import { svelte } from '@sveltejs/vite-plugin-svelte';
import tailwindcss from '@tailwindcss/vite';
import { fileURLToPath } from 'node:url';

// Giao diện build ra ../../ui (thư mục app phục vụ qua https://sohoc.app/). Nội dung bài học ở ../../content,
// app phục vụ riêng tại /content/ nên không đi qua bundle: thêm bài không cần build lại.
export default defineConfig({
  plugins: [tailwindcss(), svelte()],
  base: './',
  resolve: { alias: { $lib: fileURLToPath(new URL('./src/lib', import.meta.url)) } },
  build: {
    outDir: '../../ui',
    emptyOutDir: true,
    target: 'es2022',
    assetsDir: 'assets',
    chunkSizeWarningLimit: 600,
  },
});
