using SqlSugar;
using SqlSugar.IQueryableExtension.Sample.Models;

namespace SqlSugar.IQueryableExtension.Sample;

/// <summary>
/// 创建并初始化演示用 SqlSugar 客户端。
/// 首次运行自动建表并写入种子数据，后续运行复用已有数据。
/// </summary>
public static class SampleDb
{
    /// <summary>创建 SQLite 客户端，开启 SQL 日志输出到控制台。</summary>
    public static SqlSugarClient CreateClient()
    {
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = SampleDbConfig.ConnectionString,
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });

        // 打印每条执行的 SQL，便于理解适配器翻译结果
        db.Aop.OnLogExecuting = (sql, _) => Console.WriteLine($"[SQL] {sql}");

        db.DbMaintenance.CreateDatabase();
        db.CodeFirst.InitTables(typeof(Customer), typeof(Order));
        SeedIfEmpty(db);

        return db;
    }

    /// <summary>仅在表为空时写入演示数据，避免重复插入。</summary>
    private static void SeedIfEmpty(SqlSugarClient db)
    {
        if (db.Queryable<Customer>().Any())
            return;

        db.Insertable(new List<Customer>
        {
            new() { Name = "Alice" },
            new() { Name = "Bob" },
            new() { Name = "Carol" }
        }).ExecuteCommand();

        db.Insertable(new List<Order>
        {
            new() { CustomerId = 1, Amount = 100m, Status = "Paid" },
            new() { CustomerId = 1, Amount = 50m, Status = "Paid" },
            new() { CustomerId = 2, Amount = 200m, Status = "Pending" },
            new() { CustomerId = 3, Amount = 75m, Status = "Paid" }
        }).ExecuteCommand();
    }
}
