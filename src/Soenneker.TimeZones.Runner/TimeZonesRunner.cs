using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Clipper2Lib;
using Microsoft.Extensions.Logging;
using Soenneker.Git.Util.Abstract;
using Soenneker.Python.Util.Abstract;
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
using Soenneker.Utils.Path.Abstract;
using Soenneker.Utils.Process.Abstract;

namespace Soenneker.TimeZones.Runner;

public sealed class TimeZonesRunner
{
    private readonly IFileDownloadUtil _fileDownloadUtil;
    private readonly IGitUtil _gitUtil;
    private readonly IFileUtil _fileUtil;
    private readonly IDirectoryUtil _directoryUtil;
    private readonly IPathUtil _pathUtil;
    private readonly IPythonUtil _pythonUtil;
    private readonly IProcessUtil _processUtil;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<TimeZonesRunner> _logger;

    public TimeZonesRunner(IFileDownloadUtil fileDownloadUtil, IGitUtil gitUtil, IFileUtil fileUtil, IDirectoryUtil directoryUtil, IPathUtil pathUtil,
        IPythonUtil pythonUtil, IProcessUtil processUtil, ILoggerFactory loggerFactory, ILogger<TimeZonesRunner> logger)
    {
        _fileDownloadUtil = fileDownloadUtil;
        _gitUtil = gitUtil;
        _fileUtil = fileUtil;
        _directoryUtil = directoryUtil;
        _pathUtil = pathUtil;
        _pythonUtil = pythonUtil;
        _processUtil = processUtil;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    public async ValueTask Run(string[] args, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        RunnerOptions options = RunnerOptionsParser.Parse(args);
        string runnerRepoRoot = await FindRunnerRepositoryRoot(cancellationToken);
        string cacheDirectory = ResolvePath(runnerRepoRoot, options.CacheDirectory);
        string toolsDirectory = Path.Combine(runnerRepoRoot, "artifacts", "tools");
        string generatedOutputDirectory = Path.Combine(runnerRepoRoot, "artifacts", "timezones");
        await _directoryUtil.Create(generatedOutputDirectory, cancellationToken: cancellationToken);
        string generatedOutputPath = await _pathUtil.GetRandomUniqueFilePath(generatedOutputDirectory, ".geojson", cancellationToken);
        string gitHubToken = EnvironmentUtil.GetVariableStrict("GH__TOKEN");
        string gitName = EnvironmentUtil.GetVariableStrict("GIT__NAME");
        string gitEmail = EnvironmentUtil.GetVariableStrict("GIT__EMAIL");

        ExtractManifest manifest = await LoadManifest(options, runnerRepoRoot, cancellationToken);
        List<ExtractDefinition> extracts = manifest.Extracts.Where(static x => x.Enabled).OrderBy(static x => x.Name, StringComparer.Ordinal).ToList();

        if (extracts.Count == 0)
            throw new InvalidOperationException("No enabled extracts are configured.");

        var extractor = new OsmTimeZoneExtractor(_fileUtil, _loggerFactory.CreateLogger<OsmTimeZoneExtractor>());
        var pyosmiumPrefilter = new PyosmiumPrefilter(_fileUtil, _directoryUtil, _pythonUtil, _processUtil,
            _loggerFactory.CreateLogger<PyosmiumPrefilter>());
        var globalPaths = new Dictionary<string, Paths64>(StringComparer.Ordinal);
        var stats = new GenerationStats { ExtractsConfigured = manifest.Extracts.Count };

        _logger.LogInformation("Scope: {Scope}", options.Scope);
        _logger.LogInformation("MD5 checking: {Md5CheckingEnabled}", !options.SkipMd5Checking);
        _logger.LogInformation("pyosmium prefilter: {PyosmiumPrefilterEnabled}. Python version: {PythonVersion}. Auto-install Python: {AutoInstallPython}",
            options.UsePyosmiumPrefilter, options.PythonVersion, options.AutoInstallPython);
        _logger.LogInformation("Cache directory: {CacheDirectory}", cacheDirectory);
        _logger.LogInformation("Tools directory: {ToolsDirectory}", toolsDirectory);
        _logger.LogInformation("Generated output path: {GeneratedOutputPath}", generatedOutputPath);
        _logger.LogInformation("Data repository output path: {DataRepositoryOutputPath}", options.OutputPath);

        await _directoryUtil.Create(cacheDirectory, cancellationToken: cancellationToken);

        string dataRepositoryDirectory = await CloneDataRepository(gitHubToken, cancellationToken);
        string targetPath = ResolveDataRepositoryPath(dataRepositoryDirectory, options.OutputPath);
        Dictionary<string, string> previousChecksums = options.SkipMd5Checking
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : await LoadPreviousChecksums(dataRepositoryDirectory, cancellationToken);
        Dictionary<string, string> upstreamMd5s = options.SkipMd5Checking
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : await LoadUpstreamMd5s(extracts, cancellationToken);

        if (!options.SkipMd5Checking && !options.ForceDownload && await _fileUtil.Exists(targetPath, cancellationToken) &&
            ExtractChecksumsMatch(extracts, upstreamMd5s, previousChecksums))
        {
            _logger.LogInformation("All configured extract MD5s match the data repository checksum manifest; skipping PBF downloads and generation.");
            return;
        }

        foreach (ExtractDefinition extract in extracts)
        {
            string cachePath = Path.Combine(cacheDirectory, extract.CacheFileName);
            string? upstreamMd5 = options.SkipMd5Checking ? null : upstreamMd5s[extract.CacheFileName];
            bool md5Changed = !options.SkipMd5Checking && IsMd5Changed(extract, upstreamMd5s, previousChecksums);
            bool downloaded = await EnsureExtract(extract, cachePath, upstreamMd5, options.ForceDownload || md5Changed, cancellationToken);

            if (downloaded)
                stats.ExtractsDownloaded++;
            else
                stats.ExtractsReused++;

            _logger.LogInformation(
                "Processing extract {ExtractName}. Url: {ExtractUrl}. Cache path: {CachePath}. Upstream MD5: {UpstreamMd5}. MD5 changed: {Md5Changed}. Download: {DownloadStatus}",
                extract.Name, extract.Url, cachePath, upstreamMd5 ?? "(skipped)", md5Changed,
                downloaded ? "performed" : "skipped, reused cached file");

            string processingPath = cachePath;

            if (options.UsePyosmiumPrefilter)
                processingPath = await pyosmiumPrefilter.EnsureFilteredExtract(extract, cachePath, options, toolsDirectory,
                    options.ForceDownload || md5Changed || downloaded, cancellationToken);

            ExtractStats extractStats = (options.UsePyosmiumPrefilter
                ? extractor.ExtractComplete(extract, processingPath, options, globalPaths)
                : extractor.Extract(extract, processingPath, options, globalPaths)) with
            {
                Downloaded = downloaded,
                Md5Changed = md5Changed,
                UpstreamMd5 = upstreamMd5
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

        await TimeZoneGeoJsonWriter.Write(generatedOutputPath, features, _fileUtil, _directoryUtil, _pathUtil, cancellationToken);
        await PushToDataRepository(dataRepositoryDirectory, targetPath, generatedOutputPath, extracts, upstreamMd5s, !options.SkipMd5Checking, gitHubToken,
            gitName, gitEmail, cancellationToken);

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

    private async ValueTask PushToDataRepository(string dataRepositoryDirectory, string targetPath, string generatedOutputPath,
        IReadOnlyList<ExtractDefinition> extracts, IReadOnlyDictionary<string, string> upstreamMd5s, bool writeChecksumManifest, string gitHubToken,
        string gitName, string gitEmail, CancellationToken cancellationToken)
    {
        string? targetDirectory = Path.GetDirectoryName(targetPath);

        if (!string.IsNullOrWhiteSpace(targetDirectory))
            await _directoryUtil.Create(targetDirectory, cancellationToken: cancellationToken);

        await _fileUtil.Copy(generatedOutputPath, targetPath, cancellationToken: cancellationToken);

        if (writeChecksumManifest)
            await WriteChecksumManifest(dataRepositoryDirectory, extracts, upstreamMd5s, cancellationToken);

        bool hasChanges = await _gitUtil.HasWorkingTreeChanges(dataRepositoryDirectory, cancellationToken);

        if (!hasChanges)
        {
            _logger.LogInformation("No working tree changes detected in {RepositoryDirectory}; nothing to push.", dataRepositoryDirectory);
            return;
        }

        await _gitUtil.CommitAndPush(dataRepositoryDirectory, "Update timezone boundary GeoJSON", gitHubToken, gitName, gitEmail, cancellationToken);
    }

    private async ValueTask<bool> EnsureExtract(ExtractDefinition extract, string cachePath, string? expectedMd5, bool forceDownload,
        CancellationToken cancellationToken)
    {
        if (await _fileUtil.Exists(cachePath, cancellationToken) && !forceDownload)
        {
            if (expectedMd5 is null)
                return false;

            string cachedMd5 = await ComputeFileMd5(cachePath, cancellationToken);

            if (string.Equals(cachedMd5, expectedMd5, StringComparison.OrdinalIgnoreCase))
                return false;

            _logger.LogWarning("Cached extract {CachePath} MD5 mismatch. Expected {ExpectedMd5}, found {ActualMd5}; downloading again.", cachePath,
                expectedMd5, cachedMd5);
        }

        await _fileUtil.DeleteIfExists(cachePath, cancellationToken: cancellationToken);

        string? result = await _fileDownloadUtil.DownloadAsStream(extract.Url, cachePath, log: true, cancellationToken: cancellationToken);

        if (result is null)
            throw new InvalidOperationException($"Failed to download extract '{extract.Name}' from {extract.Url}.");

        if (expectedMd5 is null)
            return true;

        string downloadedMd5 = await ComputeFileMd5(cachePath, cancellationToken);

        if (!string.Equals(downloadedMd5, expectedMd5, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Downloaded extract '{extract.Name}' failed MD5 verification. Expected {expectedMd5}, found {downloadedMd5}.");

        return true;
    }

    private static async ValueTask<string> ComputeFileMd5(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        byte[] hash = await MD5.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
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
        string tempPath = await _pathUtil.GetRandomTempFilePath(".md5", cancellationToken);

        try
        {
            string? downloadedPath =
                await _fileDownloadUtil.DownloadWithRetry(md5Url, tempPath, null, null, null, 3, 2.0, cancellationToken: cancellationToken);

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

    private async ValueTask<Dictionary<string, string>> LoadPreviousChecksums(string dataRepositoryDirectory, CancellationToken cancellationToken)
    {
        string path = ResolveDataRepositoryPath(dataRepositoryDirectory, Constants.ExtractChecksumManifestRelativePath);

        if (!await _fileUtil.Exists(path, cancellationToken))
            return new Dictionary<string, string>(StringComparer.Ordinal);

        await using FileStream stream = _fileUtil.OpenRead(path);
        JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var checksums = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
                checksums[property.Name] = property.Value.GetString()!;
        }

        return checksums;
    }

    private async ValueTask WriteChecksumManifest(string dataRepositoryDirectory, IReadOnlyList<ExtractDefinition> extracts,
        IReadOnlyDictionary<string, string> upstreamMd5s, CancellationToken cancellationToken)
    {
        string path = ResolveDataRepositoryPath(dataRepositoryDirectory, Constants.ExtractChecksumManifestRelativePath);
        string? directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(directory))
            await _directoryUtil.Create(directory, cancellationToken: cancellationToken);

        Dictionary<string, string> manifest = extracts.OrderBy(static x => x.CacheFileName, StringComparer.Ordinal)
            .ToDictionary(static x => x.CacheFileName, x => upstreamMd5s[x.CacheFileName], StringComparer.Ordinal);

        await using FileStream stream = _fileUtil.OpenWrite(path);
        await JsonSerializer.SerializeAsync(stream, manifest, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
    }

    private static bool ExtractChecksumsMatch(IReadOnlyList<ExtractDefinition> extracts, IReadOnlyDictionary<string, string> upstreamMd5s,
        IReadOnlyDictionary<string, string> previousChecksums)
    {
        foreach (ExtractDefinition extract in extracts)
        {
            if (IsMd5Changed(extract, upstreamMd5s, previousChecksums))
                return false;
        }

        return true;
    }

    private static bool IsMd5Changed(ExtractDefinition extract, IReadOnlyDictionary<string, string> upstreamMd5s,
        IReadOnlyDictionary<string, string> previousChecksums)
    {
        string upstreamMd5 = upstreamMd5s[extract.CacheFileName];

        return !previousChecksums.TryGetValue(extract.CacheFileName, out string? previousMd5) ||
               !string.Equals(previousMd5, upstreamMd5, StringComparison.OrdinalIgnoreCase);
    }

    internal static List<TimeZoneFeature> BuildFeatures(Dictionary<string, Paths64> globalPaths, int minRingPoints)
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
            ExtractManifest? manifest =
                await JsonSerializer.DeserializeAsync<ExtractManifest>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
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

    private static string ResolvePath(string repoRoot, string path) =>
        Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(Path.Combine(repoRoot, path));

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
