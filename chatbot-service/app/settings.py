"""Cấu hình từ biến môi trường (Giải pháp kỹ thuật §11.7.2, contract §2). Secret chỉ đặt bằng biến môi trường, không commit."""

from __future__ import annotations

from pathlib import Path

from pydantic import Field, SecretStr
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=None, extra="ignore", case_sensitive=False)

    # Khóa .NET gửi trong X-Chatbot-Key (contract §2, Q-C4). Bắt buộc: thiếu thì dịch vụ không khởi động.
    chatbot_internal_key: SecretStr = Field(min_length=16)

    # Ollama trên Colab qua ngrok (§11.7). Để trống: chatbot tạm ngưng, đường tắt vẫn trả lời.
    ollama_base_url: str = ""
    ollama_api_key: SecretStr = SecretStr("")
    ollama_model: str = "qwen2.5:3b"
    ollama_timeout_seconds: float = Field(default=60, gt=0)
    health_timeout_seconds: float = Field(default=3, gt=0)
    health_cache_seconds: float = Field(default=5, ge=0)

    # Short-term Memory và chủ cuộc trò chuyện (§11.4.1, contract §3.3).
    data_dir: Path = Path("/data")
    conversation_ttl_minutes: int = Field(default=30, ge=1)
    purge_interval_seconds: int = Field(default=300, ge=10)

    config_dir: Path = Path(__file__).resolve().parent.parent / "config"

    @property
    def database_path(self) -> Path:
        return self.data_dir / "chatbot.sqlite3"
