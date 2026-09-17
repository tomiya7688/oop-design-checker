namespace OopDesignChecker.Configuration;

public static class CheckerConfigurationPathResolver
{
    public static string? Resolve(string targetPath, string? explicitPath)
    {
        var targetDirectory = GetTargetDirectory(targetPath);
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return ResolveExplicit(targetPath, explicitPath);
        }

        return FindNearestDefaultConfiguration(targetDirectory);
    }

    public static string ResolveExplicit(string? targetPath, string explicitPath)
    {
        if (string.IsNullOrWhiteSpace(explicitPath))
        {
            throw new ArgumentException("Configuration path is required.", nameof(explicitPath));
        }

        if (Path.IsPathRooted(explicitPath))
        {
            var fullPath = Path.GetFullPath(explicitPath);
            EnsureConfigurationExists(fullPath);
            return fullPath;
        }

        var checkedPaths = new List<string>();
        var currentDirectoryPath = Path.GetFullPath(explicitPath, Directory.GetCurrentDirectory());
        checkedPaths.Add(currentDirectoryPath);
        if (File.Exists(currentDirectoryPath))
        {
            return currentDirectoryPath;
        }

        if (!string.IsNullOrWhiteSpace(targetPath))
        {
            var targetDirectory = GetTargetDirectory(targetPath);
            var targetRelativePath = Path.GetFullPath(explicitPath, targetDirectory);
            if (!checkedPaths.Contains(targetRelativePath, StringComparer.OrdinalIgnoreCase))
            {
                checkedPaths.Add(targetRelativePath);
            }

            if (File.Exists(targetRelativePath))
            {
                return targetRelativePath;
            }
        }

        throw new InvalidOperationException(
            $"Configuration file does not exist. Checked: {string.Join("; ", checkedPaths)}"
        );
    }

    private static string? FindNearestDefaultConfiguration(string targetDirectory)
    {
        var directory = new DirectoryInfo(targetDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, CheckerConfigurationLoader.DefaultFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string GetTargetDirectory(string targetPath)
    {
        var fullTargetPath = Path.GetFullPath(targetPath);
        if (Directory.Exists(fullTargetPath))
        {
            return fullTargetPath;
        }

        return Path.GetDirectoryName(fullTargetPath) ?? Directory.GetCurrentDirectory();
    }

    private static void EnsureConfigurationExists(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Configuration file does not exist: {path}");
        }
    }
}
