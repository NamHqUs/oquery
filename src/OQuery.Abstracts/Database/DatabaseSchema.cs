#nullable enable
using System.Collections.Generic;

namespace OQuery;

public sealed record DatabaseSchema(
    IReadOnlyList<DatabaseTable> Tables,
    IReadOnlyList<DatabaseRelationship> Relationships);

public sealed record DatabaseTable(
    string Schema,
    string Name,
    IReadOnlyList<DatabaseColumn> Columns);

public sealed record DatabaseColumn(
    string Name,
    string DataType,
    bool IsPrimaryKey,
    bool IsNullable);

public sealed record DatabaseRelationship(
    string Name,
    string SourceSchema,
    string SourceTable,
    IReadOnlyList<string> SourceColumns,
    string TargetSchema,
    string TargetTable,
    IReadOnlyList<string> TargetColumns,
    bool IsUnique);
