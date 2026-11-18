"""Công cụ model được phép gọi (contract §5; Giải pháp §11.5). Whitelist: đúng các hàm build_tools trả về.

Mỗi công cụ bọc đúng hàm của đường tắt (app/intents.py) nên định dạng, lọc hồ sơ, xử lý lỗi và quy tắc "không gọi API đổi/hủy"
giữ nguyên. Danh tính lấy từ runtime.context (ChatContext do .NET truyền), không có tham số Id cho model điền (BOT-03).
Model nhận câu trả lời rút gọn không có Id; lựa chọn bấm được (có Id) ghi vào context.turn để trả thẳng cho web bệnh nhân.
Không có công cụ thay đổi dữ liệu; không có công cụ HTTP/SQL tùy ý.
"""

from __future__ import annotations

from typing import Literal

from langchain.tools import ToolRuntime, tool

from app.backend import SessionExpired
from app.config_tables import Tables
from app.intents import ToolIntentHandler
from app.router import Routed
from app.schemas import ChatContext
from app.slots import match_specialty_name, parse_date, today_vietnam

SESSION_EXPIRED_NOTE = "Phiên đăng nhập của bệnh nhân đã hết hạn. Dừng lại, không gọi thêm công cụ."


def build_tools(tables: Tables, handler: ToolIntentHandler) -> list:
    async def run(ctx: ChatContext, intent: str, slots: dict | None = None, note: str = "") -> str:
        """Chạy đúng hàm của đường tắt. Câu trả lời theo mẫu (kèm note cho bệnh nhân) ghi vào context.turn: công cụ là
        return_direct nên bệnh nhân nhận câu mẫu này, không qua lần gọi model thứ hai (Giải pháp §11.3.5). Model chỉ thấy
        kết quả ở các lượt sau, dưới dạng dữ kiện có nhãn trường, không có Id."""
        turn = ctx.turn
        today = turn.today or today_vietnam()
        try:
            response, state = await handler.handle(Routed("SHORTCUT", intent, slots=slots or {}), ctx, {}, today)
        except SessionExpired:
            turn.session_expired = True
            return SESSION_EXPIRED_NOTE
        turn.intents.append(intent)
        turn.options.extend(response.options)
        turn.replies.append(f"{note} {response.reply}".strip())
        # Ví dụ hỏi ngày (COLLECTING): lượt sau bệnh nhân trả lời ngày thì đường tắt nối tiếp được.
        turn.state = state
        lines = [response.reply]
        if response.options:
            lines.append(f"Đã hiện {len(response.options)} lựa chọn cho bệnh nhân bấm. Dữ kiện từng dòng:")
            lines.extend(response.facts or [f"- {o.label}" for o in response.options])
        return "\n".join(lines)

    @tool(return_direct=True)
    async def search_doctors(date_text: str, runtime: ToolRuntime[ChatContext], specialty: str | None = None) -> str:
        """Tìm bác sĩ còn suất khám trong một ngày, kèm cấp bậc, chuyên khoa và phí khám.

        Args:
            date_text: ngày khám đúng như bệnh nhân nói, ví dụ "ngày mai", "thứ sáu", "20/11".
            specialty: tên chuyên khoa nếu bệnh nhân nói rõ, ví dụ "da liễu"; bỏ trống nếu bệnh nhân không nói.
        """
        ctx = runtime.context
        slots: dict = {}
        note = ""
        if specialty:
            code = match_specialty_name(tables, specialty)
            if code is None:
                # Không tự chọn khoa thay bệnh nhân: tìm mọi khoa và nói rõ.
                note = "Mình không thấy chuyên khoa bạn nhắc trong danh mục nhận đặt lịch, nên tìm ở mọi khoa."
            else:
                slots["specialty_code"] = code
        service_date = parse_date(date_text, ctx.turn.today or today_vietnam())
        if service_date is not None:
            slots["service_date"] = service_date.isoformat()
        # Không hiểu ngày: hàm tìm lịch hỏi lại ngày theo câu mẫu; không đoán ngày.
        return await run(ctx, "SEARCH_SLOTS", slots, note)

    @tool(return_direct=True)
    async def list_my_appointments(runtime: ToolRuntime[ChatContext]) -> str:
        """Liệt kê lịch đã hẹn / đã đặt sắp tới của bệnh nhân (mã lịch hẹn, ngày, bác sĩ, trạng thái). Không cần ngày."""
        return await run(runtime.context, "VIEW_APPOINTMENTS")

    @tool(return_direct=True)
    async def get_tickets(runtime: ToolRuntime[ChatContext]) -> str:
        """Xem vé khám: mã lịch hẹn, trạng thái và hạn check-in của các lịch sắp tới."""
        return await run(runtime.context, "VIEW_TICKET")

    @tool(return_direct=True)
    async def get_exam_payments(runtime: ToolRuntime[ChatContext]) -> str:
        """Xem tình hình phí khám: lịch đã thanh toán, lượt đặt đang chờ thanh toán, khoản đang chờ hoàn."""
        return await run(runtime.context, "VIEW_EXAM_PAYMENT")

    @tool(return_direct=True)
    async def guide_change(action: Literal["cancel", "reschedule"], runtime: ToolRuntime[ChatContext]) -> str:
        """Hướng dẫn hủy hoặc đổi lịch và liệt kê các lịch còn hủy/đổi được. Không tự hủy hay đổi lịch.

        Args:
            action: "cancel" để hủy lịch, "reschedule" để đổi lịch.
        """
        return await run(runtime.context, "CANCEL" if action == "cancel" else "RESCHEDULE")

    return [search_doctors, list_my_appointments, get_tickets, get_exam_payments, guide_change]
