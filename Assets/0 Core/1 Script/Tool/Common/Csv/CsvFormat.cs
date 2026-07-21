/// <summary>
/// CSV 解析用的字符/字符串常量
/// </summary>
public static class CsvFormat
{
    /// <summary>列分隔符。</summary>
    public const char Comma = ',';

    /// <summary>字段引号（RFC4180 引号包裹）。</summary>
    public const char Quote = '"';

    /// <summary>数组/多值字段的分隔符（如 PurchaseRestriction、食材列表）。</summary>
    public const char ArraySeparator = '+';

    /// <summary>UTF-8 BOM（零宽不换行空格 U+FEFF）。Excel 导出 CSV 首格常残留，解析首列 Id 前需 TrimStart 掉。</summary>
    public const char Bom = '﻿';
}
