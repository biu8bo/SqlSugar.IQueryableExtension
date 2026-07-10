using System;
using System.Linq.Expressions;

namespace SqlSugar.IQueryableExtension.Internal;

/// <summary>
/// 统一 LINQ Lambda 参数名，避免 SqlSugar 在投影/合并表后出现别名不一致。
/// </summary>
internal static class SugarLambdaNormalizer
{
    /// <summary>
    /// SqlSugar 单表及 MergeTable 后推荐使用的参数别名。
    /// </summary>
    public const string DefaultParameterName = "it";

    /// <summary>
    /// 将单参数 Lambda 的参数名规范为 <see cref="DefaultParameterName"/>。
    /// </summary>
    public static LambdaExpression Normalize(LambdaExpression lambda, Type entityType)
    {
        if (lambda.Parameters.Count != 1)
            return lambda;

        var parameter = lambda.Parameters[0];
        if (parameter.Type == entityType && parameter.Name == DefaultParameterName)
            return lambda;

        var normalizedParameter = Expression.Parameter(entityType, DefaultParameterName);
        var body = new ParameterReplacer(parameter, normalizedParameter).Visit(lambda.Body);
        return Expression.Lambda(body!, normalizedParameter);
    }
}
