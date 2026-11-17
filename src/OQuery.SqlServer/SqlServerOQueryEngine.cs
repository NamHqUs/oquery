using System.Data;

namespace OQuery;

public class SqlServerOQueryEngine(IDbConnection dbConnection, IMetadataProvider metaProvider, IFunctionProvider functionProvider)
    : OQueryEngine(dbConnection, metaProvider, functionProvider, new SqlServerDialect())
{
    public SqlServerOQueryEngine(IDbConnection dbConnection, IMetadataProvider metaProvider)
        : this(dbConnection, metaProvider, new FunctionProvider(new SqlServerDialect()))
    {
    }
}
