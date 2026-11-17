#!/usr/bin/env bash
# Triển khai một bản lên VPS Staging (Giải pháp kỹ thuật v2.13 §8.6). Chạy trên VPS, trong bản checkout của repo (cần compose.*.yaml
# và infrastructure/sqlserver/*.sql), sau khi đã cập nhật 4 dòng *_IMAGE trong .env.staging bằng digest từ scripts/build-images.sh:
#   scripts/deploy-staging.sh            # cập nhật bản mới
#   SEED=1 scripts/deploy-staging.sh     # lần đầu: thêm dịch vụ CLS và bảng giá giả lập
#   ENV_FILE=<file> SKIP_TUNNEL=1 ...    # dùng file env khác, không bật cloudflared
#   SKIP_PULL=1 ...                      # dùng image có sẵn trên máy, không kéo từ registry
# Thứ tự: kéo image → SQL Server, Redis → tạo DB/login (an toàn khi chạy lại) → migration → Api, Worker, nginx → cloudflared.
set -euo pipefail

cd "$(dirname "$0")/.."
ENV_FILE="${ENV_FILE:-.env.staging}"
[ -f "$ENV_FILE" ] || { echo "Không thấy $ENV_FILE (mẫu: .env.staging.example)." >&2; exit 1; }

dc() { docker compose --env-file "$ENV_FILE" -f compose.yaml -f compose.staging.yaml "$@"; }

dc --profile release --profile seed config --quiet
# SKIP_PULL=1: image đã có sẵn trên máy (diễn tập bằng image build cục bộ).
if [ "${SKIP_PULL:-0}" != 1 ]; then
  echo "== kéo image"
  dc --profile release --profile seed pull
fi
echo "== SQL Server, Redis"
dc up -d --wait sqlserver redis
echo "== tạo DB và login runtime"
dc --profile release run --rm db-init
echo "== migration"
dc --profile release run --rm migrator
echo "== Api, Worker, nginx"
dc up -d --wait api worker web
# Api mới có IP mới; nginx tự phân giải lại sau tối đa 10 giây, reload để nối ngay.
dc exec -T web nginx -s reload
# SKIP_TUNNEL=1: chưa tạo tunnel hoặc đang diễn tập trên máy (kiểm qua `docker compose exec web`).
if [ "${SKIP_TUNNEL:-0}" != 1 ]; then
  echo "== Cloudflare Tunnel"
  dc up -d cloudflared
fi

if [ "${SEED:-0}" = 1 ]; then
  echo "== dữ liệu giả lập (chờ Api seed cấp bậc và phòng)"
  dc --profile seed run --rm db-seed
fi

dc ps
echo "Xong. Kiểm tra: https://<PUBLIC_HOST>/ và https://<STAFF_HOST>/ (qua Cloudflare Access)."
