"""Nạp và chạy bộ kịch bản (tests/scenarios, tests/safety) qua API thật của dịch vụ với .NET giả. Dùng chung cho pytest (không model)
và scripts/run_scenarios.py (có model). Định dạng kịch bản: xem đầu tests/scenarios/scenarios.yaml."""

from __future__ import annotations

import re
import time
import uuid
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

import httpx
import yaml
from fastapi.testclient import TestClient

from tests.fake_dotnet import HEADERS, PATIENT, TOKEN, USER, FakeDotnet

ROOT = Path(__file__).resolve().parent
SUITES = {"scenarios": ROOT / "scenarios" / "scenarios.yaml", "safety": ROOT / "safety" / "safety.yaml"}
_UUID = re.compile(r"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")


@dataclass(frozen=True)
class Scenario:
    suite: str
    id: str
    turns: list[dict]

    @property
    def needs_model(self) -> bool:
        return any(t["expect"].get("route") == "MODEL" for t in self.turns)


@dataclass
class TurnResult:
    scenario: str
    turn: int
    text: str
    route: str
    status: int
    ms: int
    problems: list[str] = field(default_factory=list)
    reply: str = ""

    @property
    def passed(self) -> bool:
        return not self.problems


def load(suites: dict[str, Path] = SUITES) -> list[Scenario]:
    scenarios = []
    for suite, path in suites.items():
        with path.open(encoding="utf-8") as f:
            data = yaml.safe_load(f)
        for item in data["scenarios"]:
            scenarios.append(Scenario(suite, item["id"], item["turns"]))
    ids = [s.id for s in scenarios]
    duplicates = {i for i in ids if ids.count(i) > 1}
    if duplicates:
        raise ValueError(f"Trùng id kịch bản: {sorted(duplicates)}")
    return scenarios


def versions() -> str:
    parts = []
    for suite, path in SUITES.items():
        with path.open(encoding="utf-8") as f:
            parts.append(f"{suite} {yaml.safe_load(f)['version']}")
    return "; ".join(parts)


def run(client: TestClient, fake: FakeDotnet, scenario: Scenario, model_available: bool) -> list[TurnResult]:
    """Chạy một kịch bản trong một cuộc trò chuyện mới. Không có model: dừng ở lượt MODEL đầu tiên sau khi kiểm nó thật sự tới
    model (dịch vụ trả 503 ChatbotUnavailable vì model tắt)."""
    conversation = str(uuid.uuid4())
    results = []
    for index, turn in enumerate(scenario.turns, start=1):
        expect = turn["expect"]
        content = {"suggestion": turn["suggestion"]} if "suggestion" in turn else {"text": turn["say"]}
        label = turn.get("say") or f"[nút {turn.get('suggestion')}]"
        writes_before = len(fake.writes)
        started = time.perf_counter()
        response = client.post("/v1/turns", headers=HEADERS, json={
            "conversationId": conversation, "userId": USER, "patientId": PATIENT, "messageId": str(uuid.uuid4()), **content})
        ms = int((time.perf_counter() - started) * 1000)
        route = expect.get("route", "SHORTCUT")
        result = TurnResult(scenario.id, index, label, route, response.status_code, ms)

        if route == "MODEL" and not model_available:
            if response.status_code != 503:
                result.problems.append(f"phải tới model (503 khi model tắt), nhận {response.status_code}: {response.text[:120]}")
            results.append(result)
            break

        if response.status_code != 200:
            result.problems.append(f"HTTP {response.status_code}: {response.text[:160]}")
            results.append(result)
            break
        body = response.json()
        result.reply = body["reply"]
        result.problems += check(expect, body)
        if len(fake.writes) > writes_before:
            result.problems.append("chatbot đã gửi request ghi tới .NET")
        results.append(result)
    return results


def check(expect: dict[str, Any], body: dict[str, Any]) -> list[str]:
    problems = []
    reply: str = body["reply"]
    options: list[dict] = body["options"]

    if "intent" in expect and body.get("intent") != expect["intent"]:
        problems.append(f"intent {body.get('intent')} ≠ {expect['intent']}")
    for text in expect.get("reply_contains", []):
        if str(text) not in reply:
            problems.append(f"thiếu «{text}»")
    if (any_of := expect.get("reply_contains_any")) and not any(str(t) in reply for t in any_of):
        problems.append(f"không có chuỗi nào trong {any_of}")
    for text in expect.get("reply_not_contains", []):
        if str(text) in reply:
            problems.append(f"không được có «{text}»")
    if "options" in expect and len(options) != expect["options"]:
        problems.append(f"{len(options)} lựa chọn ≠ {expect['options']}")
    if (kind := expect.get("options_kind")) and (not options or any(o["kind"] != kind for o in options)):
        problems.append(f"lựa chọn phải là {kind}: {[o['kind'] for o in options]}")
    if (day := expect.get("first_option_date")) and (not options or options[0].get("serviceDate") != day):
        problems.append(f"lựa chọn đầu phải ngày {day}")
    if "option_codes" in expect and [o.get("specialtyCode") for o in options] != expect["option_codes"]:
        problems.append(f"mã chuyên khoa {[o.get('specialtyCode') for o in options]} ≠ {expect['option_codes']}")
    if "suggestions" in expect and len(body.get("suggestions", [])) != expect["suggestions"]:
        problems.append(f"{len(body.get('suggestions', []))} gợi ý ≠ {expect['suggestions']}")
    # Luôn kiểm: câu trả lời không lộ Id hay token.
    if _UUID.search(reply) or TOKEN in reply:
        problems.append("câu trả lời chứa Id hoặc token")
    return problems


def offline_model() -> httpx.MockTransport:
    return httpx.MockTransport(FakeDotnet())
