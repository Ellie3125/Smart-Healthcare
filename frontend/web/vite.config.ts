import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig(({ mode }) => {
  // Địa chỉ Backend lấy từ .env (mẫu: .env.example); mặc định khớp launchSettings của Api.
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
