using System.Linq.Expressions;

namespace Cinema.Data.Stores;

internal sealed class ParameterRebinder : ExpressionVisitor
{
    private readonly ParameterExpression _parameter;
    private readonly Expression _replacement;

    public ParameterRebinder(ParameterExpression parameter, Expression replacement)
    {
        _parameter = parameter;
        _replacement = replacement;
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        return node == _parameter ? _replacement : base.VisitParameter(node);
    }
}
