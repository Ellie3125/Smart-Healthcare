"""Câu trả lời của đường tắt, ghép từ câu mẫu trong config/routing.yaml (không gọi model lần hai, Giải pháp §11.3.5)."""

from __future__ import annotations

from app.config_tables import Tables
from app.router import Routed
from app.schemas import ChatState, Suggestion, TurnOption, TurnResponse

# Gợi ý thao tác tiếp theo sau câu từ chối hoặc chào hỏi.
_NEXT_STEPS: list[Suggestion] = ["SEARCH_SLOTS", "VIEW_APPOINTMENTS", "SUGGEST_SPECIALTY"]


def shortcut_reply(tables: Tables, routed: Routed) -> tuple[TurnResponse, ChatState]:
    """Câu trả lời và State mới cho một lượt đi đường tắt."""
    intent = routed.intent
    match intent:
        case "EMERGENCY":
            # Kết thúc intent, không gợi ý đặt lịch (§11.3.4 bước 2).
            return TurnResponse(reply=tables.template("emergency"), intent=intent), {"intent": intent, "stage": "ABORTED"}
        case "OUT_OF_SCOPE":
            return (
                TurnResponse(reply=tables.template("medical_advice"), intent=intent, suggestions=["SUGGEST_SPECIALTY", "SEARCH_SLOTS"]),
                {"intent": intent, "stage": "DONE"},
            )
        case "HELP":
            return TurnResponse(reply=tables.template("help"), intent=intent, suggestions=_NEXT_STEPS), {"intent": intent, "stage": "DONE"}
        case "SUGGEST_SPECIALTY":
            return _specialty_reply(tables, routed)
        case _:
            # Intent có công cụ: bước 4 nối công cụ gọi API .NET. Giữ intent ở COLLECTING để lượt sau nối tiếp được.
            return (
                TurnResponse(reply=tables.template("tool_pending"), intent=intent),
                {"intent": intent, "stage": "COLLECTING", "slots": {}, "missing_slots": []},
            )


def _specialty_reply(tables: Tables, routed: Routed) -> tuple[TurnResponse, ChatState]:
    if not routed.has_symptoms:
        return (
            TurnResponse(reply=tables.template("specialty_ask"), intent="SUGGEST_SPECIALTY"),
            {"intent": "SUGGEST_SPECIALTY", "stage": "COLLECTING", "slots": {}, "missing_slots": ["symptom"]},
        )

    disclaimer = tables.specialties.disclaimer
    if routed.specialty_codes:
        chosen = [tables.specialty_by_code[c] for c in routed.specialty_codes]
        reply = f"{tables.template('specialty_match', names=', '.join(s.name for s in chosen))} {disclaimer}"
    else:
        chosen = list(tables.specialty_by_code.values())
        reply = f"{tables.template('specialty_fallback')} {disclaimer}"

    # Chỉ lưu mã chuyên khoa đã gợi ý; mô tả triệu chứng không vào State (Giải pháp §11.3.4).
    # Bệnh nhân tự chọn chuyên khoa rồi tìm lịch; chatbot không tự đặt lịch theo gợi ý.
    return (
        TurnResponse(
            reply=reply,
            intent="SUGGEST_SPECIALTY",
            suggestions=["SEARCH_SLOTS"],
            options=[
                TurnOption(kind="Specialty", specialty_code=s.code, label=f"{s.name}: {s.description}") for s in chosen
            ],
        ),
        {"intent": "SUGGEST_SPECIALTY", "stage": "DONE", "slots": {"specialty_codes": [s.code for s in chosen]}},
    )
