namespace Soenneker.TimeZones.Runner.Configuration;

public static class RunnerOptionsParser
{
    public static RunnerOptions Parse(string[] args)
    {
        var options = new RunnerOptions();

        for (var i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            options = arg switch
            {
                "--scope" => options with { Scope = ReadValue(args, ref i, arg).ToLowerInvariant() },
                "--extract-url" => options with { ExtractUrl = ReadValue(args, ref i, arg) },
                "--extract-list" => options with { ExtractListPath = ReadValue(args, ref i, arg) },
                "--cache-directory" => options with { CacheDirectory = ReadValue(args, ref i, arg) },
                "--output" => options with { OutputPath = ReadValue(args, ref i, arg) },
                "--force-download" => options with { ForceDownload = true },
                "--skip-md5-checking" => options with { SkipMd5Checking = true },
                "--enable-md5-checking" => options with { SkipMd5Checking = false },
                "--include-admin-boundaries" => options with { IncludeAdminBoundaries = true },
                "--exclude-admin-boundaries" => options with { IncludeAdminBoundaries = false },
                "--disable-pyosmium-prefilter" => options with { UsePyosmiumPrefilter = false },
                "--python-version" => options with { PythonVersion = ReadValue(args, ref i, arg) },
                "--disable-python-auto-install" => options with { AutoInstallPython = false },
                "--min-ring-points" => options with { MinRingPoints = ParseInt(ReadValue(args, ref i, arg), arg) },
                "--verbose" => options with { Verbose = true },
                _ => throw new ArgumentException($"Unknown argument '{arg}'.")
            };
        }

        if (options.Scope is not ("world" or "continent" or "url"))
            throw new ArgumentException("--scope must be one of: world, continent, url.");

        if (string.IsNullOrWhiteSpace(options.PythonVersion))
            throw new ArgumentException("--python-version must not be empty.");

        if (options.MinRingPoints < 4)
            throw new ArgumentException("--min-ring-points must be at least 4.");

        return options;
    }

    private static string ReadValue(string[] args, ref int index, string argumentName)
    {
        if (index + 1 >= args.Length)
            throw new ArgumentException($"{argumentName} requires a value.");

        index++;
        return args[index];
    }

    private static int ParseInt(string value, string argumentName)
    {
        return int.TryParse(value, out int result) ? result : throw new ArgumentException($"{argumentName} requires a numeric value.");
    }
}
