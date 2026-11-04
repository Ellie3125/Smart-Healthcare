using Billing.Infrastructure;
using Clinical.Infrastructure;
using DocumentsReporting.Infrastructure;
using IdentityPatients.Infrastructure;
using MedicationCatalog.Infrastructure;
using Notifications.Infrastructure;
using Scheduling.Infrastructure;
using Shared.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("SmartHealthcare");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:SmartHealthcare chưa được cấu hình (dotnet user-secrets hoặc biến môi trường ConnectionStrings__SmartHealthcare).");
}

// Migration chạy bằng dotnet ef như một bước release có kiểm soát (DB-03), không tự migrate khi khởi động.
builder.Services
    .AddIdentityPatientsInfrastructure(connectionString)
    .AddSchedulingInfrastructure(connectionString)
    .AddClinicalInfrastructure(connectionString)
    .AddMedicationCatalogInfrastructure(connectionString)
    .AddBillingInfrastructure(connectionString)
    .AddNotificationsInfrastructure(connectionString)
    .AddDocumentsReportingInfrastructure(connectionString)
    .AddSharedInfrastructure(connectionString);

// Liveness: tiến trình đang phục vụ HTTP; không kiểm DB để container không bị coi là chết khi SQL Server tạm gián đoạn.
builder.Services.AddHealthChecks();

var app = builder.Build();
app.MapHealthChecks("/health/live");
app.Run();
