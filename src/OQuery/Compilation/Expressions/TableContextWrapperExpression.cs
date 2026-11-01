using OQuery;

namespace OQuery.Compilation.Expressions;

class TableContextWrapperExpression(TableExpression tableContext, string alias)
    : TableExpression(CreateEntity(tableContext), alias)
{
    private static Entity CreateEntity(TableExpression tableContext)
        => new Entity(
            Name: $"Wrap_{tableContext.Entity.Name}",
            DbTable: $"({tableContext})",
            IsRawSql: true,
            Fields: tableContext.ProjectionItems.Select(e => new Field(e.Alias, e.Alias)).ToList());
}
