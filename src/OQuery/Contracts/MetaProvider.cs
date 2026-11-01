#pragma warning disable IDE0130
namespace OQuery.Contracts;
#pragma warning restore IDE0130

public interface IMetadataProvider
{
    Entity GetEntity(string entityName);
}

public class MetadataProvider : IMetadataProvider
{
    private readonly Dictionary<string, Entity> _entities;

    public MetadataProvider(IEnumerable<Entity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);

        _entities = entities.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
    }

    public Entity GetEntity(string entityName)
    {
        if (_entities.TryGetValue(entityName, out Entity? entity))
            return entity;

        throw new EntityNotFoundException(entityName);
    }

}

public sealed class MetaProvider : MetadataProvider, IMetaProvider
{
    public MetaProvider(IEnumerable<Entity> entities)
        : base(entities)
    {
    }
}

public interface IMetaProvider : IMetadataProvider
{
}
