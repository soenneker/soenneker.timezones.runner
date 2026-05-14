using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Soenneker.Enums.DeployEnvironment;
using Soenneker.Extensions.LoggerConfiguration;

namespace Soenneker.TimeZones.Runner;

public static class Program
{
    private static string? _environment;

    private static CancellationTokenSource? _cts;

    public static async Task Main(string[] args)
    {
        _environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        if (string.IsNullOrWhiteSpace(_environment))
            _environment = "Development";

        _cts = new CancellationTokenSource();
        Console.CancelKeyPress += OnCancelKeyPress;

        try
        {
            await CreateHostBuilder(args).RunConsoleAsync(_cts.Token);
        }
        catch (Exception e)
        {
            Log.Error(e, "Stopped program because of exception");
            throw;
        }
        finally
        {
            Console.CancelKeyPress -= OnCancelKeyPress;

            _cts.Dispose();
            await Log.CloseAndFlushAsync();
        }
    }

    /// <summary>
    /// Used for WebApplicationFactory, cannot delete, cannot change access, cannot change number of parameters.
    /// </summary>
    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        _environment ??= Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        if (string.IsNullOrWhiteSpace(_environment))
            _environment = "Development";

        DeployEnvironment envEnum = DeployEnvironment.FromName(_environment);

        LoggerConfigurationExtension.BuildBootstrapLoggerAndSetGloballySync(envEnum);

        IHostBuilder host = Host.CreateDefaultBuilder(args)
                                .ConfigureAppConfiguration((hostingContext, builder) =>
                                {
                                    builder.AddEnvironmentVariables();
                                    builder.SetBasePath(hostingContext.HostingEnvironment.ContentRootPath);

                                    builder.Build();
                                })
                                .UseSerilog()
                                .ConfigureServices((_, services) => { Startup.ConfigureServices(services, args); });

        return host;
    }

    private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs eventArgs)
    {
        eventArgs.Cancel = true;
        _cts?.Cancel();
    }
}
