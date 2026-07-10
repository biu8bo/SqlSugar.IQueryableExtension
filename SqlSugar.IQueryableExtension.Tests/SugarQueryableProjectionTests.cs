using SqlSugar.IQueryableExtension.Tests.Infrastructure;
using SqlSugar.IQueryableExtension.Tests.Models;

namespace SqlSugar.IQueryableExtension.Tests;

/// <summary>
/// 投影（Select）相关测试：单表投影、联表投影、投影后继续 Where/OrderBy。
/// </summary>
[Collection(nameof(SqliteCollection))]
public class SugarQueryableProjectionTests
{
    private readonly SqliteTestDatabase _database;

    public SugarQueryableProjectionTests(SqliteTestDatabase database)
    {
        _database = database;
    }

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

        Assert.Equal(3, query.Count());
    }

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

        Assert.Equal(2, result.Count);
        Assert.All(result, x => Assert.True(x.Amount is 100m or 50m));
    }

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
