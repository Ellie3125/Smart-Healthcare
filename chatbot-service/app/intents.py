"""Intent có công cụ, chạy trên đường tắt: gọi công cụ chỉ đọc rồi ghép câu trả lời từ câu mẫu (không gọi model).

Lỗi .NET (5xx, timeout, từ chối) → câu "chưa lấy được dữ liệu", State giữ nguyên để hỏi lại (BOT-06). Token hết hạn → SessionExpired
lan ra ngoài để lượt trả 401 và web bệnh nhân đăng nhập lại.
Đặt, đổi, hủy chưa thực thi được qua chatbot: cần đề nghị chờ xác nhận (E1, contract §4) mà .NET chưa có; chatbot liệt kê lịch và
hướng dẫn thao tác trên web bệnh nhân. Chatbot không bao giờ tự gọi API hủy/đổi (BOT-05).
"""

from __future__ import annotations

from datetime import date, datetime
from decimal import Decimal

from app.backend import BackendError, BackendNotFound, SessionExpired
from app.config_tables import Tables
from app.router import Routed
from app.schemas import ChatContext, ChatState, TurnOption, TurnResponse
from app.slots import VIETNAM, format_date
from app.tools.models import AppointmentOut
from app.tools.read_tools import ReadTools

MAX_OPTIONS = 5
TOOL_INTENTS = frozenset({"SEARCH_SLOTS", "BOOK", "VIEW_APPOINTMENTS", "RESCHEDULE", "CANCEL", "VIEW_TICKET", "VIEW_EXAM_PAYMENT", "VIEW_VISIT_HISTORY"})


