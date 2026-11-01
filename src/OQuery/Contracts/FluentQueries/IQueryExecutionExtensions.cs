using OQuery.Execution;
using System.Linq.Expressions;
using System.Reflection;

#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130
public static partial class FluentQueryExtensions
{
    public static OQueryResult ToResult(this IOQuery source)
        => source.Provider.ExecuteResult(source.Expression);

    public static Task<OQueryResult> ToResultAsync(
        this IOQuery source,
        CancellationToken cancellationToken = default)
        => source.Provider.ExecuteResultAsync(source.Expression, cancellationToken);

    public static List<Dictionary<string, object?>> Take(this IOQuery source, int count)
        => source.TakeQuery(count).ToList();

    public static List<Dictionary<string, object?>> ToList(this IOQuery source)
        => [.. (IEnumerable<Dictionary<string, object?>>)source];

    public static List<T> ToList<T>(this IOQuery source)
        where T : new()
        => [.. source.ToList().Select(Execution.StrongTypeMaterializer.Materialize<T>)];

    public static (List<Dictionary<string, object?>> Records, long TotalRecords) ToListWithTotalRecords(this IOQuery source)
        =>  GetTotalRecordsProvider(source.Provider).ExecuteWithTotalRecords(source.Expression);

    public static (List<T> Records, long TotalRecords) ToListWithTotalRecords<T>(this IOQuery source)
        where T : new()
    {
        var result = source.ToListWithTotalRecords();
        return (result.Records.Select(Execution.StrongTypeMaterializer.Materialize<T>).ToList(), result.TotalRecords);
    }

    public static SqlTranslationResult ToSql(this IOQuery source)
        => source.Provider.GetQuery(source.Expression);

    public static Task<List<Dictionary<string, object?>>> ToListAsync(
        this IOQuery source,
        CancellationToken cancellationToken = default)
        => source.Provider.ExecuteAsync(source.Expression, cancellationToken);

    public static async Task<List<T>> ToListAsync<T>(
        this IOQuery source,
        CancellationToken cancellationToken = default)
        where T : new()
        => (await source.ToListAsync(cancellationToken))
            .Select(Execution.StrongTypeMaterializer.Materialize<T>)
            .ToList();

    public static Task<(List<Dictionary<string, object?>> Records, long TotalRecords)> ToListWithTotalRecordsAsync(
        this IOQuery source,
        CancellationToken cancellationToken = default)
        => GetTotalRecordsProvider(source.Provider)
            .ExecuteWithTotalRecordsAsync(source.Expression, cancellationToken);

    public static async Task<(List<T> Records, long TotalRecords)> ToListWithTotalRecordsAsync<T>(
        this IOQuery source,
        CancellationToken cancellationToken = default)
        where T : new()
    {
        var result = await source.ToListWithTotalRecordsAsync(cancellationToken);
        return (result.Records.Select(Execution.StrongTypeMaterializer.Materialize<T>).ToList(), result.TotalRecords);
    }

    public static Dictionary<string, object?> FirstOrDefault(this IOQuery source, Expression<Func<IQueryParameter, bool>> predicate)
        => source.Where(predicate).FirstOrDefault();
    public static Dictionary<string, object?> FirstOrDefault(this IOQuery source)
        => source.Take(1).FirstOrDefault()!;

    public static T? FirstOrDefault<T>(this IOQuery source)
        where T : new()
        => source.ToList<T>().FirstOrDefault();
    public static Task<Dictionary<string, object?>?> FirstOrDefaultAsync(
        this IOQuery source,
        Expression<Func<IQueryParameter, bool>> predicate,
        CancellationToken cancellationToken = default)
        => source.Where(predicate).FirstOrDefaultAsync(cancellationToken);
    public static async Task<Dictionary<string, object?>?> FirstOrDefaultAsync(
        this IOQuery source,
        CancellationToken cancellationToken = default)
        => (await source.TakeAsync(1, cancellationToken)).FirstOrDefault();

