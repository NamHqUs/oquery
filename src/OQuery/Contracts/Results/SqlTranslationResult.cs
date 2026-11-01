#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public sealed record SqlTranslationResult(string CommandText, IReadOnlyList<SqlParameter> Parameters);

public sealed record SqlParameter(string Name, object? Value);
