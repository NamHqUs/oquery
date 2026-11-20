#nullable enable
using System.Collections.Generic;

namespace OQuery;

public sealed record DatabaseTableData(
    string Name,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows);
