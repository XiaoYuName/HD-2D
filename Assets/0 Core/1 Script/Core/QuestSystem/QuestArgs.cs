using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 配置里的一段位置参数：<c>类型:参数1:参数2</c>，多段用 <c>/</c> 分隔。
    /// 例：<c>DayPassed:2/CompleteGame:1001:2:1</c>。
    /// 各段是什么含义由对应的目标 / 触发 / 奖励实现自己解释；可选参数一律放末尾。
    /// </summary>
    public class QuestArgs
    {
        static readonly char[] EntrySep = { '/' };
        static readonly char[] ArgSep = { ':' };

        /// <summary>第一段，类型名。</summary>
        public string Head = string.Empty;

        /// <summary>类型名之后的参数。</summary>
        public string[] Args = Array.Empty<string>();

        /// <summary>这段参数写在哪一行，报错定位用，形如「任务 10001」「目标 10002」。</summary>
        public string Owner = string.Empty;

        /// <summary>原文，报错时贴出来。</summary>
        public string Raw = string.Empty;

        public int Count => Args.Length;

        public bool Has(int index) => index >= 0 && index < Args.Length && Args[index].Length > 0;

        #region 取值

        public string GetString(int index, string defaultValue)
            => Has(index) ? Args[index] : defaultValue;

        public long GetLong(int index, long defaultValue)
        {
            if (!Has(index)) return defaultValue;
            if (long.TryParse(Args[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)) return value;

            LogError($"第 {index + 1} 个参数 \"{Args[index]}\" 不是整数");
            return defaultValue;
        }

        public int GetInt(int index, int defaultValue) => (int)GetLong(index, defaultValue);

        public float GetFloat(int index, float defaultValue)
        {
            if (!Has(index)) return defaultValue;
            if (float.TryParse(Args[index], NumberStyles.Float, CultureInfo.InvariantCulture, out float value)) return value;

            LogError($"第 {index + 1} 个参数 \"{Args[index]}\" 不是数字");
            return defaultValue;
        }

        /// <summary>枚举参数，写枚举名（不区分大小写）或直接写数字都行。</summary>
        public T GetEnum<T>(int index, T defaultValue) where T : struct
        {
            if (!Has(index)) return defaultValue;
            if (Enum.TryParse(Args[index], true, out T parsed) && Enum.IsDefined(typeof(T), parsed)) return parsed;

            LogError($"第 {index + 1} 个参数 \"{Args[index]}\" 不是合法的 {typeof(T).Name}");
            return defaultValue;
        }

        /// <summary>类型名转枚举。</summary>
        public T GetHead<T>(T defaultValue) where T : struct
        {
            if (Enum.TryParse(Head, true, out T parsed) && Enum.IsDefined(typeof(T), parsed)) return parsed;

            LogError($"类型名 \"{Head}\" 不是已定义的 {typeof(T).Name}");
            return defaultValue;
        }

        #endregion

        /// <summary>
        /// 参数个数校验。位置参数没有 key 可对照，所以每种类型在读表时报一下自己的写法，
        /// 段数不够当场报错并把正确写法打出来，而不是运行时静默不推进。
        /// </summary>
        public bool Require(int least, string usage)
        {
            if (Args.Length >= least) return true;

            Debug.LogError($"[Quest] {Owner} 的 \"{Raw}\" 参数不足（需要 {least} 个，实际 {Args.Length} 个），正确写法: {usage}");
            return false;
        }

        void LogError(string reason) => Debug.LogError($"[Quest] {Owner} 的 \"{Raw}\" {reason}");

        public override string ToString() => Raw;

        /// <summary>拆单独一段（目标列只写一条）。空串返回 null。</summary>
        public static QuestArgs Split(string text, string owner)
        {
            QuestArgs[] list = SplitList(text, owner);
            if (list.Length == 0) return null;
            if (list.Length > 1) Debug.LogError($"[Quest] {owner} 的 \"{text}\" 只能写一条，多余的被忽略");
            return list[0];
        }

        /// <summary>拆一整列。空串返回空数组。</summary>
        public static QuestArgs[] SplitList(string text, string owner)
        {
            List<QuestArgs> result = new();
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<QuestArgs>();

            foreach (string rawEntry in text.Split(EntrySep, StringSplitOptions.RemoveEmptyEntries))
            {
                string entry = rawEntry.Trim();
                if (entry.Length == 0) continue;

                string[] parts = entry.Split(ArgSep);
                for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();

                QuestArgs args = new() { Head = parts[0], Owner = owner, Raw = entry };
                if (parts.Length > 1)
                {
                    args.Args = new string[parts.Length - 1];
                    Array.Copy(parts, 1, args.Args, 0, args.Args.Length);
                }
                result.Add(args);
            }
            return result.ToArray();
        }
    }
}
