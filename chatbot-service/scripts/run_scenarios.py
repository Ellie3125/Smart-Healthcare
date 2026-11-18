"""Chạy toàn bộ bộ kịch bản với model thật (Giải pháp kỹ thuật §11.2) và lưu kết quả:

    OLLAMA_BASE_URL=https://<static domain ngrok> OLLAMA_API_KEY=<khóa> OLLAMA_MODEL=qwen2.5:3b \
        uv run python scripts/run_scenarios.py [--only safety] [--note "ghi chú"]

Chạy lại toàn bộ khi đổi tag model, system prompt, bảng intent, bảng gợi ý chuyên khoa hoặc mô tả công cụ. Mỗi lần chạy ghi
tests/scenarios/results/<thời điểm>_<model>.json: tag model, phiên bản cấu hình và kịch bản, mã băm chỉ dẫn hệ thống, kết quả
từng lượt (đạt/lỗi, thời gian). Dùng .NET giả và ngày cố định (tests/fake_dotnet.py), nên không cần Api hay dữ liệu thật.
Lỗi đầu tiên của một kịch bản dừng kịch bản đó. Thoát mã 1 nếu có kịch bản không đạt.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
import tempfile
from collections import Counter
from datetime import UTC, datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))

import httpx  # noqa: E402
from fastapi.testclient import TestClient  # noqa: E402

from app.agent.prompt import SYSTEM_PROMPT  # noqa: E402
from app.config_tables import load_tables  # noqa: E402
from app.main import create_app  # noqa: E402
from app.settings import Settings  # noqa: E402
from tests.fake_dotnet import KEY, NOW, FakeDotnet  # noqa: E402
from tests.scenario_runner import load, run, versions  # noqa: E402

RESULTS = ROOT / "tests" / "scenarios" / "results"


def main() -> int:
    parser = argparse.ArgumentParser(description="Chạy bộ kịch bản với model thật")
    parser.add_argument("--only", choices=["scenarios", "safety"], help="chỉ chạy một bộ")
    parser.add_argument("--note", default="", help="ghi chú lưu kèm kết quả (ví dụ: Colab T4, prompt v3)")
    args = parser.parse_args()

    env = Settings(chatbot_internal_key=KEY)
    if not env.ollama_base_url:
        sys.exit("Thiếu OLLAMA_BASE_URL.")
    model_base = env.ollama_base_url.rstrip("/")

    def model_passthrough(request: httpx.Request) -> httpx.Response:
        # /health của dịch vụ đi qua transport giả; chuyển tiếp thật tới Ollama (agent gọi Ollama bằng client riêng).
        real = httpx.get(f"{model_base}{request.url.path}", headers={"x-api-key": request.headers.get("x-api-key", "")}, timeout=10)
        return httpx.Response(real.status_code, content=real.content, headers={"content-type": real.headers.get("content-type", "")})

    scenarios = [s for s in load() if not args.only or s.suite == args.only]
    results = []
    with tempfile.TemporaryDirectory() as data_dir:
        settings = env.model_copy(update={"data_dir": Path(data_dir), "dotnet_api_base_url": "http://api.example.test",
                                          "health_cache_seconds": 0})
        for scenario in scenarios:
            fake = FakeDotnet(model_handler=model_passthrough)
            with TestClient(create_app(settings, transport=httpx.MockTransport(fake), clock=lambda: NOW)) as client:
                for r in run(client, fake, scenario, model_available=True):
                    mark = "ĐẠT " if r.passed else "LỖI "
                    print(f"{mark} {scenario.suite}:{scenario.id} lượt {r.turn} ({r.route}, {r.ms} ms) «{r.text}»")
                    for problem in r.problems:
                        print(f"       - {problem}")
                    if not r.passed and r.reply:
                        print(f"       trả lời: {r.reply[:200]!r}")
                    results.append({"suite": scenario.suite, "scenario": scenario.id, "turn": r.turn, "text": r.text,
                                    "route": r.route, "status": r.status, "ms": r.ms, "passed": r.passed,
                                    "problems": r.problems, "reply": r.reply})

    failed = sorted({(r["suite"], r["scenario"]) for r in results if not r["passed"]})
    model_turns = [r["ms"] for r in results if r["route"] == "MODEL"]
    summary = {
        "ran_at_utc": datetime.now(UTC).isoformat(timespec="seconds"),
        "model": settings.ollama_model,
        "note": args.note,
        "config_version": load_tables(settings.config_dir).version,
        "scenario_version": versions(),
        "system_prompt_sha256": hashlib.sha256(SYSTEM_PROMPT.encode()).hexdigest()[:16],
        "scenarios": len(scenarios),
        "scenarios_failed": [f"{s}:{i}" for s, i in failed],
        "turns": dict(Counter("passed" if r["passed"] else "failed" for r in results)),
        "model_turn_ms": {"count": len(model_turns), "max": max(model_turns, default=0),
                          "median": sorted(model_turns)[len(model_turns) // 2] if model_turns else 0},
        "results": results,
    }
    RESULTS.mkdir(parents=True, exist_ok=True)
    out = RESULTS / f"{datetime.now(UTC):%Y%m%dT%H%M%SZ}_{settings.ollama_model.replace(':', '-')}.json"
    out.write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")

    print(f"\n{len(scenarios) - len(failed)}/{len(scenarios)} kịch bản đạt; lượt cần model: {summary['model_turn_ms']}")
    print(f"Kết quả: {out.relative_to(ROOT)}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
