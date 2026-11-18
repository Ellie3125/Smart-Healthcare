"""Model có dùng được không (Giải pháp kỹ thuật §11.7.5, contract §2.2).

available = gọi được OLLAMA_BASE_URL/api/tags kèm x-api-key trong thời gian ngắn VÀ có tag OLLAMA_MODEL. False khi chưa cấu hình,
lỗi kết nối, 401 của ngrok, trang lỗi ngrok khi Colab tắt (không phải JSON), hoặc thiếu tag. Cache vài giây để không gọi ngrok liên tục.
"""

from __future__ import annotations

import asyncio
import logging
import time

import httpx

from app.settings import Settings

log = logging.getLogger("chatbot.health")


class ModelHealth:
    def __init__(self, settings: Settings, client: httpx.AsyncClient) -> None:
        self._settings = settings
        self._client = client
        self._lock = asyncio.Lock()
        self._checked_at = float("-inf")
        self._available = False

    @property
    def model(self) -> str:
        return self._settings.ollama_model

    async def available(self) -> bool:
        async with self._lock:
            if time.monotonic() - self._checked_at < self._settings.health_cache_seconds:
                return self._available
            self._available = await self._probe()
            self._checked_at = time.monotonic()
            return self._available

    async def _probe(self) -> bool:
        base = self._settings.ollama_base_url.rstrip("/")
        if not base:
            return False
        try:
            response = await self._client.get(
                f"{base}/api/tags",
                headers={"x-api-key": self._settings.ollama_api_key.get_secret_value()},
                timeout=self._settings.health_timeout_seconds,
            )
            if response.status_code != 200:
                log.info("Model tạm ngưng: /api/tags trả %s", response.status_code)
                return False
            models = response.json().get("models", [])
        except (httpx.HTTPError, ValueError) as error:
            # Không ghi địa chỉ ngrok vào log.
            log.info("Model tạm ngưng: %s", type(error).__name__)
            return False

        wanted = self._settings.ollama_model
        names = {m.get("name") for m in models} | {m.get("model") for m in models}
        if wanted in names or f"{wanted}:latest" in names:
            return True
        log.info("Model tạm ngưng: Ollama chưa có tag đã ghim")
        return False
