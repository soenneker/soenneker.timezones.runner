using Microsoft.Extensions.DependencyInjection;
using Soenneker.Git.Util.Registrars;
using Soenneker.Utils.Directory.Registrars;
using Soenneker.Utils.File.Download.Registrars;
using Soenneker.Utils.File.Registrars;

namespace Soenneker.TimeZones.Runner;

/// <summary>
/// Console type startup
/// </summary>
public static class Startup
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.SetupIoC();
    }

    public static IServiceCollection SetupIoC(this IServiceCollection services)
    {
        services.AddHostedService<ConsoleHostedService>()
                .AddSingleton<TimeZonesRunner>()
                .AddFileDownloadUtilAsSingleton()
                .AddGitUtilAsSingleton()
                .AddFileUtilAsSingleton()
                .AddDirectoryUtilAsSingleton();

        return services;
    }
}
