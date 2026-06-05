using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Soenneker.TimeZones.Runner;

/// <summary>
/// Represents the console hosted service.
/// </summary>
public sealed class ConsoleHostedService : IHostedService
{
    private readonly ILogger<ConsoleHostedService> _logger;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly TimeZonesRunner _timeZonesRunner;

    private int? _exitCode;

    public ConsoleHostedService(ILogger<ConsoleHostedService> logger, IHostApplicationLifetime appLifetime, TimeZonesRunner timeZonesRunner)
    {
        _logger = logger;
        _appLifetime = appLifetime;
        _timeZonesRunner = timeZonesRunner;
    }

    /// <summary>
    /// Executes the start async operation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _appLifetime.ApplicationStarted.Register(() =>
        {
            Task.Run(async () =>
            {
                _logger.LogInformation("Running console hosted service ...");

                try
                {
                    string[] args = Environment.GetCommandLineArgs().Skip(1).ToArray();

                    await _timeZonesRunner.Run(args, cancellationToken);

                    _logger.LogInformation("Complete!");

                    _exitCode = 0;
                }
                catch (Exception e)
                {
                    if (Debugger.IsAttached)
                        Debugger.Break();

                    _logger.LogError(e, "Unhandled exception");

                    await Task.Delay(2000, cancellationToken);
                    _exitCode = 1;
                }
                finally
                {
                    _appLifetime.StopApplication();
                }
            }, cancellationToken);
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes the stop async operation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Exiting with return code: {exitCode}", _exitCode);

        Environment.ExitCode = _exitCode.GetValueOrDefault(-1);
        return Task.CompletedTask;
    }
}
