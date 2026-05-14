namespace Soenneker.TimeZones.Runner.Abstract;

public interface ITimeZonesRunner
{
    ValueTask Run(string[] args, CancellationToken cancellationToken = default);
}
