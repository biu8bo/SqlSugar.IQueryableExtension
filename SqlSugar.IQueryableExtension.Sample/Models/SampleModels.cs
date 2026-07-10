using SqlSugar;

namespace SqlSugar.IQueryableExtension.Sample.Models;

/// <summary>客户表。</summary>
[SugarTable("customer")]
public class Customer
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

/// <summary>订单表（避免使用 SQLite 保留字 order）。</summary>
[SugarTable("orders")]
public class Order
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public decimal Amount { get; set; }

    public string Status { get; set; } = string.Empty;
}

/// <summary>单表投影 DTO。</summary>
public class OrderAmountDto
{
    public int OrderId { get; set; }

    public decimal Amount { get; set; }
}

/// <summary>联表投影 DTO。</summary>
public class OrderCustomerDto
{
    public int OrderId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public decimal Amount { get; set; }
}
