"""Cuộc trò chuyện: chủ sở hữu và State giữa các lượt (contract §3.3 đề xuất Q-C2; Giải pháp §11.4.1).

Lưu trong SQLite trên volume /data của dịch vụ chatbot (tệp riêng cạnh tệp checkpointer của agent): restart không mất, không
thêm bảng ở DB nghiệp vụ. Dịch vụ chạy một instance nên SQLite đủ dùng.

- Lượt đầu với conversationId mới tạo bản ghi gắn userId, patientId; lượt sau khác userId/patientId → ConversationNotFound.
- Không hoạt động quá TTL thì coi như hết hạn (404) và bị dọn định kỳ.
- Chỉ lưu State (intent, stage, slot đã chuẩn hóa); không lưu nội dung tin nhắn, token, mô tả triệu chứng.
"""

from __future__ import annotations

import asyncio
import json
from collections import defaultdict
from contextlib import asynccontextmanager
from dataclasses import dataclass
from datetime import UTC, datetime, timedelta
from pathlib import Path
from typing import AsyncIterator

import aiosqlite

from app.schemas import ChatState

_SCHEMA = """
CREATE TABLE IF NOT EXISTS conversation (
    conversation_id TEXT PRIMARY KEY,
    user_id         TEXT NOT NULL,
    patient_id      TEXT NOT NULL,
    created_utc     TEXT NOT NULL,
    last_active_utc TEXT NOT NULL,
    state_json      TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_conversation_last_active ON conversation (last_active_utc);
"""


class ConversationNotFound(Exception):
    """Không có, không thuộc người gọi, hoặc đã hết hạn. Không phân biệt các trường hợp để không lộ cuộc trò chuyện của người khác."""


@dataclass
class Conversation:
    conversation_id: str
    user_id: str
    patient_id: str
    state: ChatState


class ConversationStore:
    def __init__(self, path: Path, ttl: timedelta) -> None:
        self._path = path
        self._ttl = ttl
        self._db: aiosqlite.Connection | None = None
        # Tuần tự hóa các lượt của cùng một cuộc trò chuyện (hai lượt song song không ghi đè State của nhau).
        self._locks: defaultdict[str, asyncio.Lock] = defaultdict(asyncio.Lock)

    async def open(self) -> None:
        self._path.parent.mkdir(parents=True, exist_ok=True)
        self._db = await aiosqlite.connect(self._path)
        await self._db.execute("PRAGMA journal_mode=WAL")
        await self._db.executescript(_SCHEMA)
        await self._db.commit()

    async def close(self) -> None:
        if self._db is not None:
            await self._db.close()
            self._db = None

    @property
    def _conn(self) -> aiosqlite.Connection:
        if self._db is None:
            raise RuntimeError("ConversationStore chưa open().")
        return self._db

    @asynccontextmanager
    async def turn(self, conversation_id: str, user_id: str, patient_id: str, now: datetime) -> AsyncIterator[Conversation]:
        """Giữ khóa cuộc trò chuyện trong một lượt; State gán vào conversation.state được lưu khi thoát khối không lỗi."""
        async with self._locks[conversation_id]:
            conversation = await self._load_or_create(conversation_id, user_id, patient_id, now)
            yield conversation
            await self._conn.execute(
                "UPDATE conversation SET state_json = ?, last_active_utc = ? WHERE conversation_id = ?",
                (json.dumps(conversation.state, ensure_ascii=False), _iso(now), conversation_id),
            )
            await self._conn.commit()

    async def delete(self, conversation_id: str, user_id: str) -> None:
        """Xóa khi đúng chủ; không có thì coi như đã xóa. Khác chủ → ConversationNotFound."""
        async with self._locks[conversation_id]:
            row = await self._fetch(conversation_id)
            if row is None:
                return
            if row[1] != user_id:
                raise ConversationNotFound
            await self._conn.execute("DELETE FROM conversation WHERE conversation_id = ?", (conversation_id,))
            await self._conn.commit()
        self._locks.pop(conversation_id, None)

    async def purge_expired(self, now: datetime) -> list[str]:
        """Xóa cuộc trò chuyện quá hạn; trả Id để xóa luôn Short-term Memory của agent."""
        cutoff = _iso(now - self._ttl)
        cursor = await self._conn.execute("SELECT conversation_id FROM conversation WHERE last_active_utc < ?", (cutoff,))
        ids = [row[0] for row in await cursor.fetchall()]
        if ids:
            await self._conn.executemany("DELETE FROM conversation WHERE conversation_id = ?", [(i,) for i in ids])
            await self._conn.commit()
        return ids

    async def _fetch(self, conversation_id: str) -> tuple | None:
        cursor = await self._conn.execute(
            "SELECT conversation_id, user_id, patient_id, last_active_utc, state_json FROM conversation WHERE conversation_id = ?",
            (conversation_id,),
        )
        return await cursor.fetchone()

    async def _load_or_create(self, conversation_id: str, user_id: str, patient_id: str, now: datetime) -> Conversation:
        row = await self._fetch(conversation_id)
        if row is None:
            await self._conn.execute(
                "INSERT INTO conversation (conversation_id, user_id, patient_id, created_utc, last_active_utc, state_json)"
                " VALUES (?, ?, ?, ?, ?, '{}')",
                (conversation_id, user_id, patient_id, _iso(now), _iso(now)),
            )
            await self._conn.commit()
            return Conversation(conversation_id, user_id, patient_id, {})

        _, owner, patient, last_active, state_json = row
        if owner != user_id or patient != patient_id:
            raise ConversationNotFound
        if datetime.fromisoformat(last_active) < now - self._ttl:
            await self._conn.execute("DELETE FROM conversation WHERE conversation_id = ?", (conversation_id,))
            await self._conn.commit()
            raise ConversationNotFound
        return Conversation(conversation_id, owner, patient, json.loads(state_json))


def _iso(moment: datetime) -> str:
    # Độ dài cố định để so sánh chuỗi trong SQL đúng thứ tự thời gian.
    return moment.astimezone(UTC).isoformat(timespec="microseconds")


def utcnow() -> datetime:
    return datetime.now(UTC)
