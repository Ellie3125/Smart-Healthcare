"""Agent LangChain với model giả theo kịch bản (không cần Ollama): đường đi của công cụ, bộ nhớ, lỗi và dữ liệu gửi tới model."""

from __future__ import annotations

import re
import uuid
from typing import Any

import httpx
import pytest
from fastapi.testclient import TestClient
from langchain_core.language_models import BaseChatModel
from langchain_core.messages import AIMessage, BaseMessage, HumanMessage, ToolMessage
from langchain_core.outputs import ChatGeneration, ChatResult

from app.agent.memory import sanitize, window
from app.main import create_app
from app.settings import Settings
from tests.conftest import CONFIG_DIR
from tests.unit.test_tools_api import HEADERS, KEY, NOW, PATIENT, TOKEN, USER, FakeDotnet

UUID_RE = re.compile(r"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}")


class ScriptedModel(BaseChatModel):
    """Trả lần lượt các AIMessage cho trước; ghi lại mọi danh sách tin nhắn đã nhận."""

    script: list[Any]
    seen: list[list[BaseMessage]] = []

    @property
    def _llm_type(self) -> str:
        return "scripted"

    def bind_tools(self, tools: Any, **kwargs: Any) -> ScriptedModel:
        return self

    def _generate(self, messages: list[BaseMessage], stop: Any = None, run_manager: Any = None, **kwargs: Any) -> ChatResult:
        self.seen.append(list(messages))
        step = self.script.pop(0)
        if isinstance(step, Exception):
            raise step
        return ChatResult(generations=[ChatGeneration(message=step)])


def call(name: str, **args: Any) -> AIMessage:
    return AIMessage(content="", tool_calls=[{"name": name, "args": args, "id": f"call-{uuid.uuid4().hex[:8]}"}])


class ModelUp(FakeDotnet):
    """.NET giả của test_tools_api, thêm Ollama báo có tag đã ghim để /health = available."""

    def __call__(self, request: httpx.Request) -> httpx.Response:
        if request.url.host == "model.example.test":
            return httpx.Response(200, json={"models": [{"name": "qwen2.5:3b"}]})
        return super().__call__(request)


def app_with(tmp_path, model: ScriptedModel, fake: FakeDotnet | None = None) -> TestClient:
    settings = Settings(chatbot_internal_key=KEY, data_dir=tmp_path, config_dir=CONFIG_DIR, health_cache_seconds=0,
                        ollama_base_url="https://model.example.test", ollama_api_key="k",
                        dotnet_api_base_url="http://api.example.test")
    return TestClient(create_app(settings, transport=httpx.MockTransport(fake or ModelUp()), clock=lambda: NOW, model=model))


def say(c: TestClient, conversation: str, text: str) -> httpx.Response:
    body = {"conversationId": conversation, "userId": USER, "patientId": PATIENT, "messageId": str(uuid.uuid4()), "text": text}
    return c.post("/v1/turns", json=body, headers=HEADERS)


FREE_TEXT = "có bác sĩ nào rảnh vào thứ sáu này không nhỉ"  # không khớp đường tắt nào: tới model


def test_tool_answer_is_the_template_reply_and_model_is_called_once(tmp_path):
    conversation = str(uuid.uuid4())
    model = ScriptedModel(script=[
        call("search_doctors", date_text="thứ sáu này", specialty="da liễu"),
        AIMessage(content="Bác sĩ thứ nhất là BS Trần Thị Da, phí 150.000 VND."),
    ], seen=[])
    with app_with(tmp_path, model) as c:
        first = say(c, conversation, FREE_TEXT)
        follow_up = say(c, conversation, "bác sĩ thứ nhất phí bao nhiêu")

    body = first.json()
    assert first.status_code == 200
    # return_direct: câu mẫu của công cụ, không qua model lần hai (Giải pháp §11.3.5).
    assert body["reply"].startswith("Các bác sĩ còn suất ngày 20/11/2026 ở Khoa Da liễu")
    assert body["intent"] == "SEARCH_SLOTS"
    assert [o["kind"] for o in body["options"]] == ["Doctor"] and body["options"][0]["serviceDate"] == "2026-11-20"
    assert len(model.seen) == 2  # mỗi lượt một lần gọi model
    assert follow_up.json()["reply"] == "Bác sĩ thứ nhất là BS Trần Thị Da, phí 150.000 VND."

    # Lượt sau model đọc kết quả cũ dưới dạng dữ kiện có nhãn trường.
    tool_result = next(m for m in model.seen[1] if isinstance(m, ToolMessage)).content
    assert "1. Tên: BS Trần Thị Da | Cấp bậc: Bác sĩ Chuyên khoa I | Khoa: Khoa Da liễu | Còn: 12 suất | Phí: 150.000 VND" in tool_result
    sent = "\n".join(str(m.content) for messages in model.seen for m in messages)
    assert TOKEN not in sent and PATIENT not in sent and USER not in sent
    assert not UUID_RE.search(sent)  # không Id nào (bác sĩ, lịch hẹn, cuộc trò chuyện) tới model


