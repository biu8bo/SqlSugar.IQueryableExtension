namespace SqlSugar.IQueryableExtension.Sample.Services;

/// <summary>
/// 模拟 README 中的第三方筛选组件：在占位 IQueryable 上透传，由调用方追加 Where 等条件。
/// </summary>
internal sealed class StubFilterService
{
    /// <summary>对传入查询应用筛选逻辑（此处仅透传，条件由链式调用追加）。</summary>
    public IQueryable<T> Apply<T>(IQueryable<T> query) => query;
}
