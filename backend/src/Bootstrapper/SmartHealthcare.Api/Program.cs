using Billing.Infrastructure;
using Clinical.Infrastructure;
using DocumentsReporting.Infrastructure;
using IdentityPatients.Api;
using IdentityPatients.Infrastructure;
using IdentityPatients.Infrastructure.Seed;
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

builder.Services.AddControllers()
    .AddIdentityPatientsApi();

builder.Services.AddProblemDetails();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000", "http://127.0.0.1:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Migration chạy bằng dotnet ef như một bước release có kiểm soát (DB-03), không tự migrate khi khởi động.
builder.Services
    .AddIdentityPatientsInfrastructure(builder.Configuration, connectionString)
    .AddSchedulingInfrastructure(connectionString)
    .AddClinicalInfrastructure(connectionString)
    .AddMedicationCatalogInfrastructure(connectionString)
    .AddBillingInfrastructure(connectionString)
    .AddNotificationsInfrastructure(connectionString)
    .AddDocumentsReportingInfrastructure(connectionString)
    .AddSharedInfrastructure(connectionString);

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Seed dữ liệu mẫu (idempotent) chỉ chạy trong môi trường Development hoặc khi bật cấu hình Seed:Enabled
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Seed:Enabled"))
{
    using var scope = app.Services.CreateScope();
    var seedService = scope.ServiceProvider.GetRequiredService<IdentityPatientsSeedService>();
    await seedService.SeedAsync();
}

app.Run();
