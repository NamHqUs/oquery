using OQuery.Execution;
using System.Data;

#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public class OQueryEngine(
    IDbConnection dbConnection,
    IMetadataProvider metaProvider,
    IFunctionProvider functionProvider,
    ISqlDialect? sqlDialect = null) : IDisposable
{
    private readonly ISqlDialect _sqlDialect = sqlDialect ?? SqlDialect.ForConnection(dbConnection);
    private bool _disposed;

    internal IFunctionProvider FunctionProvider => functionProvider;

    public OQueryEngine(IDbConnection dbConnection, IMetadataProvider metaProvider, ISqlDialect? sqlDialect = null)
        : this(
            dbConnection,
            metaProvider,
            new FunctionProvider(sqlDialect ?? SqlDialect.ForConnection(dbConnection)),
            sqlDialect)
    {
    }

    public OQueryEngine(IDbConnection dbConnection, IEnumerable<Entity> entities, IEnumerable<SqlFunctionDefintion>? functions = null, ISqlDialect? sqlDialect = null):
        this(
            dbConnection,
            new MetadataProvider(entities),
            new FunctionProvider(sqlDialect ?? SqlDialect.ForConnection(dbConnection), functions),
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
