using Billing.Api;
using Billing.Application;
using Billing.Infrastructure;
using Clinical.Api;
using Clinical.Application;
using Clinical.Infrastructure;
using DocumentsReporting.Api;
using DocumentsReporting.Application;
using DocumentsReporting.Infrastructure;
using IdentityPatients.Api;
using IdentityPatients.Infrastructure;
using IdentityPatients.Infrastructure.Seed;
using IdentityPatients.Application;
using MedicationCatalog.Api;
using MedicationCatalog.Application;
using MedicationCatalog.Infrastructure;
using Microsoft.OpenApi;
using Notifications.Api;
using Notifications.Application;
using Notifications.Infrastructure;
using Scheduling.Api;
using Scheduling.Application;
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

builder.Services
    .AddIdentityPatientsApplication().AddIdentityPatientsApi()
    .AddSchedulingApplication().AddSchedulingApi()
    .AddClinicalApplication().AddClinicalApi()
    .AddMedicationCatalogApplication().AddMedicationCatalogApi()
    .AddBillingApplication().AddBillingApi()
    .AddNotificationsApplication().AddNotificationsApi()
    .AddDocumentsReportingApplication().AddDocumentsReportingApi();

string[] moduleTags =
[
    IdentityPatients.Api.DependencyInjection.Tag,
    Scheduling.Api.DependencyInjection.Tag,
    Clinical.Api.DependencyInjection.Tag,
    MedicationCatalog.Api.DependencyInjection.Tag,
    Billing.Api.DependencyInjection.Tag,
    Notifications.Api.DependencyInjection.Tag,
    DocumentsReporting.Api.DependencyInjection.Tag,
];

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    // Route group chưa có endpoint thì OpenAPI không sinh tag; khai sẵn tag của 7 module.
    document.Tags ??= new HashSet<OpenApiTag>();
    foreach (var tag in moduleTags.Where(t => document.Tags.All(x => x.Name != t)))
    {
        document.Tags.Add(new OpenApiTag { Name = tag });
    }
    return Task.CompletedTask;
}));

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

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Smart Healthcare v1"));
}

app.MapIdentityPatientsEndpoints();
app.MapSchedulingEndpoints();
app.MapClinicalEndpoints();
app.MapMedicationCatalogEndpoints();
app.MapBillingEndpoints();
app.MapNotificationsEndpoints();
app.MapDocumentsReportingEndpoints();

app.Run();
