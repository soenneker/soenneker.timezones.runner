using Soenneker.Tests.HostedUnit;

namespace Soenneker.TimeZones.Runner.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public class ConsoleHostedServiceTests : HostedUnitTest
{
    public ConsoleHostedServiceTests(Host host) : base(host)
    {
    }

    [Test]
    public void Default()
    {
    }
}
