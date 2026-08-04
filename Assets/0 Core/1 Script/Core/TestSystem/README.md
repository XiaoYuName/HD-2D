# TestSystem 快速扩展指南

当需求是“TestSystem 中添加一个类别，XXX 测试”时，优先只新增一个 `Categories/XXXTestCategory.cs`，不要先阅读整个测试系统，也不要手改 `TestRegistry.g.cs`。

## 推荐方式：手写分类

适用于需要读取 Luban 表、动态生成多个按钮、共享常量或辅助方法的测试功能。

```csharp
namespace TestSystem
{
    public sealed class XxxTestCategory : ITestCategory
    {
        public string Title => "XXX 测试";

        public void SetActions(TestActionList actionList)
        {
            actionList.Add("执行测试操作", RunTest);
        }

        static void RunTest()
        {
            // 调用项目运行时代码。
        }
    }
}
```

文件放在：

```text
Assets/0 Core/1 Script/Core/TestSystem/Categories/
```

实现要求：

- 类实现 `ITestCategory`。
- 类必须是非抽象、非泛型，并有公开无参构造；通常直接声明为 `public sealed class`。
- `Title` 是左侧分类名称。
- 在 `SetActions` 中通过 `actionList.Add("按钮文字", 回调)` 添加按钮。
- 需要按配表生成按钮时，在 `SetActions` 中遍历表数据；Lambda 捕获 ID 时，应先复制到循环内局部变量。
- 测试方法必须自行检查 Manager 是否已初始化、配置是否存在，失败时输出明确的 `[Test]` 警告。

## 简单方式：TestAction 特性

只有一两个无参静态按钮，而且不需要动态生成时，可以把特性加到现有运行时类的方法上：

```csharp
[TestAction("XXX 测试", "执行测试操作")]
static void RunTest()
{
}
```

限制：方法必须是无参数 `static` 方法。需要动态按钮时仍使用 `ITestCategory`。

## 注册与编译

`TestActionHostGenerator` 会在 Unity 脚本重载后自动扫描：

- `ITestCategory` 实现；
- 包含 `[TestAction]` 方法的运行时类。

扫描结果自动写入 `Categories/TestRegistry.g.cs`。该文件带有“自动生成，请勿手改”标记，任何手工修改都会在下次脚本重载时被覆盖。

新增分类后的验证顺序：

1. 触发一次 Unity 脚本编译。
2. 确认编译无错误。
3. 确认 `TestRegistry.g.cs` 中出现新分类。
4. 进入游戏后按 `F1` 打开测试面板。
5. 点击分类及其按钮，确认数据变化和日志符合预期。

## 常见模式

添加物品：

```csharp
InventoryManager.Instance.AddItem(itemId, count);
```

修改玩家属性：

```csharp
GameDataManager.Instance.AddProperty(propertyType, value);
GameDataManager.Instance.SetProperty(propertyType, value);
```

修改角色属性：

```csharp
CharacterManager.Instance.AddProperty(characterId, propertyType, value);
```

打开项目 UI：

```csharp
UISystem.Instance.OpenUI<SomePanel>(nameof(SomePanel));
```

## 提交前检查

- 没有修改 `TestRegistry.g.cs` 的生成逻辑来注册单个类别。
- 测试按钮不会因为缺表或空数据直接抛异常。
- 按钮名称能说明动作和数量。
- 测试代码只存在于编辑器或 Development Build 的 TestSystem 入口下，不应成为正式玩法依赖。
