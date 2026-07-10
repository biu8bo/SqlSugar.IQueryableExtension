using Microsoft.Data.Sqlite;
using SqlSugar;
using SqlSugar.IQueryableExtension.Tests.Models;

namespace SqlSugar.IQueryableExtension.Tests.Infrastructure;

public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqlSugarClient Db { get; }

    public SqliteTestDatabase()
    {
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

[CollectionDefinition(nameof(SqliteCollection))]
public class SqliteCollection : ICollectionFixture<SqliteTestDatabase>
{
}
