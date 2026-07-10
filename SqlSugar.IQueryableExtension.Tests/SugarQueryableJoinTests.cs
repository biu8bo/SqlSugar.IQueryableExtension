using SqlSugar.IQueryableExtension.Tests.Infrastructure;
using SqlSugar.IQueryableExtension.Tests.Models;

namespace SqlSugar.IQueryableExtension.Tests;

/// <summary>
/// 联表（Join）相关测试：方法语法 Join、查询语法 join、联表前过滤、SQL 生成。
/// Join 内部映射为 SqlSugar InnerJoin + Select + MergeTable。
/// </summary>
[Collection(nameof(SqliteCollection))]
public class SugarQueryableJoinTests
{
    private readonly SqliteTestDatabase _database;

    public SugarQueryableJoinTests(SqliteTestDatabase database)
    {
        _database = database;
    }

    /// <summary>
    /// 验证 InnerJoin 只返回能匹配到客户的订单。
    /// 订单 Id=5（CustomerId=99）为孤立数据，应被排除。
    /// </summary>
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

    /// <summary>
    /// 验证在 Join 之前对主表 Where 过滤（推荐写法，避免投影后别名问题）。
    /// </summary>
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

        // Amount >= 100 且能匹配客户：订单 1(100) 和 3(200)
        Assert.Equal(2, result.Count);
        Assert.All(result, x => Assert.True(x.Amount >= 100m));
    }

    /// <summary>验证 C# 查询语法（from ... join ... select）可正常编译并执行。</summary>
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

    /// <summary>验证 Join 生成的 SQL 包含 join 关键字及关联字段。</summary>
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
