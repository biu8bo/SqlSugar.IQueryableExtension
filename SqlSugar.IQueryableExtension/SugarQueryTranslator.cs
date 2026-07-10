using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using SqlSugar.IQueryableExtension.Internal;

namespace SqlSugar.IQueryableExtension;

/// <summary>
/// 将 System.Linq 的 <see cref="MethodCallExpression"/> 链翻译为 SqlSugar 链式调用。
/// 只解析 LINQ 操作符外壳，Lambda 表达式体直接交给 SqlSugar 内置 ExpressionToSql 处理。
/// </summary>
internal static class SugarQueryTranslator
{
    private static readonly MethodInfo StringToObjectMethod =
        typeof(SugarQueryTranslator).GetMethod(nameof(ConvertSelector), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>将完整表达式树翻译为可执行的 <see cref="ISugarQueryable{T}"/>。</summary>
    public static object Translate(Expression expression)
    {
        return new Translator().Translate(expression);
    }

    /// <summary>执行终结操作（Count、First、ToList 等）。</summary>
    public static object Execute(Expression expression)
    {
        if (expression is MethodCallExpression terminal && IsTerminal(terminal))
        {
            var inner = Translate(terminal.Arguments[0]);
            return ExecuteTerminal(terminal, inner);
        }

        var query = Translate(expression);
        return InvokeToList(query);
    }

    /// <summary>
    /// 从类型定义或其实现的 <see cref="IQueryable{T}"/> / <see cref="IEnumerable{T}"/> 接口中解析元素类型。
    /// 用于识别 <c>EnumerableQuery&lt;T&gt;</c> 等具体包装类型。
    /// </summary>
    public static Type GetElementType(Type type)
    {
        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            if (definition == typeof(IQueryable<>) || definition == typeof(IEnumerable<>))
                return type.GetGenericArguments()[0];
        }

        foreach (var iface in type.GetInterfaces())
        {
            if (!iface.IsGenericType)
                continue;

            var ifaceDefinition = iface.GetGenericTypeDefinition();
            if (ifaceDefinition == typeof(IQueryable<>) || ifaceDefinition == typeof(IEnumerable<>))
                return iface.GetGenericArguments()[0];
        }

        return type;
    }

    private static readonly HashSet<string> TerminalMethods = new(StringComparer.Ordinal)
    {
        nameof(Queryable.Count),
        nameof(Queryable.LongCount),
        nameof(Queryable.Any),
        nameof(Queryable.First),
        nameof(Queryable.FirstOrDefault),
        nameof(Queryable.Single),
        nameof(Queryable.SingleOrDefault),
    };

    private static bool IsTerminal(MethodCallExpression expression)
    {
        return expression.Method.DeclaringType == typeof(Queryable)
               && TerminalMethods.Contains(expression.Method.Name);
    }

    private static object ExecuteTerminal(MethodCallExpression expression, object inner)
    {
        var name = expression.Method.Name;
        var sourceType = GetEntityTypeFromSugarQuery(inner);

        switch (name)
        {
            case nameof(Queryable.Count):
                return expression.Arguments.Count == 1
                    ? Invoke(inner, nameof(ISugarQueryable<int>.Count), Type.EmptyTypes)
                    : Invoke(inner, nameof(ISugarQueryable<int>.Count), sourceType, GetPredicate(expression, 1));

            case nameof(Queryable.LongCount):
                var count = expression.Arguments.Count == 1
                    ? (int)Invoke(inner, nameof(ISugarQueryable<int>.Count), Type.EmptyTypes)
                    : (int)Invoke(inner, nameof(ISugarQueryable<int>.Count), sourceType, GetPredicate(expression, 1));
                return (long)count;

            case nameof(Queryable.Any):
                return expression.Arguments.Count == 1
                    ? Invoke(inner, nameof(ISugarQueryable<int>.Any), Type.EmptyTypes)
                    : Invoke(inner, nameof(ISugarQueryable<int>.Any), sourceType, GetPredicate(expression, 1));

            case nameof(Queryable.First):
                return InvokeFirst(inner, sourceType, GetPredicateOrNull(expression), orDefault: false);

            case nameof(Queryable.FirstOrDefault):
                return InvokeFirst(inner, sourceType, GetPredicateOrNull(expression), orDefault: true);

            case nameof(Queryable.Single):
                return InvokeSingle(inner, sourceType, GetPredicateOrNull(expression), orDefault: false);

            case nameof(Queryable.SingleOrDefault):
                return InvokeSingle(inner, sourceType, GetPredicateOrNull(expression), orDefault: true);

            default:
                throw new NotSupportedException($"暂不支持的终结 LINQ 操作符：{name}。");
        }
    }