class ToolIntentHandler:
    def __init__(self, tables: Tables, tools: ReadTools) -> None:
        self._t = tables
        self._tools = tools

    async def handle(self, routed: Routed, ctx: ChatContext, state: ChatState, today: date) -> tuple[TurnResponse, ChatState]:
        try:
            match routed.intent:
                case "SEARCH_SLOTS":
                    return await self._search(routed.slots, ctx, today)
                case "VIEW_APPOINTMENTS":
                    return await self._appointments(ctx, today)
                case "VIEW_TICKET":
                    return await self._ticket(ctx, today)
                case "VIEW_EXAM_PAYMENT":
                    return await self._payments(ctx, today)
                case "VIEW_VISIT_HISTORY":
                    return await self._visits(ctx)
                case "CANCEL":
                    return await self._change(ctx, today, "CANCEL", "hủy lịch")
                case "RESCHEDULE":
                    return await self._change(ctx, today, "RESCHEDULE", "đổi lịch")
                case _:  # BOOK: chờ E1 (đề nghị chờ xác nhận)
                    return TurnResponse(reply=self._t.template("tool_pending"), intent=routed.intent), _done(routed.intent)
        except SessionExpired:
            raise
        except BackendError:
            return TurnResponse(reply=self._t.template("backend_unavailable"), intent=routed.intent), state

    async def _search(self, slots: dict, ctx: ChatContext, today: date) -> tuple[TurnResponse, ChatState]:
        specialty = self._t.specialty_by_code.get(slots.get("specialty_code", ""))
        where = f" ở {specialty.name}" if specialty else ""
        kept = {k: v for k, v in slots.items() if k == "specialty_code"}
        if "service_date" not in slots:
            return (
                TurnResponse(reply=self._t.template("ask_date", specialty=where), intent="SEARCH_SLOTS"),
                {"intent": "SEARCH_SLOTS", "stage": "COLLECTING", "slots": kept, "missing_slots": ["service_date"]},
            )

        service_date = date.fromisoformat(slots["service_date"])
        if service_date < today:
            return (
                TurnResponse(reply=self._t.template("past_date", date=format_date(service_date)), intent="SEARCH_SLOTS"),
                {"intent": "SEARCH_SLOTS", "stage": "COLLECTING", "slots": kept, "missing_slots": ["service_date"]},
            )

        doctors = await self._tools.search_doctors(ctx, service_date, specialty.name if specialty else None)
        values = {"date": format_date(service_date), "specialty": where}
        state: ChatState = {"intent": "SEARCH_SLOTS", "stage": "DONE", "slots": slots}
        if not doctors:
            return TurnResponse(reply=self._t.template("slots_none", **values), intent="SEARCH_SLOTS"), state
        options = [
            TurnOption(
                kind="Doctor",
                doctor_id=d.doctor_id,
                service_date=d.service_date,
                label=" – ".join(
                    part for part in (d.doctor_name, d.doctor_level, d.specialty_name, f"còn {d.remaining_hint} suất", _money(d.exam_fee, d.currency)) if part
                ),
            )
            for d in doctors[:MAX_OPTIONS]
        ]
        facts = [
            f"{i}. Tên: {d.doctor_name} | Cấp bậc: {d.doctor_level or 'không rõ'} | Khoa: {d.specialty_name or 'không rõ'}"
            f" | Còn: {d.remaining_hint} suất | Phí: {_money(d.exam_fee, d.currency)}"
            for i, d in enumerate(doctors[:MAX_OPTIONS], start=1)
        ]
        return (
            TurnResponse(reply=self._t.template("slots_found", **values), intent="SEARCH_SLOTS", options=options, facts=facts),
            state,
        )

    async def _appointments(self, ctx: ChatContext, today: date) -> tuple[TurnResponse, ChatState]:
        upcoming = _upcoming(await self._tools.list_my_appointments(ctx), today)
        if not upcoming:
            return (
                TurnResponse(reply=self._t.template("appointments_none"), intent="VIEW_APPOINTMENTS", suggestions=["SEARCH_SLOTS"]),
                _done("VIEW_APPOINTMENTS"),
            )
        return (
            TurnResponse(
                reply=self._t.template("appointments_list"),
                intent="VIEW_APPOINTMENTS",
                options=[self._appointment_option(a) for a in upcoming[:MAX_OPTIONS]],
                facts=[self._appointment_fact(i, a) for i, a in enumerate(upcoming[:MAX_OPTIONS], start=1)],
                suggestions=["VIEW_TICKET", "RESCHEDULE", "CANCEL"],
            ),
            _done("VIEW_APPOINTMENTS"),
        )

    async def _ticket(self, ctx: ChatContext, today: date) -> tuple[TurnResponse, ChatState]:
        upcoming = _upcoming(await self._tools.list_my_appointments(ctx), today)
        if not upcoming:
            return TurnResponse(reply=self._t.template("appointments_none"), intent="VIEW_TICKET"), _done("VIEW_TICKET")
        lines = []
        for a in upcoming[:3]:
            line = f"• Mã lịch hẹn {a.appointment_number}: {a.doctor_name or 'bác sĩ'}, ngày {format_date(a.service_date)}, {self._status(a.status)}"
            if a.status == "Scheduled" and a.check_in_deadline_utc:
                line += f"; check-in trước {_vn_time(a.check_in_deadline_utc)}"
            lines.append(line + ".")
        reply = "\n".join([*lines, self._t.template("ticket_hint")])
        return TurnResponse(reply=reply, intent="VIEW_TICKET"), _done("VIEW_TICKET")

    async def _payments(self, ctx: ChatContext, today: date) -> tuple[TurnResponse, ChatState]:
        appointments = _upcoming(await self._tools.list_my_appointments(ctx), today)
        pending = [p for p in await self._tools.list_pending_bookings(ctx) if p.status in ("Held", "WaitingPayment")]
        refunds = await self._tools.list_my_refunds(ctx)

        lines = []
        # Lịch hẹn chỉ được xác nhận sau khi phí khám đã thu (Phạm vi §5.1), nên lịch còn hiệu lực là đã thanh toán.
        for a in appointments[:MAX_OPTIONS]:
            lines.append(f"• Lịch {a.appointment_number} ngày {format_date(a.service_date)}: đã thanh toán phí khám {_money(a.fee, a.currency)}.")
        for p in pending[:MAX_OPTIONS]:
            deadline = f", giữ chỗ tới {_vn_time(p.expires_at_utc)}" if p.expires_at_utc else ""
            lines.append(f"• Lượt đặt lịch ngày {format_date(p.service_date)}: đang chờ thanh toán{deadline}.")
        for r in refunds[:MAX_OPTIONS]:
            label = self._t.routing.refund_status_labels.get(r.status, r.status)
            lines.append(f"• Hoàn phí khám {_money(r.amount, r.currency)}: {label}.")

        if not lines:
            return TurnResponse(reply=self._t.template("payments_none"), intent="VIEW_EXAM_PAYMENT"), _done("VIEW_EXAM_PAYMENT")
        reply = "\n".join([self._t.template("payments_header"), *lines])
        return TurnResponse(reply=reply, intent="VIEW_EXAM_PAYMENT"), _done("VIEW_EXAM_PAYMENT")

    async def _visits(self, ctx: ChatContext) -> tuple[TurnResponse, ChatState]:
        try:
            visits = await self._tools.list_visit_history(ctx, MAX_OPTIONS)
        except BackendNotFound:
            # .NET chỉ mở bệnh án cho hồ sơ đã xác minh danh tính; không đoán, không thử hồ sơ khác.
            return TurnResponse(reply=self._t.template("visits_not_verified"), intent="VIEW_VISIT_HISTORY"), _done("VIEW_VISIT_HISTORY")
        if not visits:
            return TurnResponse(reply=self._t.template("visits_none"), intent="VIEW_VISIT_HISTORY"), _done("VIEW_VISIT_HISTORY")
        labels = self._t.routing.visit_status_labels
        lines = []
        for v in sorted(visits, key=lambda v: v.checked_in_at_utc, reverse=True):
            parts = [v.doctor_name or "bác sĩ phụ trách", v.specialty_name, labels.get(v.status, v.status)]
            if v.has_prescription:
                parts.append("có đơn thuốc")
            lines.append(f"• {v.checked_in_at_utc.astimezone(VIETNAM).strftime('%d/%m/%Y')}: {', '.join(p for p in parts if p)}.")
        reply = "\n".join([self._t.template("visits_header"), *lines, self._t.template("visits_hint")])
        return TurnResponse(reply=reply, intent="VIEW_VISIT_HISTORY"), _done("VIEW_VISIT_HISTORY")

    async def _change(self, ctx: ChatContext, today: date, intent: str, action: str) -> tuple[TurnResponse, ChatState]:
        changeable = [a for a in _upcoming(await self._tools.list_my_appointments(ctx), today) if a.changeable]
        if not changeable:
            return TurnResponse(reply=self._t.template("change_none", action=action), intent=intent), _done(intent)
        return (
            TurnResponse(
                reply=self._t.template("change_manual", action=action),
                intent=intent,
                options=[self._appointment_option(a) for a in changeable[:MAX_OPTIONS]],
                facts=[self._appointment_fact(i, a) for i, a in enumerate(changeable[:MAX_OPTIONS], start=1)],
            ),
            _done(intent),
        )

    def _appointment_option(self, a: AppointmentOut) -> TurnOption:
        label = " – ".join(
            part for part in (a.appointment_number, format_date(a.service_date), a.doctor_name, self._status(a.status)) if part
        )
        return TurnOption(kind="Appointment", appointment_id=a.appointment_id, service_date=a.service_date, label=label)

    def _appointment_fact(self, index: int, a: AppointmentOut) -> str:
        return (
            f"{index}. Mã lịch hẹn: {a.appointment_number} | Ngày: {format_date(a.service_date)}"
            f" | Bác sĩ: {a.doctor_name or 'không rõ'} | Trạng thái: {self._status(a.status)} | Phí đã trả: {_money(a.fee, a.currency)}"
        )

    def _status(self, status: str) -> str:
        return self._t.routing.status_labels.get(status, status)


def _upcoming(appointments: list[AppointmentOut], today: date) -> list[AppointmentOut]:
    """Lịch còn hiệu lực từ hôm nay trở đi, gần nhất trước."""
    return sorted((a for a in appointments if a.is_active and a.service_date >= today), key=lambda a: a.service_date)


def _done(intent: str) -> ChatState:
    return {"intent": intent, "stage": "DONE"}  # type: ignore[typeddict-item]


def _money(amount: Decimal, currency: str) -> str:
    return f"{int(amount):,}".replace(",", ".") + f" {currency}"


def _vn_time(moment: datetime) -> str:
    return moment.astimezone(VIETNAM).strftime("%H:%M %d/%m")
