"""Data Schema (Giải pháp kỹ thuật §11.3.1): hình dạng dữ liệu chatbot đọc qua API .NET.

Chỉ giữ trường cần để trả lời; Id dùng để gọi tiếp nằm trong option/state, không đưa vào câu trả lời hay prompt (contract §7).
"""

from __future__ import annotations

from datetime import date, datetime
from decimal import Decimal
from uuid import UUID

from pydantic import BaseModel, ConfigDict


class _Out(BaseModel):
    model_config = ConfigDict(frozen=True)


class SlotOption(_Out):
    """Một bác sĩ còn nhận đặt trong ngày. Số suất chỉ để tham khảo, không phải bằng chứng đã có suất (§11.6)."""

    doctor_id: UUID
    doctor_name: str
    doctor_level: str | None
    specialty_name: str | None
    service_date: date
    remaining_hint: int
    exam_fee: Decimal
    currency: str


class AppointmentOut(_Out):
    appointment_id: UUID
    appointment_number: str
    doctor_name: str | None
    service_date: date
    status: str
    fee: Decimal
    currency: str
    check_in_deadline_utc: datetime | None

    @property
    def is_active(self) -> bool:
        """Còn hiệu lực để đi khám (chưa hủy, chưa đổi, chưa kết thúc)."""
        return self.status in ("Scheduled", "CheckedIn")

    @property
    def changeable(self) -> bool:
        """Bệnh nhân còn tự đổi/hủy được về trạng thái (Phạm vi §6.2). Mốc 05:00 và quyền do .NET kiểm khi thực hiện."""
        return self.status == "Scheduled"


class PendingBookingOut(_Out):
    """Lượt đặt lịch dang dở: đã giữ chỗ hoặc đang chờ thanh toán."""

    booking_attempt_id: UUID
    service_date: date
    status: str
    expires_at_utc: datetime | None


class RefundOut(_Out):
    refund_request_id: UUID
    status: str
    amount: Decimal
    currency: str
    appointment_id: UUID | None
