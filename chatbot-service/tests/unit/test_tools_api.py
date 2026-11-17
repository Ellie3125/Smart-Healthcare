"""Công cụ chỉ đọc qua API thật của dịch vụ, với .NET giả trả JSON đúng hình dạng DTO hiện có trên dev."""

from __future__ import annotations

import uuid
from datetime import UTC, datetime

import httpx
import pytest
from fastapi.testclient import TestClient

from app.main import create_app
from app.settings import Settings
from tests.conftest import CONFIG_DIR

KEY = "test-internal-key-0123456789"
TOKEN = "patient-token-abc"
HEADERS = {"X-Chatbot-Key": KEY, "Authorization": f"Bearer {TOKEN}"}
NOW = datetime(2026, 11, 17, 2, 0, tzinfo=UTC)  # 09:00 thứ ba 17/11 giờ VN
PATIENT = str(uuid.uuid4())
OTHER_PATIENT = str(uuid.uuid4())
USER = str(uuid.uuid4())


def appointment(patient, number, service_date, status="Scheduled", doctor="BS Nguyễn Văn A", fee=150000):
    return {
        "appointmentId": str(uuid.uuid4()), "appointmentNumber": number, "status": status, "patientId": patient,
        "doctorId": str(uuid.uuid4()), "doctorName": doctor, "serviceDate": service_date, "assignedRoomId": str(uuid.uuid4()),
        "appliedConsultationFee": fee, "currency": "VND", "confirmedAtUtc": "2026-11-16T01:00:00Z",
        "checkInDeadlineUtc": f"{service_date}T04:00:00Z", "cancelledAtUtc": None,
    }


class FakeDotnet:
    """Ghi lại request; trả lời theo đường dẫn. fail = {path: status} để giả lỗi."""

    def __init__(self, fail: dict[str, int] | None = None) -> None:
        self.requests: list[httpx.Request] = []
        self.fail = fail or {}

    def __call__(self, request: httpx.Request) -> httpx.Response:
        if request.url.host == "model.example.test":
            return httpx.Response(404, text="offline")  # model tắt: mọi thứ ở đây phải chạy không cần model
        self.requests.append(request)
        assert request.headers["authorization"] == f"Bearer {TOKEN}"
        path = request.url.path
        if path in self.fail:
            return httpx.Response(self.fail[path], json={"title": "x", "code": "Boom"})
        if path == "/api/scheduling/patient/doctors":
            return httpx.Response(200, json={
                "serviceDate": request.url.params["serviceDate"], "page": 1, "pageSize": 50, "hasMore": False,
                "items": [
                    {"doctorId": str(uuid.uuid4()), "fullName": "BS Trần Thị Da", "doctorRankId": str(uuid.uuid4()),
                     "specialtyId": str(uuid.uuid4()), "availableSlots": 12, "examFee": 150000, "currency": "VND",
                     "totalSlots": 30, "doctorRankName": "Bác sĩ Chuyên khoa I", "specialtyName": "Khoa Da liễu"},
                    {"doctorId": str(uuid.uuid4()), "fullName": "BS Lê Văn Tim", "doctorRankId": str(uuid.uuid4()),
                     "specialtyId": str(uuid.uuid4()), "availableSlots": 3, "examFee": 300000, "currency": "VND",
                     "totalSlots": 30, "doctorRankName": "Bác sĩ Chuyên khoa II", "specialtyName": "Khoa Tim mạch"},
                    {"doctorId": str(uuid.uuid4()), "fullName": "BS Hết Suất", "doctorRankId": str(uuid.uuid4()),
                     "specialtyId": str(uuid.uuid4()), "availableSlots": 0, "examFee": 150000, "currency": "VND",
                     "totalSlots": 30, "doctorRankName": "Bác sĩ", "specialtyName": "Khoa Da liễu"},
                ],
            })
        if path == "/api/scheduling/patient/appointments":
            return httpx.Response(200, json={"page": 1, "pageSize": 50, "hasMore": False, "items": [
                appointment(PATIENT, "LH261120-AAAA0001", "2026-11-20"),
                appointment(PATIENT, "LH261118-AAAA0002", "2026-11-18", status="CheckedIn"),
                appointment(PATIENT, "LH261101-AAAA0003", "2026-11-01", status="Completed"),
                appointment(PATIENT, "LH261125-AAAA0004", "2026-11-25", status="Cancelled"),
                appointment(OTHER_PATIENT, "LH261119-BBBB0001", "2026-11-19"),  # hồ sơ khác của cùng tài khoản
            ]})
        if path == "/api/scheduling/patient/booking-attempts/recoverable":
            return httpx.Response(200, json=[
                {"bookingAttemptId": str(uuid.uuid4()), "patientId": PATIENT, "serviceDate": "2026-11-21", "doctorId": None,
                 "status": "WaitingPayment", "currentStep": "Payment", "expiresAtUtc": "2026-11-17T02:05:00Z", "allowedActions": []},
                {"bookingAttemptId": str(uuid.uuid4()), "patientId": OTHER_PATIENT, "serviceDate": "2026-11-21", "doctorId": None,
                 "status": "WaitingPayment", "currentStep": "Payment", "expiresAtUtc": None, "allowedActions": []},
            ])
        if path == "/api/billing/patient/refunds":
            assert request.url.params["patientId"] == PATIENT  # lấy từ context, không từ câu chat
            return httpx.Response(200, json=[{"refundRequestId": str(uuid.uuid4()), "status": "Approved", "amount": 150000,
                                              "currency": "VND", "appointmentId": None}])
        return httpx.Response(404, json={"code": "NotFound"})


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
