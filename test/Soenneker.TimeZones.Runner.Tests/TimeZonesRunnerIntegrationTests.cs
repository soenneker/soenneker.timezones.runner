using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Clipper2Lib;
using Microsoft.Extensions.Logging;
using Soenneker.Python.Util.Abstract;
using Soenneker.Tests.HostedUnit;
using Soenneker.TimeZones.Runner.Configuration;
using Soenneker.TimeZones.Runner.GeoJson;
using Soenneker.TimeZones.Runner.Models;
using Soenneker.TimeZones.Runner.Osm;
using Soenneker.TimeZones.Runner.Validation;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Utils.File.Abstract;
using Soenneker.Utils.Path.Abstract;
using Soenneker.Utils.Process.Abstract;

namespace Soenneker.TimeZones.Runner.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public sealed class TimeZonesRunnerIntegrationTests : HostedUnitTest
{
    private const string _runIntegrationEnvironmentVariable = "RUN_TIMEZONE_PBF_INTEGRATION_TEST";
    private const string _antarcticaPbfUrl = "https://download.geofabrik.de/antarctica-latest.osm.pbf";

    public TimeZonesRunnerIntegrationTests(Host host) : base(host)
    {
    }

    [Test]
    public async Task Antarctica_pbf_runs_through_pyosmium_pipeline()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(_runIntegrationEnvironmentVariable), "true", StringComparison.OrdinalIgnoreCase))
            return;

        IDirectoryUtil directoryUtil = Resolve<IDirectoryUtil>(true);
        IFileUtil fileUtil = Resolve<IFileUtil>(true);
        IPathUtil pathUtil = Resolve<IPathUtil>(true);
        IPythonUtil pythonUtil = Resolve<IPythonUtil>(true);
        IProcessUtil processUtil = Resolve<IProcessUtil>(true);
        ILoggerFactory loggerFactory = Resolve<ILoggerFactory>(true);

        string integrationDirectory = await directoryUtil.CreateTempDirectory();
        string toolsDirectory = Path.Combine(integrationDirectory, "tools");
        string pbfPath = Path.Combine(integrationDirectory, "antarctica-latest.osm.pbf");

        try
        {
            await Download(_antarcticaPbfUrl, pbfPath);

            var options = new RunnerOptions
            {
                Scope = "url",
                ExtractUrl = _antarcticaPbfUrl,
                CacheDirectory = integrationDirectory,
                AutoInstallPython = true
            };
            var extract = new ExtractDefinition
            {
                Name = "Antarctica Geofabrik PBF",
                Url = _antarcticaPbfUrl,
                CacheFileName = Path.GetFileName(pbfPath)
            };

            var prefilter = new PyosmiumPrefilter(fileUtil, directoryUtil, pythonUtil, processUtil, loggerFactory.CreateLogger<PyosmiumPrefilter>());
            string filteredPath = await prefilter.EnsureFilteredExtract(extract, pbfPath, options, toolsDirectory, force: true, CancellationToken.None);

            await Assert.That(await fileUtil.Exists(filteredPath)).IsTrue();

            var extractor = new OsmTimeZoneExtractor(fileUtil, loggerFactory.CreateLogger<OsmTimeZoneExtractor>());
            var globalPaths = new Dictionary<string, Paths64>(StringComparer.Ordinal);
            ExtractStats stats = extractor.ExtractComplete(extract, filteredPath, options, globalPaths);
            List<TimeZoneFeature> features = TimeZonesRunner.BuildFeatures(globalPaths, options.MinRingPoints);

            TimeZoneDatasetValidator.Validate(features, options.MinRingPoints);

            string outputPath = Path.Combine(integrationDirectory, "antarctica-timezones.geojson");
            await TimeZoneGeoJsonWriter.Write(outputPath, features, fileUtil, directoryUtil, pathUtil, CancellationToken.None);

            await Assert.That(stats.TimezoneRelationsFound).IsGreaterThan(0);
            await Assert.That(stats.WaysLoaded).IsGreaterThan(0);
            await Assert.That(stats.NodesLoaded).IsGreaterThan(0);
            await Assert.That(features.Count).IsGreaterThan(0);
            await Assert.That(new FileInfo(outputPath).Length).IsGreaterThan(0);
        }
        finally
        {
            await directoryUtil.DeleteIfExists(integrationDirectory);
        }
    }

    private static async ValueTask Download(string url, string destinationPath)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Soenneker.TimeZones.Runner.Tests");

        await using Stream responseStream = await client.GetStreamAsync(url);
        await using FileStream destinationStream = File.Create(destinationPath);
        await responseStream.CopyToAsync(destinationStream);
    }
}
