using SqlSugar.IQueryableExtension.Tests.Infrastructure;
using SqlSugar.IQueryableExtension.Tests.Models;

namespace SqlSugar.IQueryableExtension.Tests;

/// <summary>
/// 基础 LINQ 查询测试：Where、OrderBy、Select、分页、聚合及双向转换。
/// 使用 SQLite 内存库，测试数据见 <see cref="SqliteTestDatabase"/>。
/// </summary>
[Collection(nameof(SqliteCollection))]
public class SugarQueryableBasicTests
{
    private readonly SqliteTestDatabase _database;

    public SugarQueryableBasicTests(SqliteTestDatabase database)
    {
        _database = database;
    }

    /// <summary>验证 AsLinqQueryable 后可直接 ToList 并返回全部数据。</summary>
    [Fact]
    public void ToList_Returns_All_Orders()
    {
        var query = _database.Db.Queryable<Order>().AsLinqQueryable();

        var result = query.ToList();

        // 种子数据共 5 条订单
        Assert.Equal(5, result.Count);
    }

    /// <summary>验证 Where 条件过滤是否翻译为 SQL WHERE 子句。</summary>
    [Fact]
    public void Where_Filters_By_Predicate()
    {
        var query = _database.Db.Queryable<Order>().AsLinqQueryable()
            .Where(o => o.Status == "Paid");

        var result = query.ToList();

        Assert.Equal(3, result.Count);
        Assert.All(result, o => Assert.Equal("Paid", o.Status));
    }

    /// <summary>验证 OrderBy 升序排序。</summary>
    [Fact]
    public void OrderBy_Sorts_Ascending()
    {
        var query = _database.Db.Queryable<Order>().AsLinqQueryable()
            .OrderBy(o => o.Amount);

        var result = query.ToList();

        Assert.Equal(new[] { 10m, 50m, 75m, 100m, 200m }, result.Select(o => o.Amount));
    }

    /// <summary>验证 OrderByDescending 降序排序，配合 Take 取前 N 条。</summary>
    [Fact]
    public void OrderByDescending_Sorts_Descending()
    {
        var query = _database.Db.Queryable<Order>().AsLinqQueryable()
            .OrderByDescending(o => o.Amount)
            .Take(3);

        var result = query.ToList();

        Assert.Equal(new[] { 200m, 100m, 75m }, result.Select(o => o.Amount));
    }

    /// <summary>验证单表 Select 投影为 DTO（内部使用 SelectMergeTable）。</summary>
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

    /// <summary>验证 Skip + Take 分页组合。</summary>
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

    /// <summary>验证 Count 终结操作，不物化完整列表。</summary>
    [Fact]
    public void Count_Returns_Filtered_Count()
    {
        var query = _database.Db.Queryable<Order>().AsLinqQueryable()
            .Where(o => o.Status == "Paid");

        var count = query.Count();

        Assert.Equal(3, count);
    }

    /// <summary>验证 Any 在存在匹配项时返回 true。</summary>
    [Fact]
    public void Any_Returns_True_When_Match_Exists()
    {
        var query = _database.Db.Queryable<Order>().AsLinqQueryable()
            .Where(o => o.Amount > 150m);

        Assert.True(query.Any());
    }

    /// <summary>验证 Any 在无匹配项时返回 false。</summary>
    [Fact]
    public void Any_Returns_False_When_No_Match()
    {
        var query = _database.Db.Queryable<Order>().AsLinqQueryable()
            .Where(o => o.Amount > 1000m);

        Assert.False(query.Any());
    }

    /// <summary>验证 First 返回排序后的第一条记录。</summary>
    [Fact]
    public void First_Returns_First_Matching_Item()
    {
        var query = _database.Db.Queryable<Order>().AsLinqQueryable()
            .Where(o => o.Status == "Pending")
            .OrderBy(o => o.Id);

        var result = query.First();

        Assert.Equal(3, result.Id);
    }

    /// <summary>验证 FirstOrDefault 在无匹配时返回 null。</summary>
    [Fact]
    public void FirstOrDefault_Returns_Null_When_No_Match()
    {
        var query = _database.Db.Queryable<Order>().AsLinqQueryable()
            .Where(o => o.Status == "Missing");

        var result = query.FirstOrDefault();

        Assert.Null(result);
    }

    /// <summary>验证 Distinct 对实体去重（本数据集无重复行，返回 4 条）。</summary>
    [Fact]
    public void Distinct_Returns_Unique_Entity_Rows()
    {
        var query = _database.Db.Queryable<Order>().AsLinqQueryable()
            .Where(o => o.Status == "Paid" || o.Status == "Pending")
            .Distinct();

        var result = query.ToList();

        Assert.Equal(4, result.Count);
    }

    /// <summary>README 基础用法：AsLinqQueryable → LINQ 链 → ToList / AsSugarQueryable。</summary>
    [Fact]
    public void Readme_BasicUsage_AsLinqQueryable_To_SugarQueryable()
    {
        IQueryable<Order> query = _database.Db.Queryable<Order>().AsLinqQueryable();

        query = query
            .Where(o => o.Status == "Paid")
            .OrderBy(o => o.Amount)
            .Take(10);

        var list = query.ToList();
        var sql = query.AsSugarQueryable().ToSqlString();

        Assert.Equal(3, list.Count);
        Assert.Contains("Status", sql, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>验证 AsSugarQueryable 可回退到 SqlSugar 并生成正确 SQL。</summary>
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
