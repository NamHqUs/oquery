using OQuery.Execution;
using System.Linq.Expressions;
using System.Reflection;

#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130
public static partial class FluentQueryExtensions
{
    internal static class QuerySignatures
    {
        public static IOQuery Where(IOQuery source, Expression predicate) { return source; }
        public static IOQuery OrderBy(IOQuery source, Expression selection) { return source; }
        public static IOQuery ThenBy(IOQuery source, Expression selection) { return source; }
        public static IOQuery OrderByDescending(IOQuery source, Expression selection) { return source; }
        public static IOQuery ThenByDescending(IOQuery source, Expression selection) { return source; }
        public static IOQuery OrderByField(IOQuery source, IFieldDefinition field) { return source; }
        public static IOQuery OrderByDescendingField(IOQuery source, IFieldDefinition field) { return source; }
        public static IOQuery ThenByField(IOQuery source, IFieldDefinition field) { return source; }
        public static IOQuery ThenByDescendingField(IOQuery source, IFieldDefinition field) { return source; }
    }

    public static IOQuery Select(this IOQuery source, params string[] fields)
    {
        if (fields == null || fields.Length == 0)
            return source;

        var selection = new Dictionary<string, object?>();
        foreach (var e in fields)
            selection[e] = e;
        return source.Select(selection);
    }
    public static IOQuery Select(this IOQuery source, Dictionary<string, object?> selection)
        => source.Select(e => selection);
    public static IOQuery Select(this IOQuery source, Expression<Func<IQueryParameter, Dictionary<string, object?>>> selection)
        => source.CreateQuery(MethodBase.GetCurrentMethod()!, Expression.Quote(selection));

    public static IOQuery Where(this IOQuery source, Expression<Func<IQueryParameter, bool>> predicate)
        => source.CreateQuery(MethodBase.GetCurrentMethod()!, Expression.Quote(predicate));

    public static IOQuery OrderBy(this IOQuery source, params string[] fields)
        => source.CreateOrderQueryable(MethodBase.GetCurrentMethod()!.Name, fields);
    public static IOQuery ThenBy(this IOQuery source, params string[] fields)
        => source.CreateOrderQueryable(MethodBase.GetCurrentMethod()!.Name, fields);
    public static IOQuery ThenByDescending(this IOQuery source, params string[] fields)
        => source.CreateOrderQueryable(MethodBase.GetCurrentMethod()!.Name, fields);
    public static IOQuery GroupBy(this IOQuery source, params string[] fields)
        => source.CreateQuery(MethodBase.GetCurrentMethod()!, Expression.Constant(fields));
    public static IOQuery OrderByDescending(this IOQuery source, params string[] fields)
        => source.CreateOrderQueryable(MethodBase.GetCurrentMethod()!.Name, fields);
    public static IOQuery ThenByDescending<TSource>(this IOQuery source, params string[] fields) where TSource : Dictionary<string, object?>
        => source.CreateOrderQueryable(MethodBase.GetCurrentMethod()!.Name, fields);

    internal static IOQuery OrderByField(this IOQuery source, IFieldDefinition field)
        => source.CreateOrderFieldQueryable(nameof(QuerySignatures.OrderByField), field);
    internal static IOQuery OrderByDescendingField(this IOQuery source, IFieldDefinition field)
        => source.CreateOrderFieldQueryable(nameof(QuerySignatures.OrderByDescendingField), field);
    internal static IOQuery ThenByField(this IOQuery source, IFieldDefinition field)
        => source.CreateOrderFieldQueryable(nameof(QuerySignatures.ThenByField), field);
    internal static IOQuery ThenByDescendingField(this IOQuery source, IFieldDefinition field)
        => source.CreateOrderFieldQueryable(nameof(QuerySignatures.ThenByDescendingField), field);

    public static IOQuery Skip(this IOQuery source, int count)
        => source.CreateQuery(MethodBase.GetCurrentMethod()!, Expression.Constant(count));
    public static IOQuery TakeQuery(this IOQuery source, int count)
        => source.CreateQuery(GetQueryMethod(nameof(Take), 2)!, Expression.Constant(count));

}
