using SqlSugar;
using SqlSugar.IQueryableExtension.Sample.Models;

namespace SqlSugar.IQueryableExtension.Sample;

/// <summary>创建并初始化演示用 SqlSugar 客户端。</summary>
public static class SampleDb
{
    public static SqlSugarClient CreateClient()
    {
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = SampleDbConfig.ConnectionString,
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });

        db.Aop.OnLogExecuting = (sql, _) => Console.WriteLine($"[SQL] {sql}");

        db.DbMaintenance.CreateDatabase();
        db.CodeFirst.InitTables(typeof(Customer), typeof(Order));
        SeedIfEmpty(db);

        return db;
    }

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
