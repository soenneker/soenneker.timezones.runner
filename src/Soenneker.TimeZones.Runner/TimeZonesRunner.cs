using System.Diagnostics;
using System.Text.Json;
using Clipper2Lib;
using Microsoft.Extensions.Logging;
using Soenneker.Git.Util.Abstract;
using Soenneker.TimeZones.Runner.Configuration;
using Soenneker.TimeZones.Runner.GeoJson;
using Soenneker.TimeZones.Runner.Geometry;
using Soenneker.TimeZones.Runner.Models;
using Soenneker.TimeZones.Runner.Osm;
using Soenneker.TimeZones.Runner.Validation;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Utils.Environment;
using Soenneker.Utils.File.Abstract;
using Soenneker.Utils.File.Download.Abstract;

namespace Soenneker.TimeZones.Runner;

public sealed class TimeZonesRunner
{
    private readonly IFileDownloadUtil _fileDownloadUtil;
    private readonly IGitUtil _gitUtil;
    private readonly IFileUtil _fileUtil;
    private readonly IDirectoryUtil _directoryUtil;
    private readonly ILogger<TimeZonesRunner> _logger;

    public TimeZonesRunner(IFileDownloadUtil fileDownloadUtil, IGitUtil gitUtil, IFileUtil fileUtil, IDirectoryUtil directoryUtil,
        ILogger<TimeZonesRunner> logger)
    {
        _fileDownloadUtil = fileDownloadUtil;
        _gitUtil = gitUtil;
        _fileUtil = fileUtil;
        _directoryUtil = directoryUtil;
        _logger = logger;
    }

    public async ValueTask Run(string[] args, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        RunnerOptions options = RunnerOptionsParser.Parse(args);
        string runnerRepoRoot = await FindRunnerRepositoryRoot(cancellationToken);
        string cacheDirectory = ResolvePath(runnerRepoRoot, options.CacheDirectory);
        string generatedOutputPath = Path.Combine(runnerRepoRoot, "artifacts", "timezones", "timezones.geojson");
        string gitHubToken = EnvironmentUtil.GetVariableStrict("GH__TOKEN");
        string gitName = EnvironmentUtil.GetVariableStrict("GIT__NAME");
        string gitEmail = EnvironmentUtil.GetVariableStrict("GIT__EMAIL");

        ExtractManifest manifest = await LoadManifest(options, runnerRepoRoot, cancellationToken);
        List<ExtractDefinition> extracts = manifest.Extracts.Where(static x => x.Enabled).OrderBy(static x => x.Name, StringComparer.Ordinal).ToList();

        if (extracts.Count == 0)
            throw new InvalidOperationException("No enabled extracts are configured.");

        var extractor = new OsmTimeZoneExtractor(_fileUtil);
        var globalPaths = new Dictionary<string, Paths64>(StringComparer.Ordinal);
        var stats = new GenerationStats { ExtractsConfigured = manifest.Extracts.Count };

        _logger.LogInformation("Scope: {Scope}", options.Scope);
        _logger.LogInformation("Cache directory: {CacheDirectory}", cacheDirectory);
        _logger.LogInformation("Generated output path: {GeneratedOutputPath}", generatedOutputPath);
        _logger.LogInformation("Data repository output path: {DataRepositoryOutputPath}", options.OutputPath);

        await _directoryUtil.Create(cacheDirectory, cancellationToken: cancellationToken);

        string dataRepositoryDirectory = await CloneDataRepository(gitHubToken, cancellationToken);
        string targetPath = ResolveDataRepositoryPath(dataRepositoryDirectory, options.OutputPath);
        ExtractChecksumManifest previousChecksums = await LoadPreviousChecksums(dataRepositoryDirectory, cancellationToken);
        Dictionary<string, string> upstreamMd5s = await LoadUpstreamMd5s(extracts, cancellationToken);

        if (!options.ForceDownload && await _fileUtil.Exists(targetPath, cancellationToken) && ExtractChecksumsMatch(extracts, upstreamMd5s, previousChecksums))
        {
            _logger.LogInformation("All configured extract MD5s match the data repository checksum manifest; skipping PBF downloads and generation.");
            return;
        }

        foreach (ExtractDefinition extract in extracts)
        {
            string cachePath = Path.Combine(cacheDirectory, extract.CacheFileName);
            bool md5Changed = IsMd5Changed(extract, upstreamMd5s, previousChecksums);
            bool downloaded = await EnsureExtract(extract, cachePath, options.ForceDownload || md5Changed, cancellationToken);

            if (downloaded)
                stats.ExtractsDownloaded++;
            else
                stats.ExtractsReused++;

            _logger.LogInformation(
                "Processing extract {ExtractName}. Url: {ExtractUrl}. Cache path: {CachePath}. Upstream MD5: {UpstreamMd5}. MD5 changed: {Md5Changed}. Download: {DownloadStatus}",
                extract.Name, extract.Url, cachePath, upstreamMd5s[extract.CacheFileName], md5Changed, downloaded ? "performed" : "skipped, reused cached file");

            ExtractStats extractStats = extractor.Extract(extract, cachePath, options, globalPaths) with
            {
                Downloaded = downloaded,
                Md5Changed = md5Changed,
                UpstreamMd5 = upstreamMd5s[extract.CacheFileName]
            };
            stats.PerExtract.Add(extractStats);
            stats.ExtractsProcessed++;

            _logger.LogInformation(
                "Extract {ExtractName} complete. Relations scanned: {RelationsScanned}. Timezone relations found: {TimezoneRelationsFound}. Ways loaded: {WaysLoaded}. Nodes loaded: {NodesLoaded}. Incomplete rings dropped: {IncompleteRingsDropped}",
                extractStats.Name, extractStats.RelationsScanned, extractStats.TimezoneRelationsFound, extractStats.WaysLoaded, extractStats.NodesLoaded,
                extractStats.IncompleteRingsDropped);
        }

        List<TimeZoneFeature> features = BuildFeatures(globalPaths, options.MinRingPoints);
        TimeZoneDatasetValidator.Validate(features, options.MinRingPoints);
        await TimeZoneGeoJsonWriter.Write(generatedOutputPath, features, _fileUtil, _directoryUtil, cancellationToken);
        await PushToDataRepository(dataRepositoryDirectory, targetPath, generatedOutputPath, extracts, upstreamMd5s, gitHubToken, gitName, gitEmail, cancellationToken);

        stats.GlobalTimezoneFeatureCount = features.Count;
        stopwatch.Stop();

        _logger.LogInformation(
            "Global summary. Extracts configured: {ExtractsConfigured}. Extracts downloaded: {ExtractsDownloaded}. Extracts reused: {ExtractsReused}. Extracts processed: {ExtractsProcessed}. Global timezone feature count: {GlobalTimezoneFeatureCount}. Global incomplete ring count: {GlobalIncompleteRingCount}. Features written: {FeaturesWritten}. Elapsed time: {ElapsedTime}",
            stats.ExtractsConfigured, stats.ExtractsDownloaded, stats.ExtractsReused, stats.ExtractsProcessed, stats.GlobalTimezoneFeatureCount,
            stats.GlobalIncompleteRingCount, features.Count, stopwatch.Elapsed);
    }

