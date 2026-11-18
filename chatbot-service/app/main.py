"""API nội bộ của dịch vụ chatbot (contract docs/api/chatbot.md §2). Chỉ mở trong mạng Docker; .NET là bên gọi duy nhất.

Mọi route trừ /health cần X-Chatbot-Key khớp CHATBOT_INTERNAL_KEY (lớp chặn thứ hai, lớp thứ nhất là không public).
Lỗi trả application/problem+json có `code` như quy ước của .NET (docs/conventions.md).

Chạy trên máy dev (trong chatbot-service/, cần uv):
    uv sync
    uv run pytest
    CHATBOT_INTERNAL_KEY=<ít nhất 16 ký tự> DATA_DIR=./data uv run uvicorn app.main:create_app --factory --port 8000
Không đặt OLLAMA_BASE_URL thì chatbot ở trạng thái tạm ngưng: đường tắt vẫn trả lời, câu tự do trả 503 ChatbotUnavailable.
"""

from __future__ import annotations

import asyncio
import logging
import secrets
from contextlib import AsyncExitStack, asynccontextmanager, suppress
from datetime import datetime, timedelta
from typing import Annotated, AsyncIterator, Callable
from uuid import UUID

import httpx
from fastapi import Depends, FastAPI, Header, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse, Response
from langchain_core.language_models import BaseChatModel
from langgraph.checkpoint.sqlite.aio import AsyncSqliteSaver

from app.agent.agent import LangChainAgent
from app.agent.model import build_model
from app.agent.tools import build_tools
from app.backend import BackendClient, SessionExpired
from app.config_tables import load_tables
from app.conversations import ConversationNotFound, ConversationStore, utcnow
from app.intents import ToolIntentHandler
from app.model_health import ModelHealth
from app.schemas import DeleteConversationRequest, HealthResponse, TurnRequest, TurnResponse
from app.settings import Settings
from app.tools.read_tools import ReadTools
from app.turns import ChatbotUnavailable, FreeTextAgent, NotConnectedAgent, TurnService

log = logging.getLogger("chatbot")


def problem(status: int, code: str, title: str) -> JSONResponse:
    return JSONResponse(
        {"type": "about:blank", "title": title, "status": status, "code": code},
        status_code=status,
        media_type="application/problem+json",
    )


class _Problem(Exception):
    def __init__(self, status: int, code: str, title: str) -> None:
        self.status, self.code, self.title = status, code, title


