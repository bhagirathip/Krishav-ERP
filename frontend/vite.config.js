import {defineConfig} from 'vite';import vue from '@vitejs/plugin-vue';
// Dev-only fallback: api.js normally calls http://localhost:5000 directly
// while Vite serves the app on :5173 (relying on the backend's permissive
// CORS policy), so this proxy is only exercised if public/config.js is set
// to '' (same-origin) during local dev - then relative /api and /uploads
// calls need forwarding to the real API process on :5000.
export default defineConfig({plugins:[vue()],server:{proxy:{'/api':{target:'http://localhost:5000',changeOrigin:true},'/uploads':{target:'http://localhost:5000',changeOrigin:true}}}});
