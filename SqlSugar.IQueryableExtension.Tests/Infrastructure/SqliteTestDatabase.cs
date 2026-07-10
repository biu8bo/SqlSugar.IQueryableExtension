using Microsoft.Data.Sqlite;
using SqlSugar;
using SqlSugar.IQueryableExtension.Tests.Models;

namespace SqlSugar.IQueryableExtension.Tests.Infrastructure;

/// <summary>
/// 测试用 SQLite 内存数据库。
/// 使用 Cache=Shared 模式保持连接存活，同一测试集合内共享一份种子数据。
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    /// <summary>保持内存库存活的连接，Dispose 时一并释放。</summary>
    private readonly SqliteConnection _connection;

    /// <summary>SqlSugar 客户端，供各测试类注入使用。</summary>
    public SqlSugarClient Db { get; }

    public SqliteTestDatabase()
    {
        // 每个测试集合实例使用独立内存库，避免并行测试互相干扰
        var connectionString = $"Data Source=sqlsugar_iqueryable_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();

        Db = new SqlSugarClient(new ConnectionConfig
        {
            DbType = DbType.Sqlite,
            ConnectionString = connectionString,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });

        Db.DbMaintenance.CreateDatabase();
        Db.CodeFirst.InitTables(typeof(Customer), typeof(Order));
        Seed();
    }

    /// <summary>
    /// 插入固定测试数据：
    /// - 3 个客户（Alice、Bob、Carol）
    /// - 5 笔订单，其中 Id=5 的 CustomerId=99 为孤立数据，用于验证 Join 排除逻辑
    /// </summary>
    private void Seed()
    {
        var customers = new List<Customer>
        {
            new() { Id = 1, Name = "Alice" },
            new() { Id = 2, Name = "Bob" },
            new() { Id = 3, Name = "Carol" }
        };

        var orders = new List<Order>
        {
            new() { Id = 1, CustomerId = 1, Amount = 100m, Status = "Paid" },
            new() { Id = 2, CustomerId = 1, Amount = 50m, Status = "Paid" },
            new() { Id = 3, CustomerId = 2, Amount = 200m, Status = "Pending" },
            new() { Id = 4, CustomerId = 3, Amount = 75m, Status = "Paid" },
            new() { Id = 5, CustomerId = 99, Amount = 10m, Status = "Orphan" }
        };

        Db.Insertable(customers).ExecuteCommand();
        Db.Insertable(orders).ExecuteCommand();
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}

/// <summary>xUnit 集合定义，使同一集合内的测试类共享 <see cref="SqliteTestDatabase"/> 实例。</summary>
[CollectionDefinition(nameof(SqliteCollection))]
public class SqliteCollection : ICollectionFixture<SqliteTestDatabase>
{
}
