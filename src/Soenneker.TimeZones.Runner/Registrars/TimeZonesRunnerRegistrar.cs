using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Soenneker.Git.Util.Registrars;
using Soenneker.TimeZones.Runner.Abstract;
using Soenneker.Utils.File.Download.Registrars;

namespace Soenneker.TimeZones.Runner.Registrars;

public static class TimeZonesRunnerRegistrar
{
    public static IServiceCollection AddTimeZonesRunnerAsSingleton(this IServiceCollection services)
    {
        services.AddFileDownloadUtilAsSingleton().AddGitUtilAsSingleton().TryAddSingleton<ITimeZonesRunner, TimeZonesRunner>();

        return services;
    }

    public static IServiceCollection AddTimeZonesRunnerAsScoped(this IServiceCollection services)
    {
        services.AddFileDownloadUtilAsScoped().AddGitUtilAsScoped().TryAddScoped<ITimeZonesRunner, TimeZonesRunner>();

        return services;
    }
}
