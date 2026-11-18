using Microsoft.Extensions.DependencyInjection;

namespace Billing.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddBillingApplication(this IServiceCollection services) =>
        services.AddScoped(provider => ActivatorUtilities.CreateInstance<CashierShiftService>(provider));
}
