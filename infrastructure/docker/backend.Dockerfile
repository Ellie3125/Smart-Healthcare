# syntax=docker/dockerfile:1
# Build context: backend/ (xem compose.dev.yaml). Một Dockerfile, ba target:
#   api      — SmartHealthcare.Api (BE1, BE2 cùng image)
#   worker   — SmartHealthcare.Worker (Hangfire Server)
#   migrator — 8 EF Core migration bundle, chạy một lần có kiểm soát (DB-03), không chạy trong replica
# Image ghim phiên bản, không dùng latest (CTR-02, REL-04).
ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.403
ARG ASPNET_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0.14
ARG RUNTIME_DEPS_IMAGE=mcr.microsoft.com/dotnet/runtime-deps:10.0.14

FROM ${DOTNET_SDK_IMAGE} AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/Bootstrapper/SmartHealthcare.Api/SmartHealthcare.Api.csproj \
 && dotnet restore src/Bootstrapper/SmartHealthcare.Worker/SmartHealthcare.Worker.csproj

FROM build AS publish-api
RUN dotnet publish src/Bootstrapper/SmartHealthcare.Api/SmartHealthcare.Api.csproj -c Release -o /out --no-restore -p:UseAppHost=false

FROM build AS publish-worker
RUN dotnet publish src/Bootstrapper/SmartHealthcare.Worker/SmartHealthcare.Worker.csproj -c Release -o /out --no-restore -p:UseAppHost=false

FROM build AS bundles
# dotnet-ef cùng phiên bản EF Core trong Directory.Packages.props.
RUN dotnet tool install --tool-path /tools dotnet-ef --version 10.0.14
# Program.cs của Api đòi có connection string khi dựng host; giá trị này chỉ để tạo bundle, không phải secret.
ENV ConnectionStrings__SmartHealthcare="Server=build-time-placeholder"
# Tương tự, Identity đòi Jwt:SigningKey ≥ 32 byte khi dựng host. Chỉ tồn tại trong stage bundles, không vào image chạy.
ENV Jwt__SigningKey="build-time-placeholder-not-a-secret-0123456789"
RUN set -e; \
    for pair in \
      IdentityPatientsDbContext:src/Modules/IdentityPatients/IdentityPatients.Infrastructure \
      SchedulingDbContext:src/Modules/Scheduling/Scheduling.Infrastructure \
      ClinicalDbContext:src/Modules/Clinical/Clinical.Infrastructure \
      MedicationDbContext:src/Modules/MedicationCatalog/MedicationCatalog.Infrastructure \
      BillingDbContext:src/Modules/Billing/Billing.Infrastructure \
      WorkflowDbContext:src/Modules/Notifications/Notifications.Infrastructure \
      DocumentsDbContext:src/Modules/DocumentsReporting/DocumentsReporting.Infrastructure \
      SharedDbContext:src/Shared/Shared.Infrastructure; do \
      ctx="${pair%%:*}"; proj="${pair#*:}"; \
      /tools/dotnet-ef migrations bundle --context "$ctx" --project "$proj" \
        --startup-project src/Bootstrapper/SmartHealthcare.Api \
        --self-contained -r linux-x64 -o "/bundles/$ctx"; \
    done

FROM ${ASPNET_IMAGE} AS api
WORKDIR /app
COPY --from=publish-api /out .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "SmartHealthcare.Api.dll"]

# Billing.Infrastructure, IdentityPatients.Infrastructure tham chiếu Microsoft.AspNetCore.App, nên worker cũng cần runtime ASP.NET Core.
FROM ${ASPNET_IMAGE} AS worker
WORKDIR /app
COPY --from=publish-worker /out .
USER $APP_UID
ENTRYPOINT ["dotnet", "SmartHealthcare.Worker.dll"]

FROM ${RUNTIME_DEPS_IMAGE} AS migrator
WORKDIR /bundles
COPY --from=bundles /bundles .
USER $APP_UID
# Bundle dựng host của Api để tạo DbContext, nên cũng cần Jwt:SigningKey. Migrator không phát token: giá trị giữ chỗ, không phải secret.
ENV Jwt__SigningKey="migrator-placeholder-not-a-secret-0123456789"
# MIGRATION_CONNECTION do môi trường cấp lúc chạy (identity riêng cho migration, không phải identity runtime).
ENTRYPOINT ["/bin/sh", "-c", "set -e; for ctx in IdentityPatientsDbContext SchedulingDbContext ClinicalDbContext MedicationDbContext BillingDbContext WorkflowDbContext DocumentsDbContext SharedDbContext; do echo \"== $ctx\"; ./$ctx --connection \"$MIGRATION_CONNECTION\"; done"]
