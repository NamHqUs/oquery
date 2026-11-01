using System.Data;
using System.Globalization;

#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public sealed class FirebirdDialect : SqlDialectBase
{
    public override string Key => "Firebird";
    public override bool SupportsRecursiveCte => true;
    public override IEnumerable<SqlFunctionDefintion> CreateBuiltinFunctions()
        => new FirebirdFunctionDefinitionBuiltin().CreateAllFunctions();

    protected override IReadOnlyList<string> ForbiddenKeywords =>
    [
        "ALTER", "CREATE", "DELETE", "DROP", "EXECUTE", "INSERT", "INTO",
        "MERGE", "UPDATE"
    ];

    public override string QuoteIdentifier(string identifier)
        => $"\"{identifier.ToUpperInvariant().Replace("\"", "\"\"")}\"";

    public override string RenderTable(Entity entity)
        => entity.IsRawSql
            ? ValidateRawSqlEntity(entity.DbTable)
            : QuoteIdentifier(entity.DbTable ?? entity.Name);

    public override string RenderBoolean(bool value) => value ? "TRUE" : "FALSE";
    public override string RenderStringLiteral(string value) => $"'{value.Replace("'", "''")}'";
    public override string RenderStringConcatenation(string left, string right) => $"{left} || {right}";
    public override string RenderTakePrefix(int count) => $"FIRST {count} ";
    public override object? NormalizeParameterValue(object? value)
        => value is decimal decimalValue ? Convert.ToDouble(decimalValue, CultureInfo.InvariantCulture) : value;
    public override void ConfigureParameter(IDbDataParameter parameter, object? value)
    {
        var normalized = NormalizeParameterValue(value);
        if (normalized is double or float)
            parameter.DbType = DbType.Double;
        parameter.Value = normalized ?? DBNull.Value;
    }
    public override string PrepareCommandText(
        string commandText,
        IReadOnlyList<(string Name, object? Value)> parameters)
    {
        foreach (var parameter in parameters.Where(parameter =>
            parameter.Value is decimal or double or float))
        {
            commandText = commandText.Replace(
                parameter.Name,
                $"CAST({parameter.Name} AS DOUBLE PRECISION)",
                StringComparison.Ordinal);
        }

        return commandText;
    }
    public override string RenderCurrentDateTime() => "CURRENT_TIMESTAMP";
    public override string RenderDateDifferenceInDays(string startDate, string endDate)
        => $"DATEDIFF(DAY FROM {startDate} TO {endDate})";

    public override string RenderPaging(int? skip, int? take)
        => skip is null
            ? string.Empty
            : $" ROWS {skip + 1}" + (take is null ? string.Empty : $" TO {skip + take}");
}
