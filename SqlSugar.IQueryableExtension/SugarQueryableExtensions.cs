using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using SqlSugar.IQueryableExtension.Internal;

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
    /// 从 <see cref="IQueryable{T}"/> 取回底层 SqlSugar 查询。
    /// 适用于本库 <see cref="AsLinqQueryable{T}"/> 创建、或使用 <see cref="SugarQueryProvider"/> 的查询。
    /// </summary>
    /// <exception cref="InvalidOperationException">查询来源无法识别时抛出，需改用 <see cref="AsSugarQueryable{T}(IQueryable{T}, ISqlSugarClient)"/>。</exception>
    public static ISugarQueryable<T> AsSugarQueryable<T>(this IQueryable<T> query)
        where T : class, new()
    {
        if (TryGetExistingSugarQuery(query, out ISugarQueryable<T>? sugarQuery))
            return sugarQuery;

        throw new InvalidOperationException(
            "无法识别该 IQueryable 的来源。请使用 AsLinqQueryable() 创建查询，" +
            "或调用 AsSugarQueryable(db) 并传入 SqlSugar 客户端以指定数据源。");
    }

    /// <summary>
    /// 将任意标准 LINQ <see cref="IQueryable{T}"/> 翻译为 SqlSugar 查询。
    /// 通过替换表达式树根节点为 <c>db.Queryable&lt;TSource&gt;()</c> 后重放 LINQ 操作链。
    /// </summary>
    /// <param name="query">第三方库构建的 IQueryable（如动态筛选组件返回的查询）。</param>
    /// <param name="db">SqlSugar 客户端，提供数据库查询根节点。</param>
    /// <typeparam name="T">当前 IQueryable 的元素类型（投影后可为 DTO）。</typeparam>
    /// <remarks>
    /// 仅支持本库 <see cref="SugarQueryTranslator"/> 已实现的 LINQ 操作符。
    /// 内存集合（<c>list.AsQueryable()</c>）的表达式可被翻译，但执行时将转为 SQL 而非内存过滤。
    /// </remarks>
    public static ISugarQueryable<T> AsSugarQueryable<T>(this IQueryable<T> query, ISqlSugarClient db)
        where T : class, new()
    {
        if (query == null) throw new ArgumentNullException(nameof(query));
        if (db == null) throw new ArgumentNullException(nameof(db));

        if (TryGetExistingSugarQuery(query, out ISugarQueryable<T>? sugarQuery))
            return sugarQuery;

        var sourceType = QueryRootReplacer.GetSourceElementType(query.Expression);
        var seedQueryable = CreateSeedQueryable(db, sourceType);
        var newRoot = Expression.Constant(seedQueryable, seedQueryable.GetType());
        var translatedExpression = QueryRootReplacer.Replace(query.Expression, newRoot);

        return (ISugarQueryable<T>)SugarQueryTranslator.Translate(translatedExpression);
    }

    /// <summary>
    /// 从任意 <see cref="IQueryable{T}"/> 构建 SqlSugar 查询（<see cref="AsSugarQueryable{T}(IQueryable{T}, ISqlSugarClient)"/> 的别名）。
    /// </summary>
    /// <param name="db">SqlSugar 客户端，提供数据库查询根节点。</param>
    /// <param name="query">待翻译的标准 LINQ 查询。</param>
    public static ISugarQueryable<T> ToSugarQueryable<T>(this ISqlSugarClient db, IQueryable<T> query)
        where T : class, new()
    {
        return query.AsSugarQueryable(db);
    }

    /// <summary>
    /// 尝试从本库适配的 IQueryable 中直接提取 SqlSugar 查询，避免重复翻译。
    /// </summary>
    private static bool TryGetExistingSugarQuery<T>(IQueryable<T> query, out ISugarQueryable<T> sugarQuery)
        where T : class, new()
    {
        if (query is SugarQueryable<T> sugarQueryable)
        {
            sugarQuery = sugarQueryable.Inner;
            return true;
        }

        if (query.Provider is SugarQueryProvider)
        {
            sugarQuery = (ISugarQueryable<T>)SugarQueryTranslator.Translate(query.Expression);
            return true;
        }

        sugarQuery = null!;
        return false;
    }

    /// <summary>
    /// 按源实体类型反射创建 <see cref="SugarQueryable{TEntity}"/> 种子节点，作为表达式树替换根。
    /// </summary>
    private static object CreateSeedQueryable(ISqlSugarClient db, Type sourceType)
    {
        var method = typeof(SugarQueryableExtensions)
            .GetMethod(nameof(CreateSeedQueryableGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(sourceType);
        return method.Invoke(null, new object[] { db })!;
    }

    /// <summary>创建 <c>db.Queryable&lt;TEntity&gt;().AsLinqQueryable()</c> 对应的表达式树常量根。</summary>
    private static SugarQueryable<TEntity> CreateSeedQueryableGeneric<TEntity>(ISqlSugarClient db)
        where TEntity : class, new()
    {
        var provider = new SugarQueryProvider();
        return new SugarQueryable<TEntity>(db.Queryable<TEntity>(), provider);
    }
}
