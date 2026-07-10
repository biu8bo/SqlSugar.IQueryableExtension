# SqlSugar.IQueryableExtension

将 [SqlSugar](https://github.com/DotNetNext/SqlSugar) 的 `ISugarQueryable<T>` 适配为标准 `System.Linq.IQueryable<T>`，让 SqlSugar 查询无缝接入 LINQ 生态（动态查询库、筛选组件、通用仓储抽象等）。

## 项目描述

SqlSugar.IQueryableExtension 是一个轻量级桥接库。它通过自定义 `IQueryable` + `IQueryProvider` 实现，解析 LINQ 的 `MethodCallExpression` 操作符外壳，并将 Lambda 表达式直接交给 SqlSugar 内置的 `ExpressionToSql` 翻译为 SQL——无需重复实现表达式体转换，也无需维护双轨查询对象。

## 特性

- **标准 IQueryable 适配**：第三方库可直接消费 `IQueryable<T>`
- **表达式树翻译**：支持 `Where`、`Select`、`OrderBy`、`Skip`、`Take`、`Join` 等常用操作
- **投影支持**：单表 `SelectMergeTable`、联表 `MergeTable`，投影后可继续 `Where` / `OrderBy`
- **双向转换**：`AsLinqQueryable()` 进入 LINQ 世界；`AsSugarQueryable()` 取回本库查询，`AsSugarQueryable(db)` 翻译外部 IQueryable
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

### 引用本仓库

在业务项目中添加对本仓库核心类库的项目引用：

```bash
# 克隆仓库后，在业务项目目录执行（按实际路径调整）
dotnet add reference ../SqlSugar.IQueryableExtension/SqlSugar.IQueryableExtension/SqlSugar.IQueryableExtension.csproj
```

或在 `.csproj` 中手动添加：

```xml
<ItemGroup>
  <ProjectReference Include="..\SqlSugar.IQueryableExtension\SqlSugar.IQueryableExtension\SqlSugar.IQueryableExtension.csproj" />
</ItemGroup>
```

代码中引入命名空间：

```csharp
using SqlSugar;
using SqlSugar.IQueryableExtension;
```

### 普通 IQueryable 转 ISugarQueryable

任意标准 `IQueryable<T>`（内存集合、第三方筛选组件、动态查询库返回的查询等）均可通过 `AsSugarQueryable(db)` 转为 SqlSugar 查询。库会将表达式树根替换为 `db.Queryable<TSource>()`，再重放整条 LINQ 链，最终在数据库侧执行 SQL。

**示例 1：内存集合上的 LINQ 条件**

```csharp
// 普通 IQueryable：在空列表上叠加 Where / OrderBy（仅构建表达式树）
IQueryable<Order> query = new List<Order>().AsQueryable()
    .Where(o => o.Status == "Paid")
    .OrderBy(o => o.Amount);

// 转为 ISugarQueryable，在数据库执行
ISugarQueryable<Order> sugar = query.AsSugarQueryable(db);

var list = sugar.ToList();
var sql  = sugar.ToSqlString();
```

**示例 2：第三方组件返回的 IQueryable**

```csharp
// 筛选组件通常在占位 IQueryable 上追加条件
IQueryable<Order> filtered = filterService
    .Apply(new List<Order>().AsQueryable())
    .Where(o => o.Amount >= 100m);

// 交给 SqlSugar 执行
var orders = filtered.AsSugarQueryable(db).ToList();
```

**示例 3：含 Select 投影的普通 IQueryable**

```csharp
IQueryable<OrderAmountDto> projected = new List<Order>().AsQueryable()
    .Where(o => o.Status == "Paid")
    .Select(o => new OrderAmountDto
    {
        OrderId = o.Id,
        Amount = o.Amount
    });

// 投影后的 IQueryable 同样可转换
ISugarQueryable<OrderAmountDto> sugar = projected.AsSugarQueryable(db);
var dtos = sugar.OrderBy(x => x.Amount).ToList();
```

**示例 4：从 db 侧调用（等价写法）**

```csharp
IQueryable<Order> query = BuildQueryFromThirdParty(); // 任意来源的普通 IQueryable

ISugarQueryable<Order> sugar = db.ToSugarQueryable(query);
var count = sugar.Count();
```

> 说明：`AsSugarQueryable()` 无参重载仅适用于本库 `AsLinqQueryable()` 创建的查询；普通 `IQueryable` 必须传入 `db` 以指定数据源。仅支持本库已实现的 LINQ 操作符。

### 基础用法（SqlSugar → IQueryable）

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

// 3. 本库创建的 IQueryable 可直接取回底层查询
var sql = query.AsSugarQueryable().ToSqlString();
```

### API 参考

| 扩展方法 | 作用 |
|----------|------|
| `ISugarQueryable<T>.AsLinqQueryable()` | SqlSugar 查询 → 标准 `IQueryable<T>` |
| `IQueryable<T>.AsSugarQueryable()` | 本库适配的 `IQueryable` → `ISugarQueryable<T>` |
| `IQueryable<T>.AsSugarQueryable(ISqlSugarClient db)` | 任意 `IQueryable` → `ISugarQueryable<T>`（替换表达式树根后重放 LINQ 链） |
| `ISqlSugarClient.ToSugarQueryable(IQueryable<T>)` | 上者的别名，从 `db` 侧调用 |

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

**正向：SqlSugar → LINQ 生态**

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

**反向：外部 IQueryable → SqlSugar**

```
第三方 IQueryable<T>（Expression 树）
       │  AsSugarQueryable(db)
       ▼
QueryRootReplacer：根节点替换为 db.Queryable<TSource>()
       │  重放 Where / Select / Join / OrderBy … 链
       ▼
SugarQueryTranslator → ISugarQueryable<T> → SQL → Database
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

演示项目包含 5 个场景：基础查询、单表投影、联表投影、投影后过滤、外部 IQueryable 转换。

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
