"""Model nền qua Ollama trên Colab (Giải pháp kỹ thuật §11.4.1, §11.7.2). Không dùng API model của nhà cung cấp, không fine-tune."""

from __future__ import annotations

from langchain_ollama import ChatOllama

from app.settings import Settings


def build_model(settings: Settings) -> ChatOllama:
    return ChatOllama(
        base_url=settings.ollama_base_url.rstrip("/"),  # static domain ngrok tới Ollama trên Colab
        model=settings.ollama_model,  # tag đã ghim, trùng tag pull trong notebook
        temperature=0,  # trả lời ổn định để bộ kịch bản so được giữa các lần chạy
        num_predict=settings.ollama_num_predict,
        keep_alive=-1,  # giữ model trong bộ nhớ Colab giữa các lượt
        client_kwargs={
            "headers": {"x-api-key": settings.ollama_api_key.get_secret_value()},
            "timeout": settings.ollama_timeout_seconds,
        },
    )
