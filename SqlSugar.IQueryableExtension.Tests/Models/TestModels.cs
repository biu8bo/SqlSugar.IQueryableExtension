using SqlSugar;

namespace SqlSugar.IQueryableExtension.Tests.Models;

/// <summary>客户实体，映射 customer 表。</summary>
[SugarTable("customer")]
public class Customer
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

/// <summary>订单实体，映射 orders 表（避免 SQLite 保留字 order）。</summary>
[SugarTable("orders")]
public class Order
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>外键，关联 <see cref="Customer.Id"/>。</summary>
    public int CustomerId { get; set; }

    public decimal Amount { get; set; }

    public string Status { get; set; } = string.Empty;
}

/// <summary>联表投影 DTO，用于 Join 测试结果映射。</summary>
public class OrderCustomerDto
{
    public int OrderId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public decimal Amount { get; set; }
}

/// <summary>单表投影 DTO，仅包含订单 Id 与金额。</summary>
public class OrderAmountDto
{
    public int OrderId { get; set; }

    public decimal Amount { get; set; }
}
