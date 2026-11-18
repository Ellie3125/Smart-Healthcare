// Tự build và deploy VPS Staging khi nhánh staging có commit mới. Jenkins chạy trên chính VPS (infrastructure/jenkins/),
// build image tại chỗ (không registry) rồi chạy scripts/deploy-staging.sh. Secret: credential "staging-env" (Secret file),
// nội dung là .env.staging do trưởng nhóm tải lên; 5 dòng *_IMAGE trong đó được thay bằng tag của commit đang build.
// Chatbot chạy thường trực (chốt 18/11/2026); OLLAMA_BASE_URL trống thì chatbot ở trạng thái tạm ngưng.
pipeline {
  agent any
  triggers { pollSCM('H/5 * * * *') }
  options {
    disableConcurrentBuilds()
    timeout(time: 60, unit: 'MINUTES')
    buildDiscarder(logRotator(numToKeepStr: '20'))
    timestamps()
  }
  parameters {
    booleanParam(name: 'SEED', defaultValue: false, description: 'Seed dữ liệu giả lập (lần deploy đầu hoặc làm mới dữ liệu demo)')
    booleanParam(name: 'TUNNEL', defaultValue: false, description: 'Bật cloudflared (cần CLOUDFLARE_TUNNEL_TOKEN thật trong staging-env)')
  }
  environment {
    // Trùng tên project khi deploy tay để dùng chung volume DB.
    COMPOSE_PROJECT_NAME = 'smart-healthcare'
    CI_ENV = '.env.staging.ci'
  }
  stages {
    stage('Build và deploy') {
      steps {
        withCredentials([file(credentialsId: 'staging-env', variable: 'ENV_SRC')]) {
          sh '''#!/usr/bin/env bash
            set -euo pipefail
            TAG=$(git rev-parse --short=12 HEAD)
            PUBLIC_HOST=$(grep -E '^PUBLIC_HOST=' "$ENV_SRC" | cut -d= -f2-)
            PUSH=0 REGISTRY=sh-local TAG="$TAG" PATIENT_PORTAL_URL="https://$PUBLIC_HOST" scripts/build-images.sh

            umask 077
            cp "$ENV_SRC" "$CI_ENV"
            chmod 600 "$CI_ENV"
            for n in api worker migrator web chatbot; do
              var="$(echo "$n" | tr '[:lower:]' '[:upper:]')_IMAGE"
              sed -i "/^${var}=/d" "$CI_ENV"
              echo "${var}=sh-local/smart-healthcare/$n:$TAG" >> "$CI_ENV"
            done

            SKIP_TUNNEL=1; [ "$TUNNEL" = true ] && SKIP_TUNNEL=0
            SEED_FLAG=0; [ "$SEED" = true ] && SEED_FLAG=1
            ENV_FILE="$CI_ENV" SKIP_PULL=1 SKIP_TUNNEL=$SKIP_TUNNEL SEED=$SEED_FLAG CHATBOT=1 scripts/deploy-staging.sh

            code=$(docker compose --env-file "$CI_ENV" -f compose.yaml -f compose.staging.yaml exec -T web \
              curl -s -o /dev/null -w '%{http_code}' -H 'Host: localhost' http://localhost/healthz)
            echo "healthz: $code"
            [ "$code" = 200 ]
          '''
        }
      }
    }
    stage('Dọn image cũ') {
      steps {
        // Giữ 2 tag mới nhất mỗi image để rollback nhanh.
        sh '''#!/usr/bin/env bash
          for n in api worker migrator web chatbot; do
            docker images "sh-local/smart-healthcare/$n" --format '{{.Tag}}' | tail -n +3 \
              | xargs -r -I{} docker rmi "sh-local/smart-healthcare/$n:{}" || true
          done
          docker image prune -f
        '''
      }
    }
  }
  post {
    always { sh 'rm -f "$CI_ENV"' }
  }
}