def test_unparseable_date_asks_again_and_the_answer_continues_without_model(tmp_path):
    conversation = str(uuid.uuid4())
    model = ScriptedModel(script=[call("search_doctors", date_text="hôm nào đó", specialty="da liễu")], seen=[])
    fake = ModelUp()
    with app_with(tmp_path, model, fake) as c:
        ask = say(c, conversation, FREE_TEXT).json()
        assert not any(r.url.path.endswith("/doctors") for r in fake.requests)  # không đoán ngày
        answer = say(c, conversation, "thứ sáu").json()
    assert ask["reply"].startswith("Bạn muốn khám ngày nào ở Khoa Da liễu?") and ask["options"] == []
    assert len(model.seen) == 1  # câu trả lời ngày đi đường tắt nhờ State đang hỏi ngày
    assert answer["options"][0]["serviceDate"] == "2026-11-20"


def test_empty_model_answer_gets_not_understood_with_suggestions(tmp_path):
    model = ScriptedModel(script=[AIMessage(content="")], seen=[])
    with app_with(tmp_path, model) as c:
        body = say(c, str(uuid.uuid4()), "abc xyz gì đó").json()
    assert body["reply"].startswith("Mình chưa hiểu ý bạn")
    assert body["suggestions"] == ["SEARCH_SLOTS", "VIEW_APPOINTMENTS", "SUGGEST_SPECIALTY"]


def test_conversation_memory_persists_and_is_deleted_with_conversation(tmp_path):
    conversation = str(uuid.uuid4())
    model = ScriptedModel(script=[
        AIMessage(content="Bạn muốn khám ngày nào?"),
        AIMessage(content="Mình sẽ tìm giúp."),
        AIMessage(content="Chào bạn."),
    ], seen=[])
    with app_with(tmp_path, model) as c:
        say(c, conversation, "bác sĩ nào giỏi nhất vậy")
        say(c, conversation, "bác sĩ đó còn lịch không")
        c.request("DELETE", f"/v1/conversations/{conversation}", json={"userId": USER}, headers=HEADERS)
        say(c, conversation, "bác sĩ nào còn lịch")

    second = [m.content for m in model.seen[1] if isinstance(m, HumanMessage)]
    assert second == ["bác sĩ nào giỏi nhất vậy", "bác sĩ đó còn lịch không"]  # nhớ lượt trước
    third = [m.content for m in model.seen[2] if isinstance(m, HumanMessage)]
    assert third == ["bác sĩ nào còn lịch"]  # đã xóa cùng cuộc trò chuyện


def test_model_failure_is_unavailable_and_next_turn_still_works(tmp_path):
    conversation = str(uuid.uuid4())
    model = ScriptedModel(script=[ConnectionError("ngrok offline"), AIMessage(content="Mình đây.")], seen=[])
    with app_with(tmp_path, model) as c:
        failed = say(c, conversation, FREE_TEXT)
        ok = say(c, conversation, "bác sĩ nào còn lịch")
    assert (failed.status_code, failed.json()["code"]) == (503, "ChatbotUnavailable")
    assert ok.status_code == 200 and ok.json()["reply"] == "Mình đây."


def test_expired_token_inside_tool_is_session_expired_and_history_stays_valid(tmp_path):
    conversation = str(uuid.uuid4())
    model = ScriptedModel(script=[call("list_my_appointments"), AIMessage(content="Mình đây.")], seen=[])
    fake = ModelUp(fail={"/api/scheduling/patient/appointments": 401})
    with app_with(tmp_path, model, fake) as c:
        expired = say(c, conversation, "mấy cái hẹn khám của mình ra sao rồi")
        again = say(c, conversation, "bác sĩ nào còn lịch")
    assert (expired.status_code, expired.json()["code"]) == (401, "SessionExpired")
    assert again.status_code == 200
    assert sanitize(model.seen[1]) == model.seen[1]  # không còn lời gọi công cụ dở dang


def test_guide_change_tool_never_calls_change_api(tmp_path):
    model = ScriptedModel(script=[call("guide_change", action="cancel"), AIMessage(content="Bạn hủy trong mục Lịch của tôi nhé.")], seen=[])
    fake = ModelUp()
    with app_with(tmp_path, model, fake) as c:
        body = say(c, str(uuid.uuid4()), "mình muốn bỏ cái hẹn hôm thứ sáu").json()
    assert body["intent"] == "CANCEL" and [o["kind"] for o in body["options"]] == ["Appointment"]
    assert all(r.method == "GET" for r in fake.requests)


def test_window_keeps_recent_messages_starting_with_a_human_turn():
    msgs: list[BaseMessage] = []
    for i in range(6):
        msgs.append(HumanMessage(f"h{i}", id=f"h{i}"))
        tool_call = AIMessage(content="", id=f"a{i}", tool_calls=[{"name": "x", "args": {}, "id": f"c{i}"}])
        msgs += [tool_call, ToolMessage("r", tool_call_id=f"c{i}", id=f"t{i}"), AIMessage(f"done{i}", id=f"d{i}")]
    kept = window(msgs, 6)
    assert isinstance(kept[0], HumanMessage) and len(kept) <= 6
    assert kept[-1].content == "done5"


def test_sanitize_drops_unanswered_calls_and_orphan_results():
    dangling = AIMessage(content="", id="a", tool_calls=[{"name": "x", "args": {}, "id": "c1"}])
    orphan = ToolMessage("r", tool_call_id="zz", id="t")
    msgs = [HumanMessage("h", id="h"), dangling, orphan, HumanMessage("h2", id="h2")]
    assert [m.id for m in sanitize(msgs)] == ["h", "h2"]
