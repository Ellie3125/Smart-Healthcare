"""Chuẩn hóa câu tiếng Việt để so khớp cụm từ trong bảng cấu hình.

Bệnh nhân có thể gõ có dấu hoặc không dấu. Bỏ dấu cả hai phía làm "ngạt mũi" khớp nhầm dấu hiệu nguy hiểm "ngất", nên:
câu có dấu thì so với cụm có dấu; chỉ câu gõ hoàn toàn không dấu mới so với cụm đã bỏ dấu.
"""

from __future__ import annotations

import re
import unicodedata
from dataclasses import dataclass

_NON_WORD = re.compile(r"[^\w]+", re.UNICODE)


def _collapse(text: str) -> str:
    return " ".join(_NON_WORD.sub(" ", text).split())


def accented(text: str) -> str:
    """Chữ thường, dạng NFC, bỏ dấu câu, gộp khoảng trắng; giữ dấu tiếng Việt."""
    return _collapse(unicodedata.normalize("NFC", text).lower())


def plain(text: str) -> str:
    """Như accented() nhưng bỏ dấu tiếng Việt (đ → d)."""
    decomposed = unicodedata.normalize("NFD", accented(text))
    stripped = "".join(c for c in decomposed if unicodedata.category(c) != "Mn")
    return stripped.replace("đ", "d")


def has_diacritics(text: str) -> bool:
    return accented(text) != plain(text)


@dataclass(frozen=True)
class Phrase:
    """Một cụm từ cấu hình, giữ sẵn hai dạng để so khớp."""

    raw: str
    accented: str
    plain: str

    @staticmethod
    def of(raw: str) -> Phrase:
        return Phrase(raw, accented(raw), plain(raw))


@dataclass(frozen=True)
class Message:
    """Câu của người dùng đã chuẩn hóa; matches() so khớp nguyên từ theo đúng chế độ có dấu/không dấu."""

    text: str
    accent_mode: bool

    @staticmethod
    def of(raw: str) -> Message:
        return Message(f" {accented(raw)} ", True) if has_diacritics(raw) else Message(f" {plain(raw)} ", False)

    def matches(self, phrase: Phrase) -> bool:
        needle = phrase.accented if self.accent_mode else phrase.plain
        return bool(needle) and f" {needle} " in self.text

    def any(self, phrases: list[Phrase]) -> bool:
        return any(self.matches(p) for p in phrases)
