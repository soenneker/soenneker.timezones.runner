using Microsoft.Extensions.DependencyInjection;
using Soenneker.Git.Util.Registrars;
using Soenneker.GitHub.Repositories.Releases.Registrars;
using Soenneker.Python.Util.Registrars;
using Soenneker.Utils.Directory.Registrars;
using Soenneker.Utils.Dotnet.NuGet.Registrars;
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
    /// <summary>
    /// Configures services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static void ConfigureServices(IServiceCollection services)
    {
        services.SetupIoC();
    }

    /// <summary>
    /// Sets up io c.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The result of the operation.</returns>
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
                .AddProcessUtilAsSingleton()
                .AddDotnetNuGetUtilAsSingleton()
                .AddGitHubRepositoriesReleasesUtilAsSingleton();

        return services;
    }
}
