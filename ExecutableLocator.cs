namespace LoupixDeck.Plugin.HyperHeadset;

/// <summary>Finds an executable the same way a shell would look it up on <c>PATH</c>.</summary>
internal static class ExecutableLocator
{
    public static bool Exists(string executable)
    {
        if (string.IsNullOrWhiteSpace(executable))
            return false;

        if (executable.Contains(Path.DirectorySeparatorChar)
            || executable.Contains(Path.AltDirectorySeparatorChar))
        {
            return File.Exists(ExpandHome(executable));
        }

        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
            return false;

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory, executable);
            if (File.Exists(candidate))
                return true;
            if (OperatingSystem.IsWindows() && File.Exists(candidate + ".exe"))
                return true;
        }

        return false;
    }

    private static string ExpandHome(string path)
    {
        if (path != "~" && !path.StartsWith("~/", StringComparison.Ordinal))
            return path;

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return path == "~" ? home : Path.Combine(home, path[2..]);
    }
}