def create_app(settings: Settings | None = None, agent: FreeTextAgent | None = None,
               transport: httpx.AsyncBaseTransport | None = None, clock: Callable[[], datetime] = utcnow,
               model: BaseChatModel | None = None) -> FastAPI:
    """agent: thay toàn bộ đường câu tự do (test). model: dùng LangChainAgent với model khác Ollama (test bằng model giả).
    Không truyền gì: có OLLAMA_BASE_URL thì dùng agent thật, không thì câu tự do trả ChatbotUnavailable."""
    settings = settings or Settings()
    tables = load_tables(settings.config_dir)
    if not logging.getLogger().handlers:
        logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s %(message)s")

    @asynccontextmanager
    async def lifespan(app: FastAPI) -> AsyncIterator[None]:
        async with AsyncExitStack() as stack:
            store = ConversationStore(settings.database_path, timedelta(minutes=settings.conversation_ttl_minutes))
            await store.open()
            stack.push_async_callback(store.close)
            client = httpx.AsyncClient(transport=transport, headers={"User-Agent": "SmartHealthcare-Chatbot/0.1"})
            stack.push_async_callback(client.aclose)
            health = ModelHealth(settings, client)
            backend = BackendClient(settings.dotnet_api_base_url, client, settings.backend_timeout_seconds)
            handler = ToolIntentHandler(tables, ReadTools(backend))

            free_text: FreeTextAgent = agent or NotConnectedAgent()
            if agent is None and (model is not None or settings.ollama_base_url):
                settings.data_dir.mkdir(parents=True, exist_ok=True)
                saver = await stack.enter_async_context(AsyncSqliteSaver.from_conn_string(str(settings.checkpoint_path)))
                await saver.setup()  # tạo bảng ngay, để xóa cuộc trò chuyện chưa có checkpoint không lỗi
                free_text = LangChainAgent(
                    model if model is not None else build_model(settings),
                    build_tools(tables, handler),
                    saver,
                    settings.ollama_timeout_seconds,
                    settings.agent_max_messages,
                    tables.template("not_understood"),
                    tables.template("out_of_scope_free"),
                )

            app.state.store = store
            app.state.health = health
            app.state.agent = free_text
            app.state.turns = TurnService(tables, store, health, free_text, handler, clock)
            purge = asyncio.create_task(_purge_loop(store, free_text, settings.purge_interval_seconds))
            log.info("Chatbot sẵn sàng; cấu hình %s; agent %s", tables.version, type(free_text).__name__)
            try:
                yield
            finally:
                purge.cancel()
                with suppress(asyncio.CancelledError):
                    await purge

    app = FastAPI(title="Smart Healthcare chatbot (nội bộ)", lifespan=lifespan, docs_url=None, redoc_url=None, openapi_url=None)

    def require_key(x_chatbot_key: Annotated[str | None, Header()] = None) -> None:
        expected = settings.chatbot_internal_key.get_secret_value()
        if not x_chatbot_key or not secrets.compare_digest(x_chatbot_key.encode(), expected.encode()):
            raise _Problem(401, "InvalidChatbotKey", "Thiếu hoặc sai X-Chatbot-Key.")

    @app.exception_handler(_Problem)
    async def _on_problem(_: Request, error: _Problem) -> JSONResponse:
        return problem(error.status, error.code, error.title)

    @app.exception_handler(RequestValidationError)
    async def _on_invalid(_: Request, error: RequestValidationError) -> JSONResponse:
        # Không trả lại nội dung đầu vào (có thể chứa tin nhắn của bệnh nhân).
        return problem(400, "InvalidInput", "Dữ liệu gửi lên không hợp lệ.")

    @app.get("/health", response_model=HealthResponse)
    async def health(request: Request) -> HealthResponse:
        checker: ModelHealth = request.app.state.health
        return HealthResponse(available=await checker.available(), model=checker.model)

    @app.post("/v1/turns", response_model=TurnResponse, response_model_exclude_none=False, dependencies=[Depends(require_key)])
    async def turn(body: TurnRequest, request: Request,
                   authorization: Annotated[str | None, Header()] = None) -> TurnResponse | JSONResponse:
        token = _bearer(authorization)
        try:
            return await request.app.state.turns.handle(body, token)
        except ConversationNotFound:
            return problem(404, "ConversationNotFound", "Không tìm thấy cuộc trò chuyện hoặc đã hết hạn.")
        except ChatbotUnavailable:
            return problem(503, "ChatbotUnavailable", "Chatbot đang tạm ngưng, chỉ hoạt động trong buổi demo.")
        except SessionExpired:
            # .NET trả lại cho web bệnh nhân để đăng nhập lại; cuộc trò chuyện vẫn còn (contract §2.1, §3.2).
            return problem(401, "SessionExpired", "Phiên đăng nhập đã hết hạn.")

    @app.delete("/v1/conversations/{conversation_id}", status_code=204, dependencies=[Depends(require_key)])
    async def delete_conversation(conversation_id: UUID, body: DeleteConversationRequest, request: Request) -> Response:
        try:
            await request.app.state.store.delete(str(conversation_id), str(body.user_id))
        except ConversationNotFound:
            return problem(404, "ConversationNotFound", "Không tìm thấy cuộc trò chuyện.")
        await _forget(request.app.state.agent, [str(conversation_id)])
        return Response(status_code=204)

    return app


def _bearer(authorization: str | None) -> str:
    """Token Patient do .NET chuyển nguyên; chỉ dùng để gọi API .NET (bước 4), không giải mã, không ghi log."""
    scheme, _, token = (authorization or "").partition(" ")
    if scheme.lower() != "bearer" or not token.strip():
        raise _Problem(401, "MissingUserToken", "Thiếu Authorization: Bearer của người dùng.")
    return token.strip()


async def _forget(agent: FreeTextAgent, conversation_ids: list[str]) -> None:
    """Xóa Short-term Memory của agent (checkpoint) cùng lúc với cuộc trò chuyện."""
    forget = getattr(agent, "forget", None)
    if forget is None:
        return
    for conversation_id in conversation_ids:
        await forget(conversation_id)


async def _purge_loop(store: ConversationStore, agent: FreeTextAgent, interval_seconds: int) -> None:
    while True:
        await asyncio.sleep(interval_seconds)
        try:
            removed = await store.purge_expired(utcnow())
            await _forget(agent, removed)
            if removed:
                log.info("Đã xóa %d cuộc trò chuyện hết hạn", len(removed))
        except Exception:  # job nền không được làm chết dịch vụ
            log.exception("Lỗi khi xóa cuộc trò chuyện hết hạn")