    private static LambdaExpression? GetPredicateOrNull(MethodCallExpression expression)
    {
        return expression.Arguments.Count > 1 ? GetPredicate(expression, 1) : null;
    }

    private static LambdaExpression GetPredicate(MethodCallExpression expression, int index)
    {
        return GetLambda(expression.Arguments[index]);
    }

    private static object InvokeToList(object inner)
    {
        var method = inner.GetType().GetMethod(nameof(ISugarQueryable<int>.ToList), Type.EmptyTypes)!;
        return method.Invoke(inner, null)!;
    }

    private static object InvokeFirst(object inner, Type entityType, LambdaExpression? predicate, bool orDefault)
    {
        if (!orDefault)
            return predicate == null
                ? Invoke(inner, "First", Type.EmptyTypes)
                : Invoke(inner, "First", entityType, predicate);

        var list = predicate == null
            ? (IList)InvokeToList(InvokeTake(inner, 1))
            : (IList)InvokeToList(InvokeTake(InvokeWhere(inner, entityType, predicate), 1));

        return list.Count == 0 ? null! : list[0]!;
    }

    private static object InvokeSingle(object inner, Type entityType, LambdaExpression? predicate, bool orDefault)
    {
        if (!orDefault)
            return predicate == null
                ? Invoke(inner, "Single", Type.EmptyTypes)
                : Invoke(inner, "Single", entityType, predicate);

        var list = predicate == null
            ? (IList)InvokeToList(inner)
            : (IList)InvokeToList(InvokeWhere(inner, entityType, predicate));

        return list.Count switch
        {
            0 => null!,
            1 => list[0]!,
            _ => throw new InvalidOperationException("序列包含多个元素。")
        };
    }

    private static object InvokeWhere(object source, Type sourceType, LambdaExpression predicate)
    {
        var normalized = SugarLambdaNormalizer.Normalize(predicate, sourceType);
        var predicateType = typeof(Expression<>).MakeGenericType(typeof(Func<,>).MakeGenericType(sourceType, typeof(bool)));
        var method = source.GetType().GetMethod(nameof(ISugarQueryable<int>.Where), new[] { predicateType })!;
        return method.Invoke(source, new object[] { normalized })!;
    }

    private static object InvokeTake(object source, int count)
    {
        var method = source.GetType().GetMethod(nameof(ISugarQueryable<int>.Take), new[] { typeof(int) })!;
        return method.Invoke(source, new object[] { count })!;
    }

    private static object Invoke(object inner, string methodName, Type entityType, LambdaExpression predicate)
    {
        var normalized = SugarLambdaNormalizer.Normalize(predicate, entityType);
        var methods = inner.GetType().GetMethods().Where(m => m.Name == methodName).ToArray();
        var predicateType = typeof(Expression<>).MakeGenericType(typeof(Func<,>).MakeGenericType(entityType, typeof(bool)));
        var method = methods.FirstOrDefault(m =>
        {
            var parameters = m.GetParameters();
            return parameters.Length == 1 && parameters[0].ParameterType == predicateType;
        });

        if (method == null)
            throw new NotSupportedException($"SqlSugar 未提供 {entityType.Name}.{methodName} 的表达式重载。");

        return method.Invoke(inner, new object[] { normalized })!;
    }

    private static object Invoke(object inner, string methodName, Type[] typeArguments, params object[] args)
    {
        var method = inner.GetType().GetMethod(methodName, typeArguments)!;
        return method.Invoke(inner, args)!;
    }

    private static Type GetEntityTypeFromSugarQuery(object source)
    {
        foreach (var iface in source.GetType().GetInterfaces())
        {
            if (iface.IsGenericType && iface.Name.StartsWith("ISugarQueryable", StringComparison.Ordinal))
                return iface.GetGenericArguments()[0];
        }

        throw new NotSupportedException($"类型 '{source.GetType().Name}' 不是 SqlSugar 查询对象。");
    }

    /// <summary>将 OrderBy 的键选择器转换为 SqlSugar 需要的 Expression&lt;Func&lt;T, object&gt;&gt;。</summary>
    private static Expression<Func<T, object>> ConvertSelector<T, TKey>(Expression<Func<T, TKey>> selector)
    {
        var parameter = selector.Parameters[0];
        var body = Expression.Convert(selector.Body, typeof(object));
        return Expression.Lambda<Func<T, object>>(body, parameter);
    }

    /// <summary>单表 Select 投影，使用 SelectMergeTable 以便后续可对投影 DTO 继续 Where/OrderBy。</summary>
    private static object InvokeSelectGeneric<TSource, TResult>(object source, LambdaExpression selector)
        where TSource : class, new()
    {
        var typed = (ISugarQueryable<TSource>)source;
        var typedSelector = (Expression<Func<TSource, TResult>>)selector;
        return typed.SelectMergeTable(typedSelector);
    }

