"""Bộ kịch bản không cần model (Giải pháp §11.2): mọi lượt đường tắt phải đạt; lượt cần model phải thật sự tới model.
Chạy lại mỗi lần pytest, nên đổi bảng intent / bảng gợi ý / câu mẫu mà làm hỏng kịch bản thì biết ngay."""

from __future__ import annotations

import httpx
import pytest
from fastapi.testclient import TestClient

from app.main import create_app
from app.settings import Settings
from tests.conftest import CONFIG_DIR
from tests.fake_dotnet import KEY, NOW, FakeDotnet
from tests.scenario_runner import load, run

SCENARIOS = load()


@pytest.mark.parametrize("scenario", SCENARIOS, ids=[f"{s.suite}:{s.id}" for s in SCENARIOS])
def test_scenario_without_model(tmp_path, scenario):
    fake = FakeDotnet()  # model tắt
    settings = Settings(chatbot_internal_key=KEY, data_dir=tmp_path, config_dir=CONFIG_DIR, health_cache_seconds=0,
                        ollama_base_url="https://model.example.test", ollama_api_key="k",
                        dotnet_api_base_url="http://api.example.test")
    with TestClient(create_app(settings, transport=httpx.MockTransport(fake), clock=lambda: NOW)) as client:
        results = run(client, fake, scenario, model_available=False)
    problems = [f"lượt {r.turn} «{r.text}»: {p}" for r in results for p in r.problems]
    assert not problems, "\n".join(problems)


def test_every_intent_has_scenarios():
    covered = {t["expect"].get("intent") for s in SCENARIOS for t in s.turns}
    for intent in ["SEARCH_SLOTS", "VIEW_APPOINTMENTS", "VIEW_TICKET", "VIEW_EXAM_PAYMENT", "VIEW_VISIT_HISTORY", "CANCEL", "RESCHEDULE",
                   "SUGGEST_SPECIALTY", "EMERGENCY", "OUT_OF_SCOPE", "HELP"]:
        assert intent in covered, f"Thiếu kịch bản cho {intent}"
    assert any(s.needs_model for s in SCENARIOS) and any(not s.needs_model for s in SCENARIOS)
