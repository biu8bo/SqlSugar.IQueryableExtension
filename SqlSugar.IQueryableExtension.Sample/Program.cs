using SqlSugar;
using SqlSugar.IQueryableExtension;
using SqlSugar.IQueryableExtension.Sample.Models;
using SqlSugar.IQueryableExtension.Sample.Services;

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

        DemoReadmePlainIQueryableExamples(db);
        DemoBasicQuery(db);
        DemoSingleTableProjection(db);
        DemoJoinProjection(db);
        DemoPostProjectionFilter(db);
    }

    /// <summary>
    /// 场景 1：README 基础用法（SqlSugar → IQueryable）。
    /// 演示 AsLinqQueryable → Where → OrderBy → Take 链式调用。
    /// </summary>
    private static void DemoBasicQuery(SqlSugarClient db)
    {
        Console.WriteLine("=== 基础查询（README 基础用法） ===");

        IQueryable<Order> query = db.Queryable<Order>().AsLinqQueryable();

        query = query
            .Where(o => o.Status == "Paid")
            .OrderBy(o => o.Amount)
            .Take(3);

        var list = query.ToList();
        var sql = query.AsSugarQueryable().ToSqlString();

        Console.WriteLine(sql);
        foreach (var order in list)
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

        Console.WriteLine(filtered.AsSugarQueryable().ToSqlString());
        foreach (var item in filtered.ToList())
        {
            Console.WriteLine($"High value: {item.CustomerName} -> {item.Amount}");
        }
    }

    /// <summary>
    /// 场景 5：README「普通 IQueryable 转 ISugarQueryable」四种写法演示。
    /// </summary>
    private static void DemoReadmePlainIQueryableExamples(SqlSugarClient db)
    {
        Console.WriteLine();
        Console.WriteLine("=== README 普通 IQueryable 转换 ===");

        DemoReadmeExample1(db);
        DemoReadmeExample2(db);
        DemoReadmeExample3(db);
        DemoReadmeExample4(db);
    }

    /// <summary>README 示例 1：内存集合上的 LINQ 条件。</summary>
    private static void DemoReadmeExample1(SqlSugarClient db)
    {
        Console.WriteLine();
        Console.WriteLine("--- 示例 1：内存集合 LINQ 条件 ---");

        IQueryable<Order> query = new List<Order>().AsQueryable()
            .Where(o => o.Status == "Paid")
            .OrderBy(o => o.Amount);

        ISugarQueryable<Order> sugar = query.AsSugarQueryable(db);

        Console.WriteLine(sugar.ToSqlString());
        foreach (var order in sugar.ToList())
        {
            Console.WriteLine($"Paid order #{order.Id} Amount={order.Amount}");
        }
    }

    /// <summary>README 示例 2：第三方组件返回的 IQueryable。</summary>
    private static void DemoReadmeExample2(SqlSugarClient db)
    {
        Console.WriteLine();
        Console.WriteLine("--- 示例 2：第三方筛选组件 ---");

        var filterService = new StubFilterService();

        IQueryable<Order> filtered = filterService
            .Apply(new List<Order>().AsQueryable())
            .Where(o => o.Amount >= 100m);

        var orders = filtered.AsSugarQueryable(db).ToList();
        foreach (var order in orders)
        {
            Console.WriteLine($"High amount order #{order.Id} Amount={order.Amount}");
        }
    }

    /// <summary>README 示例 3：含 Select 投影的普通 IQueryable。</summary>
    private static void DemoReadmeExample3(SqlSugarClient db)
    {
        Console.WriteLine();
        Console.WriteLine("--- 示例 3：投影后转换 ---");

        IQueryable<OrderAmountDto> projected = new List<Order>().AsQueryable()
            .Where(o => o.Status == "Paid")
            .Select(o => new OrderAmountDto
            {
                OrderId = o.Id,
                Amount = o.Amount
            });

        ISugarQueryable<OrderAmountDto> sugar = projected.AsSugarQueryable(db);
        foreach (var dto in sugar.OrderBy(x => x.Amount).ToList())
        {
            Console.WriteLine($"DTO OrderId={dto.OrderId} Amount={dto.Amount}");
        }
    }

    /// <summary>README 示例 4：从 db 侧调用 ToSugarQueryable。</summary>
    private static void DemoReadmeExample4(SqlSugarClient db)
    {
        Console.WriteLine();
        Console.WriteLine("--- 示例 4：db.ToSugarQueryable ---");

        IQueryable<Order> query = BuildQueryFromThirdParty();

        ISugarQueryable<Order> sugar = db.ToSugarQueryable(query);
        Console.WriteLine($"Count={sugar.Count()}");
        Console.WriteLine(sugar.ToSqlString());
    }

    /// <summary>模拟 README 示例 4 中任意来源的普通 IQueryable。</summary>
    private static IQueryable<Order> BuildQueryFromThirdParty()
    {
        return new List<Order>().AsQueryable()
            .Where(o => o.Amount >= 100m);
    }
}
