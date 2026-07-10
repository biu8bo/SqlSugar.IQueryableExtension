using SqlSugar;
using SqlSugar.IQueryableExtension;
using SqlSugar.IQueryableExtension.Sample.Models;

namespace SqlSugar.IQueryableExtension.Sample;

/// <summary>
/// 演示 SqlSugar.IQueryableExtension 的典型用法。
/// </summary>
public static class Program
{
    public static void Main()
    {
        using var db = SampleDb.CreateClient();

        DemoBasicQuery(db);
        DemoSingleTableProjection(db);
        DemoJoinProjection(db);
        DemoPostProjectionFilter(db);
    }

    /// <summary>基础 Where / OrderBy / Take。</summary>
    private static void DemoBasicQuery(SqlSugarClient db)
    {
        Console.WriteLine("=== 基础查询 ===");

        var query = db.Queryable<Order>().AsLinqQueryable()
            .Where(o => o.Status == "Paid")
            .OrderBy(o => o.Amount)
            .Take(3);

        foreach (var order in query)
        {
            Console.WriteLine($"Order #{order.Id} Amount={order.Amount}");
        }
    }

    /// <summary>单表 Select 投影为 DTO。</summary>
    private static void DemoSingleTableProjection(SqlSugarClient db)
    {
        Console.WriteLine();
        Console.WriteLine("=== 单表投影 ===");

        var query = db.Queryable<Order>().AsLinqQueryable()
            .Select(o => new OrderAmountDto
            {
                OrderId = o.Id,
                Amount = o.Amount
            })
            .Where(x => x.Amount >= 75m);

        foreach (var item in query.ToList())
        {
            Console.WriteLine($"DTO OrderId={item.OrderId} Amount={item.Amount}");
        }
    }

    /// <summary>联表 Join 并投影为 DTO。</summary>
    private static void DemoJoinProjection(SqlSugarClient db)
    {
        Console.WriteLine();
        Console.WriteLine("=== 联表投影 ===");

        var orders = db.Queryable<Order>().AsLinqQueryable();
        var customers = db.Queryable<Customer>().AsLinqQueryable();

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

        foreach (var item in query.OrderBy(x => x.OrderId).ToList())
        {
            Console.WriteLine($"{item.OrderId} | {item.CustomerName} | {item.Amount}");
        }
    }

    /// <summary>联表投影后继续 Where / OrderBy（MergeTable + 参数名规范化）。</summary>
    private static void DemoPostProjectionFilter(SqlSugarClient db)
    {
        Console.WriteLine();
        Console.WriteLine("=== 投影后过滤 ===");

        var query =
            from o in db.Queryable<Order>().AsLinqQueryable()
            join c in db.Queryable<Customer>().AsLinqQueryable()
                on o.CustomerId equals c.Id
            select new OrderCustomerDto
            {
                OrderId = o.Id,
                CustomerName = c.Name,
                Amount = o.Amount
            };

        var filtered = query
            .Where(x => x.Amount >= 100m)
            .OrderByDescending(x => x.Amount);

        Console.WriteLine(filtered.AsSugarQueryable().ToSqlString());
        foreach (var item in filtered.ToList())
        {
            Console.WriteLine($"High value: {item.CustomerName} -> {item.Amount}");
        }
    }
}
