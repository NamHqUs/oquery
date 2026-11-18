#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130
public abstract class MetadataNotFoundException(string message) : Exception(message)
{
}

public class EntityNotFoundException(string entityName) 
    : MetadataNotFoundException($"Entity [{entityName}] was not found.")
{
    public string EntityName { get; } = entityName;
}

public class FieldNotFoundException(string entityName, string fieldName) 
    : MetadataNotFoundException($"Field [{fieldName}] was not found in entity [{entityName}].")
{
    public string EntityName { get; } = entityName;
    public string FieldName { get; } = fieldName;
}

public class RelationNotFoundException(string entityName, string relationName) 
    : MetadataNotFoundException($"Relation [{relationName}] was not found in entity [{entityName}].")
{
    public string EntityName { get; } = entityName;
    public string RelationName { get; } = relationName;
}
