namespace SqlSugar.IQueryableExtension.Sample;

/// <summary>本地 SQLite 数据库配置。</summary>
public static class SampleDbConfig
{
    public static string DatabasePath { get; } =
        Path.Combine(AppContext.BaseDirectory, "sample.db");

    public static string ConnectionString { get; } =
        $"Data Source={DatabasePath}";
}
