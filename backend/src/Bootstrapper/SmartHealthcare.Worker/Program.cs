using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Data.SqlClient;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("SmartHealthcare");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:SmartHealthcare chưa được cấu hình (dotnet user-secrets hoặc biến môi trường ConnectionStrings__SmartHealthcare).");
}

// Hangfire tự tạo và quản lý schema hangfire; EF không tạo bảng này.
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
    {
        SchemaName = "hangfire",
        PrepareSchemaIfNecessary = true,
        SqlClientFactory = SqlClientFactory.Instance,
    }));
builder.Services.AddHangfireServer();

builder.Build().Run();