    /// <summary>LINQ Join → SqlSugar InnerJoin + Select + MergeTable。</summary>
    private static object InvokeJoinGeneric<TOuter, TInner, TKey, TResult>(
        Translator translator,
        MethodCallExpression expression)
        where TOuter : class, new()
        where TInner : class, new()
    {
        var outer = (ISugarQueryable<TOuter>)translator.Visit(expression.Arguments[0]);
        var inner = (ISugarQueryable<TInner>)translator.Visit(expression.Arguments[1]);
        var outerKey = GetLambda(expression.Arguments[2]);
        var innerKey = GetLambda(expression.Arguments[3]);
        var resultSelector = GetLambda(expression.Arguments[4]);

        var joinExpression = BuildJoinExpression<TOuter, TInner, TKey>(outerKey, innerKey, resultSelector);
        var joined = outer.InnerJoin(inner, joinExpression);

        // MergeTable 将多表结果集折叠为单表，后续可对投影 DTO 继续 LINQ 操作
        return joined.Select((Expression<Func<TOuter, TInner, TResult>>)resultSelector).MergeTable();
    }

    /// <summary>根据 Join 结果选择器的参数名，构造与 SqlSugar 别名一致的关联条件。</summary>
    private static Expression<Func<TOuter, TInner, bool>> BuildJoinExpression<TOuter, TInner, TKey>(
        LambdaExpression outerKeySelector,
        LambdaExpression innerKeySelector,
        LambdaExpression resultSelector)
    {
        var outerParameter = Expression.Parameter(typeof(TOuter), resultSelector.Parameters[0].Name);
        var innerParameter = Expression.Parameter(typeof(TInner), resultSelector.Parameters[1].Name);

        var outerBody = new ParameterReplacer(outerKeySelector.Parameters[0], outerParameter)
            .Visit(outerKeySelector.Body);
        var innerBody = new ParameterReplacer(innerKeySelector.Parameters[0], innerParameter)
            .Visit(innerKeySelector.Body);

        var body = Expression.Equal(outerBody!, innerBody!);
        return Expression.Lambda<Func<TOuter, TInner, bool>>(body, outerParameter, innerParameter);
    }

    private sealed class Translator
    {
        public object Translate(Expression expression) => Visit(expression);

