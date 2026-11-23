# Jenkins cho VPS Staging: build image và chạy scripts/deploy-staging.sh qua Docker của host (socket mount trong compose.yaml cùng thư mục).
# Java 21.0.13.1 vá CVE-2026-114938 (Java4Shell); tag Jenkins 2.580.2 còn kèm Java cũ nên thay JRE bằng bản Temurin.
FROM eclipse-temurin:21.0.13.1_2-jre AS java
# 2.580.2: bản vá core theo advisory 28/10/2026.
FROM jenkins/jenkins:2.580.2-lts-jdk21
USER root
RUN rm -rf /opt/java/openjdk
COPY --from=java /opt/java/openjdk /opt/java/openjdk
RUN java -version 2>&1 | grep -q '"21.0.13.1"'
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
