# CLA-GOV-001 — Evidence tự kiểm bộ quy chuẩn

- Ngày: 29/10/2026, Windows 11, Git Bash, Python (chạy với `-I`).
- cwd: D:\ĐA\Smart-Healthcare, branch ellie3125/claude-governance, HEAD 44eeee0 (trước commit hồ sơ Bước 5).

## 1. Đường dẫn được tham chiếu, rule ID, settings.json
Command: `python -I <scratchpad>/check_refs.py .` — exit 0.
```
rule_ids 104 dupes []
settings.json valid
```
Không có dòng MISSING. Kiểm âm tính: thêm tạm `docs/ai/instructions/khong-ton-tai.md` vào CLAUDE.md → script in hai dòng MISSING và exit 1; đã khôi phục bằng `git checkout -- CLAUDE.md`.

Script đã dùng:
```python
import json, re, sys, collections
from pathlib import Path
root = Path(sys.argv[1])
files = [root / p for p in ("AGENTS.md", "GEMINI.md", "CLAUDE.md")]
files += list((root / "docs/ai").rglob("*.md")) + list((root / ".agents").rglob("SKILL.md"))
files += list((root / ".agents/rules").glob("*.md")) + list((root / ".claude").rglob("*.md"))
instr = {p.name for p in (root / "docs/ai/instructions").glob("*.md")}
path_re = re.compile(r"(?:\.agents|\.claude|docs/ai|docs/business|docs/architecture)/[\w./-]+\.(?:md|json)")
name_re = re.compile(r"\b([a-z0-9]+(?:-[a-z0-9]+)+\.md)\b")
missing = set()
for f in files:
    text = f.read_text(encoding="utf-8")
    for m in path_re.findall(text):
        if "<" not in m and not (root / m).exists():
            missing.add(f"{f.relative_to(root)} -> {m}")
    for m in name_re.findall(text):
        if m not in instr and not list(root.rglob(m)) and "-v2.1" not in m:
            missing.add(f"{f.relative_to(root)} -> {m}")
for line in sorted(missing):
    print("MISSING", line)
ids = collections.Counter(re.findall(r"\*\*([A-Z]+-\d+)\*\*", "".join(p.read_text(encoding="utf-8") for p in (root / "docs/ai/instructions").glob("*.md"))))
dupes = [k for k, v in ids.items() if v > 1]
print("rule_ids", len(ids), "dupes", dupes)
json.loads((root / ".claude/settings.json").read_text(encoding="utf-8"))
print("settings.json valid")
sys.exit(1 if missing or dupes else 0)
```
Giới hạn: chỉ kiểm đường dẫn .agents/.claude/docs/ai/docs/business/docs/architecture và tên file dạng kebab-case `.md`; đường dẫn ứng dụng tương lai (compose.yaml, compose.*.yaml, mobile/**) là dòng owner, không phải tham chiếu bắt buộc tồn tại.

## 2. Luật công nghệ trái v2.1
Command: `grep -rnE "MinIO|RabbitMQ|VNPAY|CatBoost|LightGBM" --include=*.md --include=*.json . | grep -vE "^\./docs/(business|architecture)/"`
Còn hai dòng, đều hợp lệ:
- docs/ai/check_governance.md:7 — câu kiểm liệt kê các từ chỉ được còn ở phần lịch sử.
- docs/ai/instructions/object-storage.md:10 — trích ý v2.1 §8 "không lấy bài học MinIO làm quyết định đổi kho".

## 3. Scope diff so với base
Command: `git diff --name-status -M 3d7030c..HEAD` rồi lọc đường dẫn ngoài allowed_paths → không có. Không đụng backend/**, frontend/**, infrastructure/**, ml-service/** hay code ứng dụng.

## Chưa kiểm chứng
- Claude Code thực sự nạp CLAUDE.md, .claude/rules (paths) và .claude/skills trong một phiên mới: Not run.
- Hiệu lực deny/allow của .claude/settings.json trong phiên thật: Not run (mới kiểm JSON hợp lệ và cú pháp theo tài liệu code.claude.com/docs/en/permissions).
- Build/test ứng dụng: không áp dụng, chưa có source ứng dụng.