    public static async Task<T?> FirstOrDefaultAsync<T>(
        this IOQuery source,
        CancellationToken cancellationToken = default)
        where T : new()
        => (await source.ToListAsync<T>(cancellationToken)).FirstOrDefault();

    public static int Count(this IOQuery source)
    {
        var expression = source.CreateQuery(MethodBase.GetCurrentMethod()!).Expression;
        return Convert.ToInt32(source.Provider.ExecuteScalar(expression));
    }
    public static int Count(this IOQuery source, Expression<Func<IQueryParameter, bool>> predicate)
        => source.Where(predicate).Count();
    public static Task<int> CountAsync(
        this IOQuery source,
        CancellationToken cancellationToken = default)
        => ExecuteScalarAsync<int>(source, cancellationToken);
    public static Task<int> CountAsync(
        this IOQuery source,
        Expression<Func<IQueryParameter, bool>> predicate,
        CancellationToken cancellationToken = default)
        => source.Where(predicate).CountAsync(cancellationToken);

    public static bool Any(this IOQuery source, Expression<Func<IQueryParameter, bool>> predicate)
    {
        var expression = source.CreateQuery(MethodBase.GetCurrentMethod()!).Expression;
        return Convert.ToBoolean(source.Provider.ExecuteScalar(expression));
    }
    public static Task<bool> AnyAsync(
        this IOQuery source,
        Expression<Func<IQueryParameter, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var expression = source.CreateQuery(GetQueryMethod(nameof(Any), 1)!).Expression;
        return ExecuteScalarAsync<bool>(source, expression, cancellationToken);
    }

    private static IOQuery CreateQuery(this IOQuery source, MethodBase method, params Expression[] expressions)
    {
        var arguments = new List<Expression> { source.Expression };
        arguments.AddRange(expressions);

        return source.Provider.CreateQuery(Expression.Call(null, (MethodInfo)method, arguments.ToArray()));
    }
    private static IOQuery CreateOrderQueryable(this IOQuery source, string methodName, params string[] fields)
    {
        Expression<Func<IQueryParameter, string[]>> lambda = (e) => fields;
        return source.CreateQuery(typeof(QuerySignatures).GetMethod(methodName)!, Expression.Quote(lambda));
    }
    private static IOQuery CreateOrderFieldQueryable(this IOQuery source, string methodName, IFieldDefinition field)
        => source.CreateQuery(
            typeof(QuerySignatures).GetMethod(methodName)!,
            Expression.Constant(field, typeof(IFieldDefinition)));

    private static Task<T> ExecuteScalarAsync<T>(IOQuery source, CancellationToken cancellationToken)
        => ExecuteScalarAsync<T>(
            source,
            source.CreateQuery(GetQueryMethod(typeof(T) == typeof(int) ? nameof(Count) : nameof(Any), 1)!).Expression,
            cancellationToken);

    private static async Task<T> ExecuteScalarAsync<T>(
        IOQuery source,
        Expression expression,
        CancellationToken cancellationToken)
    {
        var value = await source.Provider.ExecuteScalarAsync(expression, cancellationToken);

        if (typeof(T) == typeof(int))
            return (T)(object)Convert.ToInt32(value);

        if (typeof(T) == typeof(bool))
            return (T)(object)Convert.ToBoolean(value);

        return (T)value!;
    }

    private static Task<List<Dictionary<string, object?>>> TakeAsync(
        this IOQuery source,
        int count,
        CancellationToken cancellationToken)
        => source.Provider.ExecuteAsync(
            source.CreateQuery(GetQueryMethod(nameof(Take), 2)!, Expression.Constant(count)).Expression,
            cancellationToken);

    private static MethodInfo? GetQueryMethod(string name, int parameterCount)
        => typeof(FluentQueryExtensions).GetMethods()
            .FirstOrDefault(method => method.Name == name && method.GetParameters().Length == parameterCount);

    private static DbQueryProvider GetTotalRecordsProvider(IOQueryProvider provider)
        => provider as DbQueryProvider
            ?? throw new NotSupportedException(
                $"The query provider [{provider.GetType().FullName}] does not support total-record queries.");
}
