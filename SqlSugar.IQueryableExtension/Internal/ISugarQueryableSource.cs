using System;

namespace SqlSugar.IQueryableExtension.Internal;

/// <summary>
/// 从 <see cref="SugarQueryable{T}"/> 中提取底层 SqlSugar 查询对象的内部契约。
/// </summary>
internal interface ISugarQueryableSource
{
    /// <summary>底层 <see cref="ISugarQueryable{T}"/> 实例（运行时类型可能带多泛型参数）。</summary>
    object Inner { get; }

    /// <summary>当前 IQueryable 的元素类型。</summary>
    Type ElementType { get; }
}
