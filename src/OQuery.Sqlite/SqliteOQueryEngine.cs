using System.Data;

namespace OQuery;

public class SqliteOQueryEngine(IDbConnection dbConnection, IMetadataProvider metaProvider, IFunctionProvider functionProvider)
    : OQueryEngine(dbConnection, metaProvider, functionProvider, new SqliteDialect())
{
    public SqliteOQueryEngine(IDbConnection dbConnection, IMetadataProvider metaProvider)
        : this(dbConnection, metaProvider, new FunctionProvider(new SqliteDialect()))
    {
    }
}
