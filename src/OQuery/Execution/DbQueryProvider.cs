using OQuery;
using OQuery.Compilation;
using OQuery.Compilation.Models;
using System.Data;
using System.Linq.Expressions;
using System.Text;
using DbCommand = System.Data.Common.DbCommand;
using DbConnection = System.Data.Common.DbConnection;

namespace OQuery.Execution;

class DbQueryProvider(
    IMetadataProvider metaProvider,
    IFunctionProvider functionProvider,
    IDbConnection dbConnection,
    ISqlDialect sqlDialect,
    string entityName) : IOQueryProvider
{
    public readonly Entity Entity = metaProvider.GetEntity(entityName);

    public IOQuery CreateQuery(Expression expression)
        => new DbQueryable(this, expression);

    public SqlTranslationResult GetQuery(Expression expression)
    {
        var compiled = Translate(expression);
        var parameterList = compiled.Parameters.ToArray();
        var commandText = SqlFormatter.Format(compiled.CommandText);
        commandText = sqlDialect.RenderQueryWithParameters(
            commandText,
            parameterList.Select(parameter => (parameter.Name, parameter.Value)).ToArray());

        return new SqlTranslationResult(
            commandText,
            parameterList.Select(e => new SqlParameter(e.Name, e.Value)).ToArray());
    }

    public OQueryResult ExecuteResult(Expression expression)
    {
        var compiled = Translate(expression);
        using var cmd = CreateDbCommand(compiled.CommandText, compiled.Parameters);
        using var reader = cmd.ExecuteReader();
        return DbReader.ReadResult(reader, compiled.ProjectionItems);
    }

    public IEnumerable<Dictionary<string, object?>> Execute(Expression expression)
        => Execute(Translate(expression));

    private IEnumerable<Dictionary<string, object?>> Execute(CompiledQuery compiled)
    {
        var cmd = CreateDbCommand(compiled.CommandText, compiled.Parameters);
        return new DbReader(cmd.ExecuteReader(), compiled.ProjectionItems);
    }

    public (List<Dictionary<string, object?>> Records, long TotalRecords) ExecuteWithTotalRecords(Expression expression)
    {
        var compiled = Translate(expression, includeTotalRecords: true);
        var records = new List<Dictionary<string, object?>>(Execute(compiled));
        var commandText = compiled.TotalRecordsCommandText
            ?? throw new InvalidOperationException("The total-record SQL was not generated.");

        using var cmd = CreateDbCommand(commandText, compiled.Parameters);
        var value = cmd.ExecuteScalar();
        return (records, ConvertTotalRecords(value));
    }

    public async Task<(List<Dictionary<string, object?>> Records, long TotalRecords)> ExecuteWithTotalRecordsAsync(
        Expression expression,
        CancellationToken cancellationToken)
    {
        var compiled = Translate(expression, includeTotalRecords: true);
        var records = await ExecuteAsync(compiled, cancellationToken);
        var commandText = compiled.TotalRecordsCommandText
            ?? throw new InvalidOperationException("The total-record SQL was not generated.");
        var value = await ExecuteScalarAsync(compiled, commandText, cancellationToken);

        return (records, ConvertTotalRecords(value));
    }

    public object? ExecuteScalar(Expression expression)
    {
        var compiled = Translate(expression);

        var cmd = CreateDbCommand(compiled.CommandText, compiled.Parameters);
        return cmd.ExecuteScalar();
    }

    public Task<List<Dictionary<string, object?>>> ExecuteAsync(Expression expression, CancellationToken cancellationToken)
        => ExecuteAsync(Translate(expression), cancellationToken);

    public async Task<OQueryResult> ExecuteResultAsync(
        Expression expression,
        CancellationToken cancellationToken)
    {
        var compiled = Translate(expression);
        var connection = GetAsyncConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        using var cmd = CreateDbCommand(compiled.CommandText, compiled.Parameters);
        if (cmd is not DbCommand dbCommand)
            throw new InvalidOperationException(
                $"The database command type [{cmd.GetType().FullName}] does not support asynchronous execution.");

        await using var reader = await dbCommand.ExecuteReaderAsync(cancellationToken);
        return await DbReader.ReadResultAsync(reader, compiled.ProjectionItems, cancellationToken);
    }

    private async Task<List<Dictionary<string, object?>>> ExecuteAsync(
        CompiledQuery compiled,
        CancellationToken cancellationToken)
    {
        var connection = GetAsyncConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        using var cmd = CreateDbCommand(compiled.CommandText, compiled.Parameters);
        if (cmd is not DbCommand dbCommand)
            throw new InvalidOperationException(
                $"The database command type [{cmd.GetType().FullName}] does not support asynchronous execution.");

        await using var reader = await dbCommand.ExecuteReaderAsync(cancellationToken);
        return await DbReader.ReadAllAsync(reader, compiled.ProjectionItems, cancellationToken);
    }

    public Task<object?> ExecuteScalarAsync(Expression expression, CancellationToken cancellationToken)
    {
        var compiled = Translate(expression);
        return ExecuteScalarAsync(compiled, compiled.CommandText, cancellationToken);
    }

    private async Task<object?> ExecuteScalarAsync(
        CompiledQuery compiled,
        string commandText,
        CancellationToken cancellationToken)
    {
        var connection = GetAsyncConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        using var cmd = CreateDbCommand(commandText, compiled.Parameters);
        if (cmd is not DbCommand dbCommand)
            throw new InvalidOperationException(
                $"The database command type [{cmd.GetType().FullName}] does not support asynchronous execution.");

        return await dbCommand.ExecuteScalarAsync(cancellationToken);
    }

    public string GetQueryText(Expression expression)
    {
        var query = GetQuery(expression);
        var sb = new StringBuilder(SqlFormatter.Format(query.CommandText));

        foreach (var parameter in query.Parameters)
        {
            sb.AppendLine();
            sb.AppendFormat("{0} = {1} (Type:{2})",
                parameter.Name,
                parameter.Value ?? "[NULL]",
                parameter.Value?.GetType().Name ?? "UNKNOWN");
        }

        return sb.ToString();
    }

    private CompiledQuery Translate(Expression expression)
        => Translate(expression, includeTotalRecords: false);

    private CompiledQuery Translate(Expression expression, bool includeTotalRecords)
        => new SqlQueryCompiler(metaProvider, functionProvider, sqlDialect)
            .Compile(expression, includeTotalRecords);

    private static long ConvertTotalRecords(object? value)
    {
        if (value is null or DBNull)
            throw new InvalidOperationException("The database did not return a total record count.");

        var count = Convert.ToInt64(value);
        return count > SqlQueryCompiler.TotalRecordsLimit ? -1 : count;
    }

    private IDbCommand CreateDbCommand(string sqlQuery, IEnumerable<DbParameter> parameters)
    {
        var parameterList = parameters.ToArray();
        var prepared = sqlDialect.PrepareCommand(
            sqlQuery,
            parameterList.Select(parameter => (parameter.Name, parameter.Value)).ToArray());
        var commandText = prepared.CommandText;

        if (dbConnection.State != ConnectionState.Open)
            dbConnection.Open();

        var cmd = dbConnection.CreateCommand();
        sqlDialect.ConfigureCommand(cmd);
        cmd.CommandText = commandText;

        foreach (var e in prepared.Parameters)
        {
            var para = cmd.CreateParameter();
            para.ParameterName = e.Name;
            sqlDialect.ConfigureParameter(para, e.Value);
            cmd.Parameters.Add(para);
        }

        return cmd;
    }

    private DbConnection GetAsyncConnection()
        => dbConnection as DbConnection
            ?? throw new InvalidOperationException(
                $"The database connection type [{dbConnection.GetType().FullName}] does not support asynchronous execution.");
}
