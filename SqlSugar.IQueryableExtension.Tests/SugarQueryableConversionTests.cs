using System.Linq.Expressions;
using SqlSugar.IQueryableExtension.Tests.Infrastructure;
using SqlSugar.IQueryableExtension.Tests.Models;

namespace SqlSugar.IQueryableExtension.Tests;

/// <summary>
/// 验证 README「普通 IQueryable 转 ISugarQueryable」章节的四种写法。
/// 使用 SQLite 内存库，测试数据见 <see cref="SqliteTestDatabase"/>。
/// </summary>
[Collection(nameof(SqliteCollection))]
public class SugarQueryableConversionTests
{
    private readonly SqliteTestDatabase _database;

    /// <summary>注入共享 SQLite 测试库。</summary>
    public SugarQueryableConversionTests(SqliteTestDatabase database)
    {
        _database = database;
    }

    /// <summary>README 示例 1：内存集合上的 LINQ 条件。</summary>
    [Fact]
    public void Readme_Example1_PlainIQueryable_With_Where_OrderBy()
    {
        // 普通 IQueryable：在空列表上叠加 Where / OrderBy（仅构建表达式树）
        IQueryable<Order> query = new List<Order>().AsQueryable()
            .Where(o => o.Status == "Paid")
            .OrderBy(o => o.Amount);

        // 转为 ISugarQueryable，在数据库执行
        ISugarQueryable<Order> sugar = query.AsSugarQueryable(_database.Db);

        var list = sugar.ToList();
        var sql = sugar.ToSqlString();

        Assert.Equal(3, list.Count);
        Assert.Equal(new[] { 50m, 75m, 100m }, list.Select(o => o.Amount));
        Assert.Contains("orders", sql, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>README 示例 2：第三方组件返回的 IQueryable。</summary>
    [Fact]
    public void Readme_Example2_ThirdParty_FilterService()
    {
        var filterService = new StubFilterService();

        // 筛选组件通常在占位 IQueryable 上追加条件
        IQueryable<Order> filtered = filterService
            .Apply(new List<Order>().AsQueryable())
            .Where(o => o.Amount >= 100m);

        // 交给 SqlSugar 执行
        var orders = filtered.AsSugarQueryable(_database.Db).ToList();

        Assert.Equal(2, orders.Count);
        Assert.All(orders, o => Assert.True(o.Amount >= 100m));
    }

    /// <summary>README 示例 3：含 Select 投影的普通 IQueryable。</summary>
    [Fact]
    public void Readme_Example3_PlainIQueryable_With_Projection()
    {
        IQueryable<OrderAmountDto> projected = new List<Order>().AsQueryable()
            .Where(o => o.Status == "Paid")
            .Select(o => new OrderAmountDto
            {
                OrderId = o.Id,
                Amount = o.Amount
            });

        // 投影后的 IQueryable 同样可转换
        ISugarQueryable<OrderAmountDto> sugar = projected.AsSugarQueryable(_database.Db);
        var dtos = sugar.OrderBy(x => x.Amount).ToList();

        Assert.Equal(3, dtos.Count);
        Assert.Equal(new[] { 50m, 75m, 100m }, dtos.Select(x => x.Amount));
    }

    /// <summary>README 示例 4：从 db 侧调用（等价写法）。</summary>
    [Fact]
    public void Readme_Example4_Db_ToSugarQueryable()
    {
        IQueryable<Order> query = BuildQueryFromThirdParty();

        ISugarQueryable<Order> sugar = _database.Db.ToSugarQueryable(query);
        var count = sugar.Count();

        Assert.Equal(2, count);
    }

    /// <summary>本库 IQueryable 经第三方包装后，无参 AsSugarQueryable 仍可取回底层查询。</summary>
    [Fact]
    public void AsSugarQueryable_Works_With_SugarQueryProvider_Wrapper()
    {
        var inner = _database.Db.Queryable<Order>().AsLinqQueryable()
            .Where(o => o.Status == "Paid");

        IQueryable<Order> wrapped = new ForwardingQueryable<Order>(inner);

        var sugar = wrapped.AsSugarQueryable();

        Assert.Equal(3, sugar.Count());
    }

    /// <summary>模拟 README 示例 4 中任意来源的普通 IQueryable。</summary>
    private static IQueryable<Order> BuildQueryFromThirdParty()
    {
        return new List<Order>().AsQueryable()
            .Where(o => o.Amount >= 100m);
    }

    /// <summary>转发包装器：仅暴露 Expression 与 Provider，模拟第三方库行为。</summary>
    private sealed class ForwardingQueryable<T> : IQueryable<T>
    {
        /// <summary>从内层查询复制表达式树与 Provider。</summary>
        public ForwardingQueryable(IQueryable<T> inner)
        {
            Expression = inner.Expression;
            Provider = inner.Provider;
        }

        public Type ElementType => typeof(T);
        public Expression Expression { get; }
        public IQueryProvider Provider { get; }

        public IEnumerator<T> GetEnumerator() => Provider.CreateQuery<T>(Expression).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
