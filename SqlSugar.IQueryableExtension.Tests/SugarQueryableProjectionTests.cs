using SqlSugar.IQueryableExtension.Tests.Infrastructure;
using SqlSugar.IQueryableExtension.Tests.Models;

namespace SqlSugar.IQueryableExtension.Tests;

/// <summary>
/// 投影（Select）相关测试：单表投影、联表投影、投影后继续 Where/OrderBy/Count。
/// 依赖 SelectMergeTable（单表）和 MergeTable（联表）将结果折叠为单表后再操作。
/// </summary>
[Collection(nameof(SqliteCollection))]
public class SugarQueryableProjectionTests
{
    private readonly SqliteTestDatabase _database;

    public SugarQueryableProjectionTests(SqliteTestDatabase database)
    {
        _database = database;
    }

    /// <summary>单表投影后对 DTO 字段 Where + OrderBy。</summary>
    [Fact]
    public void Select_Then_Where_On_Projection()
    {
        var query = _database.Db.Queryable<Order>().AsLinqQueryable()
            .Select(o => new OrderAmountDto
            {
                OrderId = o.Id,
                Amount = o.Amount
            })
            .Where(x => x.Amount >= 100m)
            .OrderBy(x => x.OrderId);

        var result = query.ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, x => Assert.True(x.Amount >= 100m));
    }

    /// <summary>投影后对 DTO 执行 Count 终结操作。</summary>
    [Fact]
    public void Select_Then_Count_On_Projection()
    {
        var query = _database.Db.Queryable<Order>().AsLinqQueryable()
            .Select(o => new OrderAmountDto
            {
                OrderId = o.Id,
                Amount = o.Amount
            })
            .Where(x => x.Amount < 100m);

        // Amount < 100：订单 2(50)、4(75)、5(10) 共 3 条
        Assert.Equal(3, query.Count());
    }

    /// <summary>联表投影后对 DTO 继续 Where + OrderBy（MergeTable 后的单表操作）。</summary>
    [Fact]
    public void Join_Then_Where_On_Projection()
    {
        var orders = _database.Db.Queryable<Order>().AsLinqQueryable();
        var customers = _database.Db.Queryable<Customer>().AsLinqQueryable();

        var query = orders.Join(
                customers,
                o => o.CustomerId,
                c => c.Id,
                (o, c) => new OrderCustomerDto
                {
                    OrderId = o.Id,
                    CustomerName = c.Name,
                    Amount = o.Amount
                })
            .Where(x => x.Amount >= 100m)
            .OrderBy(x => x.OrderId);

        var result = query.ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { 1, 3 }, result.Select(x => x.OrderId));
    }

    /// <summary>联表投影后再次 Select 收窄字段（DTO → DTO）。</summary>
    [Fact]
    public void Join_Then_Select_Again_On_Projection()
    {
        var orders = _database.Db.Queryable<Order>().AsLinqQueryable();
        var customers = _database.Db.Queryable<Customer>().AsLinqQueryable();

        var query = orders.Join(
                customers,
                o => o.CustomerId,
                c => c.Id,
                (o, c) => new OrderCustomerDto
                {
                    OrderId = o.Id,
                    CustomerName = c.Name,
                    Amount = o.Amount
                })
            .Where(x => x.CustomerName == "Alice")
            .Select(x => new OrderAmountDto
            {
                OrderId = x.OrderId,
                Amount = x.Amount
            });

        var result = query.ToList();

        // Alice 有两笔订单：100 和 50
        Assert.Equal(2, result.Count);
        Assert.All(result, x => Assert.True(x.Amount is 100m or 50m));
    }

    /// <summary>验证投影查询生成的 SQL 包含投影字段名。</summary>
    [Fact]
    public void Projection_Generates_Expected_Sql()
    {
        var query = _database.Db.Queryable<Order>().AsLinqQueryable()
            .Select(o => new OrderAmountDto
            {
                OrderId = o.Id,
                Amount = o.Amount
            })
            .Where(x => x.Amount > 0);

        var sql = query.AsSugarQueryable().ToSqlString();

        Assert.Contains("Amount", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Id", sql, StringComparison.OrdinalIgnoreCase);
    }
}
