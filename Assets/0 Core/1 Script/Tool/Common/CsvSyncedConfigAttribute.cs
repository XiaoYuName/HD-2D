using System;

/// <summary>
/// 「CSV 驱动」配置 SO 的标记：类上挂本特性，并按约定包含两个序列化字段——
/// Dictionary&lt;string, TData&gt; dataDict（Id → 行数据）与 UnityEngine.Object csvTable（引用对应 CSV）。
/// CSV 变更时 CsvConfigAutoSync 自动重新导入；也可在资产 Inspector 右上角齿轮菜单手动「从 CSV 导入配置」。
/// TData 可由 CsvConfigCodeGen 从表头自动生成，列按「列名 = 字段名（忽略大小写）」反射填充。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public class CsvSyncedConfigAttribute : Attribute { }
