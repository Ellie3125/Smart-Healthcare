# syntax=docker/dockerfile:1
# Dịch vụ chatbot (FastAPI). Chỉ mở trong mạng Docker nội bộ, .NET là bên gọi duy nhất (contract docs/api/chatbot.md §2).
#   docker build -t <registry>/smart-healthcare/chatbot:<tag> chatbot-service
# Biến môi trường: CHATBOT_INTERNAL_KEY (bắt buộc), OLLAMA_BASE_URL, OLLAMA_API_KEY, OLLAMA_MODEL, OLLAMA_TIMEOUT_SECONDS.
# Volume /data: SQLite của cuộc trò chuyện (Short-term Memory), giữ qua restart.

ARG PYTHON_IMAGE=python:3.12.8-slim-bookworm
ARG UV_IMAGE=ghcr.io/astral-sh/uv:0.10.12

FROM ${UV_IMAGE} AS uv

FROM ${PYTHON_IMAGE} AS build
COPY --from=uv /uv /usr/local/bin/uv
WORKDIR /app
ENV UV_COMPILE_BYTECODE=1 UV_LINK_MODE=copy UV_PYTHON_DOWNLOADS=never
COPY pyproject.toml uv.lock .python-version ./
RUN uv sync --frozen --no-dev --no-install-project
COPY app/ ./app/
COPY config/ ./config/
COPY scripts/ ./scripts/

FROM ${PYTHON_IMAGE} AS runtime
RUN useradd --system --uid 10001 --home-dir /app chatbot && mkdir -p /data && chown chatbot /data
WORKDIR /app
COPY --from=build --chown=chatbot /app /app
ENV PATH="/app/.venv/bin:$PATH" PYTHONUNBUFFERED=1 DATA_DIR=/data
USER chatbot
VOLUME ["/data"]
EXPOSE 8000
HEALTHCHECK --interval=30s --timeout=5s --retries=3 CMD python -c "import urllib.request; urllib.request.urlopen('http://127.0.0.1:8000/health', timeout=4)"
CMD ["uvicorn", "app.main:create_app", "--factory", "--host", "0.0.0.0", "--port", "8000", "--no-access-log"]
