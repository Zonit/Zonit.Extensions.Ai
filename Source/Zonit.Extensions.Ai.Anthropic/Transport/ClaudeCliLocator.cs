namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Cross-platform discovery of the <c>claude</c> CLI executable. Resolution order:
/// an explicit <see cref="AnthropicCliOptions.ExecutablePath"/>, then PATH, then the
/// well-known install locations for the current OS — npm global, the native installer's
/// user-local <c>bin</c>, and the <b>Claude Desktop</b>-bundled CLI under its per-version
/// folder (newest version first, so app updates are picked up automatically). The
/// auto-discovered result is cached for the process lifetime; an explicit path is
/// validated on every call. When nothing is found, set
/// <see cref="AnthropicCliOptions.ExecutablePath"/> to the absolute path.
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
    /// OS-specific install locations probed after PATH: npm global bin, the native
    /// installer's user-local <c>bin</c>, and the Claude Desktop-bundled CLI (per-version
    /// folder, newest first).
    /// </summary>
    private static IReadOnlyList<string> WellKnownDirectories()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dirs = new List<string>();

        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);        // Roaming
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(appData)) dirs.Add(Path.Combine(appData, "npm"));
            if (!string.IsNullOrEmpty(home)) dirs.Add(Path.Combine(home, ".local", "bin"));
            // Claude Desktop ships the CLI at <Roaming|Local>\Claude\claude-code\<version>\claude.exe
            if (!string.IsNullOrEmpty(appData)) AddClaudeDesktopVersionDirs(dirs, Path.Combine(appData, "Claude", "claude-code"));
            if (!string.IsNullOrEmpty(localAppData)) AddClaudeDesktopVersionDirs(dirs, Path.Combine(localAppData, "Claude", "claude-code"));
        }
        else if (OperatingSystem.IsMacOS())
        {
            if (!string.IsNullOrEmpty(home))
            {
                dirs.Add(Path.Combine(home, ".local", "bin"));
                dirs.Add(Path.Combine(home, ".npm-global", "bin"));
                AddClaudeDesktopVersionDirs(dirs, Path.Combine(home, "Library", "Application Support", "Claude", "claude-code"));
            }
            dirs.Add("/usr/local/bin");
            dirs.Add("/opt/homebrew/bin");
            dirs.Add("/usr/bin");
        }
        else // Linux and other Unix
        {
            if (!string.IsNullOrEmpty(home))
            {
                dirs.Add(Path.Combine(home, ".local", "bin"));
                dirs.Add(Path.Combine(home, ".npm-global", "bin"));
                AddClaudeDesktopVersionDirs(dirs, Path.Combine(home, ".config", "Claude", "claude-code"));
            }
            dirs.Add("/usr/local/bin");
            dirs.Add("/opt/homebrew/bin");
            dirs.Add("/usr/bin");
        }

        return dirs;
    }

    /// <summary>
    /// Claude Desktop installs the CLI under a per-version subfolder
    /// (e.g. <c>…\claude-code\2.1.170\claude.exe</c>). Adds each version folder as a
    /// candidate directory, newest first, so an app update is picked up without any config.
    /// </summary>
    private static void AddClaudeDesktopVersionDirs(List<string> dirs, string parent)
    {
        if (!System.IO.Directory.Exists(parent)) return;
        string[] subdirs;
        try { subdirs = System.IO.Directory.GetDirectories(parent); }
        catch { return; } // unreadable — skip
        dirs.AddRange(OrderVersionDirsDescending(subdirs));
    }

    /// <summary>Orders version subfolders newest-first (semantic version, then ordinal name).</summary>
    internal static string[] OrderVersionDirsDescending(string[] directories)
    {
        var ordered = (string[])directories.Clone();
        Array.Sort(ordered, static (a, b) =>
        {
            var byVersion = ParseVersion(Path.GetFileName(b)).CompareTo(ParseVersion(Path.GetFileName(a)));
            return byVersion != 0 ? byVersion : string.CompareOrdinal(Path.GetFileName(b), Path.GetFileName(a));
        });
        return ordered;
    }

    private static Version ParseVersion(string? name)
        => Version.TryParse(name, out var v) ? v : new Version(0, 0);

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
