import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig(({ mode }) => {
  // Backend chạy ở địa chỉ khác thì tạo frontend/web/.env với dòng API_TARGET=http://localhost:<cổng>; mặc định khớp launchSettings của Api.
  const target = loadEnv(mode, process.cwd(), "").API_TARGET || "http://localhost:5000";
  const toBackend = { target, changeOrigin: true };

  return {
    plugins: [react()],
    server: {
      port: 5173,
      proxy: { "/auth": toBackend, "/me": toBackend, "/api": toBackend },
    },
  };
});