        internal object Visit(Expression expression)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.Constant:
                    return VisitConstant((ConstantExpression)expression);
                case ExpressionType.Call:
                    return VisitMethodCall((MethodCallExpression)expression);
                default:
                    throw new NotSupportedException($"不支持的表达式节点：{expression.NodeType}。");
            }
        }

        private static object VisitConstant(ConstantExpression expression)
        {
            if (expression.Value is ISugarQueryableSource source)
                return CloneSugarQuery(source.Inner);

            if (expression.Value is IQueryable queryable && queryable is ISugarQueryableSource sugarSource)
                return CloneSugarQuery(sugarSource.Inner);

            throw new NotSupportedException("查询根节点必须通过 AsLinqQueryable() 创建。");
        }

        /// <summary>SqlSugar 的 Join/Where 等操作会修改原实例，翻译前必须克隆。</summary>
        private static object CloneSugarQuery(object inner)
        {
            var cloneMethod = inner.GetType().GetMethod(nameof(ISugarQueryable<int>.Clone), Type.EmptyTypes)!;
            return cloneMethod.Invoke(inner, null)!;
        }

        private object VisitMethodCall(MethodCallExpression expression)
        {
            var source = Visit(expression.Arguments[0]);
            var sourceType = GetSugarEntityType(source);
            var name = expression.Method.Name;

            switch (name)
            {
                case nameof(Queryable.Where):
                    return InvokeWhere(source, sourceType, GetLambda(expression.Arguments[1]));

                case nameof(Queryable.Select):
                    return InvokeSelect(source, sourceType, expression.Type, GetLambda(expression.Arguments[1]));

                case nameof(Queryable.OrderBy):
                    return InvokeOrderBy(source, sourceType, GetLambda(expression.Arguments[1]), ascending: true);

                case nameof(Queryable.OrderByDescending):
                    return InvokeOrderBy(source, sourceType, GetLambda(expression.Arguments[1]), ascending: false);

                case nameof(Queryable.ThenBy):
                    return InvokeOrderBy(source, sourceType, GetLambda(expression.Arguments[1]), ascending: true);

                case nameof(Queryable.ThenByDescending):
                    return InvokeOrderBy(source, sourceType, GetLambda(expression.Arguments[1]), ascending: false);

                case nameof(Queryable.Skip):
                    return InvokeSkip(source, (int)expression.Arguments[1].GetConstantValue());

                case nameof(Queryable.Take):
                    return InvokeTake(source, (int)expression.Arguments[1].GetConstantValue());

                case nameof(Queryable.Distinct):
                    return InvokeDistinct(source);

                case nameof(Queryable.Join):
                    return InvokeJoin(expression);

                case nameof(Queryable.AsQueryable):
                    return source;

                default:
                    throw new NotSupportedException($"暂不支持的 LINQ 操作符：{name}。");
            }
        }

        private static Type GetSugarEntityType(object source) => GetEntityTypeFromSugarQuery(source);

        private object InvokeJoin(MethodCallExpression expression)
        {
            var genericArguments = expression.Method.GetGenericArguments();
            var method = typeof(SugarQueryTranslator)
                .GetMethod(nameof(InvokeJoinGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(
                    genericArguments[0],
                    genericArguments[1],
                    genericArguments[2],
                    genericArguments[3]);
            return method.Invoke(null, new object[] { this, expression })!;
        }

        private static object InvokeWhere(object source, Type sourceType, LambdaExpression predicate)
        {
            return SugarQueryTranslator.InvokeWhere(source, sourceType, predicate);
        }

        private static object InvokeSelect(object source, Type sourceType, Type resultQueryableType, LambdaExpression selector)
        {
            var resultType = resultQueryableType.GetGenericArguments()[0];
            var method = typeof(SugarQueryTranslator)
                .GetMethod(nameof(InvokeSelectGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(sourceType, resultType);
            return method.Invoke(null, new object[] { source, selector })!;
        }

        private static object InvokeOrderBy(object source, Type sourceType, LambdaExpression keySelector, bool ascending)
        {
            var normalized = SugarLambdaNormalizer.Normalize(keySelector, sourceType);
            var keyType = normalized.ReturnType;
            var convertMethod = StringToObjectMethod.MakeGenericMethod(sourceType, keyType);
            var objectSelector = (LambdaExpression)convertMethod.Invoke(null, new object[] { normalized })!;

            var expressionFuncType = typeof(Expression<>).MakeGenericType(
                typeof(Func<,>).MakeGenericType(sourceType, typeof(object)));

            if (ascending)
            {
                var method = FindMethod(
                    source,
                    nameof(ISugarQueryable<int>.OrderBy),
                    expressionFuncType,
                    typeof(OrderByType));
                return method.Invoke(source, new object[] { objectSelector, OrderByType.Asc })!;
            }

            var descendingMethod = FindMethod(
                source,
                nameof(ISugarQueryable<int>.OrderByDescending),
                expressionFuncType);
            return descendingMethod.Invoke(source, new object[] { objectSelector })!;
        }

        private static MethodInfo FindMethod(object source, string name, params Type[] parameterTypes)
        {
            var method = source.GetType().GetMethods()
                .FirstOrDefault(m =>
                {
                    if (m.Name != name)
                        return false;

                    var parameters = m.GetParameters();
                    if (parameters.Length != parameterTypes.Length)
                        return false;

                    for (var i = 0; i < parameters.Length; i++)
                    {
                        if (parameters[i].ParameterType != parameterTypes[i])
                            return false;
                    }

                    return true;
                });

            if (method == null)
            {
                var signature = string.Join(", ", parameterTypes.Select(t => t.Name));
                throw new NotSupportedException(
                    $"SqlSugar 查询类型 '{source.GetType().Name}' 未提供 {name}({signature})。");
            }

            return method;
        }

        private static object InvokeSkip(object source, int count)
        {
            var method = source.GetType().GetMethod(nameof(ISugarQueryable<int>.Skip), new[] { typeof(int) })!;
            return method.Invoke(source, new object[] { count })!;
        }

        private static object InvokeTake(object source, int count)
        {
            return SugarQueryTranslator.InvokeTake(source, count);
        }

        private static object InvokeDistinct(object source)
        {
            var method = source.GetType().GetMethod(nameof(ISugarQueryable<int>.Distinct), Type.EmptyTypes)!;
            return method.Invoke(source, null)!;
        }
    }

    private static LambdaExpression GetLambda(Expression expression)
    {
        if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Quote)
            return (LambdaExpression)unary.Operand;

        return (LambdaExpression)expression;
    }

    private static object GetConstantValue(this Expression expression)
    {
        if (expression is ConstantExpression constant)
            return constant.Value!;

        var lambda = Expression.Lambda(expression);
        return lambda.Compile().DynamicInvoke()!;
    }
}
