using System.Linq.Expressions;
using SqlSugar.IQueryableExtension.Tests.Infrastructure;
using SqlSugar.IQueryableExtension.Tests.Models;

namespace SqlSugar.IQueryableExtension.Tests;

/// <summary>
/// 验证 IQueryable → ISugarQueryable 的反向转换。
/// 覆盖本库适配查询、第三方包装器，以及需传入 <see cref="ISqlSugarClient"/> 的外部 LINQ 表达式树。
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

    /// <summary>模拟第三方库包装 IQueryable，仅转发 Expression/Provider。</summary>
    [Fact]
    public void AsSugarQueryable_Works_With_SugarQueryProvider_Wrapper()
    {
        var inner = _database.Db.Queryable<Order>().AsLinqQueryable()
            .Where(o => o.Status == "Paid");

        IQueryable<Order> wrapped = new ForwardingQueryable<Order>(inner);

        var sugar = wrapped.AsSugarQueryable();

        Assert.Equal(3, sugar.Count());
    }

    /// <summary>将内存 IQueryable 的表达式树翻译为 SqlSugar 查询并执行。</summary>
    [Fact]
    public void AsSugarQueryable_With_Db_Translates_External_Linq_Query()
    {
        // 模拟第三方组件在内存 IQueryable 上构建的条件
        IQueryable<Order> external = new List<Order>
        {
            new() { Id = 1, Status = "Paid", Amount = 1, CustomerId = 1 }
        }.AsQueryable().Where(o => o.Status == "Paid");

        var sugar = external.AsSugarQueryable(_database.Db);

        // 实际查询数据库，而非内存列表
        Assert.Equal(3, sugar.Count());
    }

    /// <summary>外部 IQueryable 含 Select 投影时，可翻译为 SqlSugar 投影查询。</summary>
    [Fact]
    public void AsSugarQueryable_With_Db_Translates_Projection_Query()
    {
        IQueryable<OrderAmountDto> external = new List<Order>
        {
            new() { Id = 1, Amount = 100m, Status = "Paid", CustomerId = 1 }
        }.AsQueryable()
            .Where(o => o.Status == "Paid")
            .Select(o => new OrderAmountDto { OrderId = o.Id, Amount = o.Amount });

        var sugar = external.AsSugarQueryable(_database.Db);

        Assert.Equal(3, sugar.Count());
    }

    /// <summary>db.ToSugarQueryable 扩展方法等价于 query.AsSugarQueryable(db)。</summary>
    [Fact]
    public void ToSugarQueryable_On_Db_Works()
    {
        IQueryable<Order> external = new List<Order>().AsQueryable()
            .Where(o => o.Amount >= 100m);

        var sugar = _database.Db.ToSugarQueryable(external);

        Assert.Equal(2, sugar.Count());
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
