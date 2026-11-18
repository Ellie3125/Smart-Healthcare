# Jenkins cho VPS Staging: build image và chạy scripts/deploy-staging.sh qua Docker của host (socket mount trong compose.yaml cùng thư mục).
FROM jenkins/jenkins:2.528.2-lts-jdk21
USER root
# Docker CLI + Compose plugin từ repo chính thức của Docker (không cài Docker daemon trong container).
RUN apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates curl gnupg \
 && install -m 0755 -d /etc/apt/keyrings \
 && curl -fsSL https://download.docker.com/linux/debian/gpg -o /etc/apt/keyrings/docker.asc \
 && echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/debian $(. /etc/os-release && echo "$VERSION_CODENAME") stable" > /etc/apt/sources.list.d/docker.list \
 && apt-get update \
 && apt-get install -y --no-install-recommends docker-ce-cli docker-buildx-plugin docker-compose-plugin \
 && rm -rf /var/lib/apt/lists/*
USER jenkins
# Plugin cần cho Jenkinsfile ở gốc repo; cài sẵn để không phụ thuộc trình cài đặt lần đầu.
RUN jenkins-plugin-cli --plugins workflow-aggregator git credentials-binding timestamper
