using System.Linq.Expressions;

namespace SqlSugar.IQueryableExtension.Internal;

/// <summary>
/// 替换表达式树中的参数节点，用于拼接 Join 条件或规范 Lambda 参数名。
/// </summary>
internal sealed class ParameterReplacer : ExpressionVisitor
{
    private readonly ParameterExpression _from;
    private readonly ParameterExpression _to;

    public ParameterReplacer(ParameterExpression from, ParameterExpression to)
    {
        _from = from;
        _to = to;
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        return node == _from ? _to : base.VisitParameter(node);
    }
}
