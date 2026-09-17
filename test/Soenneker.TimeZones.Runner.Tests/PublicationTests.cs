using System;
using System.Text.Json;
using System.Threading.Tasks;
using Soenneker.TimeZones.Runner.Configuration;

namespace Soenneker.TimeZones.Runner.Tests;

public sealed class PublicationTests
{
    [Test]
    public async Task Preparation_and_publication_are_separate_commands()
    {
        RunnerOptions prepare = RunnerOptionsParser.Parse(["--prepare-publication", "artifacts/publication.json"]);
        RunnerOptions publish = RunnerOptionsParser.Parse(["--publish-publication", "artifacts/publication.json"]);

        await Assert.That(prepare.PreparePublication).IsEqualTo("artifacts/publication.json");
        await Assert.That(prepare.PublishPublication).IsNull();
        await Assert.That(publish.PublishPublication).IsEqualTo("artifacts/publication.json");
        await Assert.That(publish.PreparePublication).IsNull();
    }

    [Test]
    public async Task Conflicting_publication_commands_are_rejected()
    {
        await Assert.That(() => RunnerOptionsParser.Parse([
            "--prepare-publication", "prepare.json", "--publish-publication", "publish.json"
        ])).Throws<ArgumentException>();
    }

    [Test]
    public async Task Empty_publication_paths_are_rejected()
    {
        await Assert.That(() => RunnerOptionsParser.Parse(["--prepare-publication", " "])).Throws<ArgumentException>();
        await Assert.That(() => RunnerOptionsParser.Parse(["--publish-publication", ""])).Throws<ArgumentException>();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Publication_state_preserves_version_paths_and_checksum_decision(bool pushChecksum)
    {
        var prepared = new TimeZonesRunner.PreparedPublication("/tmp/data", "/tmp/data/timezones.geojson",
            "/tmp/data/artifacts/packages/Soenneker.TimeZones.Data.4.0.137.nupkg", "4.0.137", pushChecksum);

        string json = JsonSerializer.Serialize(prepared);
        TimeZonesRunner.PreparedPublication restored = JsonSerializer.Deserialize<TimeZonesRunner.PreparedPublication>(json)!;

        await Assert.That(restored).IsEqualTo(prepared);
    }
}
