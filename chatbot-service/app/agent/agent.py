"""Agent cho câu tự do (Giải pháp kỹ thuật §8.7, §11.3, §11.4): LangChain create_agent với model nền qua Ollama trên Colab.

Không tự dựng graph LangGraph. Bốn lớp ánh xạ vào tham số có sẵn: Data Schema = DTO của công cụ, State Schema = AgentChatState,
Context Schema = ChatContext (runtime context, không lưu checkpoint), Short-term Memory = checkpointer SQLite theo thread_id =
conversationId cùng cửa sổ ShortTermWindow.

Model lỗi kết nối, 401 của ngrok hoặc quá OLLAMA_TIMEOUT_SECONDS → ChatbotUnavailable: lượt dừng, không đổi dữ liệu (công cụ chỉ đọc).
Token hết hạn trong lúc gọi công cụ → SessionExpired sau khi lượt kết thúc gọn (lịch sử không bị dở dang).
"""

from __future__ import annotations

import asyncio
import logging
from typing import NotRequired

from langchain.agents import AgentState, create_agent
from langchain_core.language_models import BaseChatModel
from langchain_core.messages import AIMessage, HumanMessage
from langgraph.checkpoint.base import BaseCheckpointSaver

from app.agent.memory import ShortTermWindow
from app.agent.prompt import SYSTEM_PROMPT
from app.backend import SessionExpired
from app.schemas import ChatContext, ChatState, Intent, Stage, TurnResponse
from app.turns import ChatbotUnavailable

log = logging.getLogger("chatbot.agent")

MAX_OPTIONS = 5


class AgentChatState(AgentState):
    """State Schema (§11.3.2) trên state của agent. Hội thoại của đường tắt lưu riêng ở ConversationStore."""

    intent: NotRequired[Intent]
    stage: NotRequired[Stage]
    slots: NotRequired[dict]
    missing_slots: NotRequired[list[str]]
    proposal_id: NotRequired[str]


class LangChainAgent:
    def __init__(self, model: BaseChatModel, tools: list, checkpointer: BaseCheckpointSaver, timeout_seconds: float,
                 max_messages: int, fallback_reply: str) -> None:
        self._checkpointer = checkpointer
        self._timeout = timeout_seconds
        self._fallback = fallback_reply
        self._graph = create_agent(
            model=model,
            tools=tools,
            system_prompt=SYSTEM_PROMPT,
            middleware=[ShortTermWindow(max_messages)],
            state_schema=AgentChatState,
            context_schema=ChatContext,
            checkpointer=checkpointer,
        )

    async def run(self, context: ChatContext, state: ChatState, text: str) -> tuple[TurnResponse, ChatState]:
        try:
            result = await asyncio.wait_for(
                self._graph.ainvoke(
                    {"messages": [HumanMessage(text)]},
                    config={"configurable": {"thread_id": context.conversation_id}},
                    context=context,
                ),
                timeout=self._timeout,
            )
        except SessionExpired:
            raise
        except Exception as error:  # model/ngrok/Colab: không đoán, dừng lượt (§11.7.5)
            log.info("Model không trả lời: %s", type(error).__name__)
            raise ChatbotUnavailable from error

        turn = context.turn
        if turn.session_expired:
            raise SessionExpired(401)

        # Công cụ return_direct: bệnh nhân nhận câu mẫu của công cụ (không qua model lần hai). Không gọi công cụ: lời của model.
        # Model trả rỗng (model nhỏ không hiểu câu): câu "chưa hiểu" kèm gợi ý, không đoán.
        reply = "\n".join(turn.replies) or _last_ai_text(result["messages"]) or self._fallback
        intent = turn.intents[-1] if turn.intents else None
        suggestions = [] if turn.replies else ["SEARCH_SLOTS", "VIEW_APPOINTMENTS", "SUGGEST_SPECIALTY"]
        response = TurnResponse(reply=reply, intent=intent, options=turn.options[:MAX_OPTIONS],
                                suggestions=suggestions if reply == self._fallback else [])
        new_state: ChatState = turn.state or ({"intent": intent, "stage": "DONE"} if intent else {})
        return response, new_state

    async def forget(self, conversation_id: str) -> None:
        """Xóa Short-term Memory khi cuộc trò chuyện bị đóng hoặc hết hạn (§11.4.1)."""
        await self._checkpointer.adelete_thread(conversation_id)


def _last_ai_text(messages: list) -> str:
    for message in reversed(messages):
        if isinstance(message, AIMessage) and not message.tool_calls:
            content = message.content if isinstance(message.content, str) else ""
            return content.strip()
    return ""
