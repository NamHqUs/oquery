using OQuery.Compilation.Models;
using System.Collections;
using System.Data;
using System.Data.Common;

namespace OQuery.Execution;

class DbReader(
    IDataReader reader,
    IEnumerable<ProjectionItem> projectionItems,
    bool isResultInHierarchy = false,
    Action<Dictionary<string, object?>>? onProcessDataRecord = null,
    Func<string, object, object>? onProcessDataField = null) : IEnumerable<Dictionary<string, object?>>, IEnumerable
{
    #region Inner Classes

    class FieldMap(int dbColumnIndex, string fieldName)
    {
        public readonly int DbColumnIndex = dbColumnIndex;
        public readonly string FieldName = fieldName;
        public Dictionary<string, FieldMap>? Children;
    };

    class Enumerator : IEnumerator<Dictionary<string, object?>>, IEnumerator, IDisposable
    {
        private readonly IDataReader reader;
        private readonly Action<Dictionary<string, object?>>? onProcessDataRecord;
        private readonly Func<string, object, object>? onProcessDataField;
        private readonly Lazy<Dictionary<string, FieldMap>> _dbMapping;
        public Dictionary<string, object?> Current { get; private set; } = default!;

        public Enumerator(
            IDataReader reader,
            IEnumerable<ProjectionItem> projectionItems,
            bool isResultInHierarchy = false,
            Action<Dictionary<string, object?>>? onProcessDataRecord = null,
            Func<string, object, object>? onProcessDataField = null)
        {
            this.reader = reader;
            this.onProcessDataRecord = onProcessDataRecord;
            this.onProcessDataField = onProcessDataField;
            _dbMapping = new Lazy<Dictionary<string, FieldMap>>(
                () => BuildMapping(projectionItems, isResultInHierarchy));
        }

        object IEnumerator.Current => this.Current;

        public bool MoveNext()
        {
            if (!reader.Read())
                return false;

            var record = ReadRecord(reader, _dbMapping.Value);

            onProcessDataRecord?.Invoke(record);

            this.Current = record;
            return true;
        }

        public void Reset() { }

        public void Dispose()
            => reader.Dispose();

        internal static Dictionary<string, object?> ReadRecord(IDataReader reader, Dictionary<string, FieldMap> map,
            Action<Dictionary<string, object?>>? onProcessDataRecord = null,
            Func<string, object, object>? onProcessDataField = null)
        {
            var record = new Dictionary<string, object?>();
            foreach (var e in map)
            {
                var fieldMap = e.Value;
                object val = fieldMap.Children == null 
                    ? reader.GetValue(fieldMap.DbColumnIndex) 
                    : ReadRecord(reader, fieldMap.Children);

                record[e.Key] =
                    val == DBNull.Value 
                        ? null 
                        : val is Dictionary<string, object?> || onProcessDataField == null
                            ? val! 
                            : onProcessDataField(fieldMap.FieldName, val);
            }
            return record;
        }

        internal static Dictionary<string, FieldMap> BuildMapping(IEnumerable<ProjectionItem> projectionItems, bool isResultInHierarchy)
        {
            var mapRoot = new Dictionary<string, FieldMap>();
            var colIndex = 0;

            foreach (var e in projectionItems)
            {
                if (isResultInHierarchy)
                {
                    var eles = e.Alias.Split('.');

                    FieldMap? fieldMap = null;
                    foreach (var propName in eles)
                    {
                        var current = fieldMap == null ? mapRoot : (fieldMap.Children ??= []);

                        current[propName] = fieldMap = current.TryGetValue(propName, out fieldMap) ? fieldMap : new FieldMap(colIndex, e.Alias);
                    }
                }
                else
                    mapRoot[e.Alias] = new FieldMap(colIndex, e.Alias);

                colIndex++;
            }

            return mapRoot;
        }
    }

    #endregion

    public IEnumerator<Dictionary<string, object?>> GetEnumerator()
        => new Enumerator(reader, projectionItems, isResultInHierarchy, onProcessDataRecord, onProcessDataField);

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    internal static async Task<List<Dictionary<string, object?>>> ReadAllAsync(
        DbDataReader reader,
        IEnumerable<ProjectionItem> projectionItems,
        CancellationToken cancellationToken)
    {
        var mapping = Enumerator.BuildMapping(projectionItems, false);
        var records = new List<Dictionary<string, object?>>();

        while (await reader.ReadAsync(cancellationToken))
            records.Add(Enumerator.ReadRecord(reader, mapping));

        return records;
    }

    internal static OQueryResult ReadResult(
        IDataReader reader,
        IEnumerable<ProjectionItem> projectionItems)
    {
        var projections = projectionItems.ToArray();
        var fields = projections
            .Select((projection, index) => new OQueryResultField(
                projection.Alias,
                reader.GetFieldType(index)))
            .ToArray();
        var data = new List<object?[]>();

        while (reader.Read())
        {
            var values = new object?[projections.Length];
            for (var index = 0; index < values.Length; index++)
            {
                var value = reader.GetValue(index);
                values[index] = value is DBNull ? null : value;
            }

            data.Add(values);
        }

        return new OQueryResult(fields, data);
    }

    internal static async Task<OQueryResult> ReadResultAsync(
        DbDataReader reader,
        IEnumerable<ProjectionItem> projectionItems,
        CancellationToken cancellationToken)
    {
        var projections = projectionItems.ToArray();
        var fields = projections
            .Select((projection, index) => new OQueryResultField(
                projection.Alias,
                reader.GetFieldType(index)))
            .ToArray();
        var data = new List<object?[]>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var values = new object?[projections.Length];
            for (var index = 0; index < values.Length; index++)
            {
                var value = reader.GetValue(index);
                values[index] = value is DBNull ? null : value;
            }

            data.Add(values);
        }

        return new OQueryResult(fields, data);
    }
}
