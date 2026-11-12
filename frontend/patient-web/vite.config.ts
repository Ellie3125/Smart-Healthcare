import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig(({ mode }) => {
  // Địa chỉ Backend chỉ khai ở .env (mẫu: .env.example), không có giá trị mặc định trong code.
  const target = loadEnv(mode, process.cwd(), "").API_TARGET;
  if (!target) {
    throw new Error("Thiếu API_TARGET: chép frontend/patient-web/.env.example thành frontend/patient-web/.env.");
  }
  const toBackend = { target, changeOrigin: true };

  return {
    plugins: [react()],
    server: {
      port: 3000,
      proxy: { "/auth": toBackend, "/me": toBackend, "/api": toBackend },
    },
  };
});
