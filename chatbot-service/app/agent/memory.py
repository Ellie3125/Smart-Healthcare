"""Cửa sổ Short-term Memory (Giải pháp kỹ thuật §11.4.1): giữ các tin nhắn gần nhất của cuộc trò chuyện trước mỗi lần gọi model.

Prompt càng dài model trên Colab càng chậm (§11.3.5), nên chỉ giữ cửa sổ cố định; số tin nhắn chốt sau khi đo (§11.7.3).
Đồng thời sửa lịch sử hỏng: lượt trước bị ngắt (timeout, Colab tắt) có thể để lại lời gọi công cụ chưa có kết quả, hoặc kết quả công
cụ mồ côi; model từ chối lịch sử như vậy, nên bỏ chúng trước khi gọi.
"""

from __future__ import annotations

from typing import Any

from langchain.agents.middleware import AgentMiddleware
from langchain_core.messages import AIMessage, AnyMessage, HumanMessage, RemoveMessage, ToolMessage
from langgraph.graph.message import REMOVE_ALL_MESSAGES


def sanitize(messages: list[AnyMessage]) -> list[AnyMessage]:
    """Bỏ AIMessage có lời gọi công cụ chưa đủ kết quả và ToolMessage không thuộc lời gọi nào còn giữ."""
    answered = {m.tool_call_id for m in messages if isinstance(m, ToolMessage)}
    kept_calls: set[str] = set()
    result: list[AnyMessage] = []
    for m in messages:
        if isinstance(m, AIMessage) and m.tool_calls:
            ids = {c["id"] for c in m.tool_calls}
            if not ids <= answered:
                continue
            kept_calls |= ids
        if isinstance(m, ToolMessage) and m.tool_call_id not in kept_calls:
            continue
        result.append(m)
    return result


def window(messages: list[AnyMessage], max_messages: int) -> list[AnyMessage]:
    """Cửa sổ gần nhất, bắt đầu bằng câu của bệnh nhân (không cắt giữa một lần gọi công cụ)."""
    clean = sanitize(messages)
    if len(clean) > max_messages:
        clean = clean[-max_messages:]
    while clean and not isinstance(clean[0], HumanMessage):
        clean = clean[1:]
    return sanitize(clean)


class ShortTermWindow(AgentMiddleware):
    def __init__(self, max_messages: int) -> None:
        super().__init__()
        self.max_messages = max_messages

    def before_model(self, state: Any, runtime: Any) -> dict | None:
        messages = state["messages"]
        kept = window(messages, self.max_messages)
        if [m.id for m in kept] == [m.id for m in messages]:
            return None
        return {"messages": [RemoveMessage(id=REMOVE_ALL_MESSAGES), *kept]}

    async def abefore_model(self, state: Any, runtime: Any) -> dict | None:
        return self.before_model(state, runtime)
