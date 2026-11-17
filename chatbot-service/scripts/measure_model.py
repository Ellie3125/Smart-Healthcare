"""Đo model trước khi chốt tag (Giải pháp kỹ thuật §11.7.3). Chạy từ máy sẽ gọi model (VPS Staging, hoặc máy dev):

    OLLAMA_BASE_URL=https://<static domain ngrok> OLLAMA_API_KEY=<khóa> OLLAMA_MODEL=qwen2.5:3b \
        uv run python scripts/measure_model.py --runs 10

Đo hai thứ, đúng như agent gọi model (cùng chỉ dẫn hệ thống, cùng định nghĩa công cụ, cùng header x-api-key):
  1. Tốc độ sinh (eval rate) qua /api/generate: khoảng 8 token/giây trở lên là dùng được cho demo.
  2. Thời gian một lần model chọn công cụ cho câu tự do, p50/p95: phải dưới OLLAMA_TIMEOUT_SECONDS và dưới khoảng 100 giây
     (giới hạn một request qua Cloudflare). Agent dùng return_direct nên mỗi lượt có công cụ chỉ gọi model một lần.
Không gọi .NET, không cần dữ liệu thật. Không in khóa.
"""

from __future__ import annotations

import argparse
import asyncio
import statistics
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import httpx  # noqa: E402
from langchain_core.messages import HumanMessage, SystemMessage  # noqa: E402

from app.agent.model import build_model  # noqa: E402
from app.agent.prompt import SYSTEM_PROMPT  # noqa: E402
from app.agent.tools import build_tools  # noqa: E402
from app.config_tables import load_tables  # noqa: E402
from app.settings import Settings  # noqa: E402

QUESTIONS = [
    "có bác sĩ nào rảnh vào thứ sáu này không nhỉ",
    "mấy cái hẹn khám của mình ra sao rồi",
    "mình trả tiền khám rồi đúng không",
    "cho mình cái mã để đi khám",
    "tuần sau còn ai khám tim không",
]


async def eval_rate(settings: Settings) -> float:
    async with httpx.AsyncClient(timeout=settings.ollama_timeout_seconds) as client:
        response = await client.post(
            f"{settings.ollama_base_url.rstrip('/')}/api/generate",
            headers={"x-api-key": settings.ollama_api_key.get_secret_value()},
            json={"model": settings.ollama_model, "prompt": "Đặt lịch khám bác sĩ Nam ngày mai", "stream": False,
                  "options": {"num_predict": 60}},
        )
        response.raise_for_status()
        body = response.json()
    return body["eval_count"] / (body["eval_duration"] / 1e9)


async def tool_choice_latency(settings: Settings, runs: int) -> list[tuple[float, str]]:
    tables = load_tables(settings.config_dir)
    tools = build_tools(tables, handler=None)  # chỉ dùng định nghĩa công cụ để gửi cho model, không chạy công cụ
    model = build_model(settings).bind_tools(tools)
    results = []
    for i in range(runs):
        question = QUESTIONS[i % len(QUESTIONS)]
        started = time.perf_counter()
        message = await model.ainvoke([SystemMessage(SYSTEM_PROMPT), HumanMessage(question)])
        elapsed = time.perf_counter() - started
        chosen = ", ".join(c["name"] for c in message.tool_calls) or "(không gọi công cụ)"
        results.append((elapsed, chosen))
        print(f"  {elapsed:6.1f}s  {chosen:<24} ← {question}")
    return results


async def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--runs", type=int, default=10)
    args = parser.parse_args()
    settings = Settings(chatbot_internal_key="x" * 16)  # khóa nội bộ không dùng ở đây
    if not settings.ollama_base_url:
        sys.exit("Thiếu OLLAMA_BASE_URL.")

    print(f"Model {settings.ollama_model}; timeout {settings.ollama_timeout_seconds:.0f}s")
    rate = await eval_rate(settings)
    print(f"1. Tốc độ sinh: {rate:.1f} token/giây ({'đạt' if rate >= 8 else 'CHẬM'}, ngưỡng 8)")

    print("2. Thời gian chọn công cụ:")
    await tool_choice_latency(settings, 1)  # làm nóng: nạp model và phần đầu prompt cố định
    timings = [t for t, _ in await tool_choice_latency(settings, args.runs)]
    p50 = statistics.median(timings)
    p95 = statistics.quantiles(timings, n=20)[18] if len(timings) >= 2 else timings[0]
    limit = min(settings.ollama_timeout_seconds, 100)
    print(f"   p50 {p50:.1f}s, p95 {p95:.1f}s ({'đạt' if p95 < limit else 'VƯỢT'} giới hạn {limit:.0f}s)")


if __name__ == "__main__":
    asyncio.run(main())
