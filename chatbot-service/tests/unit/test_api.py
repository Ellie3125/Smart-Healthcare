from __future__ import annotations

import uuid
from datetime import UTC, datetime, timedelta

import httpx
import pytest
from fastapi.testclient import TestClient

from app.main import create_app
from app.schemas import ChatContext, ChatState, TurnResponse
from app.settings import Settings
from tests.conftest import CONFIG_DIR

KEY = "test-internal-key-0123456789"
HEADERS = {"X-Chatbot-Key": KEY, "Authorization": "Bearer user-token"}


def settings(tmp_path, **overrides) -> Settings:
    values = {
        "chatbot_internal_key": KEY,
        "data_dir": tmp_path,
        "config_dir": CONFIG_DIR,
        "ollama_base_url": "https://model.example.test",
        "ollama_api_key": "model-key",
        "health_cache_seconds": 0,
    }
    values.update(overrides)
    return Settings(**values)


def ollama(status: int = 200, models: list[str] | None = None) -> httpx.MockTransport:
    def handler(request: httpx.Request) -> httpx.Response:
        assert request.headers["x-api-key"] == "model-key"
        if status != 200:
            return httpx.Response(status, text="<html>ngrok error</html>")
        return httpx.Response(200, json={"models": [{"name": m, "model": m} for m in (models or ["qwen2.5:3b"])]})

    return httpx.MockTransport(handler)


class EchoAgent:
    """Agent giả cho đường câu tự do (bước 7 thay bằng LangChain)."""

    def __init__(self) -> None:
        self.contexts: list[ChatContext] = []

    async def run(self, context: ChatContext, state: ChatState, text: str) -> tuple[TurnResponse, ChatState]:
        self.contexts.append(context)
        return TurnResponse(reply="model-reply", intent="SEARCH_SLOTS"), {"intent": "SEARCH_SLOTS", "stage": "COLLECTING"}


def turn_body(conversation: str, user: str, patient: str, **content) -> dict:
    return {"conversationId": conversation, "userId": user, "patientId": patient, "messageId": str(uuid.uuid4()), **content}


@pytest.fixture
def ids():
    return str(uuid.uuid4()), str(uuid.uuid4()), str(uuid.uuid4())


def test_health_reports_model_and_does_not_need_key(tmp_path):
    with TestClient(create_app(settings(tmp_path), transport=ollama())) as client:
        assert client.get("/health").json() == {"available": True, "model": "qwen2.5:3b"}


@pytest.mark.parametrize(
    ("transport", "overrides"),
    [
        (ollama(status=401), {}),  # traffic policy của ngrok từ chối
        (ollama(status=404), {}),  # trang lỗi ngrok khi Colab tắt
        (ollama(models=["llama3:8b"]), {}),  # thiếu tag đã ghim
        (ollama(), {"ollama_base_url": ""}),  # chưa cấu hình
    ],
)
def test_health_unavailable_cases(tmp_path, transport, overrides):
    with TestClient(create_app(settings(tmp_path, **overrides), transport=transport)) as client:
        assert client.get("/health").json()["available"] is False


def test_turn_requires_internal_key_and_user_token(tmp_path, ids):
    with TestClient(create_app(settings(tmp_path), transport=ollama())) as client:
        body = turn_body(*ids, text="xin chào")
        no_key = client.post("/v1/turns", json=body, headers={"Authorization": "Bearer t"})
        wrong_key = client.post("/v1/turns", json=body, headers={"X-Chatbot-Key": "wrong", "Authorization": "Bearer t"})
        no_token = client.post("/v1/turns", json=body, headers={"X-Chatbot-Key": KEY})
    assert (no_key.status_code, no_key.json()["code"]) == (401, "InvalidChatbotKey")
    assert wrong_key.status_code == 401
    assert (no_token.status_code, no_token.json()["code"]) == (401, "MissingUserToken")


@pytest.mark.parametrize(
    "content",
    [{}, {"text": "a", "suggestion": "CANCEL"}, {"text": "   "}, {"text": "x" * 1001}, {"suggestion": "DROP_TABLE"}],
)
def test_turn_input_validation(tmp_path, ids, content):
    with TestClient(create_app(settings(tmp_path), transport=ollama())) as client:
        response = client.post("/v1/turns", json=turn_body(*ids, **content), headers=HEADERS)
    assert (response.status_code, response.json()["code"]) == (400, "InvalidInput")
    assert "x" * 50 not in response.text  # không trả lại nội dung đầu vào


def test_specialty_conversation_over_two_turns(tmp_path, ids):
    with TestClient(create_app(settings(tmp_path), transport=ollama())) as client:
        ask = client.post("/v1/turns", json=turn_body(*ids, suggestion="SUGGEST_SPECIALTY"), headers=HEADERS).json()
        answer = client.post("/v1/turns", json=turn_body(*ids, text="đau bụng, ợ chua"), headers=HEADERS).json()
    assert ask["intent"] == "SUGGEST_SPECIALTY" and ask["options"] == []
    assert answer["options"][0]["kind"] == "Specialty"
    assert answer["options"][0]["specialtyCode"] == "TIEU_HOA"
    assert answer["suggestions"] == ["SEARCH_SLOTS"]
    assert answer["proposal"] is None


