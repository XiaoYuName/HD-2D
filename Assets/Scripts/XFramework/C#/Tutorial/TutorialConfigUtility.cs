using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace XFramework
{
    /// <summary>
    /// 引导配置在 Inspector 上要用的一点公共东西。
    /// </summary>
    public static class TutorialConfigUtility
    {
        /// <summary>
        /// 界面ID下拉的数据源：直接读生成出来的 <see cref="UIKeys"/> 常量。
        /// 手打 PageID 拼错是运行时才炸的错，能选就别打字。
        /// </summary>
        public static IEnumerable<string> GetPageIds()
        {
            return typeof(UIKeys)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .Select(field => (string)field.GetRawConstantValue())
                .OrderBy(id => id);
        }
    }
}
