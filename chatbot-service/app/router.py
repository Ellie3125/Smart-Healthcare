"""Bộ định tuyến đường tắt chạy trước model (Giải pháp kỹ thuật §11.3.4, §11.3.5; contract §6). Không gọi model.

Thứ tự kiểm cố định:
  1. Dấu hiệu nguy hiểm — luôn trước tiên, kể cả khi câu cũng khớp chuyên khoa hay intent (§11.3.4 bước 2).
  2. Hỏi tư vấn chuyên môn (chẩn đoán, thuốc, kê đơn, xét nghiệm) — từ chối (Phạm vi §2.2).
  3. Chào hỏi / hỏi chatbot làm được gì.
  4. Cụm từ của intent (bảng intents).
  5. Mô tả triệu chứng, hoặc đang trả lời câu hỏi "mô tả triệu chứng" của lượt trước — gợi ý chuyên khoa.
  6. Còn lại là câu tự do, cần model.
Nút gợi ý (suggestion) đi thẳng tới intent, không qua các bước trên.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Literal

from app.config_tables import Tables
from app.schemas import ChatState, Intent, Suggestion
from app.textnorm import Message

Route = Literal["SHORTCUT", "MODEL"]


@dataclass(frozen=True)
class Routed:
    route: Route
    intent: Intent | None = None
    # Mã chuyên khoa khớp, điểm cao trước (chỉ với SUGGEST_SPECIALTY từ mô tả triệu chứng).
    specialty_codes: list[str] = field(default_factory=list)
    # SUGGEST_SPECIALTY: True khi có mô tả để tra bảng; False khi chỉ bấm nút/hỏi "khám khoa nào" mà chưa mô tả.
    has_symptoms: bool = False


def route(tables: Tables, state: ChatState, text: str | None, suggestion: Suggestion | None) -> Routed:
    if suggestion is not None:
        return Routed("SHORTCUT", suggestion)
    assert text is not None
    message = Message.of(text)

    if message.any(tables.red_flags):
        return Routed("SHORTCUT", "EMERGENCY")
    if message.any(tables.medical_advice):
        return Routed("SHORTCUT", "OUT_OF_SCOPE")
    if message.any(tables.help):
        return Routed("SHORTCUT", "HELP")

    codes = match_specialties(tables, message)
    for intent, phrases in tables.intents:
        if message.any(phrases):
            if intent == "SUGGEST_SPECIALTY":
                return Routed("SHORTCUT", "SUGGEST_SPECIALTY", codes, has_symptoms=bool(codes))
            return Routed("SHORTCUT", intent)

    if codes:
        return Routed("SHORTCUT", "SUGGEST_SPECIALTY", codes, has_symptoms=True)
    # Lượt trước chatbot hỏi mô tả triệu chứng: câu này là câu trả lời, kể cả khi không khớp bảng (hiện danh sách để tự chọn).
    if state.get("intent") == "SUGGEST_SPECIALTY" and state.get("stage") == "COLLECTING":
        return Routed("SHORTCUT", "SUGGEST_SPECIALTY", [], has_symptoms=True)
    return Routed("MODEL")


def match_specialties(tables: Tables, message: Message) -> list[str]:
    """Chuyên khoa có điểm > 0, điểm cao trước; hòa điểm giữ thứ tự trong bảng. Từ khóa triệu chứng 2 điểm, vị trí 1 điểm."""
    scored: list[tuple[int, int, str]] = []
    for order, (code, keywords, body_parts) in enumerate(tables.hints):
        score = 2 * sum(message.matches(p) for p in keywords) + sum(message.matches(p) for p in body_parts)
        if score > 0:
            scored.append((-score, order, code))
    return [code for _, _, code in sorted(scored)][: tables.specialties.max_suggestions]
