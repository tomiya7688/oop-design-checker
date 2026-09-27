using System.Xml.Linq;

namespace OopDesignChecker.Analysis;

internal static class ProjectTypeDetector
{
    public static bool IsApplication(string rootPath)
    {
        foreach (
            var projectFile in Directory.EnumerateFiles(
                rootPath,
                "*.csproj",
                SearchOption.AllDirectories
            )
        )
        {
            if (PathFilter.ShouldIgnore(projectFile))
            {
                continue;
            }

            var document = XDocument.Load(projectFile);
            var outputType = document
                .Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "OutputType")
                ?.Value;

            if (
                string.Equals(outputType, "Exe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(outputType, "WinExe", StringComparison.OrdinalIgnoreCase)
            )
            {
                return true;
            }
        }

        return false;
    }
}
