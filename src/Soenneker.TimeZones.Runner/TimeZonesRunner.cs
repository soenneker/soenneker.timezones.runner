using System.Diagnostics;
using System.Text.Json;
using Clipper2Lib;
using Microsoft.Extensions.Logging;
using Soenneker.Git.Util.Abstract;
using Soenneker.GitHub.Repositories.Releases.Abstract;
using Soenneker.Python.Util.Abstract;
using Soenneker.TimeZones.Runner.Configuration;
using Soenneker.TimeZones.Runner.GeoJson;
using Soenneker.TimeZones.Runner.Geometry;
using Soenneker.TimeZones.Runner.Models;
using Soenneker.TimeZones.Runner.Osm;
using Soenneker.TimeZones.Runner.Validation;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Utils.Dotnet.Abstract;
using Soenneker.Utils.Dotnet.NuGet.Abstract;
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
    private readonly IDotnetUtil _dotnetUtil;
    private readonly IDotnetNuGetUtil _dotnetNuGetUtil;
    private readonly IGitHubRepositoriesReleasesUtil _releasesUtil;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<TimeZonesRunner> _logger;

    public TimeZonesRunner(IFileDownloadUtil fileDownloadUtil, IGitUtil gitUtil, IFileUtil fileUtil, IDirectoryUtil directoryUtil, IPathUtil pathUtil,
        IPythonUtil pythonUtil, IProcessUtil processUtil, IDotnetUtil dotnetUtil, IDotnetNuGetUtil dotnetNuGetUtil,
        IGitHubRepositoriesReleasesUtil releasesUtil, ILoggerFactory loggerFactory, ILogger<TimeZonesRunner> logger)
    {
        _fileDownloadUtil = fileDownloadUtil;
        _gitUtil = gitUtil;
        _fileUtil = fileUtil;
        _directoryUtil = directoryUtil;
        _pathUtil = pathUtil;
        _pythonUtil = pythonUtil;
        _processUtil = processUtil;
        _dotnetUtil = dotnetUtil;
        _dotnetNuGetUtil = dotnetNuGetUtil;
        _releasesUtil = releasesUtil;
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

        if (extracts.Count != 1)
            throw new InvalidOperationException("Exactly one enabled extract must be configured.");

        ExtractDefinition extract = extracts[0];

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
        string? previousMd5 = options.SkipMd5Checking ? null : await LoadPreviousChecksum(dataRepositoryDirectory, extract, cancellationToken);
        string? upstreamMd5 = options.SkipMd5Checking ? null : await DownloadExtractMd5(extract, cancellationToken);
        bool md5Changed = !options.SkipMd5Checking && !string.Equals(previousMd5, upstreamMd5, StringComparison.OrdinalIgnoreCase);

        if (!options.SkipMd5Checking && !options.ForceDownload && !md5Changed)
        {
            _logger.LogInformation("Extract MD5 matches the data repository checksum manifest; skipping PBF download and generation.");
            return;
        }

        string cachePath = Path.Combine(cacheDirectory, extract.CacheFileName);
        bool downloaded = await EnsureExtract(extract, cachePath, options.ForceDownload || md5Changed, cancellationToken);

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

        List<TimeZoneFeature> features = BuildFeatures(globalPaths, options.MinRingPoints);
        TimeZoneDatasetValidator.Validate(features, options.MinRingPoints);

        await TimeZoneGeoJsonWriter.Write(generatedOutputPath, features, _fileUtil, _directoryUtil, _pathUtil, cancellationToken);

        await PublishDataPackage(dataRepositoryDirectory, targetPath, generatedOutputPath, extract, upstreamMd5, !options.SkipMd5Checking,
            md5Changed, gitHubToken, gitName, gitEmail, cancellationToken);

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

    private async ValueTask PublishDataPackage(string dataRepositoryDirectory, string targetPath, string generatedOutputPath,
        ExtractDefinition extract, string? upstreamMd5, bool writeChecksumManifest,
        bool pushChecksumManifest, string gitHubToken, string gitName, string gitEmail, CancellationToken cancellationToken)
    {
        string version = EnvironmentUtil.GetVariableStrict("BUILD_VERSION");
        string nuGetToken = EnvironmentUtil.GetVariableStrict("NUGET__TOKEN");
        string gitHubUsername = EnvironmentUtil.GetVariableStrict("GH__USERNAME");
        string? targetDirectory = Path.GetDirectoryName(targetPath);

        if (!string.IsNullOrWhiteSpace(targetDirectory))
            await _directoryUtil.Create(targetDirectory, cancellationToken: cancellationToken);

        await _fileUtil.DeleteIfExists(targetPath, cancellationToken: cancellationToken);
        await _fileUtil.Copy(generatedOutputPath, targetPath, true, cancellationToken);

        if (writeChecksumManifest)
            await WriteChecksumManifest(dataRepositoryDirectory, extract, upstreamMd5!, cancellationToken);

        string projectPath = ResolveDataRepositoryPath(dataRepositoryDirectory,
            Path.Combine("src", Constants.DataLibrary, $"{Constants.DataLibrary}.csproj"));
        string packageOutputDirectory = Path.Combine(dataRepositoryDirectory, "artifacts", "packages");
        await _directoryUtil.Create(packageOutputDirectory, cancellationToken: cancellationToken);

        _logger.LogInformation("Building {PackageId} {Version} from {ProjectPath}.", Constants.DataLibrary, version, projectPath);

        if (!await _dotnetUtil.Restore(projectPath, cancellationToken: cancellationToken))
            throw new InvalidOperationException($"dotnet restore failed for {projectPath}.");

        if (!await _dotnetUtil.Build(projectPath, configuration: "Release", restore: false, cancellationToken: cancellationToken))
            throw new InvalidOperationException($"dotnet build failed for {projectPath}.");

        if (!await _dotnetUtil.Pack(projectPath, version, configuration: "Release", restore: false, build: false, output: packageOutputDirectory,
                cancellationToken: cancellationToken))
        {
            throw new InvalidOperationException($"dotnet pack failed for {projectPath}.");
        }

        string packagePath = Path.Combine(packageOutputDirectory, $"{Constants.DataLibrary}.{version}.nupkg");

        if (!await _fileUtil.Exists(packagePath, cancellationToken))
            throw new FileNotFoundException($"Expected package was not produced: {packagePath}", packagePath);

        _logger.LogInformation("Publishing {PackagePath} to NuGet.", packagePath);

        bool pushed = await _dotnetNuGetUtil.Push(packagePath, apiKey: nuGetToken, skipDuplicate: true, cancellationToken: cancellationToken);

        if (!pushed)
            throw new InvalidOperationException($"NuGet push failed for {packagePath}.");

        _logger.LogInformation("Creating GitHub release {Version} for {RepositoryName} with asset {AssetPath}.", version, Constants.DataRepositoryName,
            packagePath);
        await _releasesUtil.Create(gitHubUsername, Constants.DataRepositoryName, version, version, "Automated release update", packagePath, false, false,
            cancellationToken);

        await _fileUtil.DeleteIfExists(targetPath, cancellationToken: cancellationToken);

        if (!pushChecksumManifest)
        {
            _logger.LogInformation("No checksum metadata changes detected in {RepositoryDirectory}; nothing to push.", dataRepositoryDirectory);
            return;
        }

        string checksumManifestPath = Constants.ExtractChecksumManifestRelativePath.Replace('\\', '/');
        await _gitUtil.Run($"add -- \"{checksumManifestPath}\"", dataRepositoryDirectory, cancellationToken: cancellationToken);

        var env = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GIT_AUTHOR_NAME"] = gitName,
            ["GIT_AUTHOR_EMAIL"] = gitEmail,
            ["GIT_COMMITTER_NAME"] = gitName,
            ["GIT_COMMITTER_EMAIL"] = gitEmail
        };

        await _gitUtil.Run($"commit -m \"Update timezone source checksums\" -- \"{checksumManifestPath}\"", dataRepositoryDirectory, env,
            cancellationToken: cancellationToken);
        await _gitUtil.Push(dataRepositoryDirectory, gitHubToken, cancellationToken);
    }

    private async ValueTask<bool> EnsureExtract(ExtractDefinition extract, string cachePath, bool forceDownload, CancellationToken cancellationToken)
    {
        if (await _fileUtil.Exists(cachePath, cancellationToken) && !forceDownload)
            return false;

        await _fileUtil.DeleteIfExists(cachePath, cancellationToken: cancellationToken);

        string? result = await _fileDownloadUtil.DownloadAsStream(extract.Url, cachePath, log: true, cancellationToken: cancellationToken);

        if (result is null)
            throw new InvalidOperationException($"Failed to download extract '{extract.Name}' from {extract.Url}.");

        return true;
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

    private async ValueTask<string?> LoadPreviousChecksum(string dataRepositoryDirectory, ExtractDefinition extract, CancellationToken cancellationToken)
    {
        string path = ResolveDataRepositoryPath(dataRepositoryDirectory, Constants.ExtractChecksumManifestRelativePath);

        if (!await _fileUtil.Exists(path, cancellationToken))
            return null;

        await using FileStream stream = _fileUtil.OpenRead(path);
        ExtractManifest? manifest = await JsonSerializer.DeserializeAsync<ExtractManifest>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);

        return manifest?.Extracts.FirstOrDefault(x => string.Equals(x.CacheFileName, extract.CacheFileName, StringComparison.Ordinal))?.Md5;
    }

    private async ValueTask WriteChecksumManifest(string dataRepositoryDirectory, ExtractDefinition extract, string upstreamMd5,
        CancellationToken cancellationToken)
    {
        string path = ResolveDataRepositoryPath(dataRepositoryDirectory, Constants.ExtractChecksumManifestRelativePath);
        string? directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(directory))
            await _directoryUtil.Create(directory, cancellationToken: cancellationToken);

        var manifest = new ExtractManifest
        {
            Extracts = [extract with { Md5 = upstreamMd5 }]
        };

        string json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await _fileUtil.Write(path, json + Environment.NewLine, cancellationToken: cancellationToken);
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
