using System.Data;

namespace OQuery;

public class PostgresOQueryEngine(IDbConnection dbConnection, IMetadataProvider metaProvider, IFunctionProvider functionProvider)
    : OQueryEngine(dbConnection, metaProvider, functionProvider, new PostgresDialect())
{
    public PostgresOQueryEngine(IDbConnection dbConnection, IMetadataProvider metaProvider)
        : this(dbConnection, metaProvider, new FunctionProvider(new PostgresDialect()))
    {
    }
}
