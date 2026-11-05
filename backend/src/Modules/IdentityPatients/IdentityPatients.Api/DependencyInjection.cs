using IdentityPatients.Api.Controllers;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPatients.Api;

public static class DependencyInjection
{
    public static IMvcBuilder AddIdentityPatientsApi(this IMvcBuilder builder) =>
        builder.AddApplicationPart(typeof(AuthController).Assembly);
}

