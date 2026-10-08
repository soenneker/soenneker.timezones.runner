using System.Threading.Tasks;
using Soenneker.TimeZones.Runner.Configuration;
using System.Threading;

namespace Soenneker.TimeZones.Runner.Tests;

public sealed class TimeZonesRunnerTests
{
    [Test]
    public async ValueTask Defaults_to_world_scope(CancellationToken cancellationToken)
    {
        RunnerOptions options = RunnerOptionsParser.Parse([]);

        await Assert.That(options.Scope).IsEqualTo("world");
        await Assert.That(options.ForceDownload).IsFalse();
        await Assert.That(options.SkipMd5Checking).IsFalse();
        await Assert.That(options.IncludeAdminBoundaries).IsTrue();
        await Assert.That(options.UsePyosmiumPrefilter).IsTrue();
        await Assert.That(options.PythonVersion).IsEqualTo("3.12");
        await Assert.That(options.AutoInstallPython).IsTrue();
        await Assert.That(options.MinRingPoints).IsEqualTo(4);
    }

    [Test]
    public async ValueTask Can_skip_md5_checking(CancellationToken cancellationToken)
    {
        RunnerOptions options = RunnerOptionsParser.Parse(["--skip-md5-checking"]);

        await Assert.That(options.SkipMd5Checking).IsTrue();
    }

    [Test]
    public async ValueTask Can_disable_pyosmium_prefilter(CancellationToken cancellationToken)
    {
        RunnerOptions options = RunnerOptionsParser.Parse(["--disable-pyosmium-prefilter"]);

        await Assert.That(options.UsePyosmiumPrefilter).IsFalse();
    }

    [Test]
    public async ValueTask Can_exclude_admin_boundaries(CancellationToken cancellationToken)
    {
        RunnerOptions options = RunnerOptionsParser.Parse(["--exclude-admin-boundaries"]);

        await Assert.That(options.IncludeAdminBoundaries).IsFalse();
    }

    [Test]
    public async ValueTask Can_disable_python_auto_install(CancellationToken cancellationToken)
    {
        RunnerOptions options = RunnerOptionsParser.Parse(["--disable-python-auto-install"]);

        await Assert.That(options.AutoInstallPython).IsFalse();
    }
}
