# syntax=docker/dockerfile:1
# Image web: Nginx phục vụ hai bản build riêng (web bệnh nhân, web nhân viên) và proxy tới Api (Giải pháp kỹ thuật v2.13 §8.6).
# Build từ thư mục gốc repo:
#   docker build -f infrastructure/docker/frontend.Dockerfile --build-context nginx=infrastructure/nginx \
#     --build-arg PATIENT_PORTAL_URL=https://smarthealthcare.io.vn -t <registry>/smart-healthcare/web:<tag> frontend
# Hostname do biến môi trường PUBLIC_HOST, STAFF_HOST của container quyết định lúc chạy, nên một image dùng được cho nhiều môi trường;
# riêng PATIENT_PORTAL_URL (liên kết sang web bệnh nhân ở trang đăng nhập nhân viên) được Vite nhúng lúc build.

ARG NODE_IMAGE=node:24.11.1-alpine3.22
ARG NGINX_IMAGE=nginx:1.28.0-alpine3.21

FROM ${NODE_IMAGE} AS deps-patient
WORKDIR /src
COPY patient-web/package.json patient-web/package-lock.json ./
RUN npm ci --no-audit --no-fund

FROM ${NODE_IMAGE} AS deps-staff
WORKDIR /src
COPY web/package.json web/package-lock.json ./
RUN npm ci --no-audit --no-fund

# vite.config.ts bắt buộc API_TARGET (chỉ dùng cho proxy của dev server); bản build không gọi tới địa chỉ này.
# VITE_API_BASE_URL rỗng: gọi API cùng origin qua nginx.
FROM deps-patient AS build-patient
COPY patient-web/ ./
ENV API_TARGET=http://unused-at-build VITE_API_BASE_URL=
RUN npm run build

FROM deps-staff AS build-staff
ARG PATIENT_PORTAL_URL
COPY web/ ./
ENV API_TARGET=http://unused-at-build VITE_API_BASE_URL= VITE_PATIENT_PORTAL_URL=${PATIENT_PORTAL_URL}
RUN test -n "$VITE_PATIENT_PORTAL_URL" || (echo "Thiếu --build-arg PATIENT_PORTAL_URL" >&2; exit 1)
RUN npm run build

FROM ${NGINX_IMAGE} AS web
# Image nginx chạy envsubst cho /etc/nginx/templates/*.template vào /etc/nginx/conf.d lúc khởi động.
RUN rm -f /etc/nginx/conf.d/default.conf
COPY --from=nginx templates/ /etc/nginx/templates/
COPY --from=nginx snippets/ /etc/nginx/snippets/
COPY --from=build-patient /src/dist/ /usr/share/nginx/html/patient/
COPY --from=build-staff /src/dist/ /usr/share/nginx/html/staff/
# Chỉ thay hai biến này; biến của nginx ($host, $uri...) giữ nguyên.
ENV NGINX_ENVSUBST_FILTER="^(PUBLIC_HOST|STAFF_HOST)$"
EXPOSE 80
HEALTHCHECK --interval=15s --timeout=3s --retries=3 CMD wget -q -O /dev/null http://127.0.0.1/healthz || exit 1
