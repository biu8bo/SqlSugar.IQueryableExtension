namespace SqlSugar.IQueryableExtension.Sample;

/// <summary>
/// 本地 SQLite 数据库配置。
/// 数据库文件生成在程序运行目录下的 sample.db。
/// </summary>
public static class SampleDbConfig
{
    /// <summary>SQLite 数据库文件路径。</summary>
    public static string DatabasePath { get; } =
        Path.Combine(AppContext.BaseDirectory, "sample.db");

    /// <summary>SqlSugar 连接字符串。</summary>
    public static string ConnectionString { get; } =
        $"Data Source={DatabasePath}";
}
