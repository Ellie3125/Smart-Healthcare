#!/usr/bin/env bash
set -e

echo "=== 1. Chuyen sang nhanh feat/identity-patients-v2 ==="
git switch feat/identity-patients-v2 || git switch -c feat/identity-patients-v2 origin/dev

echo "=== 2. Go bo node_modules va cac file rac khoi Git index ==="
git rm -r --cached frontend/web/node_modules 2>/dev/null || true
git rm --cached frontend/web/tsconfig.tsbuildinfo 2>/dev/null || true
git rm -r --cached legacy-ui 2>/dev/null || true

echo "=== 3. Kiem tra va dam bao .gitignore ==="
git add .gitignore frontend/web/.gitignore 2>/dev/null || true

echo "=== 4. Re-commit de loai bo node_modules khoi commit cu ==="
git add -A
git commit --amend -m "feat(identity): đăng nhập JWT, phân quyền và khung web nhân viên"

echo "=== 5. Force push len origin/feat/identity-patients-v2 ==="
git push -f origin feat/identity-patients-v2

echo "=== 6. Kiem tra log commit (khong duoc co node_modules) ==="
git log -1 --stat
echo "=== HOAN TAT THANH CONG! ==="

