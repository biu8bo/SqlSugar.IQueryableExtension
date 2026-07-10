using SqlSugar.IQueryableExtension.Tests.Infrastructure;
using SqlSugar.IQueryableExtension.Tests.Models;

namespace SqlSugar.IQueryableExtension.Tests;

[Collection(nameof(SqliteCollection))]
public class SugarQueryableJoinTests
{
    private readonly SqliteTestDatabase _database;

    public SugarQueryableJoinTests(SqliteTestDatabase database)
    {
        _database = database;
    }

    [Fact]
    public void Join_Projects_Matched_Rows()
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
            });

        var result = query.OrderBy(x => x.OrderId).ToList();

        Assert.Equal(4, result.Count);
        Assert.Equal("Alice", result[0].CustomerName);
        Assert.Equal("Alice", result[1].CustomerName);
        Assert.Equal("Bob", result[2].CustomerName);
        Assert.Equal("Carol", result[3].CustomerName);
        Assert.DoesNotContain(result, x => x.OrderId == 5);
    }

    [Fact]
    public void Join_With_Where_Filters_Before_Join()
    {
        var orders = _database.Db.Queryable<Order>().AsLinqQueryable()
            .Where(o => o.Amount >= 100m);
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
                });

        var result = query.ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, x => Assert.True(x.Amount >= 100m));
    }

    [Fact]
    public void Query_Syntax_Join_Works()
    {
        var query =
            from o in _database.Db.Queryable<Order>().AsLinqQueryable().Where(o => o.Amount > 0)
            join c in _database.Db.Queryable<Customer>().AsLinqQueryable()
                on o.CustomerId equals c.Id
            select new OrderCustomerDto
            {
                OrderId = o.Id,
                CustomerName = c.Name,
                Amount = o.Amount
            };

        var result = query.ToList();

        Assert.Equal(4, result.Count);
        Assert.DoesNotContain(result, x => x.OrderId == 5);
    }

    [Fact]
    public void Join_Generates_Expected_Sql()
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
            });

        var sql = query.AsSugarQueryable().ToSqlString();

        Assert.Contains("join", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CustomerId", sql, StringComparison.OrdinalIgnoreCase);
    }
}