    private async ValueTask<string> CloneDataRepository(string gitHubToken, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cloning {RepositoryUri}...", Constants.DataRepositoryUri);
        return await _gitUtil.CloneToTempDirectory(Constants.DataRepositoryUri, gitHubToken, cancellationToken);
    }

    private async ValueTask PushToDataRepository(string dataRepositoryDirectory, string targetPath, string generatedOutputPath, IReadOnlyList<ExtractDefinition> extracts,
        IReadOnlyDictionary<string, string> upstreamMd5s, string gitHubToken, string gitName, string gitEmail, CancellationToken cancellationToken)
    {
        string? targetDirectory = Path.GetDirectoryName(targetPath);

        if (!string.IsNullOrWhiteSpace(targetDirectory))
            await _directoryUtil.Create(targetDirectory, cancellationToken: cancellationToken);

        await _fileUtil.Copy(generatedOutputPath, targetPath, cancellationToken: cancellationToken);
        await WriteChecksumManifest(dataRepositoryDirectory, extracts, upstreamMd5s, cancellationToken);

        bool hasChanges = await _gitUtil.HasWorkingTreeChanges(dataRepositoryDirectory, cancellationToken);

        if (!hasChanges)
        {
            _logger.LogInformation("No working tree changes detected in {RepositoryDirectory}; nothing to push.", dataRepositoryDirectory);
            return;
        }

        await _gitUtil.CommitAndPush(dataRepositoryDirectory, "Update timezone boundary GeoJSON", gitHubToken, gitName, gitEmail, cancellationToken);
    }

