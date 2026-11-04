#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public interface IMetadataProvider
{
    IMetadataFieldAuthorizer Authorizer { get; }
    IEntity GetEntity(string entityName);
    IReadOnlyList<Field> GetFields(IEntity entity);
    Field GetField(IEntity entity, string fieldName);
}

public class MetadataProvider : IMetadataProvider
{
    private readonly Dictionary<string, Entity> _entities;

    public MetadataProvider(
        IEnumerable<Entity> entities,
        IMetadataFieldAuthorizer? authorizer = null)
    {
        ArgumentNullException.ThrowIfNull(entities);

        _entities = entities.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
        Authorizer = authorizer
            ?? new MetadataFieldAuthorizer(accessMode: AccessMode.Allow);
    }

    public IMetadataFieldAuthorizer Authorizer { get; }
    public IReadOnlyCollection<Entity> Entities => _entities.Values;

    public IEntity GetEntity(string entityName)
    {
        if (_entities.TryGetValue(entityName, out Entity? entity))
            return entity;

        throw new EntityNotFoundException(entityName);
    }

    public IReadOnlyList<Field> GetFields(IEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (entity is not Entity metadataEntity)
            throw new EntityNotFoundException(entity.Name);

        var fields = metadataEntity.Fields
            .Where(field => Authorizer.CanReadField(entity.Name, field.Name))
            .ToArray();

        if (fields.Length == 0 && metadataEntity.Fields.Count != 0)
            throw new UnauthorizedAccessException(
                $"No fields are allowed in entity [{entity.Name}].");

        return fields;
    }

    public Field GetField(IEntity entity, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        if (entity is not Entity metadataEntity)
            throw new FieldNotFoundException(entity.Name, fieldName);

        var field = metadataEntity.GetField(fieldName);
        if (!Authorizer.CanReadField(entity.Name, field.Name))
            throw new UnauthorizedAccessException(
                $"Field [{field.Name}] is not allowed in entity [{entity.Name}].");

        return field;
    }

}
