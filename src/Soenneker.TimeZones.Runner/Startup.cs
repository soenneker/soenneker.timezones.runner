using Microsoft.Extensions.DependencyInjection;
using Soenneker.Git.Util.Registrars;
using Soenneker.Python.Util.Registrars;
using Soenneker.Utils.Directory.Registrars;
using Soenneker.Utils.File.Download.Registrars;
using Soenneker.Utils.File.Registrars;
using Soenneker.Utils.Path.Registrars;
using Soenneker.Utils.Process.Registrars;

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
                .AddDirectoryUtilAsSingleton()
                .AddPathUtilAsSingleton()
                .AddPythonUtilAsSingleton()
                .AddProcessUtilAsSingleton();

        return services;
    }
}
