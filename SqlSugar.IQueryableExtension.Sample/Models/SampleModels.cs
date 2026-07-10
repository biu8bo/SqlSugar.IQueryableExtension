using SqlSugar;

namespace SqlSugar.IQueryableExtension.Sample.Models;

/// <summary>客户表实体。</summary>
[SugarTable("customer")]
public class Customer
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// 订单表实体。
/// 表名使用 orders 而非 order，因为 order 是 SQLite 保留字。
/// </summary>
[SugarTable("orders")]
public class Order
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>外键，关联 Customer.Id。</summary>
    public int CustomerId { get; set; }

    public decimal Amount { get; set; }

    public string Status { get; set; } = string.Empty;
}

/// <summary>单表投影 DTO，仅承载订单 Id 与金额。</summary>
public class OrderAmountDto
{
    public int OrderId { get; set; }

    public decimal Amount { get; set; }
}

/// <summary>联表投影 DTO，合并订单与客户信息。</summary>
public class OrderCustomerDto
{
    public int OrderId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public decimal Amount { get; set; }
}
