using System.Threading.Tasks;
using Soenneker.TimeZones.Runner.Configuration;

namespace Soenneker.TimeZones.Runner.Tests;

public sealed class TimeZonesRunnerTests
{
    [Test]
    public async Task Defaults_to_world_scope()
    {
        RunnerOptions options = RunnerOptionsParser.Parse([]);

        await Assert.That(options.Scope).IsEqualTo("world");
        await Assert.That(options.ForceDownload).IsFalse();
        await Assert.That(options.MinRingPoints).IsEqualTo(4);
    }
}
