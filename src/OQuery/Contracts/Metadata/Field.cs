#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public sealed record Field(
    string Name,
    string? DbColumn = null,
    DataType DataType = DataType.String,
    bool IsRawSql = false);

public enum DataType
{
    String = 0,
    Number,
    Datetime,
    Boolean,
}
