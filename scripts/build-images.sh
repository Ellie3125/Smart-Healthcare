#!/usr/bin/env bash
# Build và push 4 image (api, worker, migrator, web) cho Staging/Prod, in digest để dán vào .env.staging (OPS-02: VPS chỉ pull theo
# digest, không build source). Chạy trên laptop (Git Bash/Linux/macOS) ở bất kỳ thư mục nào trong repo:
#   REGISTRY=ghcr.io/<owner> PATIENT_PORTAL_URL=https://<PUBLIC_HOST> scripts/build-images.sh
# Cần đăng nhập registry trước (ví dụ: docker login ghcr.io). TAG mặc định là commit hiện tại; working tree phải sạch
# để image khớp đúng commit (ALLOW_DIRTY=1 để bỏ qua khi thử).
set -euo pipefail

: "${REGISTRY:?Đặt REGISTRY, ví dụ ghcr.io/<owner>}"
: "${PATIENT_PORTAL_URL:?Đặt PATIENT_PORTAL_URL, ví dụ https://staging.smarthealthcare.io.vn (liên kết ở trang đăng nhập nhân viên)}"

cd "$(git rev-parse --show-toplevel)"
if [ -n "$(git status --porcelain -- backend frontend infrastructure)" ] && [ "${ALLOW_DIRTY:-0}" != 1 ]; then
  echo "Có thay đổi chưa commit trong backend/, frontend/ hoặc infrastructure/; commit trước (hoặc ALLOW_DIRTY=1)." >&2
  exit 1
fi

TAG="${TAG:-$(git rev-parse --short=12 HEAD)}"
PREFIX="${REGISTRY%/}/smart-healthcare"

for target in api worker migrator; do
  echo "== build $target"
  docker build -f infrastructure/docker/backend.Dockerfile --target "$target" -t "$PREFIX/$target:$TAG" backend
done
echo "== build web"
docker build -f infrastructure/docker/frontend.Dockerfile --build-context nginx=infrastructure/nginx \
  --build-arg PATIENT_PORTAL_URL="$PATIENT_PORTAL_URL" -t "$PREFIX/web:$TAG" frontend

out="release-$TAG.env"
: > "$out"
for name in api worker migrator web; do
  docker push "$PREFIX/$name:$TAG"
  digest=$(docker inspect --format '{{index .RepoDigests 0}}' "$PREFIX/$name:$TAG")
  var="$(echo "$name" | tr '[:lower:]' '[:upper:]')_IMAGE"
  echo "$var=$digest" >> "$out"
done

echo
echo "Digest (đã ghi vào $out; chép 4 dòng này vào .env.staging trên VPS):"
cat "$out"
