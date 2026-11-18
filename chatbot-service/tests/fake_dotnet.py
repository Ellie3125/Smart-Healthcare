""".NET giả cho test và bộ kịch bản: trả JSON đúng hình dạng DTO hiện có trên dev, ghi lại mọi request.

Dữ liệu cố định theo ngày NOW (thứ ba 17/11/2026, 09:00 giờ VN): bộ kịch bản có ngày cụ thể (thứ sáu này = 20/11) nên phải chạy với
đồng hồ cố định này. Request tới host của model đi theo model_handler (mặc định: model tắt).
"""

from __future__ import annotations

import uuid
from datetime import UTC, datetime
from typing import Callable

import httpx

KEY = "test-internal-key-0123456789"
TOKEN = "patient-token-abc"
HEADERS = {"X-Chatbot-Key": KEY, "Authorization": f"Bearer {TOKEN}"}
NOW = datetime(2026, 11, 17, 2, 0, tzinfo=UTC)  # 09:00 thứ ba 17/11 giờ VN
PATIENT = str(uuid.uuid4())
OTHER_PATIENT = str(uuid.uuid4())
USER = str(uuid.uuid4())
MODEL_HOST = "model.example.test"


def appointment(patient, number, service_date, status="Scheduled", doctor="BS Nguyễn Văn A", fee=150000):
    return {
        "appointmentId": str(uuid.uuid4()), "appointmentNumber": number, "status": status, "patientId": patient,
        "doctorId": str(uuid.uuid4()), "doctorName": doctor, "serviceDate": service_date, "assignedRoomId": str(uuid.uuid4()),
        "appliedConsultationFee": fee, "currency": "VND", "confirmedAtUtc": "2026-11-16T01:00:00Z",
        "checkInDeadlineUtc": f"{service_date}T04:00:00Z", "cancelledAtUtc": None,
    }


def encounter(checked_in_utc, doctor, specialty, status, prescription=False):
    return {
        "id": str(uuid.uuid4()), "encounterCode": "PK-1A2B3C4D", "serviceDate": checked_in_utc, "doctorName": doctor,
        "specialtyName": specialty, "primaryDiagnosis": "Viêm họng cấp", "status": status, "hasPrescription": prescription,
        "hasPublishedAttachments": False, "patientName": None, "patientProfileId": PATIENT, "isPublished": status == "Completed",
        "hasClsResults": False,
    }


def _model_offline(request: httpx.Request) -> httpx.Response:
    return httpx.Response(404, text="offline")  # model tắt: mọi thứ phải chạy không cần model


class FakeDotnet:
    """Ghi lại request; trả lời theo đường dẫn. fail = {path: status} để giả lỗi."""

    def __init__(self, fail: dict[str, int] | None = None,
                 model_handler: Callable[[httpx.Request], httpx.Response] = _model_offline) -> None:
        self.requests: list[httpx.Request] = []
        self.fail = fail or {}
        self.model_handler = model_handler

    @property
    def writes(self) -> list[httpx.Request]:
        """Request thay đổi dữ liệu tới .NET; chatbot không bao giờ được gửi (BOT-05)."""
        return [r for r in self.requests if r.method != "GET"]

    def __call__(self, request: httpx.Request) -> httpx.Response:
        if request.url.host == MODEL_HOST or request.url.host in ("127.0.0.1", "localhost", "ollama"):
            return self.model_handler(request)
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
        if path == "/api/documents/patient/encounters":
            assert request.url.params["patientProfileId"] == PATIENT  # hồ sơ của cuộc trò chuyện, không từ câu chat
            return httpx.Response(200, json={"totalCount": 2, "page": 1, "pageSize": 5, "hasMore": False, "items": [
                encounter("2026-11-10T02:15:00Z", "BS Nguyễn Văn A", "Khoa Nội", "Completed", prescription=True),
                encounter("2026-10-02T03:00:00Z", "BS Trần Thị Da", "Khoa Da liễu", "WaitingCLS"),
            ]})
        return httpx.Response(404, json={"code": "NotFound"})
