using OQuery.Execution;
using System.Data;

#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public class OQueryEngine(
    IDbConnection dbConnection,
    IMetadataProvider metaProvider,
    IFunctionProvider functionProvider,
    ISqlDialect sqlDialect) : IDisposable
{
    private readonly ISqlDialect _sqlDialect = sqlDialect;
    private bool _disposed;

    internal IFunctionProvider FunctionProvider => functionProvider;

    public OQueryEngine(IDbConnection dbConnection, IMetadataProvider metaProvider, ISqlDialect sqlDialect)
        : this(
            dbConnection,
            metaProvider,
            new FunctionProvider(sqlDialect),
            sqlDialect)
    {
    }

    public OQueryEngine(IDbConnection dbConnection, IEnumerable<Entity> entities, ISqlDialect sqlDialect, IEnumerable<SqlFunctionDefintion>? functions = null):
        this(
            dbConnection,
            new MetadataProvider(entities),
            new FunctionProvider(sqlDialect, functions),
            sqlDialect)
    {
    }

    public IOQuery Query(string entity)
    {
        var queryProvider = new OQueryProvider(
            metaProvider,
            functionProvider,
            dbConnection,
            _sqlDialect,
            entity);

        return new OQueryable(queryProvider);
    }

    public IOQuery QueryOqr(string oqr)
        => OQueryableCreator.FromOQueryText(this, oqr);

    public IOQuery QueryOqr(string oqr, string contextName)
        => OQueryableCreator.FromOQueryText(this, oqr, contextName);

    public IOQuery Query(QueryDefinition queryRequest)
        => OQueryableCreator.FromDefinition(this, queryRequest);


    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;

            if (disposing)
                dbConnection.Dispose();
        }
    }
}
