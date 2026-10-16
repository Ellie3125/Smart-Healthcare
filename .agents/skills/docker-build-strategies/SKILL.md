---
name: docker-build-strategies
description: "Viết, review hoặc tối ưu Dockerfile của Backend/ML trong Smart Healthcare; dành cho Codex."
license: "Complete terms in LICENSE.md"
---

# Build image Smart Healthcare

1. Đọc linux-docker-compose.md, scope Codex và source của component. Chốt .NET/Python/base image tương thích và license/CPU/tài nguyên từ tài liệu chính thức; không tự lấy version ví dụ làm mặc định.
2. Viết multi-stage build, tách SDK/build khỏi runtime; cache dependency theo manifest/lockfile trước copy source. Dùng exec-form ENTRYPOINT/CMD và working directory rõ.
3. Runtime non-root với quyền ghi tối thiểu; chọn base phù hợp .NET/native ML libraries. Không ép Alpine/distroless nếu chưa kiểm dependency và healthcheck.
4. .dockerignore loại secrets, source không cần thiết, cache/build output, .git, docs và .agents khỏi build context/runtime; giữ notices/license cần phân phối. Không sửa frontend/**; UI artifact do Antigravity bàn giao.
5. Credential chỉ qua cơ chế build secret/SSH phù hợp nếu cần, không ARG/ENV build, COPY, layer hoặc log; xác minh SSH host key. Pin dependency/image đã review, không latest.
6. Build một image Backend có version/digest xác định; hai instance dùng cùng image. Không huấn luyện ML lại trong mỗi request hoặc mỗi lần deploy Backend.
7. Kiểm build exit code, image user/entrypoint/size/dependency và API thật. Build thành công không tự chứng minh application ready. Ghi evidence và giới hạn.

Khi có Dockerfile thật, thực hiện build và inspect theo công cụ Docker của dự án; ghi kết quả thực tế. Việc đọc skill không chứng minh image đã build hoặc API đã hoạt động.

Nguồn: https://github.com/docker/skills/tree/f79172733725e2fd236343ee7b963c446ed85d09/skills/docker-build-strategies. Bản hướng dẫn đã được biên tập cho Smart Healthcare, ngày 16/10/2026; giữ license tác giả trong LICENSE.md.
