"""Gọi REST API công khai của .NET thay người dùng (contract docs/api/chatbot.md §5).

Mỗi lời gọi mang token Patient của chính người dùng, nên .NET kiểm quyền như với web bệnh nhân (BOT-04). Chatbot không có
quyền nào hơn người dùng, không gọi contract module hay DB. Không ghi token, URL đầy đủ hay nội dung phản hồi vào log.
"""

from __future__ import annotations

import logging
from typing import Any

import httpx

log = logging.getLogger("chatbot.backend")


class BackendError(Exception):
    """Lỗi khi gọi .NET. code lấy từ ProblemDetails (theo `code`, không theo `title` — contract §5.2)."""

    def __init__(self, status: int | None, code: str | None = None) -> None:
        super().__init__(f"{status} {code}")
        self.status = status
        self.code = code


class SessionExpired(BackendError):
    """Token của người dùng hết hạn (401): lượt trả SessionExpired để web bệnh nhân đăng nhập lại."""


class BackendNotFound(BackendError):
    """403/404: không phân biệt không có và không có quyền."""


class BackendRejected(BackendError):
    """400/409: .NET từ chối theo quy tắc nghiệp vụ; code cho biết lý do."""


class BackendUnavailable(BackendError):
    """5xx, timeout, lỗi mạng: không đoán kết quả, trả lời "chưa lấy được dữ liệu" (BOT-06)."""


class BackendClient:
    def __init__(self, base_url: str, client: httpx.AsyncClient, timeout_seconds: float) -> None:
        self._base = base_url.rstrip("/")
        self._client = client
        self._timeout = timeout_seconds

    async def get(self, path: str, token: str, params: dict[str, Any] | None = None) -> Any:
        try:
            response = await self._client.get(
                f"{self._base}{path}",
                params={k: v for k, v in (params or {}).items() if v is not None},
                headers={"Authorization": f"Bearer {token}", "Accept": "application/json"},
                timeout=self._timeout,
            )
        except httpx.HTTPError as error:
            log.warning("Gọi .NET lỗi: %s", type(error).__name__)
            raise BackendUnavailable(None) from error

        status = response.status_code
        if status < 300:
            try:
                return response.json()
            except ValueError as error:
                raise BackendUnavailable(status) from error

        code = _problem_code(response)
        log.info("Gọi .NET trả %s %s", status, code)
        if status == 401:
            raise SessionExpired(status, code)
        if status in (403, 404):
            raise BackendNotFound(status, code)
        if status in (400, 409, 422):
            raise BackendRejected(status, code)
        raise BackendUnavailable(status, code)


def _problem_code(response: httpx.Response) -> str | None:
    try:
        body = response.json()
    except ValueError:
        return None
    return body.get("code") if isinstance(body, dict) else None
