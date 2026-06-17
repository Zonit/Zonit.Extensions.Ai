namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Cross-platform discovery of the <c>claude</c> CLI executable. Resolution order:
/// an explicit <see cref="AnthropicCliOptions.ExecutablePath"/>, then PATH, then the
/// well-known install locations for the current OS. The auto-discovered result is
/// cached for the process lifetime; an explicit path is validated on every call.
/// </summary>
internal static class ClaudeCliLocator
{
    private static string? _cachedAuto;

    /// <summary>
    /// Resolves the <c>claude</c> executable. <paramref name="explicitPath"/> wins when
    /// set (and must exist). Throws <see cref="FileNotFoundException"/> when nothing is found.
    /// </summary>
    public static string Resolve(string? explicitPath)
    {
        // Fully qualified: ImplicitUsings pulls in a deprecated Zonit type named `File`.
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return ResolveCore(explicitPath, System.IO.File.Exists, pathEnvironment: null, WellKnownDirectories());

        return _cachedAuto ??= ResolveCore(
            explicitPath: null,
            System.IO.File.Exists,
            Environment.GetEnvironmentVariable("PATH"),
            WellKnownDirectories());
    }

    /// <summary>
    /// Pure resolution logic with injected filesystem / PATH for unit testing.
    /// </summary>
    internal static string ResolveCore(
        string? explicitPath,
        Func<string, bool> fileExists,
        string? pathEnvironment,
        IReadOnlyList<string> wellKnownDirectories)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            if (fileExists(explicitPath)) return explicitPath;
            throw new FileNotFoundException(
                $"Configured Claude CLI executable was not found at '{explicitPath}'. " +
                "Fix AnthropicCliOptions.ExecutablePath, or unset it to auto-discover on PATH.");
        }

        foreach (var candidate in EnumerateCandidates(pathEnvironment, wellKnownDirectories))
            if (fileExists(candidate))
                return candidate;

        throw new FileNotFoundException(BuildNotFoundMessage(pathEnvironment, wellKnownDirectories));
    }

    internal static string[] ExecutableNames() => OperatingSystem.IsWindows()
        ? ["claude.exe", "claude.cmd", "claude.bat"]
        : ["claude"];

    private static IEnumerable<string> EnumerateCandidates(string? pathEnvironment, IReadOnlyList<string> wellKnownDirectories)
    {
        var names = ExecutableNames();

        var dirs = new List<string>();
        if (!string.IsNullOrEmpty(pathEnvironment))
            dirs.AddRange(pathEnvironment.Split(Path.PathSeparator,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        dirs.AddRange(wellKnownDirectories);

        foreach (var dir in dirs)
        {
            foreach (var name in names)
            {
                string combined;
                try { combined = Path.Combine(dir, name); }
                catch { continue; } // skip PATH entries with invalid path characters
                yield return combined;
            }
        }
    }

    /// <summary>
    /// OS-specific install locations probed after PATH (npm global bin, user-local bin, etc.).
    /// </summary>
    private static IReadOnlyList<string> WellKnownDirectories()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dirs = new List<string>();

        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData)) dirs.Add(Path.Combine(appData, "npm"));
            if (!string.IsNullOrEmpty(home)) dirs.Add(Path.Combine(home, ".local", "bin"));
        }
        else
        {
            if (!string.IsNullOrEmpty(home))
            {
                dirs.Add(Path.Combine(home, ".local", "bin"));
                dirs.Add(Path.Combine(home, ".npm-global", "bin"));
            }
            dirs.Add("/usr/local/bin");
            dirs.Add("/opt/homebrew/bin");
            dirs.Add("/usr/bin");
        }

        return dirs;
    }

    private static string BuildNotFoundMessage(string? pathEnvironment, IReadOnlyList<string> wellKnownDirectories)
    {
        var names = string.Join(" / ", ExecutableNames());
        var extra = wellKnownDirectories.Count == 0 ? "(none)" : string.Join(", ", wellKnownDirectories);
        return
            $"Could not find the Claude CLI ({names}) on PATH or in well-known locations [{extra}]. " +
            "Install Claude Code (https://docs.claude.com/claude-code) and run `claude login`, " +
            "or set AnthropicCliOptions.ExecutablePath to its absolute path.";
    }
}
