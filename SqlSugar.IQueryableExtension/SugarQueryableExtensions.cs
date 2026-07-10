using System;
using System.Linq;

namespace SqlSugar.IQueryableExtension;

/// <summary>
/// SqlSugar 与 System.Linq 之间的桥接扩展方法。
/// </summary>
public static class SugarQueryableExtensions
{
    /// <summary>
    /// 将 SqlSugar 查询适配为 <see cref="IQueryable{T}"/>，供 LINQ 生态（动态查询、筛选组件等）使用。
    /// </summary>
    /// <example>
    /// <code>
    /// IQueryable&lt;Order&gt; query = db.Queryable&lt;Order&gt;().AsLinqQueryable();
    /// query = query.Where(x => x.Amount > 100).OrderBy(x => x.Id);
    /// </code>
    /// </example>
    public static IQueryable<T> AsLinqQueryable<T>(this ISugarQueryable<T> query)
        where T : class, new()
    {
        var provider = new SugarQueryProvider();
        return new SugarQueryable<T>(query, provider);
    }

    /// <summary>
    /// 从适配后的 <see cref="IQueryable{T}"/> 取回底层 SqlSugar 查询，以便使用 Includes、ToSqlString 等原生 API。
    /// </summary>
    public static ISugarQueryable<T> AsSugarQueryable<T>(this IQueryable<T> query)
        where T : class, new()
    {
        if (query is SugarQueryable<T> sugarQueryable)
            return sugarQueryable.Inner;

        throw new InvalidOperationException(
            "当前 IQueryable 不是由 SqlSugar.IQueryableExtension 创建，请先调用 AsLinqQueryable()。");
    }
}
