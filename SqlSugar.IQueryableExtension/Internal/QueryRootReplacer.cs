using System;
using System.Linq;
using System.Linq.Expressions;

namespace SqlSugar.IQueryableExtension.Internal;

/// <summary>
/// 将 LINQ 表达式树的查询根节点替换为 SqlSugar 适配器根，以便翻译第三方构建的 IQueryable。
/// </summary>
internal static class QueryRootReplacer
{
    /// <summary>
    /// 自外向内遍历 <see cref="MethodCallExpression"/> 链，将最内层数据源节点替换为 <paramref name="newRoot"/>。
    /// </summary>
    /// <param name="expression">第三方 IQueryable 的完整表达式树。</param>
    /// <param name="newRoot">替换后的 SqlSugar 适配器根节点（通常为 <see cref="SugarQueryable{T}"/> 常量）。</param>
    public static Expression Replace(Expression expression, Expression newRoot)
    {
        if (IsQueryOperator(expression))
        {
            var methodCall = (MethodCallExpression)expression;
            var arguments = methodCall.Arguments.ToArray();
            arguments[0] = Replace(methodCall.Arguments[0], newRoot);
            return methodCall.Update(null, arguments);
        }

        return newRoot;
    }

    /// <summary>
    /// 沿表达式链向内回溯，解析最内层数据源的元素类型（如 <c>EnumerableQuery&lt;Order&gt;</c> → <c>Order</c>）。
    /// </summary>
    internal static Type GetSourceElementType(Expression expression)
    {
        while (IsQueryOperator(expression))
            expression = ((MethodCallExpression)expression).Arguments[0];

        return SugarQueryTranslator.GetElementType(expression.Type);
    }

    /// <summary>
    /// 判断节点是否为 LINQ 查询操作符（<see cref="Queryable"/> 或部分 <see cref="Enumerable"/> 扩展）。
    /// </summary>
    private static bool IsQueryOperator(Expression expression)
    {
        if (expression is not MethodCallExpression methodCall)
            return false;

        if (methodCall.Method.DeclaringType == typeof(Queryable))
            return true;

        // 少数场景下 IQueryable 表达式链会混入 Enumerable 操作符
        return methodCall.Method.DeclaringType == typeof(Enumerable)
               && methodCall.Arguments[0].Type != typeof(string);
    }
}
