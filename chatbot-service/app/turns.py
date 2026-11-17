"""Một lượt chat (contract §2.1): định tuyến đường tắt trước, câu tự do mới tới model.

Model tạm ngưng, lỗi hoặc quá hạn thì lượt dừng với ChatbotUnavailable: không gọi công cụ nào, State giữ nguyên
(Giải pháp kỹ thuật §11.7.5). Đường tắt vẫn trả lời khi model tạm ngưng.
"""

from __future__ import annotations

import logging
import time
from typing import Protocol

from app.config_tables import Tables
from app.conversations import ConversationStore, utcnow
from app.model_health import ModelHealth
from app.replies import shortcut_reply
from app.router import route
from app.schemas import ChatContext, ChatState, TurnRequest, TurnResponse

log = logging.getLogger("chatbot.turns")


class ChatbotUnavailable(Exception):
    """Model không dùng được cho lượt này (Colab tắt, ngrok 401, quá OLLAMA_TIMEOUT_SECONDS, chưa nối agent)."""


class FreeTextAgent(Protocol):
    """Xử lý câu tự do bằng model. Bước 7: LangChain create_agent với state_schema, context_schema, checkpointer."""

    async def run(self, context: ChatContext, state: ChatState, text: str) -> tuple[TurnResponse, ChatState]: ...


class NotConnectedAgent:
    """Chưa nối model (trước bước 7): câu tự do coi như chatbot tạm ngưng."""

    async def run(self, context: ChatContext, state: ChatState, text: str) -> tuple[TurnResponse, ChatState]:
        raise ChatbotUnavailable


class TurnService:
    def __init__(self, tables: Tables, store: ConversationStore, health: ModelHealth, agent: FreeTextAgent) -> None:
        self._tables = tables
        self._store = store
        self._health = health
        self._agent = agent

    async def handle(self, request: TurnRequest, access_token: str) -> TurnResponse:
        started = time.perf_counter()
        context = ChatContext(
            user_id=str(request.user_id),
            patient_id=str(request.patient_id),
            conversation_id=str(request.conversation_id),
            access_token=access_token,
            message_id=str(request.message_id),
            locale=request.locale,
        )
        async with self._store.turn(context.conversation_id, context.user_id, context.patient_id, utcnow()) as conversation:
            routed = route(self._tables, conversation.state, request.text, request.suggestion)
            if routed.route == "SHORTCUT":
                response, conversation.state = shortcut_reply(self._tables, routed)
            else:
                if not await self._health.available():
                    self._log("MODEL", None, started, "unavailable")
                    raise ChatbotUnavailable
                assert request.text is not None
                response, conversation.state = await self._agent.run(context, conversation.state, request.text)
        self._log(routed.route, response.intent, started, "ok")
        return response

    @staticmethod
    def _log(route_name: str, intent: str | None, started: float, outcome: str) -> None:
        # Không ghi nội dung tin nhắn, token hay định danh (contract §7).
        log.info("turn route=%s intent=%s outcome=%s ms=%d", route_name, intent, outcome, (time.perf_counter() - started) * 1000)
