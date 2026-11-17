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
from contextlib import asynccontextmanager, suppress
from datetime import timedelta
from typing import Annotated, AsyncIterator
from uuid import UUID

import httpx
from fastapi import Depends, FastAPI, Header, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse, Response

from app.config_tables import load_tables
from app.conversations import ConversationNotFound, ConversationStore, utcnow
from app.model_health import ModelHealth
from app.schemas import DeleteConversationRequest, HealthResponse, TurnRequest, TurnResponse
from app.settings import Settings
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
               transport: httpx.AsyncBaseTransport | None = None) -> FastAPI:
    settings = settings or Settings()
    tables = load_tables(settings.config_dir)
    if not logging.getLogger().handlers:
        logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s %(message)s")

    @asynccontextmanager
    async def lifespan(app: FastAPI) -> AsyncIterator[None]:
        store = ConversationStore(settings.database_path, timedelta(minutes=settings.conversation_ttl_minutes))
        await store.open()
        client = httpx.AsyncClient(transport=transport, headers={"User-Agent": "SmartHealthcare-Chatbot/0.1"})
        health = ModelHealth(settings, client)
        app.state.store = store
        app.state.health = health
        app.state.turns = TurnService(tables, store, health, agent or NotConnectedAgent())
        purge = asyncio.create_task(_purge_loop(store, settings.purge_interval_seconds))
        log.info("Chatbot sẵn sàng; cấu hình %s", tables.version)
        try:
            yield
        finally:
            purge.cancel()
            with suppress(asyncio.CancelledError):
                await purge
            await client.aclose()
            await store.close()

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

    @app.delete("/v1/conversations/{conversation_id}", status_code=204, dependencies=[Depends(require_key)])
    async def delete_conversation(conversation_id: UUID, body: DeleteConversationRequest, request: Request) -> Response:
        try:
            await request.app.state.store.delete(str(conversation_id), str(body.user_id))
        except ConversationNotFound:
            return problem(404, "ConversationNotFound", "Không tìm thấy cuộc trò chuyện.")
        return Response(status_code=204)

    return app


def _bearer(authorization: str | None) -> str:
    """Token Patient do .NET chuyển nguyên; chỉ dùng để gọi API .NET (bước 4), không giải mã, không ghi log."""
    scheme, _, token = (authorization or "").partition(" ")
    if scheme.lower() != "bearer" or not token.strip():
        raise _Problem(401, "MissingUserToken", "Thiếu Authorization: Bearer của người dùng.")
    return token.strip()


async def _purge_loop(store: ConversationStore, interval_seconds: int) -> None:
    while True:
        await asyncio.sleep(interval_seconds)
        try:
            removed = await store.purge_expired(utcnow())
            if removed:
                log.info("Đã xóa %d cuộc trò chuyện hết hạn", removed)
        except Exception:  # job nền không được làm chết dịch vụ
            log.exception("Lỗi khi xóa cuộc trò chuyện hết hạn")
