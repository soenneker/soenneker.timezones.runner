using Microsoft.Extensions.Logging;
using Soenneker.Python.Util.Abstract;
using Soenneker.TimeZones.Runner.Configuration;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Utils.File.Abstract;
using Soenneker.Utils.Process.Abstract;

namespace Soenneker.TimeZones.Runner.Osm;

public sealed class PyosmiumPrefilter
{
    private const string _script =
        """
        import osmium
        import sys
        import time

        source_path = sys.argv[1]
        output_path = sys.argv[2]
        include_admin_boundaries = sys.argv[3].lower() == "true"
        progress_interval_seconds = 30

        def has_tag(tags, key):
            value = tags.get(key)
            return value is not None and str(value).strip() != ""

        def is_timezone_relation(tags):
            has_timezone_id = has_tag(tags, "timezone") or has_tag(tags, "tzid")

            if not has_timezone_id:
                return False

            boundary = tags.get("boundary")

            if boundary is not None:
                boundary_value = str(boundary).strip().lower()

                if boundary_value == "timezone":
                    return True

                return include_admin_boundaries and boundary_value == "administrative" and has_tag(tags, "timezone")

            return has_tag(tags, "timezone") or has_tag(tags, "tzid")

        count = 0
        scanned = 0
        last_progress = time.monotonic()
        started = last_progress

        print(f"Starting pyosmium timezone relation scan: {source_path}", flush=True)

        with osmium.BackReferenceWriter(output_path, ref_src=source_path, overwrite=True, remove_tags=True, relation_depth=0) as writer:
            for relation in osmium.FileProcessor(source_path, osmium.osm.RELATION):
                scanned += 1

                if is_timezone_relation(relation.tags):
                    writer.add(relation)
                    count += 1

                now = time.monotonic()

                if now - last_progress >= progress_interval_seconds:
                    elapsed = max(0.001, now - started)
                    print(
                        f"Scanned {scanned:,} relations; matched {count:,} timezone relations; rate {scanned / elapsed:,.0f} relations/sec.",
                        flush=True
                    )
                    last_progress = now

            print(
                f"Relation scan complete. Scanned {scanned:,} relations; matched {count:,} timezone relations. Resolving referenced ways/nodes...",
                flush=True
            )

        print(f"pyosmium prefilter complete. Wrote referenced closure to {output_path}", flush=True)
        """;

    private readonly IFileUtil _fileUtil;
    private readonly IDirectoryUtil _directoryUtil;
    private readonly IPythonUtil _pythonUtil;
    private readonly IProcessUtil _processUtil;
    private readonly ILogger<PyosmiumPrefilter> _logger;

    public PyosmiumPrefilter(IFileUtil fileUtil, IDirectoryUtil directoryUtil, IPythonUtil pythonUtil, IProcessUtil processUtil,
        ILogger<PyosmiumPrefilter> logger)
    {
        _fileUtil = fileUtil;
        _directoryUtil = directoryUtil;
        _pythonUtil = pythonUtil;
        _processUtil = processUtil;
        _logger = logger;
    }

    public async ValueTask<string> EnsureFilteredExtract(ExtractDefinition extract, string sourcePbfPath, RunnerOptions options, string toolsDirectory,
        bool force, CancellationToken cancellationToken)
    {
        string outputPath = GetFilteredPath(sourcePbfPath, options.IncludeAdminBoundaries);

        if (await _fileUtil.Exists(outputPath, cancellationToken) && !force)
        {
            _logger.LogInformation("Reusing existing pyosmium-prefiltered extract for {ExtractName}: {FilteredPbfPath}", extract.Name, outputPath);
            return outputPath;
        }

        await _fileUtil.DeleteIfExists(outputPath, cancellationToken: cancellationToken);

        string pythonPath = await EnsurePyosmiumEnvironment(toolsDirectory, options, cancellationToken);
        string scriptPath = await EnsureScript(toolsDirectory, cancellationToken);

        _logger.LogInformation("Starting pyosmium prefilter for {ExtractName}. Input: {SourcePbfPath}. Output: {FilteredPbfPath}", extract.Name,
            sourcePbfPath, outputPath);

        await _processUtil.Start(pythonPath, toolsDirectory,
            $"\"{scriptPath}\" \"{sourcePbfPath}\" \"{outputPath}\" {options.IncludeAdminBoundaries.ToString().ToLowerInvariant()}",
            waitForExit: true, timeout: null, cancellationToken: cancellationToken);

        _logger.LogInformation("Completed pyosmium prefilter for {ExtractName}. Output: {FilteredPbfPath}. Size: {FilteredPbfSize:n0} bytes", extract.Name,
            outputPath, new FileInfo(outputPath).Length);
        return outputPath;
    }

    private async ValueTask<string> EnsurePyosmiumEnvironment(string toolsDirectory, RunnerOptions options, CancellationToken cancellationToken)
    {
        string basePythonPath = await _pythonUtil.EnsureInstalled(options.PythonVersion, options.AutoInstallPython, cancellationToken);
        string venvDirectory = Path.Combine(toolsDirectory, "pyosmium-venv");
        string venvPythonPath = GetVenvPythonPath(venvDirectory);

        await _directoryUtil.Create(toolsDirectory, cancellationToken: cancellationToken);

        if (!await _fileUtil.Exists(venvPythonPath, cancellationToken))
        {
            _logger.LogInformation("Creating pyosmium Python virtual environment: {VenvDirectory}", venvDirectory);
            await _processUtil.Start(basePythonPath, toolsDirectory, $"-m venv \"{venvDirectory}\"", waitForExit: true,
                timeout: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);
        }

        _logger.LogInformation("Ensuring pyosmium package is installed in {VenvDirectory}", venvDirectory);
        await _processUtil.Start(venvPythonPath, toolsDirectory, "-m pip install --upgrade pip osmium", waitForExit: true,
            timeout: TimeSpan.FromMinutes(10), cancellationToken: cancellationToken);

        return venvPythonPath;
    }

    private async ValueTask<string> EnsureScript(string toolsDirectory, CancellationToken cancellationToken)
    {
        string scriptDirectory = Path.Combine(toolsDirectory, "pyosmium");
        string scriptPath = Path.Combine(scriptDirectory, "prefilter_timezones.py");

        await _directoryUtil.Create(scriptDirectory, cancellationToken: cancellationToken);
        await File.WriteAllTextAsync(scriptPath, _script, cancellationToken);

        return scriptPath;
    }

    private static string GetFilteredPath(string sourcePbfPath, bool includeAdminBoundaries)
    {
        string directory = Path.GetDirectoryName(sourcePbfPath) ?? "";
        string fileName = Path.GetFileNameWithoutExtension(sourcePbfPath);
        string suffix = includeAdminBoundaries ? ".timezones-admin.prefiltered.osm.pbf" : ".timezones.prefiltered.osm.pbf";

        return Path.Combine(directory, fileName + suffix);
    }

    private static string GetVenvPythonPath(string venvDirectory)
    {
        if (OperatingSystem.IsWindows())
            return Path.Combine(venvDirectory, "Scripts", "python.exe");

        return Path.Combine(venvDirectory, "bin", "python");
    }
}
