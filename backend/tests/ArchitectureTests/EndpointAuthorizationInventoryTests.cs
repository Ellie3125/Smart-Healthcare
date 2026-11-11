using System.Reflection;
using Billing.Application;
using Billing.Infrastructure;
using Clinical.Application;
using Clinical.Infrastructure;
using DocumentsReporting.Application;
using DocumentsReporting.Infrastructure;
using IdentityPatients.Api;
using IdentityPatients.Application;
using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure;
using MedicationCatalog.Application;
using MedicationCatalog.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Application;
using Notifications.Infrastructure;
using Scheduling.Application;
using Scheduling.Infrastructure;
using Shared.Infrastructure;
using Xunit;

namespace ArchitectureTests;

/// <summary>
/// Mọi endpoint đã map phải có policy kiểm vai trò và kênh (Giải pháp v2.14 "Kênh và vai trò", D6),
/// trừ danh sách công khai dưới đây. Endpoint chỉ có [Authorize] trơn cũng bị coi là thiếu policy.
/// </summary>
public sealed class EndpointAuthorizationInventoryTests
{
    // Route công khai được duyệt (CLA-FIX-013 Q3). Thêm route vào đây phải có lý do trong docs/api/<module>.md.
    private static readonly HashSet<string> PublicRoutes =
    [
        "auth/staff/login",
        "auth/patient/login",
        "auth/patient/register",
        "api/billing/payments/vnpay/ipn", // callback VNPAY, xác minh bằng chữ ký HMAC
    ];

    private static readonly HashSet<string> KnownPolicies =
    [
        .. Policies.RoleOf.Keys,
        Policies.ReceptionistOrAdmin,
        Policies.AnyChannel,
    ];

    private static WebApplication BuildApp(string environment)
    {
        // Đăng ký module giống SmartHealthcare.Api/Program.cs; endpoint lấy bằng cách gọi mọi Map*Endpoints của các assembly *.Api.
        const string connectionString = "Server=inventory-only;Database=none;";
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.Configuration["Jwt:SigningKey"] = new string('k', 32);
        builder.Services.AddControllers().AddIdentityPatientsApi();
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
            .AddSchedulingApplication()
            .AddClinicalApplication()
            .AddMedicationCatalogApplication()
            .AddBillingApplication()
            .AddNotificationsApplication()
            .AddDocumentsReportingApplication();
        foreach (var addApi in ApiMethods("Add", typeof(IServiceCollection)))
        {
            addApi.Invoke(null, [builder.Services]);
        }

        var app = builder.Build();
        app.MapControllers();
        foreach (var map in ApiMethods("Map", typeof(IEndpointRouteBuilder)))
        {
            map.Invoke(null, [app]);
        }

        return app;
    }

    private static IEnumerable<MethodInfo> ApiMethods(string prefix, Type firstParameter) =>
        Directory.GetFiles(AppContext.BaseDirectory, "*.Api.dll")
            .Select(file => Assembly.Load(AssemblyName.GetAssemblyName(file)))
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { Name: "DependencyInjection", IsAbstract: true, IsSealed: true })
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.Name.StartsWith(prefix, StringComparison.Ordinal)
                && m.Name.EndsWith(prefix == "Map" ? "Endpoints" : "Api", StringComparison.Ordinal)
                && m.GetParameters() is [var p] && p.ParameterType == firstParameter);

    // Đường dẫn đầy đủ (gồm tiền tố route group), tham số ghi {tên}.
    private static string PathOf(RouteEndpoint endpoint) => string.Join("/", endpoint.RoutePattern.PathSegments.Select(segment =>
        string.Concat(segment.Parts.Select(part => part switch
        {
            RoutePatternLiteralPart literal => literal.Content,
            RoutePatternSeparatorPart separator => separator.Content,
            RoutePatternParameterPart parameter => "{" + parameter.Name + "}",
            _ => "?"
        }))));

    private static List<RouteEndpoint> Endpoints(WebApplication app) =>
        ((IEndpointRouteBuilder)app).DataSources.SelectMany(d => d.Endpoints).OfType<RouteEndpoint>().ToList();

    // Một số module chỉ map endpoint ở Development (GOV-05: phần chưa xong không lộ ra), nên kiểm cả hai môi trường.
    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public async Task EveryMappedEndpoint_HasRoleAndChannelPolicy_OrIsApprovedPublic(string environment)
    {
        await using var app = BuildApp(environment);
        var endpoints = Endpoints(app);
        Assert.NotEmpty(endpoints);

        var missing = new List<string>();
        foreach (var endpoint in endpoints)
        {
            var route = PathOf(endpoint);
            var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy).ToList();
            if (PublicRoutes.Contains(route))
            {
                continue;
            }

            if (policies.Count == 0 || !policies.All(p => p is not null && KnownPolicies.Contains(p)))
            {
                missing.Add($"{endpoint.DisplayName} [{route}] policies=[{string.Join(",", policies)}]");
            }
        }

        Assert.True(missing.Count == 0, "Endpoint thiếu policy vai trò + kênh:\n" + string.Join("\n", missing));
    }

    [Fact]
    public async Task PublicRoutes_AreAllMapped()
    {
        await using var app = BuildApp("Development");
        var routes = Endpoints(app).Select(e => PathOf(e)).ToHashSet();

        Assert.DoesNotContain(PublicRoutes, r => !routes.Contains(r));
    }

    [Fact]
    public async Task Inventory_CoversIdentityAndSchedulingEndpoints()
    {
        await using var app = BuildApp("Development");
        var routes = Endpoints(app).Select(e => PathOf(e)).ToList();

        var all = string.Join(", ", routes);
        foreach (var prefix in new[] { "api/staff/admin", "api/staff/verifications", "api/patient/profiles", "api/scheduling/admin/doctor-days", "me" })
        {
            Assert.True(routes.Any(r => r.StartsWith(prefix, StringComparison.Ordinal)), $"Thiếu {prefix} trong: {all}");
        }
    }
}
