"""Công cụ chỉ đọc qua API thật của dịch vụ, với .NET giả trả JSON đúng hình dạng DTO hiện có trên dev."""

from __future__ import annotations

import uuid

import httpx
import pytest
from fastapi.testclient import TestClient

from app.main import create_app
from app.settings import Settings
from tests.conftest import CONFIG_DIR
from tests.fake_dotnet import HEADERS, KEY, NOW, OTHER_PATIENT, PATIENT, TOKEN, USER, FakeDotnet, appointment  # noqa: F401


def client(tmp_path, fake: FakeDotnet) -> TestClient:
    settings = Settings(chatbot_internal_key=KEY, data_dir=tmp_path, config_dir=CONFIG_DIR, health_cache_seconds=0,
                        ollama_base_url="https://model.example.test", ollama_api_key="k",
                        dotnet_api_base_url="http://api.example.test")
    return TestClient(create_app(settings, transport=httpx.MockTransport(fake), clock=lambda: NOW))


def say(c: TestClient, conversation: str, **content) -> httpx.Response:
    body = {"conversationId": conversation, "userId": USER, "patientId": PATIENT, "messageId": str(uuid.uuid4()), **content}
    return c.post("/v1/turns", json=body, headers=HEADERS)


@pytest.fixture
def conversation():
    return str(uuid.uuid4())


def test_search_asks_for_date_then_lists_doctors_of_that_specialty(tmp_path, conversation):
    fake = FakeDotnet()
    with client(tmp_path, fake) as c:
        ask = say(c, conversation, text="tôi muốn khám da liễu").json()
        found = say(c, conversation, text="thứ sáu này").json()
    assert ask["intent"] == "SEARCH_SLOTS" and "Khoa Da liễu" in ask["reply"] and ask["options"] == []
    assert "20/11/2026" in found["reply"]
    assert [o["kind"] for o in found["options"]] == ["Doctor"]  # chỉ bác sĩ Da liễu còn suất
    label = found["options"][0]["label"]
    assert "BS Trần Thị Da" in label and "còn 12 suất" in label and "150.000 VND" in label
    assert found["options"][0]["serviceDate"] == "2026-11-20"
    assert fake.requests[-1].url.params["serviceDate"] == "2026-11-20"


def test_past_date_is_refused_without_calling_dotnet(tmp_path, conversation):
    fake = FakeDotnet()
    with client(tmp_path, fake) as c:
        response = say(c, conversation, text="đặt lịch ngày 10/11/2026").json()
    assert "đã qua" in response["reply"]
    assert fake.requests == []


def test_my_appointments_only_upcoming_active_of_this_patient(tmp_path, conversation):
    with client(tmp_path, FakeDotnet()) as c:
        response = say(c, conversation, suggestion="VIEW_APPOINTMENTS").json()
    labels = [o["label"] for o in response["options"]]
    assert [label.split(" – ")[0] for label in labels] == ["LH261118-AAAA0002", "LH261120-AAAA0001"]  # gần nhất trước
    assert "đã check-in" in labels[0]
    assert all("BBBB" not in label for label in labels)


def test_ticket_shows_number_status_and_checkin_deadline(tmp_path, conversation):
    with client(tmp_path, FakeDotnet()) as c:
        reply = say(c, conversation, text="cho tôi xem vé").json()["reply"]
    assert "LH261120-AAAA0001" in reply and "check-in trước 11:00 20/11" in reply
    assert "Số thứ tự khám được cấp khi tiếp tân check-in" in reply
    assert "BBBB" not in reply


def test_exam_payment_combines_appointments_pending_booking_and_refund(tmp_path, conversation):
    with client(tmp_path, FakeDotnet()) as c:
        reply = say(c, conversation, suggestion="VIEW_EXAM_PAYMENT").json()["reply"]
    assert "LH261120-AAAA0001 ngày 20/11/2026: đã thanh toán phí khám 150.000 VND" in reply
    assert "Lượt đặt lịch ngày 21/11/2026: đang chờ thanh toán, giữ chỗ tới 09:05 17/11" in reply
    assert "Hoàn phí khám 150.000 VND: đã duyệt, nhận tiền mặt tại quầy tiếp tân" in reply
    assert reply.count("Lượt đặt lịch") == 1  # không lấy lượt của hồ sơ khác


def test_cancel_lists_changeable_appointments_and_never_calls_change_api(tmp_path, conversation):
    fake = FakeDotnet()
    with client(tmp_path, fake) as c:
        response = say(c, conversation, text="tôi muốn hủy lịch").json()
    assert [o["label"].split(" – ")[0] for o in response["options"]] == ["LH261120-AAAA0001"]  # chỉ lịch Scheduled
    assert "Lịch của tôi" in response["reply"]
    assert all(r.method == "GET" for r in fake.requests)
    assert not any(r.url.path.endswith(("/cancel", "/reschedule")) for r in fake.requests)


def test_expired_token_returns_session_expired_and_keeps_conversation(tmp_path, conversation):
    with client(tmp_path, FakeDotnet(fail={"/api/scheduling/patient/appointments": 401})) as c:
        expired = say(c, conversation, suggestion="VIEW_APPOINTMENTS")
        still_there = say(c, conversation, text="xin chào")
    assert (expired.status_code, expired.json()["code"]) == (401, "SessionExpired")
    assert still_there.status_code == 200


@pytest.mark.parametrize("status", [500, 503, 409])
def test_backend_errors_answer_without_guessing(tmp_path, conversation, status):
    with client(tmp_path, FakeDotnet(fail={"/api/scheduling/patient/doctors": status})) as c:
        response = say(c, conversation, text="đặt lịch ngày mai")
    assert response.status_code == 200
    assert "chưa lấy được dữ liệu" in response.json()["reply"]
    assert response.json()["options"] == []


def test_dotnet_timeout_is_handled(tmp_path, conversation):
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.host == "model.example.test":
            return httpx.Response(404)
        raise httpx.ReadTimeout("timeout", request=request)

    settings = Settings(chatbot_internal_key=KEY, data_dir=tmp_path, config_dir=CONFIG_DIR, dotnet_api_base_url="http://api.example.test")
    with TestClient(create_app(settings, transport=httpx.MockTransport(handler), clock=lambda: NOW)) as c:
        response = say(c, conversation, suggestion="VIEW_TICKET")
    assert response.status_code == 200 and "chưa lấy được dữ liệu" in response.json()["reply"]


def test_tools_work_while_model_is_down(tmp_path, conversation):
    with client(tmp_path, FakeDotnet()) as c:
        assert c.get("/health").json()["available"] is False
        assert say(c, conversation, suggestion="VIEW_TICKET").status_code == 200
