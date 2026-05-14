using Microsoft.Extensions.DependencyInjection;
using Soenneker.TimeZones.Runner.Registrars;

namespace Soenneker.TimeZones.Runner;

/// <summary>
/// Console type startup
/// </summary>
public static class Startup
{
    public static void ConfigureServices(IServiceCollection services, string[] args)
    {
        services.SetupIoC(args);
    }

    public static IServiceCollection SetupIoC(this IServiceCollection services, string[] args)
    {
        services.AddSingleton(args)
                .AddHostedService<ConsoleHostedService>()
                .AddTimeZonesRunnerAsSingleton();

        return services;
    }
}
