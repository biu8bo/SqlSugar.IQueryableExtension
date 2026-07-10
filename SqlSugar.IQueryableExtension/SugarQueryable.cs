using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using SqlSugar.IQueryableExtension.Internal;

namespace SqlSugar.IQueryableExtension;

/// <summary>
/// 将 <see cref="ISugarQueryable{T}"/> 包装为标准 <see cref="IQueryable{T}"/>，
/// 使第三方 LINQ 组件可以通过表达式树与 SqlSugar 协同工作。
/// </summary>
public sealed class SugarQueryable<T> : IQueryable<T>, ISugarQueryableSource, IOrderedQueryable<T>
    where T : class, new()
{
    /// <summary>底层 SqlSugar 查询对象，所有 SQL 最终由此生成。</summary>
    internal ISugarQueryable<T> Inner { get; }

    object ISugarQueryableSource.Inner => Inner;

    Type ISugarQueryableSource.ElementType => typeof(T);

    public Type ElementType => typeof(T);

    /// <summary>累积的 LINQ 表达式树，由 <see cref="SugarQueryProvider"/> 解析并翻译。</summary>
    public Expression Expression { get; }

    public IQueryProvider Provider { get; }

    /// <summary>创建查询根节点。</summary>
    internal SugarQueryable(ISugarQueryable<T> inner, SugarQueryProvider provider)
    {
        Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Expression = Expression.Constant(this);
    }

    /// <summary>由 Provider 在链式调用时创建的子查询节点。</summary>
    internal SugarQueryable(ISugarQueryable<T> inner, Expression expression, SugarQueryProvider provider)
    {
        Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }

    /// <summary>枚举时执行 SqlSugar 查询并返回结果。</summary>
    public IEnumerator<T> GetEnumerator()
    {
        return Inner.ToList().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
