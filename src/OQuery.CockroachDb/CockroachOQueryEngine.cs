using System.Data;

namespace OQuery;

public class CockroachOQueryEngine(IDbConnection dbConnection, IMetadataProvider metaProvider, IFunctionProvider functionProvider)
    : OQueryEngine(dbConnection, metaProvider, functionProvider, new CockroachDialect())
{
    public CockroachOQueryEngine(IDbConnection dbConnection, IMetadataProvider metaProvider)
        : this(dbConnection, metaProvider, new FunctionProvider(new CockroachDialect()))
    {
    }
}
