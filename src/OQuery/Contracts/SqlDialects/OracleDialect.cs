using System.Data;
using System.Globalization;

#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public sealed class OracleDialect : SqlDialectBase
{
    public override string Key => "Oracle";
    public override bool SupportsRecursiveCte => true;
    public override bool RequiresRecursiveCteKeyword => false;
    public override IEnumerable<SqlFunctionDefintion> CreateBuiltinFunctions()
        => new OracleFunctionDefinitionBuiltin().CreateAllFunctions();

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
    public override string RenderSubqueryAlias(string alias)
        => alias;

    public override string RenderBoolean(bool value) => value ? "1" : "0";
    public override string RenderStringLiteral(string value) => $"'{value.Replace("'", "''")}'";
    public override string RenderStringConcatenation(string left, string right) => $"{left} || {right}";
    public override string RenderTakePrefix(int count) => string.Empty;
    public override string RenderCurrentDateTime() => "CURRENT_TIMESTAMP";
    public override string RenderDateDifferenceInDays(string startDate, string endDate)
        => $"CAST({endDate} AS DATE) - CAST({startDate} AS DATE)";
    public override object? NormalizeParameterValue(object? value)
        => value is decimal decimalValue ? Convert.ToDouble(decimalValue, CultureInfo.InvariantCulture) : value;

    public override void ConfigureParameter(IDbDataParameter parameter, object? value)
    {
        parameter.ParameterName = parameter.ParameterName.Replace("@", ":", StringComparison.Ordinal);
        var normalized = NormalizeParameterValue(value);
        if (normalized is double or float)
            parameter.DbType = DbType.Double;
        parameter.Value = normalized ?? DBNull.Value;
    }
    public override void ConfigureCommand(IDbCommand command)
    {
        command.GetType().GetProperty("BindByName")?.SetValue(command, true);
    }

    public override string PrepareCommandText(
        string commandText,
        IReadOnlyList<(string Name, object? Value)> parameters)
        => parameters.Aggregate(
            commandText,
            (text, parameter) => text.Replace(
                parameter.Name,
                parameter.Name.Replace("@", ":", StringComparison.Ordinal),
                StringComparison.Ordinal));

    public override string RenderPaging(int? skip, int? take)
        => skip is null
            ? string.Empty
            : $" OFFSET {skip} ROWS" +
              (take is null ? string.Empty : $" FETCH NEXT {take} ROWS ONLY");
}