def test_shortcut_answers_even_when_model_is_down(tmp_path, ids):
    with TestClient(create_app(settings(tmp_path), transport=ollama(status=404))) as client:
        emergency = client.post("/v1/turns", json=turn_body(*ids, text="khó thở quá"), headers=HEADERS)
        free = client.post("/v1/turns", json=turn_body(*ids, text="bác sĩ Nam chiều nay khám không"), headers=HEADERS)
    assert emergency.status_code == 200 and emergency.json()["intent"] == "EMERGENCY"
    assert (free.status_code, free.json()["code"]) == (503, "ChatbotUnavailable")


def test_free_text_reaches_agent_with_context_from_dotnet(tmp_path, ids):
    agent = EchoAgent()
    conversation, user, patient = ids
    with TestClient(create_app(settings(tmp_path), agent=agent, transport=ollama())) as client:
        response = client.post("/v1/turns", json=turn_body(*ids, text="bác sĩ Nam chiều nay khám không"), headers=HEADERS)
    assert response.json()["reply"] == "model-reply"
    context = agent.contexts[0]
    assert (context.user_id, context.patient_id, context.conversation_id, context.access_token) == (user, patient, conversation, "user-token")
    assert "user-token" not in repr(context)


def test_free_text_without_agent_is_unavailable_even_if_model_is_up(tmp_path, ids):
    with TestClient(create_app(settings(tmp_path), transport=ollama())) as client:
        response = client.post("/v1/turns", json=turn_body(*ids, text="bác sĩ Nam chiều nay khám không"), headers=HEADERS)
    assert response.status_code == 503


def test_conversation_belongs_to_its_user_and_patient(tmp_path, ids):
    conversation, user, patient = ids
    with TestClient(create_app(settings(tmp_path), transport=ollama())) as client:
        assert client.post("/v1/turns", json=turn_body(conversation, user, patient, text="xin chào"), headers=HEADERS).status_code == 200
        other_user = client.post("/v1/turns", json=turn_body(conversation, str(uuid.uuid4()), patient, text="xin chào"), headers=HEADERS)
        other_patient = client.post("/v1/turns", json=turn_body(conversation, user, str(uuid.uuid4()), text="xin chào"), headers=HEADERS)
    assert (other_user.status_code, other_user.json()["code"]) == (404, "ConversationNotFound")
    assert other_patient.status_code == 404


def test_expired_conversation_is_not_found(tmp_path, ids):
    import sqlite3

    conversation, user, patient = ids
    s = settings(tmp_path, conversation_ttl_minutes=30)
    with TestClient(create_app(s, transport=ollama())) as client:
        client.post("/v1/turns", json=turn_body(*ids, text="xin chào"), headers=HEADERS)
        old = (datetime.now(UTC) - timedelta(minutes=31)).isoformat(timespec="microseconds")
        with sqlite3.connect(s.database_path) as db:
            db.execute("UPDATE conversation SET last_active_utc = ? WHERE conversation_id = ?", (old, conversation))
        response = client.post("/v1/turns", json=turn_body(*ids, text="xin chào"), headers=HEADERS)
    assert (response.status_code, response.json()["code"]) == (404, "ConversationNotFound")


def test_delete_only_by_owner(tmp_path, ids):
    conversation, user, patient = ids
    with TestClient(create_app(settings(tmp_path), transport=ollama())) as client:
        client.post("/v1/turns", json=turn_body(*ids, suggestion="SUGGEST_SPECIALTY"), headers=HEADERS)
        stranger = client.request("DELETE", f"/v1/conversations/{conversation}", json={"userId": str(uuid.uuid4())}, headers=HEADERS)
        owner = client.request("DELETE", f"/v1/conversations/{conversation}", json={"userId": user}, headers=HEADERS)
        again = client.request("DELETE", f"/v1/conversations/{conversation}", json={"userId": user}, headers=HEADERS)
        # Sau khi xóa, cùng Id là cuộc trò chuyện mới: State cũ (đang hỏi triệu chứng) không còn.
        fresh = client.post("/v1/turns", json=turn_body(*ids, text="người cứ thấy không ổn"), headers=HEADERS)
    assert stranger.status_code == 404
    assert (owner.status_code, again.status_code) == (204, 204)
    assert fresh.status_code == 503  # không còn ngữ cảnh "đang hỏi triệu chứng" nên là câu tự do


def test_state_is_saved_without_message_text(tmp_path, ids):
    import sqlite3

    s = settings(tmp_path)
    with TestClient(create_app(s, transport=ollama())) as client:
        client.post("/v1/turns", json=turn_body(*ids, text="tôi bị đau bụng, ợ chua"), headers=HEADERS)
    with sqlite3.connect(s.database_path) as db:
        (state_json,) = db.execute("SELECT state_json FROM conversation").fetchone()
    assert "đau bụng" not in state_json and "TIEU_HOA" in state_json
