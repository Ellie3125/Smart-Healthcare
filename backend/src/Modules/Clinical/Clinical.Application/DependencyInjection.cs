using Microsoft.Extensions.DependencyInjection;

namespace Clinical.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddClinicalApplication(this IServiceCollection services)
    {
        // IClinicalModule do AddClinicalInfrastructure đăng ký kèm vòng đời CLS. Đăng ký lại ở đây sẽ ghi đè bản đó
        // (Program gọi Application sau Infrastructure) và Start/Finish CLS ném NotSupportedException.
        return services;
    }
}
