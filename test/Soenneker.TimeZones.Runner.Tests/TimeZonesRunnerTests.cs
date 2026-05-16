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
        await Assert.That(options.SkipMd5Checking).IsTrue();
        await Assert.That(options.IncludeAdminBoundaries).IsTrue();
        await Assert.That(options.UsePyosmiumPrefilter).IsTrue();
        await Assert.That(options.PythonVersion).IsEqualTo("3.12");
        await Assert.That(options.AutoInstallPython).IsTrue();
        await Assert.That(options.MinRingPoints).IsEqualTo(4);
    }

    [Test]
    public async Task Can_enable_md5_checking()
    {
        RunnerOptions options = RunnerOptionsParser.Parse(["--enable-md5-checking"]);

        await Assert.That(options.SkipMd5Checking).IsFalse();
    }

    [Test]
    public async Task Can_disable_pyosmium_prefilter()
    {
        RunnerOptions options = RunnerOptionsParser.Parse(["--disable-pyosmium-prefilter"]);

        await Assert.That(options.UsePyosmiumPrefilter).IsFalse();
    }

    [Test]
    public async Task Can_exclude_admin_boundaries()
    {
        RunnerOptions options = RunnerOptionsParser.Parse(["--exclude-admin-boundaries"]);

        await Assert.That(options.IncludeAdminBoundaries).IsFalse();
    }

    [Test]
    public async Task Can_disable_python_auto_install()
    {
        RunnerOptions options = RunnerOptionsParser.Parse(["--disable-python-auto-install"]);

        await Assert.That(options.AutoInstallPython).IsFalse();
    }
}
