using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace SqlSugar.IQueryableExtension;

/// <summary>
/// 自定义 <see cref="IQueryProvider"/>：负责创建子查询（CreateQuery）与执行终结操作（Execute）。
/// </summary>
public sealed class SugarQueryProvider : IQueryProvider
{
    IQueryable<TElement> IQueryProvider.CreateQuery<TElement>(Expression expression)
    {
        EnsureSugarEntityType<TElement>();
        var method = typeof(SugarQueryProvider)
            .GetMethod(nameof(CreateQueryCore), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(typeof(TElement));
        return (IQueryable<TElement>)method.Invoke(this, new object[] { expression })!;
    }

    public IQueryable CreateQuery(Expression expression)
    {
        var elementType = SugarQueryTranslator.GetElementType(expression.Type);
        var method = typeof(SugarQueryProvider)
            .GetMethod(nameof(CreateQueryCore), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(elementType);
        return (IQueryable)method.Invoke(this, new object[] { expression })!;
    }

    public TResult Execute<TResult>(Expression expression)
    {
        return (TResult)Execute(expression);
    }

    /// <summary>执行 Count / ToList / First 等终结操作。</summary>
    public object Execute(Expression expression)
    {
        return SugarQueryTranslator.Execute(expression);
    }

    /// <summary>将表达式树翻译为 SqlSugar 查询并包装为新的 <see cref="SugarQueryable{T}"/>。</summary>
    private IQueryable<TElement> CreateQueryCore<TElement>(Expression expression)
        where TElement : class, new()
    {
        var inner = SugarQueryTranslator.Translate(expression);
        var sugarInner = (ISugarQueryable<TElement>)inner;
        return new SugarQueryable<TElement>(sugarInner, expression, this);
    }

    /// <summary>SqlSugar 要求实体为引用类型且有无参构造函数。</summary>
    private static void EnsureSugarEntityType<T>()
    {
        if (!typeof(T).IsClass)
            throw new NotSupportedException($"类型 '{typeof(T).Name}' 必须是引用类型。");

        if (typeof(T).GetConstructor(Type.EmptyTypes) == null)
            throw new NotSupportedException($"类型 '{typeof(T).Name}' 必须提供无参构造函数。");
    }
}
