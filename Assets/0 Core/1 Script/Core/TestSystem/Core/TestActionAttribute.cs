using System;
using System.Collections.Generic;
using System.Reflection;

namespace TestSystem
{
    /// <summary>
    /// 打在无参 static 方法上就能变成测试面板按钮，分类不存在时自动建。
    /// 承载这些方法的类由编辑期的 TestActionHostGenerator 记进 TestRegistry.g.cs，运行时只反射那几个类。
    /// 例：[TestAction("催稿游戏", "回满灵感")] static void SetInspirationMax() { ... }
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TestActionAttribute : Attribute
    {
        public readonly string CategoryTitle;
        public readonly string Label;

        public TestActionAttribute(string categoryTitle, string label)
        {
            CategoryTitle = categoryTitle;
            Label = label;
        }
    }

    /// <summary>特性按钮集合：只反射注册进来的那几个类一次，之后走缓存。</summary>
    public static class TestAttributeActionSet
    {
        const BindingFlags MethodFlags = BindingFlags.Static
            | BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.DeclaredOnly;

        static Dictionary<string, TestActionList> actionTable;
        static Type[] hosts = Array.Empty<Type>();
        static readonly List<string> titles = new ();

        /// <summary>由生成的 TestRegistry.Hosts 喂进来。</summary>
        public static void SetHosts(Type[] hostTypes)
        {
            hosts = hostTypes;
            actionTable = null;
            titles.Clear();
        }

        /// <summary>[TestAction] 里出现过的分类标题，按扫描顺序。</summary>
        public static List<string> GetTitles()
        {
            SetActionTable();
            return titles;
        }

        /// <summary>把该分类下的特性按钮追加到列表末尾。</summary>
        public static void AddActions(string categoryTitle, TestActionList actionList)
        {
            SetActionTable();
            if (actionTable.TryGetValue(categoryTitle, out TestActionList attributeActionList))
                actionList.AddRange(attributeActionList);
        }

        static void SetActionTable()
        {
            if (actionTable != null)
                return;

            actionTable = new ();
            for (int i = 0; i < hosts.Length; i++)
            {
                MethodInfo[] methods = hosts[i].GetMethods(MethodFlags);
                for (int j = 0; j < methods.Length; j++)
                {
                    TestActionAttribute attribute = methods[j].GetCustomAttribute<TestActionAttribute>();
                    if (attribute == null)
                        continue;

                    if (!actionTable.TryGetValue(attribute.CategoryTitle, out TestActionList actionList))
                    {
                        actionList = new ();
                        actionTable.Add(attribute.CategoryTitle, actionList);
                        titles.Add(attribute.CategoryTitle);
                    }

                    actionList.Add(attribute.Label, (Action)methods[j].CreateDelegate(typeof(Action)));
                }
            }
        }
    }

    /// <summary>只由 [TestAction] 定义、没有手写 ITestCategory 的分类，按钮全部来自特性。</summary>
    public sealed class AttributeTestCategory : ITestCategory
    {
        readonly string title;

        public string Title => title;

        public AttributeTestCategory(string title)
        {
            this.title = title;
        }

        public void SetActions(TestActionList actionList)
        {
        }
    }
}
