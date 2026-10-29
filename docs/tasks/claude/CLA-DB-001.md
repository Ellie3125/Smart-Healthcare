# CLA-DB-001 — Dựng database từ ERD v2.2 bằng EF Core code-first

Yêu cầu của trưởng nhóm trong chat ngày 29/10/2026: prompt "Nhiệm vụ: Dựng database Smart Healthcare từ ERD v2.2 bằng EF Core (code-first)" kèm file erd-v2.2.json; khi được hỏi về mâu thuẫn chia module, trưởng nhóm chọn "Theo ERD". Làm theo quyền giám sát OWN-10 vì backend/**, tests/backend/**, Directory.Packages.props và docs/ai/** thuộc Codex.

- task_id: CLA-DB-001
- owner_agent: claude
- owner_member: Phạm Hoà (Ellie3125, trưởng nhóm)
- base_commit: aa087a7
- branch: feat/db-erd-v2.2
- requirement_ids: MOD-01, MOD-04, DB-01, DB-03, DB-05, OWN-10; ERD v2.2 (quyết định 1B, 2A, 3B, 4B)
- allowed_paths: docs/db/**, docs/plans/db-erd-v2.2.md, backend/src/Modules/{Identity,Catalog,Scheduling,Clinical,Billing,Workflow}/{Domain,Infrastructure}/**, backend/src/Shared/Persistence/**, backend/src/Tools/DbMigrator/**, backend/SmartHealthcare.slnx, tests/backend/Database.Tests/**, Directory.Packages.props, .config/dotnet-tools.json, docs/tasks/claude/**, docs/sessions/claude/**, docs/evidence/claude/**, docs/handoffs/claude-to-codex/**
- contract_version: không áp dụng (không có API)
- acceptance_criteria: theo mục "Tiêu chí hoàn thành" trong docs/plans/db-erd-v2.2.md
- status: In progress (chờ trưởng nhóm duyệt kế hoạch)
- depends_on: không
- next_action: Trưởng nhóm duyệt docs/plans/db-erd-v2.2.md và trả lời mục "Cần trưởng nhóm xác nhận"; sau đó làm module identity.

## Quyết định trong phiên
- Chia schema theo 6 domain của ERD; lệch so với Giải pháp v2.2 §8.1 ghi trong kế hoạch và báo cáo cuối.
- Mặc định tôi đã đề xuất và trưởng nhóm chưa phản đối: EF Core 10 (khớp .NET SDK 10.0.401), dotnet-ef dạng local tool, docs/db/ và docs/plans/ coi như vùng của Codex mà Claude sửa theo OWN-10.
