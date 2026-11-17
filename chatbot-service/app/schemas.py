"""State Schema, Context Schema (Giải pháp kỹ thuật §11.3.2, §11.3.3) và DTO của API nội bộ (contract docs/api/chatbot.md §2)."""

from __future__ import annotations

from dataclasses import dataclass
from datetime import date
from typing import Literal, NotRequired, TypedDict
from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field, model_validator
from pydantic.alias_generators import to_camel

# Intent có công cụ gọi API nghiệp vụ (bước 4 nối công cụ).
ToolIntent = Literal[
    "SEARCH_SLOTS",
    "BOOK",
    "VIEW_APPOINTMENTS",
    "RESCHEDULE",
    "CANCEL",
    "VIEW_TICKET",
    "VIEW_EXAM_PAYMENT",
]

# Intent của GP §11.3.2, thêm EMERGENCY (dấu hiệu nguy hiểm, §11.3.4 bước 2) và HELP (chào hỏi, hỏi chatbot làm được gì).
Intent = Literal[
    "SEARCH_SLOTS",
    "BOOK",
    "VIEW_APPOINTMENTS",
    "RESCHEDULE",
    "CANCEL",
    "VIEW_TICKET",
    "VIEW_EXAM_PAYMENT",
    "SUGGEST_SPECIALTY",
    "OUT_OF_SCOPE",
    "EMERGENCY",
    "HELP",
]

Stage = Literal["COLLECTING", "AWAITING_CONFIRMATION", "DONE", "ABORTED"]

# Mã nút gợi ý (contract §6.2); nhãn hiển thị do web bệnh nhân giữ.
Suggestion = Literal[
    "SEARCH_SLOTS",
    "VIEW_APPOINTMENTS",
    "RESCHEDULE",
    "CANCEL",
    "VIEW_TICKET",
    "VIEW_EXAM_PAYMENT",
    "SUGGEST_SPECIALTY",
]

TEXT_MAX_LENGTH = 1000


class ChatState(TypedDict):
    """Trạng thái hội thoại qua từng lượt. Chỉ mô tả hội thoại; trạng thái nghiệp vụ luôn đọc lại từ API .NET.

    Không lưu mô tả triệu chứng (chỉ lưu mã chuyên khoa đã gợi ý), access token hay định danh của người dùng.
    Bước 7 chuyển thành lớp kế thừa AgentState của LangChain, giữ nguyên các trường.
    """

    intent: NotRequired[Intent]
    stage: NotRequired[Stage]
    slots: NotRequired[dict]
    missing_slots: NotRequired[list[str]]
    proposal_id: NotRequired[str]


@dataclass(frozen=True)
class ChatContext:
    """Ngữ cảnh cố định của một lượt, do .NET truyền sang từ phiên đã xác thực (contract §3.2).

    Công cụ đọc danh tính ở đây, không nhận UserId/PatientId do model điền. Không đưa vào prompt, checkpoint hay log.
    """

    user_id: str
    patient_id: str
    conversation_id: str
    access_token: str
    message_id: str
    locale: str = "vi"

    def __repr__(self) -> str:  # tránh lộ token/định danh nếu lỡ in ra log
        return "ChatContext(<redacted>)"


class _Camel(BaseModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True, extra="forbid")


class TurnRequest(_Camel):
    conversation_id: UUID
    user_id: UUID
    patient_id: UUID
    message_id: UUID
    text: str | None = Field(default=None, max_length=TEXT_MAX_LENGTH)
    suggestion: Suggestion | None = None
    locale: Literal["vi"] = "vi"

    @model_validator(mode="after")
    def _one_input(self) -> TurnRequest:
        if self.text is not None:
            self.text = self.text.strip() or None
        if (self.text is None) == (self.suggestion is None):
            raise ValueError("Cần đúng một trong text hoặc suggestion.")
        return self


class TurnOption(_Camel):
    """Lựa chọn bấm được trên khung chat (bác sĩ, lịch hẹn, chuyên khoa). Số suất chỉ để tham khảo."""

    kind: Literal["Doctor", "Appointment", "Specialty"]
    label: str
    specialty_code: str | None = None
    doctor_id: UUID | None = None
    service_date: date | None = None
    appointment_id: UUID | None = None


class TurnResponse(_Camel):
    reply: str
    intent: Intent | None = None
    suggestions: list[Suggestion] = []
    proposal: dict | None = None
    options: list[TurnOption] = []


class DeleteConversationRequest(_Camel):
    user_id: UUID


class HealthResponse(_Camel):
    available: bool
    model: str | None = None
