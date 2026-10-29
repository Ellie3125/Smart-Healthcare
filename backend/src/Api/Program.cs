using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using SmartHealthcare.Identity.Contracts;
using SmartHealthcare.Identity.Infrastructure;
using SmartHealthcare.Identity.Infrastructure.Security;
using SmartHealthcare.Identity.Presentation;
using SmartHealthcare.Patients.Infrastructure;
using SmartHealthcare.Patients.Presentation;

var builder = WebApplication.CreateBuilder(args);

// Bí mật (chuỗi kết nối, Jwt:SigningKey) lấy từ user-secrets hoặc biến môi trường, không đặt trong appsettings.
var connectionString = builder.Configuration.GetConnectionString("SmartHealthcare")
    ?? throw new InvalidOperationException("Thiếu ConnectionStrings:SmartHealthcare.");
var jwt = builder.Configuration.GetSection(JwtTokenOptions.Section).Get<JwtTokenOptions>() ?? new JwtTokenOptions();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    // Cho Swagger UI nút Authorize để dán access token khi thử API.
    document.Components ??= new OpenApiComponents();
    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
    document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
    };
    document.Security ??= [];
    document.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] });
    return Task.CompletedTask;
}));
builder.Services.AddIdentityModule(connectionString, jwt);
builder.Services.AddPatientsModule(connectionString);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = jwt.CreateSigningKey(),
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1),
        NameClaimType = AuthenticatedUser.UserIdClaim,
        RoleClaimType = AuthenticatedUser.RoleClaim,
    };
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthenticatedUser.PatientAccountPolicy, p => p.RequireAuthenticatedUser().RequireRole(AccountRoles.Patient));

// Giới hạn theo IP từng replica; sau Nginx cần cấu hình forwarded headers để lấy IP thật của client.
var authPermitPerMinute = builder.Configuration.GetValue("RateLimiting:AuthPermitPerMinute", 10);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(IdentityEndpoints.AuthRateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authPermitPerMinute, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Tài liệu và trang thử API chỉ bật khi phát triển; production không lộ mô tả API.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Smart Healthcare API v1"));
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapIdentityEndpoints();
app.MapPatientEndpoints();

app.Run();

public partial class Program;