    private async ValueTask<bool> EnsureExtract(ExtractDefinition extract, string cachePath, bool forceDownload, CancellationToken cancellationToken)
    {
        if (await _fileUtil.Exists(cachePath, cancellationToken) && !forceDownload)
            return false;

        await _fileUtil.DeleteIfExists(cachePath, cancellationToken: cancellationToken);

        string? result = await _fileDownloadUtil.DownloadAsStream(extract.Url, cachePath, cancellationToken: cancellationToken);

        if (result is null)
            throw new InvalidOperationException($"Failed to download extract '{extract.Name}' from {extract.Url}.");

        return true;
    }

    private async ValueTask<Dictionary<string, string>> LoadUpstreamMd5s(IReadOnlyList<ExtractDefinition> extracts, CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (ExtractDefinition extract in extracts)
        {
            string md5 = await DownloadExtractMd5(extract, cancellationToken);
            results[extract.CacheFileName] = md5;
        }

        return results;
    }

    private async ValueTask<string> DownloadExtractMd5(ExtractDefinition extract, CancellationToken cancellationToken)
    {
        string md5Url = extract.Url + ".md5";
        string tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.md5");

        try
        {
            string? downloadedPath = await _fileDownloadUtil.DownloadWithRetry(md5Url, tempPath, null, null, null, 3, 2.0, cancellationToken);

            if (downloadedPath is null)
                throw new InvalidOperationException($"Failed to download MD5 for extract '{extract.Name}' from {md5Url}.");

            string content = await _fileUtil.Read(downloadedPath, cancellationToken: cancellationToken);
            string md5 = ParseMd5(content);

            _logger.LogInformation("Fetched upstream MD5 for {ExtractName}: {Md5}", extract.Name, md5);
            return md5;
        }
        finally
        {
            await _fileUtil.DeleteIfExists(tempPath, cancellationToken: cancellationToken);
        }
    }

    private static string ParseMd5(string content)
    {
        string value = content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";

        if (value.Length != 32 || value.Any(static x => !Uri.IsHexDigit(x)))
            throw new InvalidOperationException($"Invalid MD5 content: {content}");

        return value.ToLowerInvariant();
    }

    private async ValueTask<ExtractChecksumManifest> LoadPreviousChecksums(string dataRepositoryDirectory, CancellationToken cancellationToken)
    {
        string path = ResolveDataRepositoryPath(dataRepositoryDirectory, Constants.ExtractChecksumManifestRelativePath);

        if (!await _fileUtil.Exists(path, cancellationToken))
            return new ExtractChecksumManifest();

        await using FileStream stream = _fileUtil.OpenRead(path);
        ExtractChecksumManifest? manifest = await JsonSerializer.DeserializeAsync<ExtractChecksumManifest>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);

