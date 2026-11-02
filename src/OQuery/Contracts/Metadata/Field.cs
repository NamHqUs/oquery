#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public sealed record Field
{
    public Field()
    {
        Name = string.Empty;
    }

    public Field(
        string Name,
        string? DbColumn = null,
        DataType DataType = DataType.String,
        bool IsRawSql = false)
    {
        this.Name = Name;
        this.DbColumn = DbColumn;
        this.DataType = DataType;
        this.IsRawSql = IsRawSql;
    }

    public string Name { get; init; }
    public string? DbColumn { get; init; }
    public DataType DataType { get; init; }
    public bool IsRawSql { get; init; }
}

public enum DataType
{
    String = 0,
    Number,
    Datetime,
    Boolean,
}
