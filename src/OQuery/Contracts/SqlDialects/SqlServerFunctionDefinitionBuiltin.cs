#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

internal sealed class SqlServerFunctionDefinitionBuiltin : SqlFunctionDefinitionBuiltinBase
{
    protected override string Dialect => "SqlServer";

    internal override IEnumerable<SqlFunctionDefintion> CreateAllFunctions() =>
        base.CreateAllFunctions().Append(GetDate()).Append(Round());

    private SqlFunctionDefintion GetDate() =>
        Create("GetDate", [], DataType.Datetime, "Returns the current date and time.", "GETDATE()");

    private SqlFunctionDefintion Round() =>
        Create("Round", [new("value", DataType.Number), new("digits", DataType.Number)], 
            DataType.Number, "Rounds a number to the specified number of decimal places.",
            "ROUND({1}, {2})");
}
