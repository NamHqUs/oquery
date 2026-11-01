using OQuery.Execution;
using System.Data;

#pragma warning disable IDE0130
namespace OQuery.Contracts;
#pragma warning restore IDE0130

public class OQueryEngine(
    IDbConnection dbConnection,
    IMetadataProvider metaProvider,
    IFunctionProvider functionProvider,
    ISqlDialect? sqlDialect = null) : IDisposable
{
    private readonly ISqlDialect _sqlDialect = sqlDialect ?? SqlDialect.ForConnection(dbConnection);
    private bool _disposed;

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
        var queryProvider = new DbQueryProvider(
            metaProvider,
            functionProvider,
            dbConnection,
            _sqlDialect,
            entity);

        return new DbQueryable(queryProvider);
    }

    public IOQuery QueryYml(string yml)
        => DbQueryFromQueryDto.BuildQueryable(this, yml);

    public IOQuery Query(QueryDefinition queryRequest)
        => DbQueryFromQueryDto.BuildQueryable(this, queryRequest);


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

public class SqlServerOQueryEngine(IDbConnection dbConnection, IMetadataProvider metaProvider, IFunctionProvider functionProvider)
    : OQueryEngine(dbConnection, metaProvider, functionProvider, new SqlServerDialect())
{
    public SqlServerOQueryEngine(IDbConnection dbConnection, IMetadataProvider metaProvider)
        : this(dbConnection, metaProvider, new FunctionProvider(new SqlServerDialect()))
    {
    }
}

public class SqliteOQueryEngine(IDbConnection dbConnection, IMetadataProvider metaProvider, IFunctionProvider functionProvider)
    : OQueryEngine(dbConnection, metaProvider, functionProvider, new SqliteDialect())
{
    public SqliteOQueryEngine(IDbConnection dbConnection, IMetadataProvider metaProvider)
        : this(dbConnection, metaProvider, new FunctionProvider(new SqliteDialect()))
    {
    }
}

public class PostgresOQueryEngine(IDbConnection dbConnection, IMetadataProvider metaProvider, IFunctionProvider functionProvider)
    : OQueryEngine(dbConnection, metaProvider, functionProvider, new PostgresDialect())
{
    public PostgresOQueryEngine(IDbConnection dbConnection, IMetadataProvider metaProvider)
        : this(dbConnection, metaProvider, new FunctionProvider(new PostgresDialect()))
    {
    }
}

public class CockroachOQueryEngine(IDbConnection dbConnection, IMetadataProvider metaProvider, IFunctionProvider functionProvider)
    : OQueryEngine(dbConnection, metaProvider, functionProvider, new CockroachDialect())
{
    public CockroachOQueryEngine(IDbConnection dbConnection, IMetadataProvider metaProvider)
        : this(dbConnection, metaProvider, new FunctionProvider(new CockroachDialect()))
    {
    }
}