        return manifest ?? new ExtractChecksumManifest();
    }

    private async ValueTask WriteChecksumManifest(string dataRepositoryDirectory, IReadOnlyList<ExtractDefinition> extracts,
        IReadOnlyDictionary<string, string> upstreamMd5s, CancellationToken cancellationToken)
    {
        string path = ResolveDataRepositoryPath(dataRepositoryDirectory, Constants.ExtractChecksumManifestRelativePath);
        string? directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(directory))
            await _directoryUtil.Create(directory, cancellationToken: cancellationToken);

        var manifest = new ExtractChecksumManifest
        {
            Extracts = extracts.Select(x => new ExtractChecksum
                       {
                           Name = x.Name,
                           Url = x.Url,
                           CacheFileName = x.CacheFileName,
                           Md5 = upstreamMd5s[x.CacheFileName]
                       })
                      .OrderBy(static x => x.Name, StringComparer.Ordinal)
                      .ToList()
        };

        await using FileStream stream = _fileUtil.OpenWrite(path);
        await JsonSerializer.SerializeAsync(stream, manifest, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
    }

    private static bool ExtractChecksumsMatch(IReadOnlyList<ExtractDefinition> extracts, IReadOnlyDictionary<string, string> upstreamMd5s,
        ExtractChecksumManifest previousChecksums)
    {
        foreach (ExtractDefinition extract in extracts)
        {
            if (IsMd5Changed(extract, upstreamMd5s, previousChecksums))
                return false;
        }

        return true;
    }

    private static bool IsMd5Changed(ExtractDefinition extract, IReadOnlyDictionary<string, string> upstreamMd5s, ExtractChecksumManifest previousChecksums)
    {
        string upstreamMd5 = upstreamMd5s[extract.CacheFileName];
        ExtractChecksum? previous = previousChecksums.Extracts.FirstOrDefault(x =>
            string.Equals(x.CacheFileName, extract.CacheFileName, StringComparison.Ordinal) &&
            string.Equals(x.Url, extract.Url, StringComparison.Ordinal));

        return previous is null || !string.Equals(previous.Md5, upstreamMd5, StringComparison.OrdinalIgnoreCase);
    }

    private static List<TimeZoneFeature> BuildFeatures(Dictionary<string, Paths64> globalPaths, int minRingPoints)
    {
        var features = new List<TimeZoneFeature>();

        foreach ((string tzid, Paths64 paths) in globalPaths.OrderBy(static x => x.Key, StringComparer.Ordinal))
        {
            Paths64 normalized = ClipperGeometry.Normalize(paths);
            List<List<List<Coordinate>>> multiPolygon = ClipperGeometry.ToMultiPolygon(normalized, minRingPoints);

            if (multiPolygon.Count == 0)
                continue;

            List<IReadOnlyList<Coordinate>> rings = multiPolygon.SelectMany(static x => x).Cast<IReadOnlyList<Coordinate>>().ToList();
            features.Add(new TimeZoneFeature(tzid, multiPolygon, BoundingBox.FromRings(rings)));
        }

        return features;
    }

    private async Task<ExtractManifest> LoadManifest(RunnerOptions options, string repoRoot, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.ExtractListPath))
        {
            string path = ResolvePath(repoRoot, options.ExtractListPath);
            await using FileStream stream = _fileUtil.OpenRead(path);
            ExtractManifest? manifest = await JsonSerializer.DeserializeAsync<ExtractManifest>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
            return manifest ?? throw new InvalidOperationException($"Extract manifest '{path}' could not be read.");
        }

        return options.Scope switch
        {
            "world" => DefaultExtractManifest.World,
            "continent" => DefaultExtractManifest.Continent,
            "url" => new ExtractManifest
            {
                Extracts =
                [
                    new ExtractDefinition
                    {
                        Name = "Custom",
                        Url = options.ExtractUrl,
                        CacheFileName = InferCacheFileName(options.ExtractUrl)
                    }
                ]
            },
            _ => throw new ArgumentOutOfRangeException(nameof(options.Scope), options.Scope, "Unsupported scope.")
        };
    }

    private static string InferCacheFileName(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            string fileName = Path.GetFileName(uri.LocalPath);

            if (!string.IsNullOrWhiteSpace(fileName))
                return fileName;
        }

        return "extract.osm.pbf";
    }

    private static string ResolvePath(string repoRoot, string path) => Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(Path.Combine(repoRoot, path));

    private static string ResolveDataRepositoryPath(string dataRepositoryDirectory, string outputPath)
    {
        if (Path.IsPathRooted(outputPath))
            throw new ArgumentException("--output must be relative to the Soenneker.TimeZones.Data repository.");

        string fullPath = Path.GetFullPath(Path.Combine(dataRepositoryDirectory, outputPath));
        string relativePath = Path.GetRelativePath(dataRepositoryDirectory, fullPath);

        if (Path.IsPathRooted(relativePath) || relativePath.StartsWith("..", StringComparison.Ordinal))
            throw new ArgumentException("--output must stay inside the Soenneker.TimeZones.Data repository.");

        return fullPath;
    }

    private async ValueTask<string> FindRunnerRepositoryRoot(CancellationToken cancellationToken)
    {
        string? fromCurrent = await FindRunnerRepositoryRootFrom(_directoryUtil.GetWorkingDirectory(), cancellationToken);

        if (fromCurrent is not null)
            return fromCurrent;

        string? fromBaseDirectory = await FindRunnerRepositoryRootFrom(AppContext.BaseDirectory, cancellationToken);

        if (fromBaseDirectory is not null)
            return fromBaseDirectory;

        throw new DirectoryNotFoundException("Could not locate repository root containing src/Soenneker.TimeZones.Runner.");
    }

    private async ValueTask<string?> FindRunnerRepositoryRootFrom(string start, CancellationToken cancellationToken)
    {
        var directory = new DirectoryInfo(start);

        while (directory is not null)
        {
            if (await _directoryUtil.Exists(Path.Combine(directory.FullName, "src", "Soenneker.TimeZones.Runner"), cancellationToken))
                return directory.FullName;

            directory = directory.Parent;
        }

        return null;
    }

}
