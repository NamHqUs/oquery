#nullable enable
using System.Collections.Generic;

namespace OQuery;

public sealed record DatabaseSchemaDto(
    IReadOnlyList<DatabaseTableDto> Tables,
    IReadOnlyList<DatabaseRelationshipDto> Relationships);

public sealed record DatabaseTableDto(
    string Schema,
    string Name,
    IReadOnlyList<DatabaseColumnDto> Columns);

public sealed record DatabaseColumnDto(
    string Name,
    string DataType,
    bool IsPrimaryKey,
    bool IsNullable);

public sealed record DatabaseRelationshipDto(
    string Name,
    string SourceSchema,
    string SourceTable,
    IReadOnlyList<string> SourceColumns,
    string TargetSchema,
    string TargetTable,
    IReadOnlyList<string> TargetColumns,
    bool IsUnique);
