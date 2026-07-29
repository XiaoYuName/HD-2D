// 自动生成，请勿手改：由 TestActionHostGenerator 扫描 ITestCategory 与 [TestAction] 后写入。
using System;
using UnityEngine;

namespace TestSystem
{
    /// <summary>测试面板注册表：手写分类与特性宿主类都在这里自动登记。</summary>
    public static class TestRegistry
    {
        /// <summary>带 [TestAction] 方法的类，运行时只反射这些类。</summary>
        public static readonly Type[] Hosts =
        {
        };

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AddCategories()
        {
            TestCategorySet.Clear();
            TestCategorySet.Add(new TestSystem.DressMakingTestCategory());
            TestCategorySet.Add(new TestSystem.MachiRoomTestCategory());
            TestAttributeActionSet.SetHosts(Hosts);
        }
#endif
    }
}
