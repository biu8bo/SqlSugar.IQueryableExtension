using SqlSugar;

namespace SqlSugar.IQueryableExtension.Tests.Models;

[SugarTable("customer")]
public class Customer
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

[SugarTable("orders")]
public class Order
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public decimal Amount { get; set; }

    public string Status { get; set; } = string.Empty;
}

public class OrderCustomerDto
{
    public int OrderId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public decimal Amount { get; set; }
}

public class OrderAmountDto
{
    public int OrderId { get; set; }

    public decimal Amount { get; set; }
}
