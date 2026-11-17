"""Nạp và kiểm hai bảng cấu hình: định tuyến (config/routing.yaml) và gợi ý chuyên khoa (config/specialty_hints.yaml).

Cấu hình sai thì dịch vụ không khởi động (lỗi ngay, không chạy với bảng hỏng). Mỗi lần đổi bảng phải chạy lại bộ kịch bản.
"""

from __future__ import annotations

from pathlib import Path
from typing import Literal

import yaml
from pydantic import BaseModel, ConfigDict, Field, field_validator, model_validator

from app.schemas import ToolIntent
from app.textnorm import Phrase

REQUIRED_TEMPLATES = (
    "emergency",
    "medical_advice",
    "help",
    "specialty_ask",
    "specialty_match",
    "specialty_fallback",
    "tool_pending",
    "backend_unavailable",
    "ask_date",
    "past_date",
    "slots_found",
    "slots_none",
    "appointments_none",
    "appointments_list",
    "ticket_hint",
    "payments_none",
    "payments_header",
    "change_manual",
    "change_none",
)


class _Strict(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True)


class Specialty(_Strict):
    code: str = Field(pattern=r"^[A-Z][A-Z0-9_]*$")
    name: str = Field(min_length=1)
    description: str = Field(min_length=1)
    aliases: list[str] = []


class SpecialtyHint(_Strict):
    specialty: str
    body_parts: list[str] = []
    keywords: list[str] = []


class SpecialtyTable(_Strict):
    version: str
    status: Literal["draft", "approved"]
    reviewed_by: str | None = None
    red_flags: list[str] = Field(min_length=1)
    specialties: list[Specialty] = Field(min_length=1)
    specialty_hints: list[SpecialtyHint]
    max_suggestions: int = Field(ge=1, le=3)
    disclaimer: str = Field(min_length=1)

    @model_validator(mode="after")
    def _consistent(self) -> SpecialtyTable:
        codes = [s.code for s in self.specialties]
        if len(codes) != len(set(codes)):
            raise ValueError("specialties có mã trùng.")
        unknown = {h.specialty for h in self.specialty_hints} - set(codes)
        if unknown:
            raise ValueError(f"specialty_hints dùng mã không khai trong specialties: {sorted(unknown)}")
        if self.status == "approved" and not self.reviewed_by:
            raise ValueError("Bảng đã duyệt (approved) phải ghi reviewed_by.")
        return self


class RoutingTable(_Strict):
    version: str
    medical_advice: list[str] = Field(min_length=1)
    help: list[str] = []
    intents: dict[ToolIntent | Literal["SUGGEST_SPECIALTY"], list[str]]
    templates: dict[str, str]
    status_labels: dict[str, str] = {}
    refund_status_labels: dict[str, str] = {}

    @field_validator("templates")
    @classmethod
    def _templates_complete(cls, value: dict[str, str]) -> dict[str, str]:
        missing = [k for k in REQUIRED_TEMPLATES if not value.get(k)]
        if missing:
            raise ValueError(f"Thiếu câu mẫu: {missing}")
        return value


class Tables:
    """Hai bảng đã kiểm, kèm cụm từ đã chuẩn hóa sẵn để so khớp."""

    def __init__(self, routing: RoutingTable, specialties: SpecialtyTable) -> None:
        self.routing = routing
        self.specialties = specialties
        self.red_flags = [Phrase.of(p) for p in specialties.red_flags]
        self.medical_advice = [Phrase.of(p) for p in routing.medical_advice]
        self.help = [Phrase.of(p) for p in routing.help]
        # Giữ thứ tự khai báo: intent đứng trước được ưu tiên.
        self.intents = [(intent, [Phrase.of(p) for p in phrases]) for intent, phrases in routing.intents.items()]
        self.specialty_by_code = {s.code: s for s in specialties.specialties}
        # Tên khoa và tên gọi khác, để nhận "khám da liễu" là tìm lịch khoa Da liễu.
        self.specialty_names = [
            (s.code, [Phrase.of(p) for p in [s.name, *s.aliases]]) for s in specialties.specialties
        ]
        self.hints = [
            (h.specialty, [Phrase.of(p) for p in h.keywords], [Phrase.of(p) for p in h.body_parts])
            for h in specialties.specialty_hints
        ]

    def template(self, key: str, **values: str) -> str:
        return self.routing.templates[key].format(**values)

    @property
    def version(self) -> str:
        """Phiên bản cấu hình, ghi kèm kết quả bộ kịch bản (Giải pháp §11.2)."""
        return f"routing {self.routing.version}; specialty {self.specialties.version} ({self.specialties.status})"


def load_tables(config_dir: Path) -> Tables:
    def read(name: str) -> dict:
        with (config_dir / name).open(encoding="utf-8") as f:
            return yaml.safe_load(f)

    return Tables(RoutingTable.model_validate(read("routing.yaml")), SpecialtyTable.model_validate(read("specialty_hints.yaml")))
