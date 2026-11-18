"""Công cụ chỉ đọc của chatbot (contract docs/api/chatbot.md §5.1). Whitelist: chỉ các hàm trong file này.

Danh tính lấy từ ChatContext (do .NET truyền), không nhận UserId/PatientId làm tham số (BOT-03). API .NET trả dữ liệu của mọi hồ sơ
tài khoản đại diện; công cụ lọc đúng hồ sơ của cuộc trò chuyện. Không có công cụ thay đổi dữ liệu: đặt, đổi, hủy đi qua đề nghị chờ
xác nhận (E1) khi có, và người dùng bấm nút trên web bệnh nhân.

Thiếu so với contract (ghi lại để nối khi .NET có):
  - E5 danh mục chuyên khoa cho bệnh nhân: tìm bác sĩ lọc theo tên khoa thay vì specialtyId.
  - E3 trạng thái thanh toán phí khám: ghép từ lịch hẹn (đã xác nhận nghĩa là đã thu), lượt đặt dở và yêu cầu hoàn.
  - Số thứ tự khám: DTO lịch hẹn chưa có.
"""

from __future__ import annotations

from datetime import date

from app.backend import BackendClient
from app.schemas import ChatContext
from app.textnorm import plain
from app.tools.models import AppointmentOut, PendingBookingOut, RefundOut, SlotOption, VisitOut

PAGE_SIZE = 50  # tối đa của API .NET


class ReadTools:
    def __init__(self, backend: BackendClient) -> None:
        self._backend = backend

    async def search_doctors(self, ctx: ChatContext, service_date: date, specialty_name: str | None = None) -> list[SlotOption]:
        """Bác sĩ còn suất trong ngày, lọc theo tên chuyên khoa nếu có. Bác sĩ hết suất bị bỏ."""
        page = await self._backend.get(
            "/api/scheduling/patient/doctors",
            ctx.access_token,
            {"serviceDate": service_date.isoformat(), "page": 1, "pageSize": PAGE_SIZE},
        )
        wanted = plain(specialty_name) if specialty_name else None
        options = []
        for item in page.get("items", []):
            if item.get("availableSlots", 0) <= 0:
                continue
            if wanted and plain(item.get("specialtyName") or "") != wanted:
                continue
            options.append(
                SlotOption(
                    doctor_id=item["doctorId"],
                    doctor_name=item["fullName"],
                    doctor_level=item.get("doctorRankName"),
                    specialty_name=item.get("specialtyName"),
                    service_date=service_date,
                    remaining_hint=item["availableSlots"],
                    exam_fee=item["examFee"],
                    currency=item.get("currency") or "VND",
                )
            )
        return options

    async def list_my_appointments(self, ctx: ChatContext) -> list[AppointmentOut]:
        """Lịch hẹn của hồ sơ trong cuộc trò chuyện, mới nhất trước (một trang 50)."""
        page = await self._backend.get(
            "/api/scheduling/patient/appointments", ctx.access_token, {"page": 1, "pageSize": PAGE_SIZE}
        )
        return [_appointment(item) for item in page.get("items", []) if item.get("patientId") == ctx.patient_id]

    async def list_pending_bookings(self, ctx: ChatContext) -> list[PendingBookingOut]:
        items = await self._backend.get("/api/scheduling/patient/booking-attempts/recoverable", ctx.access_token)
        return [
            PendingBookingOut(
                booking_attempt_id=item["bookingAttemptId"],
                service_date=item["serviceDate"],
                status=item["status"],
                expires_at_utc=item.get("expiresAtUtc"),
            )
            for item in items
            if item.get("patientId") == ctx.patient_id
        ]

    async def list_my_refunds(self, ctx: ChatContext) -> list[RefundOut]:
        items = await self._backend.get("/api/billing/patient/refunds", ctx.access_token, {"patientId": ctx.patient_id})
        return [
            RefundOut(
                refund_request_id=item["refundRequestId"],
                status=item["status"],
                amount=item["amount"],
                currency=item.get("currency") or "VND",
                appointment_id=item.get("appointmentId"),
            )
            for item in items
        ]

    async def list_visit_history(self, ctx: ChatContext, limit: int) -> list[VisitOut]:
        """Lượt khám gần nhất của hồ sơ trong cuộc trò chuyện. .NET chỉ mở cho hồ sơ đã xác minh (quyền ClinicalRecords);
        hồ sơ chưa xác minh hoặc không thuộc tài khoản → 403 (BackendNotFound)."""
        page = await self._backend.get(
            "/api/documents/patient/encounters", ctx.access_token, {"patientProfileId": ctx.patient_id, "page": 1, "pageSize": limit}
        )
        return [
            VisitOut(
                checked_in_at_utc=item["serviceDate"],
                doctor_name=item.get("doctorName"),
                specialty_name=item.get("specialtyName"),
                status=item["status"],
                has_prescription=bool(item.get("hasPrescription")),
            )
            for item in page.get("items", [])
        ]


def _appointment(item: dict) -> AppointmentOut:
    return AppointmentOut(
        appointment_id=item["appointmentId"],
        appointment_number=item["appointmentNumber"],
        doctor_name=item.get("doctorName"),
        service_date=item["serviceDate"],
        status=item["status"],
        fee=item["appliedConsultationFee"],
        currency=item.get("currency") or "VND",
        check_in_deadline_utc=item.get("checkInDeadlineUtc"),
    )
