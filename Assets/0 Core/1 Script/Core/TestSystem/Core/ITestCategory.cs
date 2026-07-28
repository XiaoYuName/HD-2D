using System;
using System.Collections.Generic;

namespace TestSystem
{
    /// <summary>测试面板分类：实现本接口就会被编辑期扫进 TestRegistry.g.cs 自动注册，不用手写注册。</summary>
    public interface ITestCategory
    {
        /// <summary>侧边栏按钮与右侧标题显示名。</summary>
        string Title { get; }

        /// <summary>往列表里加按钮，切换到本分类时调用。</summary>
        void SetActions(TestActionList actionList);
    }

    /// <summary>一个测试操作按钮的数据。</summary>
    public readonly struct TestActionInfo
    {
        public readonly string Label;
        public readonly Action OnClick;

        public TestActionInfo(string label, Action onClick)
        {
            Label = label;
            OnClick = onClick;
        }
    }

    /// <summary>按钮列表：Add(标签, 回调) 一行加一个按钮。</summary>
    public sealed class TestActionList : List<TestActionInfo>
    {
        public void Add(string label, Action onClick)
        {
            Add(new TestActionInfo(label, onClick));
        }
    }

    /// <summary>
    /// 测试分类注册表：本程序集不认识游戏代码，手写分类由生成的 TestRegistry 启动时注册进来，
    /// [TestAction] 特性建的分类自动追加在后面。
    /// </summary>
    public static class TestCategorySet
    {
        static readonly List<ITestCategory> categories = new ();

        public static void Add(ITestCategory category)
        {
            categories.Add(category);
        }

        /// <summary>关闭域重载时 static 不会清，注册前先清一次。</summary>
        public static void Clear()
        {
            categories.Clear();
        }

        /// <summary>手写分类 + 只由 [TestAction] 定义的分类。</summary>
        public static List<ITestCategory> GetAllCategories()
        {
            List<ITestCategory> allCategories = new (categories);
            List<string> attributeTitles = TestAttributeActionSet.GetTitles();
            for (int i = 0; i < attributeTitles.Count; i++)
                if (!IsRegisteredTitle(attributeTitles[i]))
                    allCategories.Add(new AttributeTestCategory(attributeTitles[i]));

            return allCategories;
        }

        static bool IsRegisteredTitle(string title)
        {
            for (int i = 0; i < categories.Count; i++)
                if (categories[i].Title == title)
                    return true;

            return false;
        }
    }
}
