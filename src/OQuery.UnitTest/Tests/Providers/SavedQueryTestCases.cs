namespace OQuery.UnitTest.Tests.Providers;

internal static class SavedQueryTestCases
{
    private static readonly string[] LegacyRequests =
    [
        "Test-Check Context Calculation Fields.json",
        "Test-Multiple Contexts.json",
        "Test-Query directly on REF CONTEXT.json",
        "Shared Context.json"
    ];

    public static IEnumerable<TestCaseData> Cases
        => GetRequestPaths()
            .Select((path, index) =>
            {
                var fileName = Path.GetFileName(path);
                var queryName = Path.GetFileNameWithoutExtension(path);
                return new TestCaseData(fileName)
                    .SetName($"SavedQueryExecutesOnProvider_{index + 1:00}_{Sanitize(queryName)}")
                    .SetDescription(queryName);
            });

    public static IEnumerable<string> GetRequestPaths()
    {
        var root = Path.Combine(GetRepositoryRoot(), "data-storage", "queries");
        var requests = Directory
            .EnumerateFiles(root, "*.json")
            .Where(path => !LegacyRequests.Contains(
                Path.GetFileName(path),
                StringComparer.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        if (requests.Length != 26)
            throw new InvalidDataException(
                $"Expected 26 saved query definitions, but found {requests.Length}.");

        return requests;
    }

    public static string GetRequestPath(string fileName)
        => Path.Combine(GetRepositoryRoot(), "data-storage", "queries", fileName);

    private static string GetRepositoryRoot()
        => new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "../../../../../")).FullName;

    private static string Sanitize(string queryName)
        => string.Concat(queryName.Select(character =>
            char.IsLetterOrDigit(character) ? character : '_')).Trim('_');
}
