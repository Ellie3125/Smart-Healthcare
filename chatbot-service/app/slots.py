"""Trích slot từ câu của bệnh nhân không cần model: ngày khám và chuyên khoa theo tên (Giải pháp §11.3.2, §11.3.5).

Ngày tính theo giờ Việt Nam (UTC+7, không có giờ mùa hè). Nhận: hôm nay, (ngày/sáng/chiều) mai, ngày kia/mốt, thứ hai..chủ nhật
(kèm "tuần sau/tới"), dd/mm, dd/mm/yyyy, dd-mm, "ngày 20", "ngày 20 tháng 11". Không nhận được thì trả None (hỏi lại hoặc để model).
"""

from __future__ import annotations

import re
from datetime import UTC, date, datetime, timedelta, timezone

from app.config_tables import Tables
from app.textnorm import Message, Phrase, plain

VIETNAM = timezone(timedelta(hours=7))

# Viết có dấu: câu có dấu so với dạng có dấu, nên "số thứ tự" không bị hiểu là "thứ tư" (xem textnorm).
_WEEKDAYS = [  # Monday = 0
    (0, ["thứ hai", "thứ 2"]),
    (1, ["thứ ba", "thứ 3"]),
    (2, ["thứ tư", "thứ 4"]),
    (3, ["thứ năm", "thứ 5"]),
    (4, ["thứ sáu", "thứ 6"]),
    (5, ["thứ bảy", "thứ 7"]),
    (6, ["chủ nhật", "cn"]),
]
_WEEKDAY_PHRASES = [(day, [Phrase.of(p) for p in phrases]) for day, phrases in _WEEKDAYS]
_RELATIVE = [(0, ["hôm nay"]), (1, ["ngày mai", "sáng mai", "chiều mai"]), (2, ["ngày kia", "ngày mốt"])]
_RELATIVE_PHRASES = [(offset, [Phrase.of(p) for p in phrases]) for offset, phrases in _RELATIVE]
_NEXT_WEEK = [Phrase.of(p) for p in ("tuần sau", "tuần tới")]
_NUMERIC = re.compile(r"(?<!\d)(\d{1,2})\s*[/-]\s*(\d{1,2})(?:\s*[/-]\s*(\d{4}))?(?!\d)")
_DAY_MONTH_WORDS = re.compile(r"\bngay (\d{1,2})(?: thang (\d{1,2}))?(?: nam (\d{4}))?\b")


def today_vietnam(now: datetime | None = None) -> date:
    return (now or datetime.now(UTC)).astimezone(VIETNAM).date()


def parse_date(text: str, today: date) -> date | None:
    # Dấu "/" và "-" bị bỏ khi chuẩn hóa, nên đọc dạng số trên chuỗi gốc.
    numeric = _NUMERIC.search(text)
    if numeric:
        day, month, year = int(numeric.group(1)), int(numeric.group(2)), numeric.group(3)
        return _build(day, month, int(year) if year else None, today)

    t = f" {plain(text)} "
    words = _DAY_MONTH_WORDS.search(t)
    if words:
        day = int(words.group(1))
        month = int(words.group(2)) if words.group(2) else None
        year = int(words.group(3)) if words.group(3) else None
        if month is None:
            candidate = _safe(today.year, today.month, day)
            if candidate is not None and candidate < today:
                nxt = today.replace(day=1) + timedelta(days=32)
                candidate = _safe(nxt.year, nxt.month, day)
            return candidate
        return _build(day, month, year, today)

    message = Message.of(text)
    for offset, phrases in _RELATIVE_PHRASES:
        if message.any(phrases):
            return today + timedelta(days=offset)
    if t.strip() == "mai":
        return today + timedelta(days=1)

    for weekday, phrases in _WEEKDAY_PHRASES:
        if message.any(phrases):
            target = today - timedelta(days=today.weekday()) + timedelta(days=weekday)
            if message.any(_NEXT_WEEK):
                target += timedelta(days=7)
            elif target < today:
                target += timedelta(days=7)
            return target
    return None


def _build(day: int, month: int, year: int | None, today: date) -> date | None:
    if year is not None:
        return _safe(year, month, day)
    candidate = _safe(today.year, month, day)
    # Không ghi năm mà ngày đã qua trong năm nay: hiểu là năm sau (ví dụ tháng 12 hỏi "05/01").
    if candidate is not None and candidate < today:
        candidate = _safe(today.year + 1, month, day)
    return candidate


def _safe(year: int, month: int, day: int) -> date | None:
    try:
        return date(year, month, day)
    except ValueError:
        return None


def match_specialty_name(tables: Tables, text: str) -> str | None:
    """Mã chuyên khoa khi câu nhắc tên khoa (tên đầy đủ hoặc tên gọi khác); nhiều khoa thì lấy khoa khai trước."""
    message = Message.of(text)
    for code, phrases in tables.specialty_names:
        if message.any(phrases):
            return code
    return None


def format_date(value: date) -> str:
    return value.strftime("%d/%m/%Y")
