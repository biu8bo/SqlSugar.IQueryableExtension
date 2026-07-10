using SqlSugar.IQueryableExtension.Tests.Infrastructure;
using SqlSugar.IQueryableExtension.Tests.Models;

namespace SqlSugar.IQueryableExtension.Tests;

[Collection(nameof(SqliteCollection))]
public class SugarQueryableBasicTests
{
    private readonly SqliteTestDatabase _database;

    public SugarQueryableBasicTests(SqliteTestDatabase database)
    {
        _database = database;
    }

    [Fact]
    public void ToList_Returns_All_Orders()
    {
        var query = _database.Db.Queryable<Order>().AsLinqQueryable();

        var result = query.ToList();

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void Where_Filters_By_Predicate()
    {
        var query = _database.Db.Queryable<Order>().AsLinqQueryable()
            .Where(o => o.Status == "Paid");

        var result = query.ToList();

        Assert.Equal(3, result.Count);
        Assert.All(result, o => Assert.Equal("Paid", o.Status));
    }

    [Fact]
    public void OrderBy_Sorts_Ascending()
    {
        var query = _database.Db.Queryable<Order>().AsLinqQueryable()
            .OrderBy(o => o.Amount);

        var result = query.ToList();

        Assert.Equal(new[] { 10m, 50m, 75m, 100m, 200m }, result.Select(o => o.Amount));
    }

    [Fact]
    public void OrderByDescending_Sorts_Descending()
    {
        var query = _database.Db.Queryable<Order>().AsLinqQueryable()
            .OrderByDescending(o => o.Amount)
            .Take(3);

        var result = query.ToList();

        Assert.Equal(new[] { 200m, 100m, 75m }, result.Select(o => o.Amount));
    }

    [Fact]
    public void Select_Projects_Fields()
    {
        var query = _database.Db.Queryable<Order>().AsLinqQueryable()
            .Where(o => o.Status == "Paid")
            .Select(o => new OrderAmountDto
            {
                OrderId = o.Id,
                Amount = o.Amount
            });

        var result = query.ToList();

        Assert.Equal(3, result.Count);
        Assert.Contains(result, x => x.OrderId == 1 && x.Amount == 100m);
    }

    [Fact]
    public void Skip_And_Take_Paginate()
    {
        var query = _database.Db.Queryable<Order>().AsLinqQueryable()
            .OrderBy(o => o.Id)
            .Skip(2)
            .Take(2);

        var result = query.ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { 3, 4 }, result.Select(o => o.Id));
    }

    [Fact]
    public void Count_Returns_Filtered_Count()
    {
        var query = _database.Db.Queryable<Order>().AsLinqQueryable()
            .Where(o => o.Status == "Paid");

        var count = query.Count();

        Assert.Equal(3, count);
    }

    [Fact]
    public void Any_Returns_True_When_Match_Exists()
    {
        var query = _database.Db.Queryable<Order>().AsLinqQueryable()
            .Where(o => o.Amount > 150m);

        Assert.True(query.Any());
    }

    [Fact]
    public void Any_Returns_False_When_No_Match()
    {
        var query = _database.Db.Queryable<Order>().AsLinqQueryable()
            .Where(o => o.Amount > 1000m);

        Assert.False(query.Any());
    }

    [Fact]
    public void First_Returns_First_Matching_Item()
    {
        var query = _database.Db.Queryable<Order>().AsLinqQueryable()
            .Where(o => o.Status == "Pending")
            .OrderBy(o => o.Id);

        var result = query.First();

        Assert.Equal(3, result.Id);
    }

    [Fact]
    public void FirstOrDefault_Returns_Null_When_No_Match()
    {
        var query = _database.Db.Queryable<Order>().AsLinqQueryable()
            .Where(o => o.Status == "Missing");

        var result = query.FirstOrDefault();

        Assert.Null(result);
    }

    [Fact]
    public void Distinct_Returns_Unique_Entity_Rows()
    {
        var query = _database.Db.Queryable<Order>().AsLinqQueryable()
            .Where(o => o.Status == "Paid" || o.Status == "Pending")
            .Distinct();

        var result = query.ToList();

        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void AsSugarQueryable_Round_Trips_Back_To_SqlSugar()
    {
        var linqQuery = _database.Db.Queryable<Order>().AsLinqQueryable()
            .Where(o => o.Status == "Paid")
            .OrderBy(o => o.Id)
            .Take(2);

        var sugarQuery = linqQuery.AsSugarQueryable();
        var sql = sugarQuery.ToSqlString();

        Assert.Contains("Status", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, sugarQuery.ToList().Count);
    }
}
