using Billing.Infrastructure;
using Clinical.Infrastructure;
using DocumentsReporting.Infrastructure;
using IdentityPatients.Api;
using IdentityPatients.Application;
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

// 1. Cấu hình Controllers và ProblemDetails
builder.Services.AddControllers();
builder.Services.AddProblemDetails();

// 2. Cấu hình JWT Authentication & Authorization
builder.Services.AddIdentityAuth(builder.Configuration);

// 3. Đăng ký các module
builder.Services
    .AddIdentityPatientsApplication(builder.Configuration)
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

// Seed dữ liệu ban đầu cho Identity (chỉ trong Development hoặc khi bật cờ Seed:Enabled)
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Seed:Enabled"))
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<IdentityPatients.Infrastructure.Seed.IdentitySeeder>();
    await seeder.SeedAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health/live");
app.Run();
