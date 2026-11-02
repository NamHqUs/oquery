#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130
public record Entity
{
    public Entity()
    {
        Name = string.Empty;
        DbTable = string.Empty;
        Fields = [];
    }

    public Entity(
        string Name,
        List<Field> Fields,
        string? DbSchema = null,
        string? DbTable = null,
        List<Relation>? Relations = null,
        bool IsRawSql = false)
    {
        this.Name = string.IsNullOrWhiteSpace(Name)
            ? throw new ArgumentException("Entity name is required.", nameof(Name))
            : Name;
        this.Fields = Fields ?? throw new ArgumentNullException(nameof(Fields));
        if (IsRawSql && string.IsNullOrWhiteSpace(DbTable))
            throw new ArgumentException("Raw SQL is required for a raw SQL entity.", nameof(DbTable));
        this.DbSchema = DbSchema;
        this.DbTable = string.IsNullOrWhiteSpace(DbTable) ? Name : DbTable;
        this.Relations = Relations;
        this.IsRawSql = IsRawSql;
    }

    public Entity(
        string Name,
        string DbSchema,
        string DbTable,
        List<Field> Fields,
        List<Relation>? Relations = null,
        bool IsRawSql = false)
        : this(Name, Fields, DbSchema, DbTable, Relations, IsRawSql)
    {
    }

    public string Name { get; init; }
    public string? DbSchema { get; init; }
    public string DbTable { get; init; }
    public List<Field> Fields { get; init; }
    public List<Relation>? Relations { get; init; }
    public bool IsRawSql { get; init; }

    public Field GetField(string fieldName)
        => Fields.FirstOrDefault(e => string.Equals(e.Name, fieldName, StringComparison.OrdinalIgnoreCase))
            ?? throw new FieldNotFoundException(Name, fieldName);

    public Relation GetRelation(string relationName)
        => Relations?.FirstOrDefault(e => string.Equals(e.Name, relationName, StringComparison.OrdinalIgnoreCase))
            ?? throw new RelationNotFoundException(Name, relationName);

    public override string ToString()
        => IsRawSql ? $"{Name}:{DbTable}" : $"{Name}:{DbSchema}.{DbTable}";
}
