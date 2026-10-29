# CLA-GOV-001 — Đưa Claude Code vào bộ quy chuẩn Smart Healthcare

## Ủy quyền của trưởng nhóm (chép nguyên văn)
Tôi là trưởng nhóm và chủ repo. Riêng trong task này, tôi cho phép Claude Code sửa các file hiện thuộc Codex theo docs/ai/ownership.md: AGENTS.md, GEMINI.md, README.md, .gitignore, .agents/**, docs/ai/**. Mục đích là đổi owner và cập nhật quy chuẩn theo GOV-06. Quyền này hết khi task kết thúc. Không sửa backend/**, frontend/** hay code ứng dụng.

Bổ sung ngày 29/10/2026 (trưởng nhóm duyệt trong chat, quyết định A): commit nguyên văn hai file nguồn v2.1 vào docs/business/ và docs/architecture/ trên nhánh này.

## Trường GOV-03
- task_id: CLA-GOV-001
- owner_agent: claude
- owner_member: Phạm Hoà (Ellie3125, trưởng nhóm)
- base_commit: 3d7030cc192ed17eddf8c94fbc9b8ee146fd4b5d
- branch: ellie3125/claude-governance (đã gỡ upstream origin/main để tránh push nhầm)
- requirement_ids: GOV-02, GOV-06; Phạm vi nghiệp vụ v2.1 §2, §7; Giải pháp kỹ thuật v2.1 §8, §11
- allowed_paths: AGENTS.md, GEMINI.md, README.md, .gitignore, .agents/**, docs/ai/**, CLAUDE.md, .claude/**, docs/tasks/claude/**, docs/sessions/claude/**, docs/evidence/claude/**, docs/handoffs/claude-to-codex/**, docs/business/pham-vi-nghiep-vu-v2.1.md, docs/architecture/giai-phap-ky-thuat-v2.1.md
- contract_version: không áp dụng (task quy chuẩn, không đổi API)
- status: In review
- depends_on: hai file nguồn v2.1 do trưởng nhóm cung cấp
- next_action: Trưởng nhóm review nhánh ellie3125/claude-governance rồi đưa lên dev/main theo GOV-05; Codex xử lý docs/handoffs/claude-to-codex/CLA-GOV-001.md.

## Quyết định đã duyệt (Bước 0)
- A. Commit hai file v2.1 nguyên văn.
- B. Một docker-compose.yml cho laptop và VPS, chỉ khác .env; REL-02 tách môi trường bằng .env/secrets/volume/bucket/Access riêng.
- C. App Expo ở mobile/**, owner Antigravity.
- D. Sửa đoạn cấu trúc Backend theo v2.1 §8.2.2; code giữ nguyên, chuyển đổi qua handoff Codex.
- E. messaging-rabbitmq.md → background-jobs-outbox.md; object-storage.md giữ tên.
- Đề xuất đổi ml-service → chatbot-service chỉ ghi nhận, chưa áp dụng.

## Acceptance criteria
- [x] ownership.md chuyển chatbot, Cloudflare Access/Tunnel, docs/security, CLAUDE.md, .claude/** và hồ sơ claude sang Claude Code; thêm handoff ngược chiều.
- [x] Instructions không còn luật công nghệ trái v2.1; MinIO/RabbitMQ/VNPAY/CatBoost/LightGBM chỉ còn ở dạng ghi lịch sử hoặc câu kiểm.
- [x] chatbot-langchain-ollama.md (BOT-01..10) thay ml-training-serving.md; claude-scope.md (OWN-07..09); không trùng rule ID.
- [x] CLAUDE.md dưới 80 dòng, dòng đầu `@AGENTS.md`; .claude/rules có paths; .claude/settings.json hợp lệ; .claude/settings.local.json trong .gitignore.
- [x] Năm skill mới và hai skill trỏ; skill-manifest.md cập nhật.
- [x] Handoff claude-to-codex cho phần thuộc Codex.
- [ ] Trưởng nhóm review và chấp nhận.

## Kiểm chứng
Xem docs/sessions/claude/CLA-GOV-001-20261029-01.md và docs/evidence/claude/CLA-GOV-001-selfcheck.md.
