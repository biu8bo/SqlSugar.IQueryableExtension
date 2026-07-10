# SqlSugar.IQueryableExtension

将 [SqlSugar](https://github.com/DotNetNext/SqlSugar) 的 `ISugarQueryable<T>` 适配为标准 `System.Linq.IQueryable<T>`，让 SqlSugar 查询无缝接入 LINQ 生态（动态查询库、筛选组件、通用仓储抽象等）。

## 项目描述

SqlSugar.IQueryableExtension 是一个轻量级桥接库。它通过自定义 `IQueryable` + `IQueryProvider` 实现，解析 LINQ 的 `MethodCallExpression` 操作符外壳，并将 Lambda 表达式直接交给 SqlSugar 内置的 `ExpressionToSql` 翻译为 SQL——无需重复实现表达式体转换，也无需维护双轨查询对象。

## 特性

- **标准 IQueryable 适配**：第三方库可直接消费 `IQueryable<T>`
- **表达式树翻译**：支持 `Where`、`Select`、`OrderBy`、`Skip`、`Take`、`Join` 等常用操作
- **投影支持**：单表 `SelectMergeTable`、联表 `MergeTable`，投影后可继续 `Where` / `OrderBy`
- **双向转换**：`AsLinqQueryable()` 进入 LINQ 世界，`AsSugarQueryable()` 回到 SqlSugar 原生 API
- **外部 IQueryable 接入**：任意第三方构建的 `IQueryable<T>` 可通过 `AsSugarQueryable(db)` 翻译为 SqlSugar 查询
- **查询隔离**：翻译前自动 `Clone()`，避免 SqlSugar 链式操作污染原始实例

## 支持的 LINQ 操作

| 分类 | 操作符 |
|------|--------|
| 筛选 | `Where` |
| 投影 | `Select` |
| 排序 | `OrderBy` / `OrderByDescending` / `ThenBy` / `ThenByDescending` |
| 分页 | `Skip` / `Take` |
| 联表 | `Join`（映射为 SqlSugar `InnerJoin`） |
| 聚合 | `Count` / `LongCount` / `Any` |
| 取值 | `First` / `FirstOrDefault` / `Single` / `SingleOrDefault` |
| 其他 | `Distinct` |

## 快速开始

### 安装

```bash
dotnet add package SqlSugar.IQueryableExtension
```

或在项目中引用本仓库：

```bash
dotnet add reference path/to/SqlSugar.IQueryableExtension/SqlSugar.IQueryableExtension.csproj
```

### 基础用法

```csharp
using SqlSugar;
using SqlSugar.IQueryableExtension;

// 1. 将 SqlSugar 查询适配为 IQueryable
IQueryable<Order> query = db.Queryable<Order>().AsLinqQueryable();

// 2. 使用标准 LINQ 链式调用
query = query
    .Where(o => o.Status == "Paid")
    .OrderBy(o => o.Amount)
    .Take(10);

var list = query.ToList();

// 3. 需要 SqlSugar 原生能力时，取回底层查询
var sql = query.AsSugarQueryable().ToSqlString();
```

### 外部 IQueryable 转 SqlSugar

当筛选组件、动态查询库等第三方库返回标准 `IQueryable<T>` 时，可指定 SqlSugar 数据源将其翻译为 SQL 查询：

```csharp
// 第三方组件在内存 IQueryable 上构建的表达式树
IQueryable<Order> external = filterComponent
    .Apply(new List<Order>().AsQueryable())
    .Where(o => o.Status == "Paid");

// 替换表达式树根节点为 db.Queryable<Order>()，重放 LINQ 链
var sugar = external.AsSugarQueryable(db);
// 或使用 db 扩展方法
var sugar2 = db.ToSugarQueryable(external);

var count = sugar.Count();  // 实际查询数据库
```

> 仅支持本库已实现的 LINQ 操作符；表达式树中的数据源会被替换为 `db.Queryable<TSource>()`，执行时走 SQL 而非内存过滤。

### 单表投影

```csharp
var query = db.Queryable<Order>().AsLinqQueryable()
    .Select(o => new OrderAmountDto
    {
        OrderId = o.Id,
        Amount = o.Amount
    })
    .Where(x => x.Amount >= 100m)
    .OrderBy(x => x.OrderId);

var result = query.ToList();
```

### 联表投影

```csharp
var orders = db.Queryable<Order>().AsLinqQueryable();
var customers = db.Queryable<Customer>().AsLinqQueryable();

var query = orders.Join(
    customers,
    o => o.CustomerId,
    c => c.Id,
    (o, c) => new OrderCustomerDto
    {
        OrderId = o.Id,
        CustomerName = c.Name,
        Amount = o.Amount
    })
    .Where(x => x.Amount >= 100m)
    .OrderByDescending(x => x.Amount);
```

### 查询语法

```csharp
var query =
    from o in db.Queryable<Order>().AsLinqQueryable()
    join c in db.Queryable<Customer>().AsLinqQueryable()
        on o.CustomerId equals c.Id
    select new OrderCustomerDto
    {
        OrderId = o.Id,
        CustomerName = c.Name,
        Amount = o.Amount
    };

var result = query.Where(x => x.Amount > 0).ToList();
```

## 架构说明

```
ISugarQueryable<T>
       │  AsLinqQueryable()
       ▼
SugarQueryable<T>  : IQueryable<T>
       │  Expression + Provider
       ▼
SugarQueryProvider : IQueryProvider
       │  CreateQuery / Execute
       ▼
SugarQueryTranslator (ExpressionVisitor)
       │  解析 MethodCallExpression → 调用 ISugarQueryable API
       ▼
SqlSugar ExpressionToSql → SQL → Database
```

**设计要点：**

1. 不维护双轨 `IQueryable` + `ISugarQueryable`，避免执行路径不一致
2. 不转换 SqlSugar 表达式体，只解析 LINQ 操作符外壳
3. 联表投影后调用 `MergeTable()`，单表投影使用 `SelectMergeTable()`
4. Lambda 参数名规范为 `it`，兼容 SqlSugar 别名规则

## 解决方案结构

```
SqlSugar.IQueryableExtension/
├── SqlSugar.IQueryableExtension/          # 核心类库 (netstandard2.1)
├── SqlSugar.IQueryableExtension.Tests/      # 单元测试 (xUnit + SQLite)
├── SqlSugar.IQueryableExtension.Sample/     # 演示项目
└── README.md
```

## 本地开发

### 运行测试

```bash
dotnet test SqlSugar.IQueryableExtension.Tests/SqlSugar.IQueryableExtension.Tests.csproj
```

测试使用 SQLite 内存数据库，覆盖基础查询、投影、联表、外部 IQueryable 转换等 26 个场景。

### 运行演示

```bash
dotnet run --project SqlSugar.IQueryableExtension.Sample/SqlSugar.IQueryableExtension.Sample.csproj
```

## 限制与说明

- 实体类型需为引用类型且有无参构造函数（`class, new()`）
- SqlSugar 专有 API（`Includes`、`LeftJoin`、`SplitTable` 等）请通过 `AsSugarQueryable()` 使用
- 非本库创建的 `IQueryable` 需调用 `AsSugarQueryable(db)` 并传入 `ISqlSugarClient` 以指定数据源
- `GroupBy`、左连接、`GroupJoin` 等复杂 LINQ 操作暂未支持
- 投影类型不支持 `string` 等原始类型包装查询

## 依赖

- [SqlSugarCore](https://www.nuget.org/packages/SqlSugarCore) >= 5.1.4
- .NET Standard 2.1+

## License

MIT
