using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TestSystem
{
    /// <summary>
    /// 编辑期扫出手写的 ITestCategory 分类和带 [TestAction] 方法的类，把注册代码写进 TestRegistry.g.cs。
    /// 两种写法都不用手写注册，运行时也只反射这份列表里的类。
    /// </summary>
    public static class TestActionHostGenerator
    {
        const string GeneratedPath = "Assets/0 Core/1 Script/Core/TestSystem/Categories/TestRegistry.g.cs";
        const BindingFlags MethodFlags = BindingFlags.Static
            | BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.DeclaredOnly;

        /// <summary>每次脚本重载对一次账，列表变了才重写文件。</summary>
        [InitializeOnLoadMethod]
        static void CreateRegistry()
        {
            List<string> categoryNames = new ();
            List<string> hostNames = new ();
            string frameworkName = typeof(TestActionAttribute).Assembly.GetName().Name;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                if (!IsGameAssembly(assemblies[i], frameworkName))
                    continue;

                Type[] types = assemblies[i].GetTypes();
                for (int j = 0; j < types.Length; j++)
                {
                    if (IsHandwrittenCategory(types[j]))
                        categoryNames.Add(GetTypeName(types[j]));
                    if (HasTestAction(types[j]))
                        hostNames.Add(GetTypeName(types[j]));
                }
            }

            categoryNames.Sort();
            hostNames.Sort();
            string content = GetFileContent(categoryNames, hostNames);
            if (File.Exists(GeneratedPath) && File.ReadAllText(GeneratedPath) == content)
                return;

            File.WriteAllText(GeneratedPath, content);
            AssetDatabase.ImportAsset(GeneratedPath);
            Debug.Log($"测试注册表已更新：手写分类 {categoryNames.Count} 个，特性类 {hostNames.Count} 个：{GeneratedPath}");
        }

        /// <summary>只看引用了测试框架的运行时程序集，编辑器程序集里的方法进不了运行时文件。</summary>
        static bool IsGameAssembly(Assembly assembly, string frameworkName)
        {
            string name = assembly.GetName().Name;
            if (name == frameworkName || name.EndsWith("Editor"))
                return false;

            AssemblyName[] references = assembly.GetReferencedAssemblies();
            for (int i = 0; i < references.Length; i++)
                if (references[i].Name == frameworkName)
                    return true;

            return false;
        }

        /// <summary>能直接 new 出来的 ITestCategory 实现类。</summary>
        static bool IsHandwrittenCategory(Type type)
        {
            return typeof(ITestCategory).IsAssignableFrom(type)
                && !type.IsAbstract
                && !type.IsInterface
                && !type.IsGenericTypeDefinition
                && type.GetConstructor(Type.EmptyTypes) != null;
        }

        static bool HasTestAction(Type type)
        {
            MethodInfo[] methods = type.GetMethods(MethodFlags);
            for (int i = 0; i < methods.Length; i++)
                if (methods[i].GetCustomAttribute<TestActionAttribute>() != null)
                    return true;

            return false;
        }

        static string GetTypeName(Type type)
        {
            return type.FullName.Replace('+', '.');
        }

        static string GetFileContent(List<string> categoryNames, List<string> hostNames)
        {
            StringBuilder builder = new ();
            builder.AppendLine("// 自动生成，请勿手改：由 TestActionHostGenerator 扫描 ITestCategory 与 [TestAction] 后写入。");
            builder.AppendLine("using System;");
            builder.AppendLine("using UnityEngine;");
            builder.AppendLine();
            builder.AppendLine("namespace TestSystem");
            builder.AppendLine("{");
            builder.AppendLine("    /// <summary>测试面板注册表：手写分类与特性宿主类都在这里自动登记。</summary>");
            builder.AppendLine("    public static class TestRegistry");
            builder.AppendLine("    {");
            builder.AppendLine("        /// <summary>带 [TestAction] 方法的类，运行时只反射这些类。</summary>");
            builder.AppendLine("        public static readonly Type[] Hosts =");
            builder.AppendLine("        {");
            for (int i = 0; i < hostNames.Count; i++)
                builder.AppendLine($"            typeof({hostNames[i]}),");
            builder.AppendLine("        };");
            builder.AppendLine();
            builder.AppendLine("        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]");
            builder.AppendLine("        static void AddCategories()");
            builder.AppendLine("        {");
            builder.AppendLine("            TestCategorySet.Clear();");
            for (int i = 0; i < categoryNames.Count; i++)
                builder.AppendLine($"            TestCategorySet.Add(new {categoryNames[i]}());");
            builder.AppendLine("            TestAttributeActionSet.SetHosts(Hosts);");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString();
        }
    }
}
