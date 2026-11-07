using OQuery.Compilation.Expressions;

namespace OQuery.Compilation.Models;

class ProjectionItem(SqlExpression item, string alias, bool isHidden = false)
{
    public readonly string Alias = alias;
    public readonly SqlExpression Item = item;
    public readonly bool IsHidden = isHidden;

    public ProjectionItem(FieldExpression fieldExpression)
        : this(fieldExpression, fieldExpression.MetaField.Name)
    {
    }

    public ProjectionItem(string tableAlias, Field field)
        : this(new FieldExpression(tableAlias, field))
    {
    }

    public override string ToString() => Alias;
}
