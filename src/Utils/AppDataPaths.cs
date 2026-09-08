using System.IO;

namespace SnipIt.Utils;

internal static class AppDataPaths
{
    // Optional isolated profile for portable installations and development testing.
    public static string GetFolder(Environment.SpecialFolder defaultFolder)
    {
        var custom = Environment.GetEnvironmentVariable("SNIPIT_DATA_DIRECTORY");
        return string.IsNullOrWhiteSpace(custom)
            ? Path.Combine(Environment.GetFolderPath(defaultFolder), "SnipIt")
            : Path.GetFullPath(custom);
    }
}
