using SqlSugar;
using SqlSugar.IQueryableExtension;
using SqlSugar.IQueryableExtension.Sample.Models;

namespace SqlSugar.IQueryableExtension.Sample;

/// <summary>
/// 演示 SqlSugar.IQueryableExtension 的典型用法。
/// 运行方式：dotnet run --project SqlSugar.IQueryableExtension.Sample
/// </summary>
public static class Program
{
    public static void Main()
    {
        // 创建本地 SQLite 数据库并写入演示数据
        using var db = SampleDb.CreateClient();

        DemoBasicQuery(db);
        DemoSingleTableProjection(db);
        DemoJoinProjection(db);
        DemoPostProjectionFilter(db);
    }

    /// <summary>
    /// 场景 1：基础 LINQ 查询。
    /// 演示 AsLinqQueryable → Where → OrderBy → Take 链式调用。
    /// </summary>
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

    /// <summary>
    /// 场景 2：单表 Select 投影。
    /// SelectMergeTable 将结果折叠为单表，之后可对 DTO 继续 Where。
    /// </summary>
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

    /// <summary>
    /// 场景 3：联表 Join 并投影为 DTO。
    /// LINQ Join 映射为 SqlSugar InnerJoin + Select + MergeTable。
    /// </summary>
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

    /// <summary>
    /// 场景 4：联表投影后继续 Where / OrderBy。
    /// 使用查询语法 join，演示投影 DTO 上的二次过滤与 AsSugarQueryable 查看 SQL。
    /// </summary>
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

        // 通过 AsSugarQueryable 取回底层查询，打印最终 SQL
        Console.WriteLine(filtered.AsSugarQueryable().ToSqlString());
        foreach (var item in filtered.ToList())
        {
            Console.WriteLine($"High value: {item.CustomerName} -> {item.Amount}");
        }
    }
}
